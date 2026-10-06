using System.Text.Json.Serialization;

namespace GilSweep.Core.Catalog;

/// <summary>The gathering job an item needs. JSON names match the v1 data files.</summary>
public enum Job
{
    [JsonStringEnumMemberName("MIN")]
    Miner,

    [JsonStringEnumMemberName("BTN")]
    Botanist,

    /// <summary>Either job can get it (maps, crystals, aetherial reduction); the higher level counts.</summary>
    [JsonStringEnumMemberName("both")]
    Either,
}

/// <summary>Story progress, in release order. An item from a later expansion is locked.</summary>
public enum Expansion
{
    ARR,
    HW,
    StB,
    ShB,
    EW,
    DT,
}

public enum ItemKind
{
    [JsonStringEnumMemberName("node")]
    Node,

    [JsonStringEnumMemberName("unspoiled")]
    Unspoiled,

    [JsonStringEnumMemberName("legendary")]
    Legendary,

    [JsonStringEnumMemberName("ephemeral")]
    Ephemeral,

    [JsonStringEnumMemberName("map")]
    Map,

    [JsonStringEnumMemberName("crystal")]
    Crystal,

    [JsonStringEnumMemberName("diadem")]
    Diadem,

    [JsonStringEnumMemberName("reduction")]
    Reduction,

    /// <summary>Bought from a vendor and flipped. Tracked so it never shows up as a farm.</summary>
    [JsonStringEnumMemberName("vendor")]
    Vendor,

    /// <summary>Free Company submarine loot. Tracked so it never shows up as a farm.</summary>
    [JsonStringEnumMemberName("submarine")]
    Submarine,

    /// <summary>Retainer venture loot. Tracked so it never shows up as a farm.</summary>
    [JsonStringEnumMemberName("venture")]
    Venture,
}

public static class ItemKinds
{
    /// <summary>Top sellers that are not gathered at all. v1 never recommends these and neither does v2.</summary>
    public static bool IsTrap(this ItemKind kind) => kind is ItemKind.Vendor or ItemKind.Submarine or ItemKind.Venture;

    /// <summary>Kinds a farm rotation is built from (v1's digest prune list uses the same set).</summary>
    public static bool IsFarmRotation(this ItemKind kind) =>
        kind is ItemKind.Node or ItemKind.Unspoiled or ItemKind.Legendary or ItemKind.Ephemeral or ItemKind.Diadem or ItemKind.Reduction;

    public static string Describe(this ItemKind kind) => kind switch
    {
        ItemKind.Node => "Regular node",
        ItemKind.Unspoiled => "Unspoiled node",
        ItemKind.Legendary => "Legendary node",
        ItemKind.Ephemeral => "Ephemeral node",
        ItemKind.Map => "Treasure map",
        ItemKind.Crystal => "Crystals",
        ItemKind.Diadem => "Diadem",
        ItemKind.Reduction => "Aetherial reduction",
        ItemKind.Vendor => "Vendor item",
        ItemKind.Submarine => "Submarine loot",
        ItemKind.Venture => "Retainer venture",
        _ => kind.ToString(),
    };
}

public static class Jobs
{
    public static string Short(this Job job) => job switch
    {
        Job.Miner => "MIN",
        Job.Botanist => "BTN",
        _ => "both",
    };

    public static string Name(this Job job) => job switch
    {
        Job.Miner => "Miner",
        Job.Botanist => "Botanist",
        _ => "Miner or Botanist",
    };
}

public static class Expansions
{
    public static IReadOnlyList<Expansion> All { get; } = Enum.GetValues<Expansion>();

    public static string Name(this Expansion expansion) => expansion switch
    {
        Expansion.ARR => "A Realm Reborn",
        Expansion.HW => "Heavensward",
        Expansion.StB => "Stormblood",
        Expansion.ShB => "Shadowbringers",
        Expansion.EW => "Endwalker",
        _ => "Dawntrail",
    };
}

/// <summary>
/// One entry in the item database: the curated list shipped with the app plus items the user
/// verified and added. Field names match v1's items.json and snapshot rows.
/// </summary>
public class CatalogItem
{
    public string Name { get; set; } = "";

    public int Id { get; set; }

    public Job Job { get; set; }

    public int Level { get; set; }

    /// <summary>Node location, such as "Twinpools, Coerthas Western Highlands".</summary>
    public string Where { get; set; } = "";

    public ItemKind Kind { get; set; }

    public Expansion Expansion { get; set; }

    public string? Note { get; set; }

    /// <summary>Timed nodes: the Eorzea hours a window opens, such as [10, 22].</summary>
    public List<int>? Spawns { get; set; }

    /// <summary>Timed nodes: how long a window stays open, in Eorzea minutes (120 = 2 ET hours ≈ 5.8 real minutes).</summary>
    public int? Uptime { get; set; }

    /// <summary>Added by the user in Settings; removable.</summary>
    public bool? Custom { get; set; }

    [JsonIgnore]
    public bool IsTimed => Spawns is { Count: > 0 };

    [JsonIgnore]
    public GatheringNode Node => new(Where, Kind, IsTimed ? new SpawnSchedule(Spawns!, Uptime ?? SpawnSchedule.DefaultUptime) : null);
}

/// <summary>Where an item is gathered, and when if the node is timed.</summary>
public sealed record GatheringNode(string Location, ItemKind Kind, SpawnSchedule? Schedule)
{
    /// <summary>The zone, the part after the last comma ("Coerthas Western Highlands").</summary>
    public string Zone => Location.Contains(',', StringComparison.Ordinal) ? Location[(Location.LastIndexOf(',') + 1)..].Trim() : Location;
}

/// <summary>A timed node's windows: the Eorzea hours it opens and how long each window lasts.</summary>
public sealed record SpawnSchedule(IReadOnlyList<int> Hours, int UptimeEtMinutes)
{
    /// <summary>v1's default when a timed item has no uptime: 2 Eorzea hours.</summary>
    public const int DefaultUptime = 120;
}
