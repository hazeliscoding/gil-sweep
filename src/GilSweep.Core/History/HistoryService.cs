using System.Globalization;
using System.Text.Json;
using GilSweep.Core.Catalog;
using GilSweep.Core.Market;
using GilSweep.Core.Market.Universalis;
using GilSweep.Core.Platform;
using GilSweep.Core.Serialization;
using GilSweep.Core.Sweep;
using Microsoft.Extensions.Logging;

namespace GilSweep.Core.History;

/// <summary>One point of an item's local price history. <see cref="T"/> is Unix milliseconds.</summary>
public sealed record HistoryPoint(long T, long Avg, double VelDay);

public sealed record DigestChange(int Id, string Name, long AvgThen, long AvgNow, double? AvgPct, double VelThen, double VelNow);

/// <summary>A farm-rotation item that sold under 5 a day on your world in each of the last three sweeps.</summary>
public sealed record PruneSuggestion(int Id, string Name, IReadOnlyList<double> RecentVel);

/// <summary>Week over week: the latest snapshot against one about a week older (v1's digest).</summary>
public sealed record Digest(
    string World,
    string LatestDate,
    string? BaselineDate,
    double? DaysApart,
    IReadOnlyList<DigestChange> Changes,
    IReadOnlyList<PruneSuggestion> Prune);

/// <summary>v1's Universalis backfill cache file.</summary>
public sealed class BackfillFile
{
    public string World { get; set; } = "";

    public string FetchedAt { get; set; } = "";

    public Dictionary<string, List<HistoryPoint>> Series { get; set; } = [];
}

/// <summary>The user's local market memory: what changed week over week and each item's price history.</summary>
public interface IHistoryService
{
    Digest Digest(string world);

    /// <summary>Per-item price and velocity, oldest first, at most 60 points: the backfill plus every snapshot.</summary>
    IReadOnlyDictionary<int, IReadOnlyList<HistoryPoint>> Series(string world);

    bool HasBackfill(string world);

    /// <summary>
    /// One round of Universalis sale history for every tracked item, as daily quantity-weighted
    /// averages, so charts have shape before the archive has grown. Returns the items covered.
    /// </summary>
    Task<int> BackfillAsync(string world, CancellationToken cancellationToken = default);
}

public sealed class HistoryService(
    IMarketSnapshotStore store,
    IItemCatalog catalog,
    IUniversalisClient universalis,
    IAppEnvironment environment,
    TimeProvider clock,
    ILogger<HistoryService> logger) : IHistoryService
{
    public const int SeriesLength = 60;

    public Digest Digest(string world) => BuildDigest(store.Load(world), world);

    public IReadOnlyDictionary<int, IReadOnlyList<HistoryPoint>> Series(string world) =>
        BuildSeries(store.Load(world), world, ReadBackfill(world));

    public bool HasBackfill(string world) => File.Exists(BackfillPath(world));

    public async Task<int> BackfillAsync(string world, CancellationToken cancellationToken = default)
    {
        var histories = new Dictionary<int, SaleHistory>();
        foreach (var chunk in catalog.Items.Select(item => item.Id).Chunk(UniversalisClient.MaxHistoryIds))
        {
            foreach (var (id, history) in await universalis.GetHistoryAsync(world, chunk, 300, null, cancellationToken).ConfigureAwait(false))
            {
                histories[id] = history;
            }
        }

        var series = BuildBackfill(histories);
        var file = new BackfillFile
        {
            World = world,
            FetchedAt = clock.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            Series = series.ToDictionary(pair => pair.Key.ToString(CultureInfo.InvariantCulture), pair => pair.Value),
        };
        AtomicFile.WriteAllText(BackfillPath(world), JsonSerializer.Serialize(file, GilSweepJsonContext.Compact.BackfillFile));
        logger.LogInformation("Backfilled history for {Count} items on {World}", series.Count, world);
        return series.Count;
    }

    /// <summary>
    /// v1's digest. The baseline is the newest snapshot at least 5 days older than the latest,
    /// or the oldest one while the archive is young. Changes rank by how much the item's daily
    /// gil throughput moved; trap kinds are left out.
    /// </summary>
    public static Digest BuildDigest(IReadOnlyList<MarketSnapshot> snapshots, string world)
    {
        var latest = snapshots.Count > 0 ? snapshots[^1] : null;
        if (latest is null || snapshots.Count < 2)
        {
            return new Digest(world, latest?.Date ?? "", null, null, [], []);
        }

        MarketSnapshot? baseline = null;
        foreach (var snapshot in snapshots.Take(snapshots.Count - 1))
        {
            if (latest.TakenAt - snapshot.TakenAt >= TimeSpan.FromDays(5))
            {
                baseline = snapshot;
            }
        }

        baseline ??= snapshots[0];
        var thenById = new Dictionary<int, SnapshotRow>();
        foreach (var row in baseline.Rows)
        {
            thenById[row.Id] = row;
        }

        var changes = latest.Rows
            .Where(row => !row.Kind.IsTrap() && thenById.ContainsKey(row.Id))
            .Select(row =>
            {
                var then = thenById[row.Id];
                var change = new DigestChange(
                    row.Id,
                    row.Name,
                    then.Avg,
                    row.Avg,
                    then.Avg != 0 ? JsMath.ToFixed((double)(row.Avg - then.Avg) / then.Avg * 100, 1) : null,
                    then.VelDay,
                    row.VelDay);
                return (Change: change, Delta: Math.Abs(row.Avg * row.VelDay - then.Avg * then.VelDay));
            })
            .OrderByDescending(entry => entry.Delta)
            .Take(10)
            .Select(entry => entry.Change)
            .ToList();

        var prune = new List<PruneSuggestion>();
        var recent = snapshots.Skip(Math.Max(0, snapshots.Count - 3)).ToList();
        if (recent.Count >= 3)
        {
            foreach (var item in latest.Rows.Where(row => row.Kind.IsFarmRotation()))
            {
                var rows = recent.Select(snapshot => snapshot.Rows.FirstOrDefault(row => row.Id == item.Id)).OfType<SnapshotRow>().ToList();
                if (rows.Count == 3 && rows.All(row => row.VelScope == MarketScope.World && row.VelDay < 5))
                {
                    prune.Add(new PruneSuggestion(item.Id, item.Name, [.. rows.Select(row => row.VelDay)]));
                }
            }
        }

        return new Digest(
            world,
            latest.Date,
            baseline.Date,
            JsMath.ToFixed((latest.TakenAt - baseline.TakenAt).TotalMilliseconds / 86400000, 1),
            changes,
            prune);
    }

    /// <summary>v1's history series: backfill points, then each snapshot's, sorted by time, last 60 kept.</summary>
    public static IReadOnlyDictionary<int, IReadOnlyList<HistoryPoint>> BuildSeries(
        IEnumerable<MarketSnapshot> snapshots, string world, IReadOnlyDictionary<int, List<HistoryPoint>>? backfill)
    {
        var series = new Dictionary<int, List<HistoryPoint>>();
        foreach (var (id, points) in backfill ?? new Dictionary<int, List<HistoryPoint>>())
        {
            series[id] = [.. points];
        }

        foreach (var snapshot in snapshots.Where(snapshot => snapshot.World == world))
        {
            if (snapshot.TakenAt == DateTimeOffset.MinValue)
            {
                continue;
            }

            var t = snapshot.TakenAt.ToUnixTimeMilliseconds();
            foreach (var row in snapshot.Rows)
            {
                if (!series.TryGetValue(row.Id, out var list))
                {
                    series[row.Id] = list = [];
                }

                list.Add(new HistoryPoint(t, row.Avg, row.VelDay));
            }
        }

        return series.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<HistoryPoint>)[.. pair.Value.OrderBy(point => point.T).TakeLast(SeriesLength)]);
    }

    /// <summary>Daily quantity-weighted average prices, bucketed at noon UTC (v1's backfill).</summary>
    public static Dictionary<int, List<HistoryPoint>> BuildBackfill(IReadOnlyDictionary<int, SaleHistory> histories)
    {
        var series = new Dictionary<int, List<HistoryPoint>>();
        foreach (var (id, history) in histories)
        {
            if (history.Entries.Count == 0)
            {
                continue;
            }

            var days = new Dictionary<long, (double Gil, long Qty)>();
            foreach (var sale in history.Entries)
            {
                var day = (long)Math.Floor(sale.SoldAt.ToUnixTimeSeconds() * 1000 / 86400000.0) * 86400000 + 43200000;
                var (gil, qty) = days.GetValueOrDefault(day);
                days[day] = (gil + (double)sale.PricePerUnit * sale.Quantity, qty + sale.Quantity);
            }

            series[id] = [.. days.Select(pair => new HistoryPoint(pair.Key, JsMath.Round(pair.Value.Gil / pair.Value.Qty), pair.Value.Qty)).OrderBy(point => point.T)];
        }

        return series;
    }

    private string BackfillPath(string world) => Path.Combine(environment.DataDirectory, $"history-backfill-{world}.json");

    private Dictionary<int, List<HistoryPoint>>? ReadBackfill(string world)
    {
        var path = BackfillPath(world);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var file = JsonSerializer.Deserialize(AtomicFile.ReadAllText(path), GilSweepJsonContext.Compact.BackfillFile);
            return file?.Series.ToDictionary(pair => int.Parse(pair.Key, CultureInfo.InvariantCulture), pair => pair.Value);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or FormatException)
        {
            // A corrupt cache only costs the charts their early points; snapshots still work.
            logger.LogWarning(ex, "Could not read the history backfill for {World}", world);
            return null;
        }
    }
}
