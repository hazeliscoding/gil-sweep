using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using GilSweep.Core.Alerts;
using GilSweep.Core.Catalog;
using GilSweep.Core.Configuration;
using GilSweep.Core.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GilSweep.Desktop.Services;

/// <summary>
/// The spawn-clock tray (v1's): the Eorzea time in the tooltip and the next node windows for
/// your character in the menu, refreshed twice a minute. Closing the window keeps the app here.
/// </summary>
public sealed class TrayController : IDisposable
{
    private readonly TrayIcon _icon;
    private readonly IItemCatalog _catalog;
    private readonly ISettingsService _settings;
    private readonly TimeProvider _clock;
    private readonly Action _show;
    private readonly Action _quit;
    private int _ticks;

    private TrayController(IServiceProvider services, Action show, Action quit)
    {
        _catalog = services.GetRequiredService<IItemCatalog>();
        _settings = services.GetRequiredService<ISettingsService>();
        _clock = services.GetRequiredService<TimeProvider>();
        _show = show;
        _quit = quit;
        _icon = new TrayIcon { Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://GilSweep/Assets/gil-sweep.ico"))) };
        _icon.Clicked += (_, _) => _show();
        TrayIcon.SetIcons(Application.Current!, [_icon]);
        services.GetRequiredService<ITicker>().Tick += (_, _) =>
        {
            if (++_ticks % 30 == 0)
            {
                Refresh();
            }
        };
        Refresh();
    }

    /// <summary>A tray failure (no system tray) must never take the app down; closing then simply quits.</summary>
    public static TrayController? TryCreate(IServiceProvider services, Action show, Action quit)
    {
        try
        {
            return new TrayController(services, show, quit);
        }
        catch (Exception ex)
        {
            services.GetRequiredService<ILogger<TrayController>>().LogWarning(ex, "Tray unavailable");
            return null;
        }
    }

    public void Dispose() => _icon.Dispose();

    private void Refresh()
    {
        var now = _clock.GetUtcNow();
        var clock = EorzeaTime.Clock(now);
        _icon.ToolTipText = $"Gil Sweep · ET {clock}";
        var menu = new NativeMenu();
        menu.Items.Add(new NativeMenuItem($"ET {clock}") { IsEnabled = false });
        menu.Items.Add(new NativeMenuItemSeparator());
        foreach (var window in NodeWindows.Next(_catalog.Items, _settings.Current, now))
        {
            var item = new NativeMenuItem(window.Up
                ? $"● {window.Item.Name}: open, closes in about {window.Minutes} min"
                : $"{window.Item.Name}: opens in about {window.Minutes} min");
            item.Click += (_, _) => _show();
            menu.Items.Add(item);
        }

        menu.Items.Add(new NativeMenuItemSeparator());
        var open = new NativeMenuItem("Open Gil Sweep");
        open.Click += (_, _) => _show();
        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => _quit();
        menu.Items.Add(open);
        menu.Items.Add(quit);
        _icon.Menu = menu;
    }
}

/// <summary>Turns alerts into Windows toasts. Price alerts from one sweep share a toast, as in v1.</summary>
public static class AlertToasts
{
    public static void Attach(IServiceProvider services, Action show)
    {
        var notifier = services.GetRequiredService<INotifier>();
        if (notifier is WindowsToastNotifier toasts)
        {
            toasts.Activated += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(show);
        }

        services.GetRequiredService<IAlertService>().Raised += (_, alerts) =>
        {
            var prices = alerts.Where(alert => alert.Kind is AlertKind.PriceSpike or AlertKind.PriceCrash).ToList();
            foreach (var alert in alerts.Except(prices))
            {
                notifier.Show(alert.Title, alert.Body);
            }

            if (prices.Count == 1)
            {
                notifier.Show(prices[0].Title, prices[0].Body);
            }
            else if (prices.Count > 1)
            {
                notifier.Show($"Price alerts · {prices.Count} watched items moved", string.Join("\n", prices.Take(4).Select(alert => alert.Title)));
            }
        };
    }
}
