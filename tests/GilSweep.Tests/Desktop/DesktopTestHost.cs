using GilSweep.Core.Catalog;
using GilSweep.Core.Configuration;
using GilSweep.Core.Market;
using GilSweep.Core.Platform;
using GilSweep.Core.Sweep;
using GilSweep.Desktop;
using GilSweep.Desktop.Services;
using GilSweep.Desktop.ViewModels;
using GilSweep.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace GilSweep.Tests.Desktop;

/// <summary>The desktop's view models on real Core services and recorded market data, with every UI service faked.</summary>
public sealed class DesktopTestHost : IDisposable
{
    private readonly ServiceProvider _provider;

    public DesktopTestHost()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAppEnvironment>(Environment);
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton(new MarketHttpOptions { RetryDelay = TimeSpan.Zero, RequestTimeout = TimeSpan.FromSeconds(5) });
        services.AddGilSweepDesktop();
        services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => Market));

        // Registered after the app's own, so these win.
        services.AddSingleton<IUiThread, InlineUiThread>();
        services.AddSingleton<ITicker>(Ticker);
        services.AddSingleton<IMotionSettings, NoMotion>();
        services.AddSingleton<IDialogService>(Dialogs);
        services.AddSingleton<IShellService>(Shell);
        services.AddSingleton<IFilePicker>(Picker);
        services.AddSingleton<INotifier>(Notifier);
        services.AddSingleton<IAppUpdater>(Updater);
        services.AddSingleton<IAppInstances>(Instances);
        _provider = services.BuildServiceProvider();
    }

    public TestEnvironment Environment { get; } = new();

    public TestClock Clock { get; } = new(new DateTimeOffset(2026, 10, 5, 18, 30, 0, TimeSpan.Zero));

    public FakeMarketHandler Market { get; } = new();

    public ManualTicker Ticker { get; } = new();

    public FakeDialogs Dialogs { get; } = new();

    public FakeShell Shell { get; } = new();

    public FakePicker Picker { get; } = new();

    public FakeNotifier Notifier { get; } = new();

    public FakeUpdater Updater { get; } = new();

    public FakeInstances Instances { get; } = new();

    public T Get<T>()
        where T : notnull => _provider.GetRequiredService<T>();

    public IServiceProvider Services => _provider;

    public AppSession Session => Get<AppSession>();

    /// <summary>An endgame Miner and Botanist on Cactuar, past the first run.</summary>
    public void SetUpCharacter() =>
        Get<ISettingsService>().Update(settings =>
        {
            settings.Levels = new GathererLevels { Miner = 100, Botanist = 100 };
            settings.MsqExpansion = Expansion.DT;
            settings.Folklore = [Expansion.HW, Expansion.StB, Expansion.ShB, Expansion.EW, Expansion.DT];
        });

    /// <summary>A character and one sweep, with the window's view model started.</summary>
    public async Task<MainWindowViewModel> StartWithSweepAsync()
    {
        SetUpCharacter();
        await Get<ISweepService>().RunAsync();
        var main = Get<MainWindowViewModel>();
        await main.InitializeAsync();
        await SettleAsync();
        return main;
    }

    /// <summary>Waits for background sweeps the view models started.</summary>
    public async Task SettleAsync()
    {
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        await Task.Delay(20);
        while (Session.IsSweeping && DateTime.UtcNow < until)
        {
            await Task.Delay(20);
        }
    }

    /// <summary>Moves the clock and lets one second pass for the view models.</summary>
    public void Advance(TimeSpan time)
    {
        Clock.Advance(time);
        Ticker.Beat();
    }

    public void Dispose()
    {
        _provider.Dispose();
        Environment.Dispose();
    }

    private sealed class InlineUiThread : IUiThread
    {
        public void Post(Action action) => action();
    }

    private sealed class NoMotion : IMotionSettings
    {
        public bool ReduceMotion => true;
    }
}

public sealed class ManualTicker : ITicker
{
    public event EventHandler? Tick;

    public void Beat() => Tick?.Invoke(this, EventArgs.Empty);
}

public sealed class FakeDialogs : IDialogService
{
    public bool Answer { get; set; } = true;

    public List<ConfirmRequest> Asked { get; } = [];

    public Task<bool> ConfirmAsync(ConfirmRequest request)
    {
        Asked.Add(request);
        return Task.FromResult(Answer);
    }
}

public sealed class FakeShell : IShellService
{
    public List<string> Opened { get; } = [];

    public void OpenUrl(string url) => Opened.Add(url);

    public void OpenFolder(string path) => Opened.Add(path);
}

public sealed class FakePicker : IFilePicker
{
    public string? Path { get; set; }

    public Task<string?> SaveCsvAsync(string suggestedName) => Task.FromResult(Path);
}

public sealed class FakeNotifier : INotifier
{
    public List<(string Title, string Body)> Shown { get; } = [];

    public void Show(string title, string body) => Shown.Add((title, body));
}

public sealed class FakeUpdater : IAppUpdater
{
    public bool IsInstalled { get; set; } = true;

    public string? Latest { get; set; }

    public bool Restarted { get; private set; }

    public Task<AvailableUpdate?> CheckAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Latest is null ? null : new AvailableUpdate(Latest));

    public Task DownloadAsync(AvailableUpdate update, Action<int> progress, CancellationToken cancellationToken = default)
    {
        progress(100);
        return Task.CompletedTask;
    }

    public void RestartToApply(AvailableUpdate update) => Restarted = true;
}

public sealed class FakeInstances : IAppInstances
{
    public bool OthersRunning { get; set; }
}

internal static class SweepTestExtensions
{
    public static MarketSnapshot Latest(this DesktopTestHost host) => host.Session.Snapshot!;
}
