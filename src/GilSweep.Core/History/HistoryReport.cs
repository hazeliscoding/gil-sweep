using System.Globalization;
using GilSweep.Core.Configuration;
using GilSweep.Core.Sweep;

namespace GilSweep.Core.History;

/// <summary>One past sweep: when, what topped it, how many items and movers.</summary>
public sealed record SweepLogEntry(DateTimeOffset At, int? BestId, string? BestName, long BestPrice, int Items, int Movers);

/// <summary>The week in a few facts, all from local snapshots.</summary>
public sealed record WeekSummary(string Text, int SweepsThisWeek, int ItemsTracked, SnapshotStats Archive);

/// <summary>What the History screen reads: a log of sweeps, a weekly summary and a CSV export.</summary>
public static class HistoryReport
{
    public static readonly TimeSpan Week = TimeSpan.FromDays(7);

    /// <summary>
    /// The newest sweeps first, each with the opportunity it ranked first at the moment it ran,
    /// for the character as configured now.
    /// </summary>
    public static List<SweepLogEntry> Log(IReadOnlyList<MarketSnapshot> snapshots, GilSweepSettings settings, int limit = 12) =>
        [.. snapshots.Reverse().Take(limit).Select(snapshot =>
        {
            var best = OpportunityBoard.Build(snapshot, null, settings, snapshot.TakenAt).Ranked.FirstOrDefault();
            return new SweepLogEntry(snapshot.TakenAt, best?.ItemId, best?.Name, best?.Row.Avg ?? 0, snapshot.Rows.Count, Ranking.Movers(snapshot.Rows).Count);
        })];

    public static WeekSummary Summarize(IReadOnlyList<MarketSnapshot> snapshots, Digest digest, GilSweepSettings settings, SnapshotStats archive, DateTimeOffset now)
    {
        var week = snapshots.Where(snapshot => now - snapshot.TakenAt <= Week).ToList();
        var items = snapshots.Count > 0 ? snapshots[^1].Rows.Count : 0;
        if (week.Count == 0)
        {
            return new WeekSummary("No sweeps this week yet. Each sweep adds to this record.", 0, items, archive);
        }

        var sentences = new List<string>();
        var tops = Log(week, settings, week.Count).Where(entry => entry.BestName is not null).GroupBy(entry => entry.BestName!).OrderByDescending(group => group.Count()).ToList();
        if (tops.Count > 0)
        {
            sentences.Add($"{tops[0].Key} was the top farm in {tops[0].Count()} of {week.Count} sweeps.");
        }

        var moves = digest.Changes.Where(change => change.AvgPct is not null).ToList();
        var since = digest.BaselineDate is { } baseline && DateTimeOffset.TryParse(baseline, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var day)
            ? " since " + day.ToString("MMM d", CultureInfo.InvariantCulture)
            : "";
        if (moves.OrderBy(change => change.AvgPct).FirstOrDefault() is { AvgPct: < -3 } fall)
        {
            sentences.Add($"{fall.Name} fell {Math.Abs(fall.AvgPct!.Value):0.#}%{since}.");
        }

        if (moves.OrderByDescending(change => change.AvgPct).FirstOrDefault() is { AvgPct: > 3 } rise)
        {
            sentences.Add($"{rise.Name} rose {rise.AvgPct!.Value:0.#}%{since}.");
        }

        if (digest.Prune.Count > 0)
        {
            sentences.Add($"{digest.Prune.Count} farm{(digest.Prune.Count == 1 ? " has" : "s have")} sold under 5 a day in each of the last three sweeps.");
        }

        return new WeekSummary(string.Join(" ", sentences), week.Count, items, archive);
    }

    /// <summary>Every snapshot row as CSV, oldest first: one line per item per sweep.</summary>
    public static void WriteCsv(IEnumerable<MarketSnapshot> snapshots, TextWriter writer)
    {
        writer.WriteLine("swept_at_utc,world,item_id,item,kind,average_price,cheapest_listing,sold_per_day,sales_scope,gil_per_day,change_pct,listings,units_listed");
        foreach (var snapshot in snapshots)
        {
            var at = snapshot.TakenAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            foreach (var row in snapshot.Rows)
            {
                writer.WriteLine(string.Join(',',
                    at,
                    Csv(snapshot.World),
                    row.Id.ToString(CultureInfo.InvariantCulture),
                    Csv(row.Name),
                    row.Kind.ToString().ToLowerInvariant(),
                    row.Avg.ToString(CultureInfo.InvariantCulture),
                    row.Min.ToString(CultureInfo.InvariantCulture),
                    row.VelDay.ToString(CultureInfo.InvariantCulture),
                    row.VelScope switch { Market.MarketScope.World => "world", Market.MarketScope.DataCenter => "dc", Market.MarketScope.Region => "region", _ => "" },
                    row.Throughput.ToString(CultureInfo.InvariantCulture),
                    row.AvgChangePct?.ToString(CultureInfo.InvariantCulture) ?? "",
                    row.Listings?.ListingsCount.ToString(CultureInfo.InvariantCulture) ?? "",
                    row.Listings?.UnitsForSale.ToString(CultureInfo.InvariantCulture) ?? ""));
            }
        }
    }

    private static string Csv(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : value;
}
