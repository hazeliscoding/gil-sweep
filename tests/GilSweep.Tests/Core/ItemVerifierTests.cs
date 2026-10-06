using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using GilSweep.Core.Catalog;
using GilSweep.Core.Serialization;
using GilSweep.Tests.Support;

namespace GilSweep.Tests.Core;

/// <summary>
/// v1's verify-and-track pipeline against recorded XIVAPI and Garland Tools responses: plain and
/// timed gatherables, crafted items (including one whose ingredients have nodes), a vendor trap,
/// an item already tracked, an unknown name and a partial name.
/// </summary>
public sealed class ItemVerifierTests
{
    private static readonly JsonNode Expected = Fixture.Json("V1", "expected-verify.json");

    public static TheoryData<string> Queries() => [.. Expected.AsObject().Select(pair => pair.Key)];

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task Verification_matches_v1(string query)
    {
        using var host = new CoreHost();
        var (result, item) = await host.Get<IItemVerifier>().VerifyAsync(query, new HashSet<int> { 5121 }, TestContext.Current.CancellationToken);

        var expected = Expected[query]!;
        var actualResult = new JsonObject
        {
            ["found"] = result.Found,
            ["id"] = result.Id,
            ["name"] = result.Name,
            ["gatherable"] = result.Gatherable,
            ["alreadyTracked"] = result.AlreadyTracked ? true : null,
            ["reason"] = result.Reason,
        };
        JsonAssert.Equivalent(expected["result"], JsonNode.Parse(actualResult.ToJsonString()), "$.result");
        var actualItem = item is null ? null : JsonNode.Parse(JsonSerializer.Serialize(item, GilSweepJsonContext.Default.CatalogItem));
        JsonAssert.Equivalent(expected["item"], actualItem, "$.item");
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public async Task An_item_with_no_nodes_recipe_or_vendor_is_tracked_as_voyage_loot()
    {
        using var host = new CoreHost();
        var doc = Fixture.Json("Verify", "garland-33916.json");
        doc["item"]!.AsObject().Remove("vendors");
        doc["item"]!.AsObject().Remove("tradeShops");
        host.Market.Respond(uri => uri.Host == "garlandtools.org", doc.ToJsonString());

        var (result, item) = await host.Get<IItemVerifier>().VerifyAsync("Grade 8 Dark Matter", new HashSet<int>(), TestContext.Current.CancellationToken);

        Assert.Equal(ItemKind.Submarine, item!.Kind);
        Assert.False(result.Gatherable);
    }

    [Fact]
    public async Task Without_Garland_verification_fails_with_a_message()
    {
        using var host = new CoreHost();
        host.Market.Fail(uri => uri.Host == "garlandtools.org", HttpStatusCode.ServiceUnavailable);

        var error = await Assert.ThrowsAsync<GilSweep.Core.GilSweepException>(() =>
            host.Get<IItemVerifier>().VerifyAsync("Zinc Ore", new HashSet<int>(), TestContext.Current.CancellationToken));

        Assert.Contains("Garland Tools", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(50, Expansion.ARR)]
    [InlineData(51, Expansion.HW)]
    [InlineData(70, Expansion.StB)]
    [InlineData(80, Expansion.ShB)]
    [InlineData(90, Expansion.EW)]
    [InlineData(100, Expansion.DT)]
    public void Node_levels_map_to_expansions_like_v1(int level, Expansion expansion) =>
        Assert.Equal(expansion, ItemVerifier.ExpansionOf(level));
}
