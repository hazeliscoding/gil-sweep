using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using GilSweep.Core.Configuration;
using GilSweep.Core.Serialization;
using GilSweep.Core.Sweep;
using GilSweep.Tests.Support;

namespace GilSweep.Tests.Core;

/// <summary>
/// v1's ranking rules for six characters (from a fresh level 20 to endgame with split levels and
/// missing crafter data), over the recorded sweep and over v1's bundled seed, whose prices are
/// months older and so produce many movers.
/// </summary>
public sealed class RankingCharacterizationTests
{
    private static readonly JsonNode Expected = Fixture.Json("V1", "expected-ranking.json");
    private static readonly MarketSnapshot Sweep = JsonSerializer.Deserialize(Fixture.Text("V1", "expected-sweep.json"), GilSweepJsonContext.Default.MarketSnapshot)!;

    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();
        foreach (var source in new[] { "sweep", "seed" })
        {
            foreach (var (config, _) in Expected["configs"]!.AsObject())
            {
                data.Add(source, config);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Lock_reasons_match_v1(string source, string config)
    {
        var (rows, _, settings, expected) = Load(source, config);
        foreach (var row in rows)
        {
            Assert.Equal((string)expected["lockReasons"]![row.Id.ToString(CultureInfo.InvariantCulture)]!, Ranking.LockReason(row, settings));
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Farm_lists_match_v1(string source, string config)
    {
        var (rows, _, settings, expected) = Load(source, config);
        Assert.Equal(Ids(expected, "farmable"), Ranking.Farmable(rows, settings).Select(row => row.Id));
        Assert.Equal(Ids(expected, "mining"), Ranking.MiningFarms(rows, settings).Select(row => row.Id));
        Assert.Equal(Ids(expected, "botany"), Ranking.BotanyFarms(rows, settings).Select(row => row.Id));
        Assert.Equal((int?)expected["topFarm"], Ranking.TopFarm(rows, settings)?.Id);
        Assert.Equal(Ids(expected, "locked"), Ranking.Locked(rows, settings).Select(row => row.Id));
        Assert.Equal(Ids(expected, "retainerTargets"), Ranking.RetainerTargets(rows, settings).Select(row => row.Id));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Maps_crystals_movers_and_folklore_match_v1(string source, string config)
    {
        var (rows, _, settings, expected) = Load(source, config);
        Assert.Equal(Ids(expected, "maps"), Ranking.Maps(rows, settings).Select(row => row.Id));
        Assert.Equal((int?)expected["bestMap"], Ranking.BestMap(rows, settings)?.Id);
        Assert.Equal(Ids(expected, "crystals"), Ranking.Crystals(rows, settings).Select(row => row.Id));
        Assert.Equal(Ids(expected, "movers"), Ranking.Movers(rows).Select(row => row.Id));
        Assert.Equal(Ids(expected, "needsFolklore"), rows.Where(row => Ranking.NeedsFolklore(row, settings)).Select(row => row.Id));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Craft_gating_and_lists_match_v1(string source, string config)
    {
        var (rows, crafts, settings, expected) = Load(source, config);
        Assert.Equal(Ids(expected, "craftableBy"), crafts.Where(craft => Ranking.CraftableBy(craft, settings)).Select(craft => craft.Id));
        Assert.Equal(Ids(expected, "topValueCrafts"), Ranking.TopValueCrafts(crafts, rows, settings).Select(craft => craft.Id));
        Assert.Equal(Ids(expected, "craftingPageOnlyMine"), Ranking.CraftingPage(crafts, rows, settings, onlyMine: true).Select(craft => craft.Id));
        Assert.Equal(Ids(expected, "craftingPageAll"), Ranking.CraftingPage(crafts, rows, settings, onlyMine: false).Select(craft => craft.Id));
    }

    private static (List<SnapshotRow> Rows, List<CraftValue> Crafts, GilSweepSettings Settings, JsonNode Expected) Load(string source, string config)
    {
        var snapshot = source == "seed" ? SweepCharacterizationTests.Seed() : Sweep;
        var settings = ConfigStore.Read(Expected["configs"]![config]!.ToJsonString());
        var crafts = source == "seed" ? [] : Sweep.Crafts!;
        return (snapshot.Rows, crafts, settings, Expected[source]![config]!);
    }

    private static List<int> Ids(JsonNode expected, string name) => [.. expected[name]!.AsArray().Select(id => (int)id!)];
}
