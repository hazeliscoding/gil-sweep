using System.Globalization;
using System.Text.Json.Nodes;
using GilSweep.Core.Catalog;
using GilSweep.Core.Market;
using GilSweep.Core.Market.Universalis;
using GilSweep.Tests.Support;

namespace GilSweep.Tests.Core;

/// <summary>v1's market drill-down and retainer advice for all 104 tracked items' recorded listings.</summary>
public sealed class MarketCharacterizationTests
{
    private static readonly JsonNode Expected = Fixture.Json("V1", "expected-market.json");

    [Fact]
    public async Task Drill_down_figures_match_v1()
    {
        using var host = new CoreHost();
        var client = host.Get<IUniversalisClient>();
        foreach (var (key, expected) in Expected["detail"]!.AsObject())
        {
            var id = int.Parse(key, CultureInfo.InvariantCulture);
            var markets = await client.GetMarketsAsync("Cactuar", [id], 30, 100, TestContext.Current.CancellationToken);
            var detail = MarketAnalyzer.Detail(markets[id]);
            var actual = new JsonObject
            {
                ["curMin"] = detail.CurMin,
                ["medPPU"] = detail.MedPpu,
                ["listedQty"] = detail.ListedQty,
                ["unitsPerDay"] = detail.UnitsPerDay,
                ["daysInv"] = detail.DaysInv,
                ["listings"] = new JsonArray([.. detail.Listings.Select(l => new JsonObject { ["ppu"] = l.PricePerUnit, ["qty"] = l.Quantity })]),
                ["sales"] = new JsonArray([.. detail.Sales.Select(s => new JsonObject { ["ppu"] = s.PricePerUnit, ["qty"] = s.Quantity, ["t"] = s.SoldAt.ToUnixTimeMilliseconds() })]),
            };
            JsonAssert.Equivalent(expected, JsonNode.Parse(actual.ToJsonString()), $"$.detail.{key}", new HashSet<string> { "hours", "peakHours" });
        }
    }

    [Fact]
    public async Task Selling_hours_match_v1()
    {
        using var host = new CoreHost();
        var client = host.Get<IUniversalisClient>();
        foreach (var (key, expected) in Expected["detail"]!.AsObject())
        {
            var id = int.Parse(key, CultureInfo.InvariantCulture);
            var markets = await client.GetMarketsAsync("Cactuar", [id], 30, 100, TestContext.Current.CancellationToken);
            var hours = MarketAnalyzer.Hours(markets[id].RecentSales, TimeZoneInfo.Utc);
            var expectedHours = expected!["hours"]!.AsArray();
            Assert.Equal(expectedHours.Select(h => (long)h!["q"]!), hours.Units);
            Assert.Equal(expectedHours.Where(h => (bool)h!["hot"]!).Select(h => (int)h!["h"]!), hours.PeakHours);
        }
    }

    [Fact]
    public async Task Retainer_advice_matches_v1()
    {
        using var host = new CoreHost();
        var client = host.Get<IUniversalisClient>();
        var items = ItemCatalog.LoadBundledItems();
        foreach (var expected in Expected["advice"]!.AsArray())
        {
            var id = (int)expected!["id"]!;
            var markets = await client.GetMarketsAsync("Cactuar", [id], 50, 50, TestContext.Current.CancellationToken);
            var advice = MarketAnalyzer.Advise(markets[id], items.First(item => item.Id == id).Kind);
            var actual = new JsonObject
            {
                ["id"] = advice.ItemId,
                ["curMin"] = advice.CurMin,
                ["medPPU"] = advice.MedPpu,
                ["listPrice"] = advice.ListPrice,
                ["stack"] = advice.Stack,
                ["daysInv"] = advice.DaysInv,
                ["unitsPerDay"] = advice.UnitsPerDay,
                ["verdict"] = advice.V1Verdict,
            };
            JsonAssert.Equivalent(expected, JsonNode.Parse(actual.ToJsonString()), $"$.advice[{id}]");
        }
    }
}
