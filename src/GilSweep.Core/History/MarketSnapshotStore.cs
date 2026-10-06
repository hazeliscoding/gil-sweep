using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using GilSweep.Core.Platform;
using GilSweep.Core.Serialization;
using GilSweep.Core.Sweep;
using Microsoft.Extensions.Logging;

namespace GilSweep.Core.History;

public sealed record SnapshotStats(int Count, long Bytes, DateTimeOffset? Oldest);

/// <summary>
/// The local snapshot archive: one JSON file per sweep in <c>snapshots/</c>, named
/// <c>sweep-&lt;ISO time&gt;-&lt;world&gt;.json</c> as v1 named them, so names sort chronologically.
/// </summary>
public interface IMarketSnapshotStore
{
    string Folder { get; }

    event EventHandler? Changed;

    /// <summary>The newest snapshot for a world, or null.</summary>
    MarketSnapshot? Latest(string world);

    /// <summary>Every snapshot for a world, oldest first.</summary>
    IReadOnlyList<MarketSnapshot> Load(string world);

    /// <summary>Stores a sweep. The previous newest snapshot drops its craft margins; only the newest needs them.</summary>
    void Save(MarketSnapshot snapshot);

    SnapshotStats Stats();

    /// <summary>
    /// Keeps the newest snapshot per world and day among files older than <paramref name="before"/>
    /// (all files when null, as v1's prune did). Returns how many were deleted.
    /// </summary>
    int PruneToOnePerDay(DateTimeOffset? before = null);

    /// <summary>Deletes snapshots taken before the cutoff.</summary>
    int DeleteOlderThan(DateTimeOffset cutoff);

    int Clear();
}

public sealed partial class MarketSnapshotStore(IAppEnvironment environment, ILogger<MarketSnapshotStore> logger) : IMarketSnapshotStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, MarketSnapshot?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public string Folder => Path.Combine(environment.DataDirectory, "snapshots");

    public event EventHandler? Changed;

    public static string FileName(MarketSnapshot snapshot) =>
        $"sweep-{(snapshot.Timestamp ?? snapshot.Date).Replace(':', '-').Replace('.', '-')}-{snapshot.World}.json";

    /// <remarks>An unreadable newest file is skipped for the one before it.</remarks>
    public MarketSnapshot? Latest(string world) =>
        Files()
            .Where(file => string.Equals(WorldOf(file), world, StringComparison.OrdinalIgnoreCase))
            .Reverse()
            .Select(Read)
            .FirstOrDefault(snapshot => snapshot is not null && string.Equals(snapshot.World, world, StringComparison.Ordinal));

    public IReadOnlyList<MarketSnapshot> Load(string world) =>
        [.. Files()
            .Where(file => string.Equals(WorldOf(file), world, StringComparison.OrdinalIgnoreCase))
            .Select(Read)
            .OfType<MarketSnapshot>()
            .Where(snapshot => string.Equals(snapshot.World, world, StringComparison.Ordinal))
            .OrderBy(snapshot => snapshot.TakenAt)];

    public void Save(MarketSnapshot snapshot)
    {
        lock (_gate)
        {
            var previous = Files().LastOrDefault(file => string.Equals(WorldOf(file), snapshot.World, StringComparison.OrdinalIgnoreCase));
            var path = Path.Combine(Folder, FileName(snapshot));
            AtomicFile.WriteAllText(path, JsonSerializer.Serialize(snapshot, GilSweepJsonContext.Compact.MarketSnapshot));
            _cache[path] = snapshot;
            if (previous is not null && previous != path)
            {
                StripCrafts(previous);
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public SnapshotStats Stats()
    {
        var files = Files();
        var bytes = files.Sum(file => new FileInfo(file).Length);
        return new SnapshotStats(files.Count, bytes, files.Count > 0 ? TimeOf(files[0]) : null);
    }

    public int PruneToOnePerDay(DateTimeOffset? before = null)
    {
        var files = Files();
        var keep = new Dictionary<string, string>();
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            var match = PruneKey().Match(name);
            keep[match.Success ? $"{match.Groups[2].Value}|{match.Groups[1].Value}" : name] = file;
        }

        var kept = keep.Values.ToHashSet();
        var deleted = Delete(files.Where(file => !kept.Contains(file) && (before is null || TimeOf(file) < before)));
        return deleted;
    }

    public int DeleteOlderThan(DateTimeOffset cutoff) => Delete(Files().Where(file => TimeOf(file) < cutoff));

    public int Clear() => Delete(Files());

    /// <summary>Snapshot files, oldest first (names start with the sweep time).</summary>
    private List<string> Files() =>
        Directory.Exists(Folder)
            ? [.. Directory.EnumerateFiles(Folder, "*.json").Order(StringComparer.Ordinal)]
            : [];

    private int Delete(IEnumerable<string> files)
    {
        var count = 0;
        lock (_gate)
        {
            foreach (var file in files.ToList())
            {
                try
                {
                    File.Delete(file);
                    _cache.Remove(file);
                    count++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(ex, "Could not delete snapshot {File}", Path.GetFileName(file));
                }
            }
        }

        if (count > 0)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return count;
    }

    /// <summary>Rewrites an older snapshot without craft margins. Reads its own copy: the cached one may be on screen.</summary>
    private void StripCrafts(string file)
    {
        try
        {
            var older = JsonSerializer.Deserialize(AtomicFile.ReadAllText(file), GilSweepJsonContext.Compact.MarketSnapshot);
            if (older?.Crafts is null)
            {
                return;
            }

            older.Crafts = null;
            AtomicFile.WriteAllText(file, JsonSerializer.Serialize(older, GilSweepJsonContext.Compact.MarketSnapshot));
            _cache[file] = older;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not trim snapshot {File}", Path.GetFileName(file));
        }
    }

    private MarketSnapshot? Read(string file)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(file, out var cached))
            {
                return cached;
            }

            MarketSnapshot? snapshot = null;
            try
            {
                snapshot = JsonSerializer.Deserialize(AtomicFile.ReadAllText(file), GilSweepJsonContext.Compact.MarketSnapshot);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                // One unreadable file must not hide the rest of the history.
                logger.LogWarning(ex, "Skipped unreadable snapshot {File}", Path.GetFileName(file));
            }

            _cache[file] = snapshot;
            return snapshot;
        }
    }

    private static string? WorldOf(string file) => PruneKey().Match(Path.GetFileName(file)) is { Success: true } match ? match.Groups[2].Value : null;

    private static DateTimeOffset TimeOf(string file)
    {
        var match = TimePart().Match(Path.GetFileName(file));
        if (!match.Success)
        {
            return DateTimeOffset.MinValue;
        }

        // "2026-10-05T18-30-00-000Z" back to "2026-10-05T18:30:00.000Z".
        var g = match.Groups;
        var iso = $"{g[1].Value}T{g[2].Value}:{g[3].Value}:{g[4].Value}.{g[5].Value}Z";
        return DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at : DateTimeOffset.MinValue;
    }

    /// <summary>v1's prune key: the day and the world (the text after the last hyphen).</summary>
    [GeneratedRegex(@"^sweep-(\d{4}-\d{2}-\d{2})T.*-(.+)\.json$")]
    private static partial Regex PruneKey();

    [GeneratedRegex(@"^sweep-(\d{4}-\d{2}-\d{2})T(\d{2})-(\d{2})-(\d{2})-(\d{3})Z-")]
    private static partial Regex TimePart();
}
