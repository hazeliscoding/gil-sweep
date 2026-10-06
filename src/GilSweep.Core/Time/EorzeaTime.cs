using System.Globalization;
using GilSweep.Core.Catalog;

namespace GilSweep.Core.Time;

/// <summary>
/// Eorzea time. One Eorzean day is 70 real minutes, so an Eorzea hour is 175 real seconds and a
/// two-hour node window lasts about 5 minutes 50 seconds. The arithmetic follows v1's operation
/// order exactly, so windows open and close on the same millisecond as before.
/// </summary>
public static class EorzeaTime
{
    public const int MinutesPerDay = 24 * 60;

    /// <summary>Real seconds per Eorzea minute (175 / 60 ≈ 2.92).</summary>
    public const double RealSecondsPerEtMinute = 175.0 / 60;

    private const double EtMsPerRealMs = 3600.0 / 175;

    /// <summary>Eorzea minute of the day, [0, 1440).</summary>
    public static double MinuteOfDay(long realMs) => realMs * EtMsPerRealMs / 60000 % MinutesPerDay;

    public static double MinuteOfDay(DateTimeOffset at) => MinuteOfDay(at.ToUnixTimeMilliseconds());

    /// <summary>"HH:MM" Eorzea time.</summary>
    public static string Clock(DateTimeOffset at)
    {
        var minute = (int)Math.Floor(MinuteOfDay(at));
        return string.Create(CultureInfo.InvariantCulture, $"{minute / 60:00}:{minute % 60:00}");
    }

    /// <summary>Eorzea minutes as whole real minutes, rounded up so "1 min" never means it already passed (v1).</summary>
    public static int RealMinutes(double etMinutes) => (int)Math.Ceiling(etMinutes * 175 / 3600);

    public static TimeSpan RealDuration(double etMinutes) => TimeSpan.FromSeconds(etMinutes * RealSecondsPerEtMinute);

    /// <summary>
    /// Where a node stands at a moment: open (and for how long) or closed (and until when).
    /// Handles windows that wrap midnight and several windows a day (v1's <c>nodeWindow</c>).
    /// </summary>
    public static NodeAvailability Availability(CatalogItem item, DateTimeOffset at) =>
        item.IsTimed ? Window(item.Spawns!, item.Uptime ?? SpawnSchedule.DefaultUptime, at.ToUnixTimeMilliseconds()) : NodeAvailability.Always;

    public static NodeAvailability Window(IReadOnlyList<int> spawns, int uptime, long realMs)
    {
        var now = MinuteOfDay(realMs);
        (int Hour, double Until)? next = null;
        foreach (var hour in spawns)
        {
            var sinceOpen = (now - hour * 60 + MinutesPerDay) % MinutesPerDay;
            if (sinceOpen < uptime)
            {
                return new NodeAvailability(NodeState.Open, uptime - sinceOpen, hour, uptime);
            }

            var untilOpen = MinutesPerDay - sinceOpen;
            if (next is null || untilOpen < next.Value.Until)
            {
                next = (hour, untilOpen);
            }
        }

        return new NodeAvailability(NodeState.Closed, next?.Until ?? 0, next?.Hour ?? (spawns.Count > 0 ? spawns[0] : 0), uptime);
    }
}

public enum NodeState
{
    /// <summary>A regular node: no window, always there.</summary>
    AlwaysAvailable,

    Open,

    Closed,
}

/// <summary>A node's state at one moment.</summary>
/// <param name="State">Open, closed, or a regular node with no window.</param>
/// <param name="EtMinutes">Eorzea minutes until the window closes (open) or opens (closed).</param>
/// <param name="SpawnHour">The current window's hour when open, the next one when closed.</param>
/// <param name="UptimeEtMinutes">How long each window lasts, in Eorzea minutes.</param>
public sealed record NodeAvailability(NodeState State, double EtMinutes, int SpawnHour, int UptimeEtMinutes)
{
    public static NodeAvailability Always { get; } = new(NodeState.AlwaysAvailable, 0, 0, 0);

    public bool IsGatherableNow => State is NodeState.Open or NodeState.AlwaysAvailable;

    /// <summary>Real time until the window closes (open) or opens (closed).</summary>
    public TimeSpan RealRemaining => EorzeaTime.RealDuration(EtMinutes);

    /// <summary>Whole real minutes, rounded up, as v1's tray and alerts showed them.</summary>
    public int RealMinutes => EorzeaTime.RealMinutes(EtMinutes);

    public bool OpensWithin(TimeSpan time) => State == NodeState.Closed && RealRemaining <= time;
}
