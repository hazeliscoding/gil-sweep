using GilSweep.Core;
using GilSweep.Core.Logging;
using GilSweep.Core.Platform;
using GilSweep.Desktop.Services;
using GilSweep.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GilSweep.Desktop;

public static class DesktopServices
{
    /// <summary>Core plus the desktop's view models and UI services. Tests and the screenshot tool replace UI services afterwards.</summary>
    public static IServiceCollection AddGilSweepDesktop(this IServiceCollection services)
    {
        services.AddGilSweepCore();
        services.AddSingleton<ILoggerProvider>(provider =>
            new FileLoggerProvider(Path.Combine(provider.GetRequiredService<IAppEnvironment>().DataDirectory, "logs")));

        services.AddSingleton<Navigator>();
        services.AddSingleton<INavigator>(provider => provider.GetRequiredService<Navigator>());
        services.AddSingleton<OverlayDialogService>();
        services.AddSingleton<IDialogService>(provider => provider.GetRequiredService<OverlayDialogService>());
        services.AddSingleton<WindowServices>();
        services.AddSingleton<IFilePicker>(provider => provider.GetRequiredService<WindowServices>());
        services.AddSingleton<IShellService, WindowsShellService>();
        services.AddSingleton<IUiThread, AvaloniaUiThread>();
        services.AddSingleton<ITicker, DispatcherTicker>();
        services.AddSingleton<IMotionSettings, WindowsMotionSettings>();
        services.AddSingleton<WindowsToastNotifier>();
        services.AddSingleton<INotifier>(provider => provider.GetRequiredService<WindowsToastNotifier>());
        services.AddSingleton<IAppUpdater, VelopackUpdater>();
        services.AddSingleton<IAppInstances, ProcessAppInstances>();

        services.AddSingleton<ProblemReporter>();
        services.AddSingleton<AppSession>();
        services.AddSingleton<UpdatesViewModel>();
        services.AddSingleton<OnboardingViewModel>();
        services.AddSingleton<SweepViewModel>();
        services.AddSingleton<MarketViewModel>();
        services.AddSingleton<CraftViewModel>();
        services.AddSingleton<WatchlistViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<PageViewModel>(provider => provider.GetRequiredService<SweepViewModel>());
        services.AddSingleton<PageViewModel>(provider => provider.GetRequiredService<MarketViewModel>());
        services.AddSingleton<PageViewModel>(provider => provider.GetRequiredService<CraftViewModel>());
        services.AddSingleton<PageViewModel>(provider => provider.GetRequiredService<WatchlistViewModel>());
        services.AddSingleton<PageViewModel>(provider => provider.GetRequiredService<HistoryViewModel>());
        services.AddSingleton<PageViewModel>(provider => provider.GetRequiredService<SettingsViewModel>());
        services.AddSingleton<MainWindowViewModel>();
        return services;
    }
}
