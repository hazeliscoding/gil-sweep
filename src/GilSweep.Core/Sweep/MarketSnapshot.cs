using System.Globalization;
using System.Text.Json.Serialization;
using GilSweep.Core.Catalog;
using GilSweep.Core.Market;

namespace GilSweep.Core.Sweep;

/// <summary>
/// One sweep: every tracked item priced on one world, plus craft margins. Stored as
/// <c>snapshots/sweep-&lt;time&gt;-&lt;world&gt;.json</c> with v1's field names, so v1 history loads as-is.
/// </summary>
public sealed class MarketSnapshot
{
    public string Date { get; set; } = "";

    /// <summary>When the sweep ran (ISO 8601, UTC). Null only on v1's bundled seed.</summary>
    public string? Timestamp { get; set; }

    public string World { get; set; } = "";

    public List<SnapshotRow> Rows { get; set; } = [];

    /// <summary>Saddlebag top sellers that aren't in the item database.</summary>
    public List<SaddlebagCandidate> SbUnknown { get; set; } = [];

    /// <summary>Craft margins. Kept on the newest snapshot only; older files drop them to save space.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<CraftValue>? Crafts { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Seed { get; set; }

    /// <summary>Why the sweep is incomplete, when a provider failed part-way. Empty for a full sweep.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Warnings { get; set; }

    /// <summary>The sweep time; v1's seed had none, so its date stands in (as v1 did).</summary>
    [JsonIgnore]
    public DateTimeOffset TakenAt =>
        Timestamp is { } timestamp && DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
            ? at
            : DateTimeOffset.TryParse(Date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var day) ? day : DateTimeOffset.MinValue;

    public SnapshotRow? Find(int itemId) => Rows.FirstOrDefault(row => row.Id == itemId);
}

/// <summary>One priced item. Item fields first (as in items.json), then the market figures.</summary>
public sealed class SnapshotRow : CatalogItem
{
    /// <summary>Average sale price, rounded to whole gil.</summary>
    public long Avg { get; set; }

    /// <summary>Cheapest current listing, rounded to whole gil.</summary>
    public long Min { get; set; }

    /// <summary>Units sold per day, one decimal.</summary>
    public double VelDay { get; set; }

    /// <summary>Which market the velocity came from. Only world velocity counts for farming.</summary>
    public MarketScope VelScope { get; set; }

    /// <summary>Gil changing hands per day: average price × units per day.</summary>
    public long Throughput { get; set; }

    public string? SbState { get; set; }

    public double? SbSoldWeek { get; set; }

    /// <summary>Change in average price since the previous sweep on this world, one decimal.</summary>
    public double? AvgChangePct { get; set; }

    /// <summary>Why it sells: top recipes that use it, leves, Grand Company supply, quests.</summary>
    public string Why { get; set; } = "";

    public DemandInfo? Demand { get; set; }

    /// <summary>Listing depth from the current listings (v2). Null on v1 snapshots or when that request failed.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ListingDepth? Listings { get; set; }
}

/// <summary>How crowded the market board is for one item.</summary>
public sealed class ListingDepth
{
    public int ListingsCount { get; set; }

    public long UnitsForSale { get; set; }

    /// <summary>Cheapest listing at the time of the sweep.</summary>
    public long Cheapest { get; set; }

    /// <summary>The cheapest listing by one of your retainers, when you have one among the listings fetched.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? OwnCheapest { get; set; }

    /// <summary>Whether the cheapest listing is yours.</summary>
    public bool CheapestIsOwn { get; set; }

    /// <summary>When a player last uploaded this world's listings to Universalis.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class SaddlebagCandidate
{
    public string Name { get; set; } = "";

    public int Id { get; set; }

    public double Avg { get; set; }

    public double SoldWeek { get; set; }

    public string State { get; set; } = "";
}

public sealed class DemandConsumer
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    /// <summary>Units of the tracked item one craft uses.</summary>
    public int Qty { get; set; }

    /// <summary>The consumer's own units sold per day on the world (0 = unknown).</summary>
    public double VelDay { get; set; }
}

public sealed class SupplyMission
{
    public int Count { get; set; }

    public int Seals { get; set; }
}

/// <summary>Who buys an item: recipes, leves, Grand Company supply and quests (Garland Tools data).</summary>
public sealed class DemandInfo
{
    public int RecipeCount { get; set; }

    public int Leves { get; set; }

    public SupplyMission? Supply { get; set; }

    public int Quests { get; set; }

    public List<DemandConsumer> TopConsumers { get; set; } = [];
}

public sealed class CraftIngredient
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public int Qty { get; set; }

    /// <summary>What one unit costs: the cheaper of buying it and crafting it from its own materials. 0 = no price.</summary>
    public long UnitPrice { get; set; }

    /// <summary>Crafting this ingredient beat buying it.</summary>
    public bool ViaCraft { get; set; }
}

/// <summary>Whether a recipe is worth crafting at sweep-time prices.</summary>
public sealed class CraftValue
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    /// <summary>Crafter job abbreviation, or "any".</summary>
    public string Job { get; set; } = "";

    public int? Lvl { get; set; }

    public int Yield { get; set; } = 1;

    public long SalePrice { get; set; }

    /// <summary>The high-quality market out-trades normal quality, so price and velocity are HQ figures.</summary>
    public bool Hq { get; set; }

    public double VelDay { get; set; }

    public MarketScope VelScope { get; set; }

    /// <summary>Σ quantity × unit price. Only trustworthy when <see cref="CostComplete"/>.</summary>
    public long Cost { get; set; }

    /// <summary>False when an ingredient had no price (vendor-only and the like).</summary>
    public bool CostComplete { get; set; }

    public long Margin { get; set; }

    public double? MarginPct { get; set; }

    /// <summary>Ingredient ids that are tracked, non-crystal items: things you can farm.</summary>
    public List<int> UsesTracked { get; set; } = [];

    public List<CraftIngredient> Ingredients { get; set; } = [];
}
