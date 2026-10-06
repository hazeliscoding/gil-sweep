using System.Text.Json;
using System.Text.Json.Nodes;
using GilSweep.Core.Configuration;
using GilSweep.Core.Serialization;
using GilSweep.Core.Sweep;
using GilSweep.Tests.Support;

namespace GilSweep.Tests.Core;

/// <summary>
/// The sweep engine against v1's own output for the same recorded market (Fixtures/V1). v1 ran
/// with its default character and its bundled seed snapshot as the previous sweep.
/// </summary>
public sealed class SweepCharacterizationTests
{
    private static readonly HashSet<string> V2Only = ["listings"];

    [Fact]
    public async Task A_sweep_produces_v1s_rows()
    {
        var (expected, actual) = await SweepBothAsync();
        JsonAssert.Equivalent(expected["rows"], actual["rows"], "$.rows", V2Only);
    }

    [Fact]
    public async Task A_sweep_produces_v1s_craft_margins()
    {
        var (expected, actual) = await SweepBothAsync();
        JsonAssert.Equivalent(expected["crafts"], actual["crafts"], "$.crafts");
    }

    [Fact]
    public async Task A_sweep_lists_v1s_untracked_top_sellers()
    {
        var (expected, actual) = await SweepBothAsync();
        JsonAssert.Equivalent(expected["sbUnknown"], actual["sbUnknown"], "$.sbUnknown");
    }

    [Fact]
    public async Task Prices_are_requested_in_v1s_chunks()
    {
        using var host = new CoreHost();
        await host.Get<ISweepEngine>().RunAsync(new GilSweepSettings(), Seed(), cancellationToken: TestContext.Current.CancellationToken);

        var expected = Fixture.Json("V1", "expected-sweep.json")["aggregatedRequestIds"]!.AsArray()
            .Select(chunk => string.Join(',', chunk!.AsArray().Select(id => (int)id!)))
            .ToList();
        var actual = host.Market.Requests
            .Where(uri => uri.AbsolutePath.StartsWith("/api/v2/aggregated/", StringComparison.Ordinal))
            .Select(uri => uri.AbsolutePath.Split('/')[^1])
            .ToList();
        Assert.Equal(expected, actual);
    }

    internal static MarketSnapshot Seed() =>
        JsonSerializer.Deserialize(Fixture.Text("V1", "seed-snapshot.json"), GilSweepJsonContext.Default.MarketSnapshot)!;

    private static async Task<(JsonNode Expected, JsonNode Actual)> SweepBothAsync()
    {
        using var host = new CoreHost();
        var snapshot = await host.Get<ISweepEngine>().RunAsync(new GilSweepSettings(), Seed(), cancellationToken: TestContext.Current.CancellationToken);
        var actual = JsonNode.Parse(JsonSerializer.Serialize(snapshot, GilSweepJsonContext.Default.MarketSnapshot))!;
        return (Fixture.Json("V1", "expected-sweep.json"), actual);
    }
}
