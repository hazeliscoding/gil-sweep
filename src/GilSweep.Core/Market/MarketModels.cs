using System.Text.Json.Serialization;

namespace GilSweep.Core.Market;

/// <summary>
/// Which market answered a price or velocity: the configured world, its data center or the
/// region. A value that only exists beyond the world means the item barely trades there.
/// </summary>
public enum MarketScope
{
    [JsonStringEnumMemberName("world")]
    World,

    [JsonStringEnumMemberName("dc")]
    DataCenter,

    [JsonStringEnumMemberName("region")]
    Region,

    [JsonStringEnumMemberName("-")]
    None,
}

/// <summary>One scope's figure. Amount is null when the scope answered without a number.</summary>
public sealed record ScopeValue(double? Amount);

/// <summary>A figure as world, data center and region each report it.</summary>
public sealed record Scoped(ScopeValue? World, ScopeValue? DataCenter, ScopeValue? Region)
{
    public static Scoped Empty { get; } = new(null, null, null);

    /// <summary>The first scope that answered, world first (v1's <c>pick</c>).</summary>
    public ScopeValue? Pick() => World ?? DataCenter ?? Region;

    /// <summary>The picked figure, or 0 when no scope answered.</summary>
    public double Value => Pick()?.Amount ?? 0;

    public MarketScope Scope => World is not null ? MarketScope.World
        : DataCenter is not null ? MarketScope.DataCenter
        : Region is not null ? MarketScope.Region
        : MarketScope.None;
}

public sealed record PriceSet(Scoped MinListing, Scoped AverageSalePrice, Scoped DailySaleVelocity)
{
    public static PriceSet Empty { get; } = new(Scoped.Empty, Scoped.Empty, Scoped.Empty);
}

/// <summary>Aggregated prices for one item: normal and high quality.</summary>
public sealed record ItemPrices(int ItemId, PriceSet Nq, PriceSet Hq);

public sealed record MarketListing(long PricePerUnit, int Quantity, bool Hq, string? RetainerName);

public sealed record MarketSale(long PricePerUnit, int Quantity, bool Hq, DateTimeOffset SoldAt);

/// <summary>Current listings and recent sales for one item on one world.</summary>
public sealed record ItemMarket(
    int ItemId,
    IReadOnlyList<MarketListing> Listings,
    IReadOnlyList<MarketSale> RecentSales,
    int ListingsCount,
    long UnitsForSale,
    DateTimeOffset? LastUploadedAt);

/// <summary>Sale history for one item, newest first.</summary>
public sealed record SaleHistory(int ItemId, IReadOnlyList<MarketSale> Entries);

/// <summary>A Saddlebag Exchange market-share row: a week's sales and a trend state.</summary>
public sealed record TrendSignal(int ItemId, string Name, double Average, double SoldWeek, string State, double? PercentChange);
