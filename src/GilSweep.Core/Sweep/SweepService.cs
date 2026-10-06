using GilSweep.Core.Configuration;
using GilSweep.Core.History;
using Microsoft.Extensions.Logging;

namespace GilSweep.Core.Sweep;

/// <summary>Runs a sweep with the current settings and keeps the archive: saves it, thins and trims old snapshots.</summary>
public interface ISweepService
{
    /// <summary>True while a sweep runs. Updates wait for it.</summary>
    bool IsRunning { get; }

    event EventHandler<MarketSnapshot>? Completed;

    /// <summary>Sweeps now. While one runs, returns that one instead of starting another.</summary>
    Task<MarketSnapshot> RunAsync(IProgress<SweepProgress>? progress = null, CancellationToken cancellationToken = default);
}

public sealed class SweepService(
    ISweepEngine engine,
    IMarketSnapshotStore store,
    ISettingsService settings,
    TimeProvider clock,
    ILogger<SweepService> logger) : ISweepService
{
    /// <summary>Every sweep from the last two days is kept; older days keep their newest.</summary>
    public static readonly TimeSpan KeepAllFor = TimeSpan.FromHours(48);

    private readonly Lock _gate = new();
    private Task<MarketSnapshot>? _running;

    public bool IsRunning => _running is { IsCompleted: false };

    public event EventHandler<MarketSnapshot>? Completed;

    public Task<MarketSnapshot> RunAsync(IProgress<SweepProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_running is { IsCompleted: false } running)
            {
                return running;
            }

            _running = RunCoreAsync(progress, cancellationToken);
            return _running;
        }
    }

    private async Task<MarketSnapshot> RunCoreAsync(IProgress<SweepProgress>? progress, CancellationToken cancellationToken)
    {
        await Task.Yield();
        var current = settings.Current;
        var previous = store.Latest(current.World);
        var snapshot = await engine.RunAsync(current, previous, progress, cancellationToken).ConfigureAwait(false);
        store.Save(snapshot);
        ApplyRetention(current, clock.GetUtcNow());
        Completed?.Invoke(this, snapshot);
        return snapshot;
    }

    private void ApplyRetention(GilSweepSettings current, DateTimeOffset now)
    {
        var thinned = store.PruneToOnePerDay(now - KeepAllFor);
        var expired = current.HistoryRetentionDays is { } days ? store.DeleteOlderThan(now - TimeSpan.FromDays(days)) : 0;
        if (thinned + expired > 0)
        {
            logger.LogInformation("History: thinned {Thinned} snapshots to one per day, removed {Expired} past retention", thinned, expired);
        }
    }
}
