using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using GilSweep.Desktop;
using GilSweep.Desktop.Services;
using GilSweep.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GilSweep.Screenshots;

internal static class Program
{
    private static string _output = "";

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHarfBuzz()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    public static int Main(string[] args)
    {
        _output = Path.GetFullPath(args.FirstOrDefault() ?? "screenshots");
        Directory.CreateDirectory(_output);
        var root = Path.Combine(Path.GetTempPath(), "gil-sweep-screens-" + Guid.NewGuid().ToString("N")[..8]);

        using var world = World.Create(root + "-seasoned", WorldKind.Seasoned);
        App.ServicesOverride = () => world.Services;
        BuildAvaloniaApp().SetupWithoutStarting();

        var window = Open(world);
        var main = (MainWindowViewModel)window.DataContext!;
        var services = world.Services;

        Shot(window, "sweep");
        var sweep = services.GetRequiredService<SweepViewModel>();
        sweep.SessionLength = sweep.SessionLengths[2];
        Shot(window, "sweep-session");
        sweep.SessionLength = sweep.SessionLengths[0];

        Go(main, AppPage.Market, services.GetRequiredService<AppSession>().Board.Best?.ItemId);
        Shot(window, "market");
        Go(main, AppPage.Craft);
        Shot(window, "craft");
        Go(main, AppPage.Watchlist);
        Shot(window, "watchlist");
        Go(main, AppPage.History);
        Shot(window, "history");
        Go(main, AppPage.Settings);
        Shot(window, "settings");
        world.Updater.Latest = "2.1.0";
        Run(services.GetRequiredService<UpdatesViewModel>().CheckNowCommand.ExecuteAsync(null));
        ScrollToEnd(window);
        Shot(window, "settings-update");

        // Universalis goes down two hours later: the cached sweep stays on screen with a banner.
        Go(main, AppPage.Sweep);
        world.Market.Down = true;
        world.Clock.Now += TimeSpan.FromMinutes(134);
        world.Ticker.Beat();
        Run(services.GetRequiredService<AppSession>().SweepAsync());
        Shot(window, "offline");
        window.Close();

        using var fresh = World.Create(root + "-fresh", WorldKind.FirstRun);
        var freshWindow = Open(fresh);
        Shot(freshWindow, "onboarding");
        ((MainWindowViewModel)freshWindow.DataContext!).Onboarding.IsOpen = false;
        Shot(freshWindow, "empty");
        freshWindow.Close();

        using var loading = World.Create(root + "-loading", WorldKind.Loading);
        var loadingWindow = Open(loading);
        Shot(loadingWindow, "loading");
        loadingWindow.Close();

        Console.WriteLine($"Screenshots written to {_output}");
        return 0;
    }

    private static Window Open(World world)
    {
        var window = ((App)Application.Current!).CreateMainWindow(world.Services);
        window.Width = 1100;
        window.Height = 720;
        window.Show();
        Settle(TimeSpan.FromSeconds(3));
        return window;
    }

    private static void Go(MainWindowViewModel main, AppPage page, int? item = null)
    {
        Run(main.ShowAsync(page, item));
        Settle(TimeSpan.FromSeconds(1.5));
    }

    private static void Run(Task task)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }

        Settle(TimeSpan.FromMilliseconds(400));
    }

    private static void Settle(TimeSpan time)
    {
        var until = DateTime.UtcNow + time;
        while (DateTime.UtcNow < until)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(15);
        }
    }

    private static void ScrollToEnd(Window window)
    {
        window.FindControl<ScrollViewer>("PageScroller")?.ScrollToEnd();
        Settle(TimeSpan.FromMilliseconds(300));
    }

    private static void Shot(Window window, string name)
    {
        Settle(TimeSpan.FromMilliseconds(300));
        var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame was rendered.");
#pragma warning disable CS0618 // The replacement overload needs encoder options this tool has no use for.
        frame.Save(Path.Combine(_output, name + ".png"));
#pragma warning restore CS0618
        Console.WriteLine($"  {name}.png");
    }
}
