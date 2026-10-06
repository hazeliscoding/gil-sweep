using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Threading;
using GilSweep.Desktop.Services;
using Velopack;

namespace GilSweep.Desktop;

internal static class Program
{
    private const string InstanceName = @"Local\hazeliscoding.GilSweep";

    [STAThread]
    [SupportedOSPlatform("windows10.0.17763.0")]
    public static int Main(string[] args)
    {
        // Setup and the uninstaller start the app with their own arguments; Velopack handles those
        // and exits. A downloaded update is only installed when the user chooses it in Settings.
        if (VelopackUpdater.IsInstalledWithSetup())
        {
            VelopackApp.Build()
                .SetAutoApplyOnStartup(false)
                .OnBeforeUninstallFastCallback(_ => WindowsToastNotifier.Unregister())
                .Run();
        }

        // One copy at a time: a second launch brings the running window (perhaps in the tray) forward.
        using var mutex = new Mutex(true, InstanceName, out var first);
        using var show = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceName + ".Show");
        if (!first)
        {
            show.Set();
            return 0;
        }

        var listener = new Thread(() =>
        {
            while (show.WaitOne())
            {
                Dispatcher.UIThread.Post(() => (Application.Current as App)?.ShowMainWindow());
            }
        })
        { IsBackground = true, Name = "Second launch" };
        listener.Start();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
