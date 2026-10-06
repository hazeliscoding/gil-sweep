using System.Net;
using GilSweep.Core.Configuration;
using GilSweep.Core.History;
using GilSweep.Core.Sweep;
using GilSweep.Core.Watchlist;
using GilSweep.Desktop.Services;
using GilSweep.Desktop.ViewModels;

namespace GilSweep.Tests.Desktop;

public sealed class FirstRunTests : IDisposable
{
    private readonly DesktopTestHost _host = new();

    [Fact]
    public async Task The_first_run_asks_three_questions_before_any_market_request()
    {
        var main = _host.Get<MainWindowViewModel>();
        await main.InitializeAsync();
        await _host.SettleAsync();

        Assert.True(main.Onboarding.IsOpen);
        Assert.DoesNotContain(_host.Market.Requests, uri => uri.AbsolutePath.Contains("/aggregated/", StringComparison.Ordinal));
        Assert.True(_host.Session.IsEmpty);
    }

    [Fact]
    public async Task Answering_saves_the_character_and_runs_the_first_sweep()
    {
        var main = _host.Get<MainWindowViewModel>();
        await main.InitializeAsync();
        var onboarding = main.Onboarding;
        onboarding.Miner = "100";
        onboarding.Botanist = "95";
        onboarding.Story = onboarding.Expansions[^1];

        await onboarding.RunFirstSweepCommand.ExecuteAsync(null);
        await _host.SettleAsync();

        Assert.False(onboarding.IsOpen);
        var saved = new ConfigStore(_host.Environment).Load();
        Assert.Equal(100, saved.Levels.Miner);
        Assert.Equal(95, saved.Levels.Botanist);
        Assert.NotNull(_host.Session.Snapshot);
        Assert.NotNull(_host.Get<SweepViewModel>().Best);
    }

    [Fact]
    public async Task Levels_outside_1_to_100_keep_the_questions_open()
    {
        var main = _host.Get<MainWindowViewModel>();
        await main.InitializeAsync();
        main.Onboarding.Miner = "120";

        await main.Onboarding.RunFirstSweepCommand.ExecuteAsync(null);

        Assert.True(main.Onboarding.IsOpen);
        Assert.Equal("Gatherer levels go from 1 to 100.", main.Onboarding.Error);
        Assert.False(new ConfigStore(_host.Environment).Exists);
    }

    public void Dispose() => _host.Dispose();
}

public sealed class SweepScreenTests : IDisposable
{
    private readonly DesktopTestHost _host = new();

    [Fact]
    public async Task The_sweep_screen_answers_what_to_farm_right_now_and_why()
    {
        await _host.StartWithSweepAsync();
        var sweep = _host.Get<SweepViewModel>();

        Assert.NotNull(sweep.Best);
        Assert.True(sweep.Best.Opportunity!.Node.IsGatherableNow);
        Assert.True(sweep.Best.Reasons.Count >= 4);
        Assert.Equal(5, sweep.Best.Signals.Count);
        Assert.DoesNotContain(sweep.Ranked, row => row.ItemId == sweep.Best.ItemId);
        Assert.Equal(Enumerable.Range(2, sweep.Ranked.Count), sweep.Ranked.Select(row => row.Rank));
        Assert.InRange(sweep.Ranked.Count, 1, SweepViewModel.RankedRows);
        Assert.All(sweep.AvailableNow, row => Assert.True(row.Opportunity!.Node.IsGatherableNow));
        Assert.Equal("updated just now", sweep.Freshness);
    }

    [Fact]
    public async Task Countdowns_follow_the_eorzea_clock()
    {
        await _host.StartWithSweepAsync();
        var sweep = _host.Get<SweepViewModel>();
        var soon = sweep.Soon.First();
        var before = soon.Countdown;

        _host.Advance(TimeSpan.FromSeconds(30));

        Assert.NotEqual(before, sweep.Soon.First(row => row.ItemId == soon.ItemId).Countdown);
        Assert.Equal("updated just now", sweep.Freshness);
    }

    [Fact]
    public async Task A_farm_session_queues_opportunities_without_a_route()
    {
        await _host.StartWithSweepAsync();
        var sweep = _host.Get<SweepViewModel>();
        Assert.Empty(sweep.SessionSteps);

        sweep.SessionLength = sweep.SessionLengths.Single(option => option.Label == "1 hour");

        Assert.True(sweep.SessionOn);
        Assert.Equal("YOUR 1 HOUR SWEEP", sweep.SessionTitle);
        Assert.Equal("NOW", sweep.SessionSteps[0].When);
        Assert.Equal("AFTER", sweep.SessionSteps[^1].When);
        sweep.ClearSessionCommand.Execute(null);
        Assert.Empty(sweep.SessionSteps);
    }

    [Fact]
    public async Task Watch_on_the_headline_watches_the_item()
    {
        await _host.StartWithSweepAsync();
        var sweep = _host.Get<SweepViewModel>();
        var best = sweep.Best!.ItemId;

        sweep.ToggleWatchCommand.Execute(best);

        Assert.True(_host.Get<IWatchlistService>().IsWatched(best));
        Assert.Equal("Watching", sweep.Best!.WatchLabel);
    }

    [Fact]
    public async Task Selecting_an_opportunity_opens_it_on_the_market_screen()
    {
        var main = await _host.StartWithSweepAsync();
        var sweep = _host.Get<SweepViewModel>();
        var item = sweep.Ranked[0].ItemId;

        sweep.OpenCommand.Execute(item);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        var market = Assert.IsType<MarketViewModel>(main.CurrentPage);
        Assert.Equal(item, market.SelectedId);
        Assert.True(main.NavItems.Single(nav => nav.Page == AppPage.Market).IsActive);
    }

    [Fact]
    public async Task An_hour_later_it_sweeps_again_on_its_own()
    {
        await _host.StartWithSweepAsync();
        var first = _host.Session.Snapshot!.TakenAt;

        _host.Advance(TimeSpan.FromMinutes(61));
        await _host.SettleAsync();

        Assert.True(_host.Session.Snapshot!.TakenAt > first);
    }

    [Fact]
    public async Task Raising_a_level_re_ranks_without_a_new_sweep()
    {
        await _host.StartWithSweepAsync();
        _host.Get<ISettingsService>().Update(settings => settings.Levels = new GathererLevels { Miner = 30, Botanist = 30 });
        var requests = _host.Market.Requests.Count;
        var low = _host.Session.Board.Ranked.Count;

        _host.Get<ISettingsService>().Update(settings => settings.Levels = new GathererLevels { Miner = 100, Botanist = 100 });

        Assert.True(_host.Session.Board.Ranked.Count > low);
        Assert.Equal(requests, _host.Market.Requests.Count);
    }

    public void Dispose() => _host.Dispose();
}

public sealed class OfflineTests : IDisposable
{
    private readonly DesktopTestHost _host = new();

    [Fact]
    public async Task When_Universalis_is_down_the_cached_sweep_stays_with_a_banner_and_a_retry()
    {
        var main = await _host.StartWithSweepAsync();
        _host.Market.Fail(uri => uri.Host == "universalis.app", HttpStatusCode.ServiceUnavailable, times: 3);
        _host.Clock.Advance(TimeSpan.FromHours(2));

        await _host.Session.SweepAsync();

        Assert.True(main.ShowOfflineBanner);
        Assert.Equal("Universalis unreachable", main.ConnectionLabel);
        Assert.Contains("cached sweep from 2h", main.OfflineDescription, StringComparison.Ordinal);
        Assert.Equal(_host.Clock.Now + AppSession.RetryAfter, _host.Session.NextRetryAt);
        Assert.NotNull(_host.Get<SweepViewModel>().Best);

        _host.Advance(AppSession.RetryAfter);
        await _host.SettleAsync();

        Assert.False(main.ShowOfflineBanner);
        Assert.Equal("Universalis · Cactuar", main.ConnectionLabel);
    }

    [Fact]
    public async Task With_no_cached_sweep_the_failure_still_explains_itself()
    {
        _host.SetUpCharacter();
        _host.Market.Fail(uri => uri.Host == "universalis.app", HttpStatusCode.GatewayTimeout);
        var main = _host.Get<MainWindowViewModel>();

        await main.InitializeAsync();
        await _host.SettleAsync();

        Assert.True(main.ShowOfflineBanner);
        Assert.Contains("no earlier sweep", main.OfflineDescription, StringComparison.Ordinal);
    }

    public void Dispose() => _host.Dispose();
}

public sealed class MarketScreenTests : IDisposable
{
    private readonly DesktopTestHost _host = new();

    [Fact]
    public async Task An_item_shows_live_listings_sales_hours_and_selling_advice()
    {
        await _host.StartWithSweepAsync();
        var market = _host.Get<MarketViewModel>();

        market.Select(12531);
        await WaitAsync(() => !market.IsLoading);

        Assert.True(market.HasItem);
        Assert.Equal("198g", market.CheapestNow);
        Assert.NotEmpty(market.Listings);
        Assert.NotEmpty(market.Sales);
        Assert.Equal(24, market.Hours.Count);
        Assert.StartsWith("List at ", market.Advice, StringComparison.Ordinal);
        Assert.True(market.PricePoints.Count > 1);
        Assert.Null(market.LoadError);
    }

    [Fact]
    public async Task A_trap_item_says_why_it_is_not_ranked()
    {
        await _host.StartWithSweepAsync();
        var market = _host.Get<MarketViewModel>();

        market.Select(49227);

        Assert.False(market.IsRanked);
        Assert.Equal("Bought from a vendor, not gathered", market.LockText);
        Assert.Equal("WHY IT ISN'T RANKED", market.RankTitle);
    }

    [Fact]
    public async Task Without_live_listings_the_sweep_figures_stay_with_a_message()
    {
        await _host.StartWithSweepAsync();
        _host.Market.Fail(uri => uri.Host == "universalis.app", HttpStatusCode.ServiceUnavailable);
        var market = _host.Get<MarketViewModel>();

        market.Select(12531);
        await WaitAsync(() => !market.IsLoading);

        Assert.StartsWith("Live listings unavailable", market.LoadError, StringComparison.Ordinal);
        Assert.Equal("198g", market.CheapestNow);
    }

    public void Dispose() => _host.Dispose();

    private static async Task WaitAsync(Func<bool> condition)
    {
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        await Task.Delay(20);
        while (!condition() && DateTime.UtcNow < until)
        {
            await Task.Delay(20);
        }
    }
}

public sealed class CraftWatchHistorySettingsTests : IDisposable
{
    private readonly DesktopTestHost _host = new();

    [Fact]
    public async Task Craft_shows_one_card_per_farmed_material()
    {
        await _host.StartWithSweepAsync();
        var craft = _host.Get<CraftViewModel>();
        await craft.ActivateAsync();

        Assert.Equal(CraftFilter.WorthIt, craft.Filter!.Value);
        Assert.NotEmpty(craft.Cards);
        Assert.Equal(craft.Cards.Count, craft.Cards.Select(card => card.RawId).Distinct().Count());
        Assert.All(craft.Cards, card => Assert.StartsWith("+", card.Difference, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Low_crafter_levels_move_recipes_to_locked()
    {
        await _host.StartWithSweepAsync();
        _host.Get<ISettingsService>().Update(settings => settings.Crafters = GilSweepSettings.CrafterJobs.ToDictionary(job => job, _ => 1));
        var craft = _host.Get<CraftViewModel>();
        await craft.ActivateAsync();

        craft.Filter = craft.Filters.Single(option => option.Value == CraftFilter.Locked);

        Assert.NotEmpty(craft.Cards);
        Assert.All(craft.Cards, card => Assert.True(card.Locked));
    }

    [Fact]
    public async Task Watchlist_switches_are_saved_and_rows_follow_the_watchlist()
    {
        await _host.StartWithSweepAsync();
        var watch = _host.Get<WatchlistViewModel>();

        watch.ToWatch = watch.Choices.Single(choice => choice.Name == "Dense Aluminum Ore");
        var row = Assert.Single(watch.Rows);
        Assert.True(row.Timed);
        Assert.True(row.NodeOpens);
        row.Undercut = true;

        Assert.True(new ConfigStore(_host.Environment).Load().WatchOf(49208)!.Undercut);
        Assert.True(watch.NeedsRetainers);
        watch.RemoveCommand.Execute(49208);
        Assert.Empty(watch.Rows);
    }

    [Fact]
    public async Task Thresholds_are_saved()
    {
        await _host.StartWithSweepAsync();
        var watch = _host.Get<WatchlistViewModel>();

        watch.Spike = watch.SpikeOptions.Single(option => option.Value == 40);
        watch.Lead = watch.LeadOptions.Single(option => option.Value == 0);

        var alerts = new ConfigStore(_host.Environment).Load().Alerts;
        Assert.Equal(40, alerts.SpikePercent);
        Assert.Equal(0, alerts.NodeLeadMinutes);
    }

    [Fact]
    public async Task History_lists_sweeps_and_exports_csv()
    {
        await _host.StartWithSweepAsync();
        var history = _host.Get<HistoryViewModel>();
        await history.ActivateAsync();
        _host.Picker.Path = Path.Combine(_host.Environment.Root, "export.csv");

        await history.ExportCommand.ExecuteAsync(null);

        Assert.Single(history.Sweeps);
        Assert.Equal("Saved export.csv", history.ExportResult);
        Assert.Equal(105, File.ReadAllLines(_host.Picker.Path).Length);
    }

    [Fact]
    public async Task A_new_world_sweeps_at_once()
    {
        await _host.StartWithSweepAsync();
        var settings = _host.Get<SettingsViewModel>();
        await settings.ActivateAsync();

        settings.World = "Siren";
        await _host.SettleAsync();

        Assert.Equal("Siren", _host.Session.Snapshot!.World);
        Assert.Contains(_host.Market.Requests, uri => uri.AbsolutePath.StartsWith("/api/v2/aggregated/Siren/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_out_of_range_level_is_refused_with_a_message()
    {
        await _host.StartWithSweepAsync();
        var settings = _host.Get<SettingsViewModel>();
        await settings.ActivateAsync();

        settings.Miner = "0";

        Assert.Equal("Gatherer levels go from 1 to 100.", settings.CharacterError);
        Assert.Equal(100, new ConfigStore(_host.Environment).Load().Levels.Miner);
    }

    [Fact]
    public async Task Clearing_snapshots_asks_first()
    {
        await _host.StartWithSweepAsync();
        var settings = _host.Get<SettingsViewModel>();
        _host.Dialogs.Answer = false;

        await settings.ClearSnapshotsCommand.ExecuteAsync(null);
        Assert.Equal(1, _host.Get<IMarketSnapshotStore>().Stats().Count);

        _host.Dialogs.Answer = true;
        await settings.ClearSnapshotsCommand.ExecuteAsync(null);

        Assert.True(Assert.Single(_host.Dialogs.Asked.Distinct()).Danger);
        Assert.Equal(0, _host.Get<IMarketSnapshotStore>().Stats().Count);
    }

    [Fact]
    public async Task Tracking_an_item_adds_it_and_prices_it()
    {
        await _host.StartWithSweepAsync();
        var settings = _host.Get<SettingsViewModel>();
        settings.TrackQuery = "Zinc Ore";

        await settings.TrackCommand.ExecuteAsync(null);
        await _host.SettleAsync();

        Assert.StartsWith("Tracked Zinc Ore", settings.TrackResult, StringComparison.Ordinal);
        Assert.Contains(settings.CustomItems, item => item.Name == "Zinc Ore");
    }

    public void Dispose() => _host.Dispose();
}

public sealed class UpdateTests : IDisposable
{
    private readonly DesktopTestHost _host = new();

    [Fact]
    public async Task An_update_waits_for_the_sweep_to_finish()
    {
        await _host.StartWithSweepAsync();
        var updates = _host.Get<UpdatesViewModel>();
        _host.Updater.Latest = "2.1.0";
        await updates.CheckNowCommand.ExecuteAsync(null);
        Assert.True(updates.HasUpdate);

        _host.Market.Fail(uri => uri.Host == "universalis.app", HttpStatusCode.GatewayTimeout);
        var sweeping = _host.Get<ISweepService>().RunAsync(cancellationToken: TestContext.Current.CancellationToken);
        await updates.UpdateAndRestartCommand.ExecuteAsync(null);

        Assert.Equal("Wait for the sweep to finish, then update.", updates.Notice);
        Assert.False(_host.Updater.Restarted);
        await Assert.ThrowsAnyAsync<Exception>(() => sweeping);

        await updates.UpdateAndRestartCommand.ExecuteAsync(null);
        Assert.True(_host.Updater.Restarted);
    }

    [Fact]
    public async Task A_portable_copy_never_checks()
    {
        _host.Updater.IsInstalled = false;
        _host.Updater.Latest = "2.1.0";

        await _host.Get<UpdatesViewModel>().CheckOnStartupAsync();

        Assert.False(_host.Get<UpdatesViewModel>().HasUpdate);
        Assert.Equal(UpdateState.Portable, _host.Get<UpdatesViewModel>().State);
    }

    public void Dispose() => _host.Dispose();
}
