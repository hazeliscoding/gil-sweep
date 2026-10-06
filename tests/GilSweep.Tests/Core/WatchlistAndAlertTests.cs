using GilSweep.Core.Alerts;
using GilSweep.Core.Catalog;
using GilSweep.Core.Configuration;
using GilSweep.Core.Sweep;
using GilSweep.Core.Time;
using GilSweep.Core.Watchlist;
using GilSweep.Tests.Support;

namespace GilSweep.Tests.Core;

public sealed class WatchlistAndAlertTests : IDisposable
{
    // Dense Aluminum Ore: legendary, Dawntrail, level 100, windows at 10:00 and 22:00 ET.
    private const int DenseAluminum = 49208;

    // Black Star: a regular node.
    private const int BlackStar = 44012;
    private readonly CoreHost _host = new();

    public WatchlistAndAlertTests() =>
        _host.Get<ISettingsService>().Update(settings =>
        {
            settings.Levels = new GathererLevels { Miner = 100, Botanist = 100 };
            settings.MsqExpansion = Expansion.DT;
        });

    private ISettingsService Settings => _host.Get<ISettingsService>();

    private IWatchlistService Watchlist => _host.Get<IWatchlistService>();

    private IAlertService Alerts => _host.Get<IAlertService>();

    [Fact]
    public void Watching_a_timed_node_turns_on_node_reminders_spikes_and_crashes()
    {
        Watchlist.Watch(DenseAluminum);
        Watchlist.Watch(BlackStar);

        var timed = Watchlist.Get(DenseAluminum)!;
        Assert.True(timed.NodeOpens && timed.PriceSpike && timed.PriceCrash);
        Assert.False(timed.Undercut || timed.Favorite);
        Assert.False(Watchlist.Get(BlackStar)!.NodeOpens);
    }

    [Fact]
    public void Toggling_and_switches_are_saved()
    {
        Assert.True(Watchlist.Toggle(DenseAluminum));
        Watchlist.Set(DenseAluminum, entry => entry.Favorite = true);

        Assert.True(new ConfigStore(_host.Environment).Load().WatchOf(DenseAluminum)!.Favorite);
        Assert.False(Watchlist.Toggle(DenseAluminum));
        Assert.Empty(new ConfigStore(_host.Environment).Load().Watchlist);
    }

    [Fact]
    public void A_reminder_arrives_once_when_a_watched_node_is_about_to_open()
    {
        Watchlist.Watch(DenseAluminum);
        var opens = MomentAtEorzeaHour(10);

        _host.Clock.Now = opens - TimeSpan.FromMinutes(8);
        Assert.Empty(Alerts.CheckNodes());

        _host.Clock.Now = opens - TimeSpan.FromMinutes(4);
        var alert = Assert.Single(Alerts.CheckNodes());
        Assert.Equal(AlertKind.NodeOpening, alert.Kind);
        Assert.StartsWith("Dense Aluminum Ore opens in 4m", alert.Title, StringComparison.Ordinal);

        _host.Clock.Now = opens - TimeSpan.FromMinutes(1);
        Assert.Empty(Alerts.CheckNodes());
    }

    [Fact]
    public void With_no_lead_time_the_alert_comes_as_the_node_opens_like_v1()
    {
        Settings.Update(settings => settings.Alerts.NodeLeadMinutes = 0);
        Watchlist.Watch(DenseAluminum);
        var opens = MomentAtEorzeaHour(22);

        _host.Clock.Now = opens - TimeSpan.FromSeconds(30);
        Assert.Empty(Alerts.CheckNodes());
        _host.Clock.Now = opens + TimeSpan.FromSeconds(30);
        Assert.Equal(AlertKind.NodeOpen, Assert.Single(Alerts.CheckNodes()).Kind);
        _host.Clock.Now = opens + TimeSpan.FromMinutes(1);
        Assert.Empty(Alerts.CheckNodes());
    }

    [Fact]
    public void Nodes_the_character_cant_gather_send_no_reminders()
    {
        Settings.Update(settings => settings.MsqExpansion = Expansion.EW);
        Watchlist.Watch(DenseAluminum);

        _host.Clock.Now = MomentAtEorzeaHour(10) - TimeSpan.FromMinutes(2);

        Assert.Empty(Alerts.CheckNodes());
    }

    [Fact]
    public void Spikes_and_crashes_follow_each_items_switches_and_thresholds()
    {
        Watchlist.Watch(1);
        Watchlist.Watch(2);
        Watchlist.Watch(3);
        Watchlist.Set(3, entry => entry.PriceCrash = false);
        var snapshot = Snapshot(Row(1, change: 31), Row(2, change: -26), Row(3, change: -40), Row(4, change: 90));

        var alerts = Alerts.CheckSweep(snapshot);

        Assert.Equal([(1, AlertKind.PriceSpike), (2, AlertKind.PriceCrash)], alerts.Select(alert => (alert.ItemId, alert.Kind)));
        Assert.Contains("+31%", alerts[0].Title, StringComparison.Ordinal);
    }

    [Fact]
    public void An_undercut_is_reported_once_per_new_low()
    {
        Settings.Update(settings => settings.RetainerNames = ["Mochi"]);
        Watchlist.Watch(1);
        Watchlist.Set(1, entry => entry.Undercut = true);

        var first = Alerts.CheckSweep(Snapshot(Row(1, cheapest: 1200, own: 1450)));
        var same = Alerts.CheckSweep(Snapshot(Row(1, cheapest: 1200, own: 1450)));
        var lower = Alerts.CheckSweep(Snapshot(Row(1, cheapest: 1100, own: 1450)));

        Assert.Equal("Item 1: undercut by 250g", Assert.Single(first).Title);
        Assert.Empty(same);
        Assert.Single(lower);
    }

    [Fact]
    public void The_alert_log_keeps_the_newest_50_across_restarts()
    {
        for (var id = 1; id <= 30; id++)
        {
            Watchlist.Watch(id);
        }

        Alerts.CheckSweep(Snapshot([.. Enumerable.Range(1, 30).Select(id => Row(id, change: 50))]));
        Alerts.CheckSweep(Snapshot([.. Enumerable.Range(1, 30).Select(id => Row(id, change: 80))]));

        var reloaded = new AlertService(Settings, _host.Get<IItemCatalog>(), _host.Environment, _host.Clock, Microsoft.Extensions.Logging.Abstractions.NullLogger<AlertService>.Instance);
        Assert.Equal(AlertService.LogSize, reloaded.Recent.Count);
        Assert.Contains("+80%", reloaded.Recent[0].Title, StringComparison.Ordinal);
    }

    public void Dispose() => _host.Dispose();

    /// <summary>The first real moment after the test clock at which Eorzea time is exactly hour:00.</summary>
    private DateTimeOffset MomentAtEorzeaHour(int hour)
    {
        var at = _host.Clock.Now;
        var minutes = (hour * 60 - EorzeaTime.MinuteOfDay(at) + EorzeaTime.MinutesPerDay) % EorzeaTime.MinutesPerDay;
        return at + EorzeaTime.RealDuration(minutes) + TimeSpan.FromMilliseconds(5);
    }

    private static MarketSnapshot Snapshot(params SnapshotRow[] rows) => new() { World = "Cactuar", Date = "2026-10-05", Timestamp = "2026-10-05T18:30:00.000Z", Rows = [.. rows] };

    private static SnapshotRow Row(int id, double? change = null, long? cheapest = null, long? own = null) => new()
    {
        Id = id,
        Name = $"Item {id}",
        Avg = 1000,
        AvgChangePct = change,
        Listings = cheapest is { } c ? new ListingDepth { Cheapest = c, OwnCheapest = own, CheapestIsOwn = false, ListingsCount = 5, UnitsForSale = 50 } : null,
    };
}
