using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using GilSweep.Core.Configuration;
using GilSweep.Desktop.Services;
using GilSweep.Desktop.ViewModels;
using GilSweep.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GilSweep.Desktop;

public partial class App : Application
{
    private MainWindow? _window;
    private TrayController? _tray;

    /// <summary>Lets the screenshot tool and UI tests supply their own services (fake market, fixed clock).</summary>
    internal static Func<IServiceProvider>? ServicesOverride { get; set; }

    public IServiceProvider Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        Services = ServicesOverride?.Invoke() ?? new ServiceCollection().AddGilSweepDesktop().BuildServiceProvider();

        // Motion is opt-out: with Windows animations off, the animation styles are never loaded.
        if (!Services.GetRequiredService<IMotionSettings>().ReduceMotion)
        {
            Styles.Add(new StyleInclude(new Uri("avares://GilSweep/App.axaml")) { Source = new Uri("avares://GilSweep/Themes/Motion.axaml") });
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Closing the window can keep Gil Sweep in the tray, so only Quit ends it.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.MainWindow = CreateMainWindow();
            _tray = TrayController.TryCreate(Services, ShowMainWindow, () => desktop.Shutdown());
            AlertToasts.Attach(Services, ShowMainWindow);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Switches to another set of services (another fake PC) and opens a window on it.</summary>
    internal MainWindow CreateMainWindow(IServiceProvider services)
    {
        Services = services;
        return CreateMainWindow();
    }

    public MainWindow CreateMainWindow()
    {
        var viewModel = Services.GetRequiredService<MainWindowViewModel>();
        var window = new MainWindow { DataContext = viewModel };
        Services.GetRequiredService<WindowServices>().TopLevel = window;
        window.Closing += OnClosing;
        _window = window;
        _ = viewModel.InitializeAsync();
        return window;
    }

    /// <summary>Brings the window back from the tray, a toast or a second launch.</summary>
    public void ShowMainWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_tray is not null && Services.GetRequiredService<ISettingsService>().Current.CloseToTray && !e.IsProgrammatic)
        {
            e.Cancel = true;
            _window?.Hide();
            return;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _tray?.Dispose();
            desktop.Shutdown();
        }
    }
}
