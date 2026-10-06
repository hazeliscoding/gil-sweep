using System.Net;
using System.Text.Json;
using GilSweep.Core;
using GilSweep.Core.Configuration;
using GilSweep.Core.History;
using GilSweep.Core.Market.Universalis;
using GilSweep.Core.Serialization;
using GilSweep.Core.Sweep;
using GilSweep.Tests.Support;

namespace GilSweep.Tests.Core;

/// <summary>Sweeping through the service: what is saved, what is kept, and what happens when a provider fails.</summary>
public sealed class SweepServiceTests : IDisposable
{
    private readonly CoreHost _host = new();

    private ISweepService Sweeps => _host.Get<ISweepService>();

    private IMarketSnapshotStore Store => _host.Get<IMarketSnapshotStore>();

    [Fact]
    public async Task A_sweep_is_saved_with_v1s_file_name_and_listing_depth()
    {
        var snapshot = await Sweeps.RunAsync(cancellationToken: TestContext.Current.CancellationToken);

        var file = Assert.Single(Directory.EnumerateFiles(Store.Folder));
        Assert.Equal("sweep-2026-10-05T18-30-00-000Z-Cactuar.json", Path.GetFileName(file));
        Assert.Null(snapshot.Warnings);
        var mythriteSand = snapshot.Find(12531)!;
        Assert.Equal(100, mythriteSand.Listings!.ListingsCount);
        Assert.Equal(8682, mythriteSand.Listings.UnitsForSale);
        Assert.Equal(198, mythriteSand.Listings.Cheapest);
    }

    [Fact]
    public async Task The_next_sweep_measures_change_against_the_last_one_and_drops_its_crafts()
    {
        await Sweeps.RunAsync(cancellationToken: TestContext.Current.CancellationToken);
        _host.Clock.Advance(TimeSpan.FromHours(1));
        _host.Market.Aggregated[12531]["nq"]!["averageSalePrice"]!["world"]!["price"] = 400.0;

        var second = await Sweeps.RunAsync(cancellationToken: TestContext.Current.CancellationToken);

        // 289 → 400 gil.
        Assert.Equal(38.4, second.Find(12531)!.AvgChangePct);
        var files = Directory.EnumerateFiles(Store.Folder).Order(StringComparer.Ordinal).ToList();
        Assert.Null(Read(files[0]).Crafts);
        Assert.NotNull(Read(files[1]).Crafts);
    }

    [Fact]
    public async Task Sweeps_older_than_two_days_thin_to_one_a_day_and_expire_after_the_retention()
    {
        _host.Get<ISettingsService>().Update(settings => settings.HistoryRetentionDays = 7);
        var start = _host.Clock.Now;
        foreach (var hours in new[] { 0, 1, 2, 24 * 3, 24 * 3 + 1, 24 * 9 + 2 })
        {
            _host.Clock.Now = start + TimeSpan.FromHours(hours);
            await Sweeps.RunAsync(cancellationToken: TestContext.Current.CancellationToken);
        }

        // Day 0 (three sweeps) expired after 7 days; day 3 thinned to its newest; day 9 kept.
        var names = Directory.EnumerateFiles(Store.Folder).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["sweep-2026-10-08T19-30-00-000Z-Cactuar.json", "sweep-2026-10-14T20-30-00-000Z-Cactuar.json"], names);
    }

    [Fact]
    public async Task Asking_twice_while_sweeping_runs_one_sweep()
    {
        var first = Sweeps.RunAsync(cancellationToken: TestContext.Current.CancellationToken);
        var second = Sweeps.RunAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(await first, await second);
        Assert.Single(Directory.EnumerateFiles(Store.Folder));
    }

    [Fact]
    public async Task When_Universalis_is_down_the_sweep_fails_with_a_message_and_history_is_untouched()
    {
        await Sweeps.RunAsync(cancellationToken: TestContext.Current.CancellationToken);
        _host.Market.Fail(uri => uri.Host == "universalis.app", HttpStatusCode.ServiceUnavailable);
        _host.Clock.Advance(TimeSpan.FromHours(1));

        var error = await Assert.ThrowsAsync<GilSweepException>(() => Sweeps.RunAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(GilSweepErrorKind.MarketUnavailable, error.Kind);
        Assert.Equal("Universalis is not responding (HTTP 503).", error.Message);
        Assert.Single(Directory.EnumerateFiles(Store.Folder));
        Assert.NotNull(Store.Latest("Cactuar")!.Crafts);
    }

    [Fact]
    public async Task Without_Saddlebag_the_sweep_completes_with_a_warning()
    {
        _host.Market.Fail(uri => uri.Host == "api.saddlebagexchange.com", HttpStatusCode.InternalServerError);

        var snapshot = await Sweeps.RunAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(snapshot.Warnings!, warning => warning.Contains("Saddlebag", StringComparison.Ordinal));
        Assert.All(snapshot.Rows, row => Assert.Null(row.SbState));
        Assert.Equal(104, snapshot.Rows.Count);
    }

    [Fact]
    public async Task When_recipe_prices_fail_the_farm_list_still_stands()
    {
        // Chunks after the second hold only demand consumers and recipe ingredients.
        var call = 0;
        _host.Market.Fail(uri => uri.AbsolutePath.Contains("/aggregated/", StringComparison.Ordinal) && ++call > 2, HttpStatusCode.GatewayTimeout);

        var snapshot = await Sweeps.RunAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(snapshot.Warnings!, warning => warning.Contains("recipe items", StringComparison.Ordinal));
        Assert.Equal(1060063, snapshot.Find(5121)!.Throughput);
    }

    [Fact]
    public async Task When_listings_fail_competition_is_unknown_but_the_sweep_completes()
    {
        _host.Market.Fail(uri => uri.Query.Contains("listings=", StringComparison.Ordinal), HttpStatusCode.GatewayTimeout);

        var snapshot = await Sweeps.RunAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(snapshot.Warnings!, warning => warning.Contains("Listing depth", StringComparison.Ordinal));
        Assert.All(snapshot.Rows, row => Assert.Null(row.Listings));
    }

    [Fact]
    public async Task Rate_limits_and_gateway_errors_are_retried()
    {
        _host.Market.Fail(uri => uri.AbsolutePath.Contains("/aggregated/", StringComparison.Ordinal), HttpStatusCode.TooManyRequests, times: 1);
        _host.Market.Fail(uri => uri.Query.Contains("listings=", StringComparison.Ordinal), HttpStatusCode.GatewayTimeout, times: 2);

        var snapshot = await Sweeps.RunAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(snapshot.Warnings);
    }

    [Fact]
    public async Task A_garbled_response_fails_with_a_message()
    {
        _host.Market.Respond(uri => uri.AbsolutePath.Contains("/aggregated/", StringComparison.Ordinal), "<html>maintenance</html>");

        var error = await Assert.ThrowsAsync<GilSweepException>(() => Sweeps.RunAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("Universalis sent a response Gil Sweep couldn't read.", error.Message);
    }

    [Fact]
    public async Task A_cancelled_sweep_stops_without_saving()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Sweeps.RunAsync(cancellationToken: cancel.Token));

        Assert.False(Directory.Exists(Store.Folder) && Directory.EnumerateFiles(Store.Folder).Any());
    }

    [Fact]
    public async Task Undercuts_are_spotted_from_your_retainer_names()
    {
        // Sakoyo has the cheapest Mythrite Sand listing in the recording.
        _host.Get<ISettingsService>().Update(settings => settings.RetainerNames = ["sakoyo"]);

        var snapshot = await Sweeps.RunAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(snapshot.Find(12531)!.Listings!.CheapestIsOwn);
        Assert.Equal(198, snapshot.Find(12531)!.Listings!.OwnCheapest);
    }

    [Fact]
    public async Task An_unreadable_newest_snapshot_falls_back_to_the_one_before()
    {
        var first = await Sweeps.RunAsync(cancellationToken: TestContext.Current.CancellationToken);
        File.WriteAllText(Path.Combine(Store.Folder, "sweep-2026-10-05T19-30-00-000Z-Cactuar.json"), "{ truncated");

        var latest = new MarketSnapshotStore(_host.Environment, Microsoft.Extensions.Logging.Abstractions.NullLogger<MarketSnapshotStore>.Instance).Latest("Cactuar");

        Assert.Equal(first.Timestamp, latest!.Timestamp);
    }

    [Fact]
    public void A_sweep_asks_about_nineteen_requests()
    {
        var engine = _host.Get<ISweepEngine>();
        Assert.Equal(13 + 5 + 1, engine.EstimateRequests(new GilSweepSettings()));
        Assert.Equal(13 + 5, engine.EstimateRequests(new GilSweepSettings { UseSaddlebag = false }));
    }

    public void Dispose() => _host.Dispose();

    private static MarketSnapshot Read(string file) =>
        JsonSerializer.Deserialize(File.ReadAllText(file), GilSweepJsonContext.Default.MarketSnapshot)!;
}

/// <summary>The recorded single-item scenarios from the handoff: normal, sparse, stale, empty and high-volume markets.</summary>
public sealed class MarketScenarioTests
{
    [Theory]
    [InlineData("normal-market.json", 12531, 100, 8682, false)]
    [InlineData("sparse-market.json", 19916, 1, 15, false)]
    [InlineData("stale-market.json", 19860, 9, 172, true)]
    [InlineData("no-listings.json", 32955, 0, 0, false)]
    [InlineData("high-volume.json", 18, 38, 214234, false)]
    public async Task Listing_depth_and_staleness_come_through(string file, int id, int listings, long units, bool stale)
    {
        using var host = new CoreHost();
        host.Market.Respond(uri => uri.AbsolutePath.EndsWith("/" + id, StringComparison.Ordinal), Fixture.Text("Universalis", file));

        var market = (await host.Get<IUniversalisClient>().GetMarketsAsync("Cactuar", [id], 20, 20, TestContext.Current.CancellationToken))[id];
        var row = new SnapshotRow { Id = id, VelDay = 10, Listings = new ListingDepth { ListingsCount = market.ListingsCount, UnitsForSale = market.UnitsForSale, UpdatedAt = market.LastUploadedAt } };
        var competition = OpportunityBoard.CompetitionOf(row, new DateTimeOffset(2026, 10, 5, 18, 30, 0, TimeSpan.Zero));

        Assert.Equal(listings, market.ListingsCount);
        Assert.Equal(units, market.UnitsForSale);
        Assert.Equal(stale, competition.Stale);
    }

    [Fact]
    public void An_empty_market_scores_as_a_shortage()
    {
        var empty = OpportunityBoard.CompetitionOf(new SnapshotRow { VelDay = 10, Listings = new ListingDepth() }, DateTimeOffset.UtcNow);
        Assert.Equal(20, GilSweep.Core.Sweep.OpportunityScorer.CompetitionPoints(empty));
    }

    [Theory]
    [InlineData("rising.json", TrendDirection.Rising)]
    [InlineData("falling.json", TrendDirection.Falling)]
    [InlineData("stable.json", TrendDirection.Stable)]
    public async Task Saddlebag_states_become_trend_directions(string file, TrendDirection direction)
    {
        using var host = new CoreHost();
        host.Market.Saddlebag = Fixture.Json("Saddlebag", file);

        var signals = await host.Get<GilSweep.Core.Market.Saddlebag.ISaddlebagClient>().GetMarketShareAsync("Cactuar", new SaddlebagQuery(), TestContext.Current.CancellationToken);

        Assert.NotEmpty(signals!);
        Assert.All(signals!, signal => Assert.Equal(direction, OpportunityBoard.TrendOf(new SnapshotRow { SbState = signal.State }, null, null).Direction));
    }
}
