using System.Globalization;
using System.Text.Json.Nodes;
using GilSweep.Core.Catalog;
using GilSweep.Core.Configuration;
using GilSweep.Core.Time;
using GilSweep.Tests.Support;

namespace GilSweep.Tests.Core;

/// <summary>
/// Eorzea time and every timed node's window at 187 moments: a sweep across three Eorzean days
/// plus the exact millisecond each window opens and closes, and one either side.
/// </summary>
public sealed class EorzeaCharacterizationTests
{
    private static readonly JsonNode Expected = Fixture.Json("V1", "expected-eorzea.json");
    private static readonly IReadOnlyList<CatalogItem> Items = ItemCatalog.LoadBundledItems();
    private static readonly JsonNode Configs = Fixture.Json("V1", "expected-ranking.json")["configs"]!;

    [Fact]
    public void The_clock_matches_v1()
    {
        foreach (var moment in Expected["times"]!.AsArray())
        {
            var at = DateTimeOffset.FromUnixTimeMilliseconds((long)moment!["t"]!);
            Assert.Equal((double)moment["minuteOfDay"]!, EorzeaTime.MinuteOfDay(at));
            Assert.Equal((string)moment["clock"]!, EorzeaTime.Clock(at));
        }
    }

    [Fact]
    public void Node_windows_match_v1_at_every_moment()
    {
        foreach (var moment in Expected["times"]!.AsArray())
        {
            var at = DateTimeOffset.FromUnixTimeMilliseconds((long)moment!["t"]!);
            foreach (var item in Items.Where(item => item.IsTimed))
            {
                var expected = moment["nodes"]![item.Id.ToString(CultureInfo.InvariantCulture)]!;
                var window = EorzeaTime.Availability(item, at);
                var context = $"{item.Name} at {at:O}";
                Assert.True((bool)expected["up"]! == (window.State == NodeState.Open), context);
                Assert.True((int)expected["nextSpawnHour"]! == window.SpawnHour, context);
                Assert.True((int)expected["endsInRealMin"]! == (window.State == NodeState.Open ? window.RealMinutes : 0), context);
                Assert.True((int)expected["nextInRealMin"]! == (window.State == NodeState.Closed ? window.RealMinutes : 0), context);
            }
        }
    }

    [Theory]
    [InlineData("nextEndgame", "endgame")]
    [InlineData("nextV1Default", "v1Default")]
    public void Tray_windows_match_v1(string field, string config)
    {
        var settings = ConfigStore.Read(Configs[config]!.ToJsonString());
        foreach (var moment in Expected["times"]!.AsArray())
        {
            var at = DateTimeOffset.FromUnixTimeMilliseconds((long)moment!["t"]!);
            var expected = moment[field]!.AsArray().Select(w => ((string)w!["name"]!, (bool)w["up"]!, (int)w["minutes"]!));
            var actual = NodeWindows.Next(Items, settings, at).Select(w => (w.Item.Name, w.Up, w.Minutes));
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Eorzea_minutes_convert_to_real_minutes_like_v1()
    {
        foreach (var (etMinutes, realMinutes) in Expected["etMinToRealMin"]!.AsObject())
        {
            Assert.Equal((int)realMinutes!, EorzeaTime.RealMinutes(double.Parse(etMinutes, CultureInfo.InvariantCulture)));
        }
    }
}
