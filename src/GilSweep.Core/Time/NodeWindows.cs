using GilSweep.Core.Catalog;
using GilSweep.Core.Configuration;

namespace GilSweep.Core.Time;

/// <summary>A timed node's window for the tray and reminders.</summary>
/// <param name="Minutes">Whole real minutes until it closes (up) or opens (down).</param>
public sealed record UpcomingWindow(CatalogItem Item, bool Up, int Minutes, NodeAvailability Availability);

public static class NodeWindows
{
    /// <summary>The story and level gate alone; timed nodes are never trap kinds (v1's <c>canGather</c>).</summary>
    public static bool CanGather(CatalogItem item, GilSweepSettings settings) =>
        item.Expansion <= settings.MsqExpansion && item.Level <= settings.LevelFor(item.Job);

    /// <summary>
    /// Timed nodes this character can gather: open ones first (soonest to close), then the
    /// soonest to open (v1's tray list).
    /// </summary>
    public static List<UpcomingWindow> Next(IEnumerable<CatalogItem> items, GilSweepSettings settings, DateTimeOffset at, int limit = 6) =>
        [.. items
            .Where(item => item.IsTimed && CanGather(item, settings))
            .Select(item =>
            {
                var availability = EorzeaTime.Availability(item, at);
                return new UpcomingWindow(item, availability.State == NodeState.Open, availability.RealMinutes, availability);
            })
            .OrderBy(window => window.Up ? 0 : 1)
            .ThenBy(window => window.Minutes)
            .Take(limit)];
}
