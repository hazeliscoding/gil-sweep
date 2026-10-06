using CommunityToolkit.Mvvm.ComponentModel;
using GilSweep.Core;
using GilSweep.Core.Alerts;
using GilSweep.Core.Configuration;
using GilSweep.Core.History;
using GilSweep.Core.Market.Universalis;
using GilSweep.Core.Sweep;
using GilSweep.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace GilSweep.Desktop.ViewModels;

/// <summary>
/// State every screen shares: the latest sweep for the configured world, the opportunity board
/// built from it (rebuilt as the Eorzea clock moves), and the sweep in progress, which keeps
/// running while the user moves between screens. Also runs the hourly sweep and the retry after a
/// failed one, and checks watched nodes for reminders.
/// </summary>
public sealed partial class AppSession : ObservableObject
{
    public static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan AutoSweepEvery = TimeSpan.FromHours(1);
    private static readonly TimeSpan StaleOnLaunch = TimeSpan.FromDays(1);
    private static readonly TimeSpan BoardEvery = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan NodeCheckEvery = TimeSpan.FromSeconds(10);

    private readonly ISettingsService _settings;
    private readonly ISweepService _sweeps;
    private readonly IMarketSnapshotStore _store;
    private readonly IAlertService _alerts;
    private readonly IUniversalisClient _universalis;
    private readonly IUiThread _ui;
    private readonly TimeProvider _clock;
    private readonly ILogger<AppSession> _logger;
    private DateTimeOffset _lastBoard = DateTimeOffset.MinValue;
    private DateTimeOffset _lastNodeCheck = DateTimeOffset.MinValue;
    private string _world;
    private string? _sweepingWorld;
    private bool _sweepAgain;
    private bool _initialized;

    public AppSession(
        ISettingsService settings,
        ISweepService sweeps,
        IMarketSnapshotStore store,
        IAlertService alerts,
        IUniversalisClient universalis,
        ITicker ticker,
        IUiThread ui,
        TimeProvider clock,
        ILogger<AppSession> logger)
    {
        _settings = settings;
        _sweeps = sweeps;
        _store = store;
        _alerts = alerts;
        _universalis = universalis;
        _ui = ui;
        _clock = clock;
        _logger = logger;
        _world = settings.Current.World;
        Board = OpportunityBoard.Empty(clock.GetUtcNow());
        _settings.Changed += (_, _) => _ui.Post(OnSettingsChanged);
        ticker.Tick += (_, _) => OnTick();
    }

    public GilSweepSettings Settings => _settings.Current;

    public DateTimeOffset Now => _clock.GetUtcNow();

    /// <summary>Raised once a second, after the session has caught up with the clock.</summary>
    public event EventHandler? Ticked;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasData), nameof(IsEmpty), nameof(IsFirstLoad), nameof(IsOffline))]
    public partial MarketSnapshot? Snapshot { get; private set; }

    [ObservableProperty]
    public partial MarketSnapshot? Baseline { get; private set; }

    [ObservableProperty]
    public partial OpportunityBoard Board { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(IsFirstLoad))]
    public partial bool IsSweeping { get; private set; }

    [ObservableProperty]
    public partial SweepProgress? Progress { get; private set; }

    /// <summary>Why the last sweep failed, for the offline banner. Cleared by the next good one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOffline), nameof(IsEmpty))]
    public partial string? SweepError { get; private set; }

    [ObservableProperty]
    public partial DateTimeOffset? NextRetryAt { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> Worlds { get; private set; } = [];

    public bool HasData => Snapshot is not null;

    /// <summary>No sweep for this world yet and none running: the empty state.</summary>
    public bool IsEmpty => Snapshot is null && !IsSweeping;

    /// <summary>The first sweep for this world is running: the loading state.</summary>
    public bool IsFirstLoad => Snapshot is null && IsSweeping;

    /// <summary>The market couldn't be reached; what's on screen is the cached snapshot.</summary>
    public bool IsOffline => SweepError is not null;

    public TimeSpan? SnapshotAge => Snapshot is { } snapshot ? Now - snapshot.TakenAt : null;

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        LoadWorld();
        _ = LoadWorldsAsync();
        if (!_settings.IsFirstRun && NeedsSweepOnLaunch())
        {
            _ = SweepAsync();
        }
    }

    /// <summary>
    /// Sweeps now. A sweep already running for this world is enough; one running for another world
    /// (the user just switched) is followed by a sweep of this one. Failures become
    /// <see cref="SweepError"/>, with a retry in a few minutes.
    /// </summary>
    public Task SweepAsync()
    {
        if (IsSweeping)
        {
            _sweepAgain |= !string.Equals(_sweepingWorld, Settings.World, StringComparison.Ordinal);
            return Task.CompletedTask;
        }

        return RunSweepsAsync();
    }

    /// <summary>Sweeps once more after the running sweep, which started before a change it should include (a newly tracked item).</summary>
    public void SweepAfterCurrent()
    {
        if (IsSweeping)
        {
            _sweepAgain = true;
            return;
        }

        _ = RunSweepsAsync();
    }

    private async Task RunSweepsAsync()
    {
        do
        {
            _sweepAgain = false;
            await SweepOnceAsync();
        }
        while (_sweepAgain);
    }

    private async Task SweepOnceAsync()
    {
        var world = Settings.World;
        _sweepingWorld = world;
        IsSweeping = true;
        Progress = null;
        try
        {
            var progress = new UiProgress<SweepProgress>(_ui, value => Progress = value);
            var snapshot = await _sweeps.RunAsync(progress);

            // A sweep of a world the user has since left is kept in History but not shown or alerted on.
            if (IsCurrent(world))
            {
                SweepError = null;
                NextRetryAt = null;
                Accept(snapshot);
                _alerts.CheckSweep(snapshot);
            }
        }
        catch (GilSweepException ex)
        {
            Fail(world, ex.Message, ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Fail(world, "The sweep could not be saved: " + ex.Message, ex);
        }
        finally
        {
            IsSweeping = false;
            Progress = null;
            _sweepingWorld = null;
        }
    }

    private void Fail(string world, string message, Exception exception)
    {
        _logger.LogWarning(exception, "Sweep of {World} failed", world);
        if (IsCurrent(world))
        {
            SweepError = message;
            NextRetryAt = Now + RetryAfter;
        }
    }

    private bool IsCurrent(string world) => string.Equals(world, Settings.World, StringComparison.Ordinal);

    /// <summary>Rebuilds the board for this moment, so node windows and countdowns are current.</summary>
    public void RefreshBoard()
    {
        _lastBoard = Now;
        Board = Snapshot is { } snapshot
            ? OpportunityBoard.Build(snapshot, Baseline, Settings, Now)
            : OpportunityBoard.Empty(Now);
    }

    private void OnTick()
    {
        var now = Now;
        if (now - _lastBoard >= BoardEvery)
        {
            RefreshBoard();
        }

        if (now - _lastNodeCheck >= NodeCheckEvery)
        {
            _lastNodeCheck = now;
            _alerts.CheckNodes();
        }

        if (!IsSweeping && !_settings.IsFirstRun)
        {
            var retryDue = NextRetryAt is { } retry && now >= retry;
            var hourlyDue = Settings.AutoSweep && SweepError is null && SnapshotAge is { } age && age >= AutoSweepEvery;
            if (retryDue || hourlyDue)
            {
                _ = SweepAsync();
            }
        }

        Ticked?.Invoke(this, EventArgs.Empty);
    }

    private void OnSettingsChanged()
    {
        if (!string.Equals(_world, Settings.World, StringComparison.Ordinal))
        {
            // Prices are per world: show what we have for the new one and sweep it right away.
            _world = Settings.World;
            SweepError = null;
            NextRetryAt = null;
            LoadWorld();
            _ = SweepAsync();
            return;
        }

        RefreshBoard();
    }

    private void LoadWorld()
    {
        var snapshot = _store.Latest(Settings.World);
        Snapshot = snapshot;
        Baseline = snapshot is null ? null : OpportunityBoard.TrendBaseline(_store.Load(Settings.World), snapshot);
        RefreshBoard();
    }

    private void Accept(MarketSnapshot snapshot)
    {
        if (!string.Equals(snapshot.World, Settings.World, StringComparison.Ordinal))
        {
            return;
        }

        Snapshot = snapshot;
        Baseline = OpportunityBoard.TrendBaseline(_store.Load(snapshot.World), snapshot);
        RefreshBoard();
    }

    /// <summary>v1 refreshed on launch when the data was a day old or from another world; hourly sweeps tighten that to an hour.</summary>
    private bool NeedsSweepOnLaunch() =>
        Snapshot is null || SnapshotAge >= (Settings.AutoSweep ? AutoSweepEvery : StaleOnLaunch);

    private async Task LoadWorldsAsync()
    {
        try
        {
            var worlds = await _universalis.GetWorldsAsync();
            _ui.Post(() => Worlds = worlds);
        }
        catch (GilSweepException ex)
        {
            // The configured world still works offline; the picker just has fewer choices.
            _logger.LogInformation("World list unavailable: {Message}", ex.Message);
            _ui.Post(() => Worlds = [Settings.World]);
        }
    }
}

/// <summary>Reports progress on the UI thread, in order.</summary>
public sealed class UiProgress<T>(IUiThread uiThread, Action<T> report) : IProgress<T>
{
    public void Report(T value) => uiThread.Post(() => report(value));
}
