using System.Globalization;
using System.Text.Json.Nodes;
using GilSweep.Core.History;
using GilSweep.Tests.Support;

namespace GilSweep.Tests.Core;

/// <summary>
/// v1's digest, history series, Universalis backfill and prune over a recorded archive: 13 daily
/// sweeps with two extra same-day sweeps and one from another world (Fixtures/V1/archive).
/// </summary>
public sealed class HistoryCharacterizationTests : IDisposable
{
    private static readonly JsonNode Expected = Fixture.Json("V1", "expected-history.json");
    private readonly CoreHost _host = new();

    public HistoryCharacterizationTests()
    {
        var folder = _host.Get<IMarketSnapshotStore>().Folder;
        Directory.CreateDirectory(folder);
        foreach (var file in Directory.EnumerateFiles(Fixture.PathOf("V1", "archive")))
        {
            File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
        }
    }

    [Fact]
    public void The_digest_matches_v1()
    {
        var digest = _host.Get<IHistoryService>().Digest("Cactuar");
        var actual = new JsonObject
        {
            ["world"] = digest.World,
            ["latestDate"] = digest.LatestDate,
            ["baselineDate"] = digest.BaselineDate,
            ["daysApart"] = digest.DaysApart,
            ["changes"] = new JsonArray([.. digest.Changes.Select(c => new JsonObject
            {
                ["id"] = c.Id,
                ["name"] = c.Name,
                ["avgThen"] = c.AvgThen,
                ["avgNow"] = c.AvgNow,
                ["avgPct"] = c.AvgPct,
                ["velThen"] = c.VelThen,
                ["velNow"] = c.VelNow,
            })]),
            ["prune"] = new JsonArray([.. digest.Prune.Select(p => new JsonObject
            {
                ["id"] = p.Id,
                ["name"] = p.Name,
                ["recentVel"] = new JsonArray([.. p.RecentVel.Select(v => JsonValue.Create(v))]),
            })]),
        };
        JsonAssert.Equivalent(Expected["digest"], JsonNode.Parse(actual.ToJsonString()), "$.digest");
    }

    [Fact]
    public void History_series_match_v1()
    {
        AssertSeries(Expected["history"]!, _host.Get<IHistoryService>().Series("Cactuar"), "$.history");
    }

    [Fact]
    public async Task The_backfill_and_the_series_built_on_it_match_v1()
    {
        var history = _host.Get<IHistoryService>();
        await history.BackfillAsync("Cactuar", TestContext.Current.CancellationToken);

        Assert.True(history.HasBackfill("Cactuar"));
        AssertSeries(Expected["historyWithBackfill"]!, history.Series("Cactuar"), "$.historyWithBackfill");
    }

    [Fact]
    public void Pruning_keeps_the_newest_sweep_per_world_and_day_like_v1()
    {
        var store = _host.Get<IMarketSnapshotStore>();
        var deleted = store.PruneToOnePerDay();

        Assert.Equal((int)Expected["prune"]!["deleted"]!, deleted);
        var kept = Directory.EnumerateFiles(store.Folder).Select(Path.GetFileName).Order(StringComparer.Ordinal);
        Assert.Equal(Expected["prune"]!["kept"]!.AsArray().Select(name => (string)name!), kept);
    }

    public void Dispose() => _host.Dispose();

    private static void AssertSeries(JsonNode expected, IReadOnlyDictionary<int, IReadOnlyList<HistoryPoint>> actual, string path)
    {
        var node = new JsonObject();
        foreach (var (id, points) in actual)
        {
            node[id.ToString(CultureInfo.InvariantCulture)] = new JsonArray([.. points.Select(p => new JsonObject { ["t"] = p.T, ["avg"] = p.Avg, ["velDay"] = p.VelDay })]);
        }

        JsonAssert.Equivalent(expected, JsonNode.Parse(node.ToJsonString()), path);
    }
}
