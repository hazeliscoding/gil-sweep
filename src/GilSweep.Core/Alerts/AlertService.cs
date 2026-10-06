using System.Globalization;
using System.Text.Json;
using GilSweep.Core.Catalog;
using GilSweep.Core.Configuration;
using GilSweep.Core.Platform;
using GilSweep.Core.Serialization;
using GilSweep.Core.Sweep;
using GilSweep.Core.Time;
using Microsoft.Extensions.Logging;

namespace GilSweep.Core.Alerts;

public enum AlertKind
{
    NodeOpening,
    NodeOpen,
    PriceSpike,
    PriceCrash,
    Undercut,
}

public sealed record AlertEvent(AlertKind Kind, int ItemId, string ItemName, string Title, string Body, DateTimeOffset At);

/// <summary>
/// Notifications for watched items: a reminder before a node opens, price spikes and crashes
/// after a sweep, and undercuts of your retainers. Every alert is kept in a short local log.
/// </summary>
public interface IAlertService
{
    /// <summary>Recent alerts, newest first.</summary>
    IReadOnlyList<AlertEvent> Recent { get; }

    /// <summary>Raised with the alerts from one check; the desktop shows them as toasts.</summary>
    event EventHandler<IReadOnlyList<AlertEvent>>? Raised;

    /// <summary>Checks watched nodes against the Eorzea clock. Call every few seconds.</summary>
    IReadOnlyList<AlertEvent> CheckNodes();

    /// <summary>Checks watched items' prices and listings in a finished sweep.</summary>
    IReadOnlyList<AlertEvent> CheckSweep(MarketSnapshot snapshot);
}

public sealed class AlertService : IAlertService
{
    public const int LogSize = 50;

    private readonly ISettingsService _settings;
    private readonly IItemCatalog _catalog;
    private readonly TimeProvider _clock;
    private readonly ILogger<AlertService> _logger;
    private readonly string _logPath;
    private readonly Lock _gate = new();
    private readonly Dictionary<int, bool> _wasOpen = [];
    private readonly Dictionary<int, long> _remindedWindow = [];
    private readonly Dictionary<int, long> _undercutAt = [];
    private List<AlertEvent> _recent;

    public AlertService(ISettingsService settings, IItemCatalog catalog, IAppEnvironment environment, TimeProvider clock, ILogger<AlertService> logger)
    {
        _settings = settings;
        _catalog = catalog;
        _clock = clock;
        _logger = logger;
        _logPath = Path.Combine(environment.DataDirectory, "alerts.json");
        _recent = Load();
    }

    public IReadOnlyList<AlertEvent> Recent
    {
        get
        {
            lock (_gate)
            {
                return [.. _recent];
            }
        }
    }

    public event EventHandler<IReadOnlyList<AlertEvent>>? Raised;

    public IReadOnlyList<AlertEvent> CheckNodes()
    {
        var settings = _settings.Current;
        var now = _clock.GetUtcNow();
        var lead = TimeSpan.FromMinutes(settings.Alerts.NodeLeadMinutes);
        var alerts = new List<AlertEvent>();
        lock (_gate)
        {
            foreach (var entry in settings.Watchlist.Where(entry => entry.NodeOpens))
            {
                if (_catalog.Find(entry.ItemId) is not { IsTimed: true } item)
                {
                    continue;
                }

                // Only nodes this character can gather (v1's rule).
                var node = NodeWindows.CanGather(item, settings) ? EorzeaTime.Availability(item, now) : null;
                var open = node?.State == NodeState.Open;
                if (node is not null && lead > TimeSpan.Zero && node.State == NodeState.Closed && node.RealRemaining <= lead)
                {
                    // One reminder per window, keyed by when it opens (to the second).
                    var window = (now + node.RealRemaining).ToUnixTimeSeconds();
                    if (!_remindedWindow.TryGetValue(item.Id, out var reminded) || Math.Abs(reminded - window) > 5)
                    {
                        _remindedWindow[item.Id] = window;
                        alerts.Add(new AlertEvent(AlertKind.NodeOpening, item.Id, item.Name,
                            $"{item.Name} opens in {Formatting.Countdown(node.RealRemaining)}",
                            $"{item.Where}. The window lasts {Formatting.Countdown(EorzeaTime.RealDuration(node.UptimeEtMinutes))}.", now));
                    }
                }
                else if (node is not null && lead == TimeSpan.Zero && open && _wasOpen.TryGetValue(item.Id, out var wasOpen) && !wasOpen)
                {
                    // v1's behavior: a toast on the closed → open transition.
                    alerts.Add(new AlertEvent(AlertKind.NodeOpen, item.Id, item.Name,
                        $"Node up: {item.Name}",
                        $"{item.Where}. Ends in about {node.RealMinutes} min.", now));
                }

                _wasOpen[item.Id] = open;
            }
        }

        Publish(alerts);
        return alerts;
    }

    public IReadOnlyList<AlertEvent> CheckSweep(MarketSnapshot snapshot)
    {
        var settings = _settings.Current;
        var now = _clock.GetUtcNow();
        var alerts = new List<AlertEvent>();
        lock (_gate)
        {
            foreach (var entry in settings.Watchlist)
            {
                if (snapshot.Find(entry.ItemId) is not { } row)
                {
                    continue;
                }

                // v1's spike check: the change since the previous sweep, both directions.
                if (row.AvgChangePct is { } change)
                {
                    if (entry.PriceSpike && change >= settings.Alerts.SpikePercent)
                    {
                        alerts.Add(new AlertEvent(AlertKind.PriceSpike, row.Id, row.Name,
                            $"{row.Name} rose {Formatting.SignedPercent(change)}",
                            $"Now {Formatting.Gil(row.Avg)} on {snapshot.World}, since the previous sweep.", now));
                    }
                    else if (entry.PriceCrash && change <= -settings.Alerts.CrashPercent)
                    {
                        alerts.Add(new AlertEvent(AlertKind.PriceCrash, row.Id, row.Name,
                            $"{row.Name} dropped {Formatting.SignedPercent(change)}",
                            $"Now {Formatting.Gil(row.Avg)} on {snapshot.World}, since the previous sweep.", now));
                    }
                }

                if (entry.Undercut && row.Listings is { OwnCheapest: { } own, CheapestIsOwn: false, Cheapest: var cheapest } && cheapest > 0 && cheapest < own)
                {
                    // Once per new low, not on every sweep while it stays undercut.
                    if (!_undercutAt.TryGetValue(row.Id, out var notified) || cheapest < notified)
                    {
                        _undercutAt[row.Id] = cheapest;
                        alerts.Add(new AlertEvent(AlertKind.Undercut, row.Id, row.Name,
                            $"{row.Name}: undercut by {Formatting.Gil(own - cheapest)}",
                            $"Cheapest is now {Formatting.Gil(cheapest)}; yours is {Formatting.Gil(own)}.", now));
                    }
                }
                else if (row.Listings is { CheapestIsOwn: true })
                {
                    _undercutAt.Remove(row.Id);
                }
            }
        }

        Publish(alerts);
        return alerts;
    }

    private void Publish(List<AlertEvent> alerts)
    {
        if (alerts.Count == 0)
        {
            return;
        }

        lock (_gate)
        {
            _recent = [.. alerts.AsEnumerable().Reverse().Concat(_recent).Take(LogSize)];
            try
            {
                AtomicFile.WriteAllText(_logPath, JsonSerializer.Serialize(_recent, GilSweepJsonContext.Compact.ListAlertEvent));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not save the alert log");
            }
        }

        _logger.LogInformation("Raised {Count} alerts: {Kinds}", alerts.Count, string.Join(", ", alerts.Select(alert => alert.Kind.ToString())));
        Raised?.Invoke(this, alerts);
    }

    private List<AlertEvent> Load()
    {
        if (!File.Exists(_logPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize(AtomicFile.ReadAllText(_logPath), GilSweepJsonContext.Compact.ListAlertEvent) ?? [];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not read the alert log");
            return [];
        }
    }

    /// <summary>"17:52", "Yesterday" or "Oct 3" for the alert log.</summary>
    public static string When(DateTimeOffset at, DateTimeOffset now, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(at, zone);
        var today = TimeZoneInfo.ConvertTime(now, zone).Date;
        return local.Date == today ? local.ToString("HH:mm", CultureInfo.InvariantCulture)
            : local.Date == today.AddDays(-1) ? "Yesterday"
            : local.ToString("MMM d", CultureInfo.InvariantCulture);
    }
}
