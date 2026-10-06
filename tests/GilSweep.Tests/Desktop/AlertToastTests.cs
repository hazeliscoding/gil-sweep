using GilSweep.Core.Alerts;
using GilSweep.Core.Sweep;
using GilSweep.Core.Watchlist;
using GilSweep.Desktop.Services;

namespace GilSweep.Tests.Desktop;

public sealed class AlertToastTests : IDisposable
{
    private readonly DesktopTestHost _host = new();

    [Fact]
    public void Price_alerts_from_one_sweep_share_a_toast_like_v1()
    {
        _host.SetUpCharacter();
        AlertToasts.Attach(_host.Services, () => { });
        var watchlist = _host.Get<IWatchlistService>();
        watchlist.Watch(1);
        watchlist.Watch(2);

        _host.Get<IAlertService>().CheckSweep(new MarketSnapshot
        {
            World = "Cactuar",
            Rows = [Row(1, 40), Row(2, -30)],
        });

        var toast = Assert.Single(_host.Notifier.Shown);
        Assert.Equal("Price alerts · 2 watched items moved", toast.Title);
        Assert.Equal("Item 1 rose +40%\nItem 2 dropped −30%", toast.Body);
    }

    [Fact]
    public void A_single_alert_gets_its_own_toast()
    {
        _host.SetUpCharacter();
        AlertToasts.Attach(_host.Services, () => { });
        _host.Get<IWatchlistService>().Watch(1);

        _host.Get<IAlertService>().CheckSweep(new MarketSnapshot { World = "Cactuar", Rows = [Row(1, 50)] });

        Assert.Equal(("Item 1 rose +50%", "Now 1,000g on Cactuar, since the previous sweep."), Assert.Single(_host.Notifier.Shown));
    }

    public void Dispose() => _host.Dispose();

    private static SnapshotRow Row(int id, double change) => new() { Id = id, Name = $"Item {id}", Avg = 1000, AvgChangePct = change };
}
