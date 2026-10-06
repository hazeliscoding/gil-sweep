using GilSweep.Core.Catalog;
using GilSweep.Core.Configuration;
using GilSweep.Core.Market;

namespace GilSweep.Core.Sweep;

public enum LockKind
{
    Submarine,
    Vendor,
    Venture,
    Expansion,
    Level,
}

/// <summary>Why an item can't be farmed by this character. <see cref="V1Text"/> is v1's wording, kept for tests.</summary>
public sealed record ItemLock(LockKind Kind, string V1Text, string Text);

/// <summary>
/// v1's ranking rules, ported unchanged and covered by characterization tests:
/// trap kinds (vendor, submarine, venture) are never farms; an item from a later expansion
/// than the character's story is locked; the job's level gates it (the higher of the two for
/// "either"); and a farm needs world-scope velocity, because a price that only trades on the
/// data center is a mirage on your world. Rows arrive sorted by throughput.
/// </summary>
public static class Ranking
{
    public const int FarmTableLimit = 12;

    public const double MoverPercent = 25;

    public static ItemLock? Lock(CatalogItem item, GilSweepSettings settings)
    {
        switch (item.Kind)
        {
            case ItemKind.Submarine:
                return new ItemLock(LockKind.Submarine, "FC submarine loot", "Submarine loot, not gathered");
            case ItemKind.Vendor:
                return new ItemLock(LockKind.Vendor, "vendor/scrip flip", "Bought from a vendor, not gathered");
            case ItemKind.Venture:
                return new ItemLock(LockKind.Venture, "retainer venture", "Retainer venture loot, not gathered");
        }

        if (item.Expansion > settings.MsqExpansion)
        {
            return new ItemLock(LockKind.Expansion, $"{item.Expansion} content", $"Needs {item.Expansion.Name()} story progress");
        }

        if (item.Level > settings.LevelFor(item.Job))
        {
            var v1Job = item.Job == Job.Either ? "level" : item.Job.Short();
            var job = item.Job == Job.Either ? "Miner or Botanist" : item.Job.Name();
            return new ItemLock(LockKind.Level, $"needs {v1Job} {item.Level}", $"Needs {job} {item.Level}");
        }

        return null;
    }

    /// <summary>v1's <c>lockReason</c>: "" when the character can farm it.</summary>
    public static string LockReason(CatalogItem item, GilSweepSettings settings) => Lock(item, settings)?.V1Text ?? "";

    public static bool Accessible(CatalogItem item, GilSweepSettings settings) => Lock(item, settings) is null;

    public static List<SnapshotRow> Farmable(IEnumerable<SnapshotRow> rows, GilSweepSettings settings) =>
        [.. rows.Where(row => Accessible(row, settings) && row.VelDay > 0 && row.VelScope == MarketScope.World && !row.Kind.IsTrap())];

    public static List<SnapshotRow> MiningFarms(IEnumerable<SnapshotRow> rows, GilSweepSettings settings, int limit = FarmTableLimit) =>
        [.. Farmable(rows, settings).Where(row => row.Job == Job.Miner && row.Kind is not (ItemKind.Map or ItemKind.Crystal)).Take(limit)];

    public static List<SnapshotRow> BotanyFarms(IEnumerable<SnapshotRow> rows, GilSweepSettings settings, int limit = FarmTableLimit) =>
        [.. Farmable(rows, settings).Where(row => row.Job == Job.Botanist && row.Kind is not (ItemKind.Map or ItemKind.Crystal)).Take(limit)];

    /// <summary>v1's dashboard headline: the higher-throughput of the mining and botany tables' leaders.</summary>
    public static SnapshotRow? TopFarm(IEnumerable<SnapshotRow> rows, GilSweepSettings settings)
    {
        var list = rows as IReadOnlyCollection<SnapshotRow> ?? [.. rows];
        return MiningFarms(list, settings).Concat(BotanyFarms(list, settings)).OrderByDescending(row => row.Throughput).FirstOrDefault();
    }

    public static List<SnapshotRow> Maps(IEnumerable<SnapshotRow> rows, GilSweepSettings settings) =>
        [.. Farmable(rows, settings).Where(row => row.Kind == ItemKind.Map)];

    /// <summary>One map per 18 hours per character: the most valuable one that sells at least twice a day.</summary>
    public static SnapshotRow? BestMap(IEnumerable<SnapshotRow> rows, GilSweepSettings settings) =>
        Maps(rows, settings).Where(row => row.VelDay >= 2).OrderByDescending(row => row.Avg).FirstOrDefault();

    public static List<SnapshotRow> Crystals(IEnumerable<SnapshotRow> rows, GilSweepSettings settings) =>
        [.. Farmable(rows, settings).Where(row => row.Kind == ItemKind.Crystal)];

    /// <summary>A swing of 25% or more since the previous sweep, on an item that sells at least 5 a day.</summary>
    public static List<SnapshotRow> Movers(IEnumerable<SnapshotRow> rows) =>
        [.. rows.Where(row => row.AvgChangePct is { } change && Math.Abs(change) >= MoverPercent && row.VelDay >= 5)];

    public static List<SnapshotRow> Locked(IEnumerable<SnapshotRow> rows, GilSweepSettings settings, int limit = FarmTableLimit) =>
        [.. rows.Where(row => !Accessible(row, settings) && row.Throughput > 0).Take(limit)];

    /// <summary>Legendary nodes need their expansion's folklore book. v1 annotated these rather than hiding them.</summary>
    public static bool NeedsFolklore(CatalogItem item, GilSweepSettings settings) =>
        item.Kind == ItemKind.Legendary && !settings.Folklore.Contains(item.Expansion);

    /// <summary>Can the configured crafters make this? "any" recipes go by the best crafter.</summary>
    public static bool CraftableBy(CraftValue craft, GilSweepSettings settings) =>
        craft.Lvl is not { } level || level <= CrafterCap(craft.Job, settings);

    public static int CrafterCap(string job, GilSweepSettings settings)
    {
        if (job != "any")
        {
            return settings.CrafterLevel(job);
        }

        var best = settings.Crafters.Values.DefaultIfEmpty(0).Max();
        return best > 0 ? best : GilSweepSettings.MaxLevel;
    }

    /// <summary>Crafts with a complete, positive margin on world sales that use something this character can farm.</summary>
    public static List<CraftValue> TopValueCrafts(IEnumerable<CraftValue> crafts, IEnumerable<SnapshotRow> rows, GilSweepSettings settings, int limit = 5) =>
        [.. CraftingCandidates(crafts, rows, settings, onlyMine: true).Take(limit)];

    /// <summary>v1's Crafting page list (first 40).</summary>
    public static List<CraftValue> CraftingPage(IEnumerable<CraftValue> crafts, IEnumerable<SnapshotRow> rows, GilSweepSettings settings, bool onlyMine) =>
        [.. CraftingCandidates(crafts, rows, settings, onlyMine).Take(40)];

    /// <summary>Ids of the non-crystal, non-map items this character can farm right now.</summary>
    public static HashSet<int> FarmableMaterialIds(IEnumerable<SnapshotRow> rows, GilSweepSettings settings) =>
        [.. Farmable(rows, settings).Where(row => row.Kind is not (ItemKind.Crystal or ItemKind.Map)).Select(row => row.Id)];

    /// <summary>
    /// What you'd realistically have on retainers: the top 10 farmable materials, up to 3 maps
    /// that sell at least twice a day, and crystals moving over 100,000 gil a day.
    /// </summary>
    public static List<SnapshotRow> RetainerTargets(IEnumerable<SnapshotRow> rows, GilSweepSettings settings)
    {
        var farmable = Farmable(rows, settings);
        var materials = farmable.Where(row => row.Kind is not (ItemKind.Map or ItemKind.Crystal)).Take(10);
        var maps = farmable.Where(row => row.Kind == ItemKind.Map && row.VelDay >= 2).OrderByDescending(row => row.Avg).Take(3);
        var crystals = farmable.Where(row => row.Kind == ItemKind.Crystal && row.Throughput > 100_000);
        return [.. materials, .. maps, .. crystals];
    }

    private static IEnumerable<CraftValue> CraftingCandidates(IEnumerable<CraftValue> crafts, IEnumerable<SnapshotRow> rows, GilSweepSettings settings, bool onlyMine)
    {
        var mine = FarmableMaterialIds(rows, settings);
        return crafts
            .Where(craft => craft.CostComplete && craft.VelScope == MarketScope.World && craft.Margin > 0)
            .Where(craft => CraftableBy(craft, settings))
            .Where(craft => !onlyMine || craft.UsesTracked.Any(mine.Contains));
    }
}
