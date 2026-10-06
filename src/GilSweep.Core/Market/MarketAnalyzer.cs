using GilSweep.Core.Catalog;

namespace GilSweep.Core.Market;

/// <summary>What the live listings and recent sales say about one item.</summary>
/// <param name="CurMin">Cheapest listing; 0 when nothing is listed.</param>
/// <param name="MedPpu">Median price recent sales cleared at.</param>
/// <param name="ListedQty">Units across the listings fetched.</param>
/// <param name="UnitsPerDay">Units sold per day over the span of the recent sales.</param>
/// <param name="DaysInv">Listed units ÷ units per day: how long the market board's stock lasts.</param>
/// <param name="Listings">The 10 cheapest listings.</param>
/// <param name="Sales">Recent sales, newest first.</param>
public sealed record MarketDetail(
    long CurMin,
    long MedPpu,
    long ListedQty,
    double UnitsPerDay,
    double? DaysInv,
    IReadOnlyList<MarketListing> Listings,
    IReadOnlyList<MarketSale> Sales);

public enum SellingVerdict
{
    Healthy,
    EmptyMarket,
    FloorCrashed,
    Saturated,
    Shortage,
}

/// <summary>How to list an item on a retainer (v1's retainer plan).</summary>
/// <param name="ListPrice">Undercut the cheapest by 1, or hold near the clearing price when the floor crashed.</param>
/// <param name="Stack">The median quantity buyers actually take, rounded to a natural size; maps sell singly.</param>
public sealed record SellingAdvice(
    int ItemId,
    long CurMin,
    long MedPpu,
    long ListPrice,
    int Stack,
    double? DaysInv,
    long UnitsPerDay,
    SellingVerdict Verdict)
{
    /// <summary>v1's verdict text, kept for tests.</summary>
    public string V1Verdict => Verdict switch
    {
        SellingVerdict.EmptyMarket => "EMPTY MARKET — list high",
        SellingVerdict.FloorCrashed => "floor crashed — hold price",
        SellingVerdict.Saturated => "saturated — low priority",
        SellingVerdict.Shortage => "shortage — price up",
        _ => "healthy",
    };

    public string Text => Verdict switch
    {
        SellingVerdict.EmptyMarket => "Nobody is selling. List high.",
        SellingVerdict.FloorCrashed => "Someone dumped stock far below what buyers pay. Hold your price.",
        SellingVerdict.Saturated => "More than a week of stock is listed. Low priority.",
        SellingVerdict.Shortage => "Less than a day of stock is listed. Price up.",
        _ => "Stock and sales are balanced.",
    };
}

/// <summary>Sales per hour of the day, in the user's time zone.</summary>
public sealed record HourProfile(IReadOnlyList<long> Units, IReadOnlyList<int> PeakHours)
{
    public bool HasData => PeakHours.Count > 0;
}

/// <summary>v1's market drill-down and retainer heuristics, ported unchanged.</summary>
public static class MarketAnalyzer
{
    public static MarketDetail Detail(ItemMarket market)
    {
        var listings = market.Listings.OrderBy(listing => listing.PricePerUnit).ToList();
        var sales = market.RecentSales;
        var listedQty = listings.Sum(listing => (long)listing.Quantity);
        var unitsPerDay = JsMath.ToFixed(SoldQuantity(sales) / SpanDays(sales), 1);
        return new MarketDetail(
            listings.Count > 0 ? listings[0].PricePerUnit : 0,
            Median(sales.Select(sale => sale.PricePerUnit)),
            listedQty,
            unitsPerDay,
            unitsPerDay > 0 ? JsMath.ToFixed(listedQty / unitsPerDay, 1) : null,
            [.. listings.Take(10)],
            sales);
    }

    public static SellingAdvice Advise(ItemMarket market, ItemKind kind)
    {
        var listings = market.Listings.OrderBy(listing => listing.PricePerUnit).ToList();
        var sales = market.RecentSales;
        var curMin = listings.Count > 0 ? listings[0].PricePerUnit : 0;
        var listedQty = listings.Sum(listing => (long)listing.Quantity);
        var unitsPerDay = SoldQuantity(sales) / SpanDays(sales);
        var medStack = Median(sales.Select(sale => (long)sale.Quantity));
        var medPpu = Median(sales.Select(sale => sale.PricePerUnit));
        var daysInv = unitsPerDay > 0 ? listedQty / unitsPerDay : double.PositiveInfinity;
        var crashedFloor = curMin > 0 && medPpu > 0 && curMin < medPpu * 0.6;
        var listPrice = curMin == 0 ? medPpu : crashedFloor ? JsMath.Round(medPpu * 0.95) : Math.Max(curMin - 1, 1);
        var verdict = curMin == 0 ? SellingVerdict.EmptyMarket
            : crashedFloor ? SellingVerdict.FloorCrashed
            : daysInv > 7 ? SellingVerdict.Saturated
            : daysInv < 1 ? SellingVerdict.Shortage
            : SellingVerdict.Healthy;
        return new SellingAdvice(
            market.ItemId,
            curMin,
            medPpu,
            listPrice,
            kind == ItemKind.Map ? 1 : NiceStack(medStack),
            double.IsFinite(daysInv) ? JsMath.ToFixed(daysInv, 1) : null,
            JsMath.Round(unitsPerDay),
            verdict);
    }

    /// <summary>
    /// Units sold by local hour of day. The peak hours are the top three by units (ties
    /// included), as v1's drill-down marked them.
    /// </summary>
    public static HourProfile Hours(IEnumerable<MarketSale> sales, TimeZoneInfo zone)
    {
        var buckets = new long[24];
        foreach (var sale in sales)
        {
            buckets[TimeZoneInfo.ConvertTime(sale.SoldAt, zone).Hour] += sale.Quantity;
        }

        var sorted = buckets.OrderByDescending(units => units).ToArray();
        var hotCut = sorted[2] != 0 ? sorted[2] : long.MaxValue;
        var peaks = Enumerable.Range(0, 24).Where(hour => buckets[hour] >= hotCut && buckets[hour] > 0).ToList();
        return new HourProfile(buckets, peaks);
    }

    /// <summary>Rounds a quantity to the stack sizes people actually list at.</summary>
    public static int NiceStack(long quantity) =>
        quantity >= 5000 ? 9999
        : quantity >= 500 ? 999
        : quantity >= 50 ? 99
        : quantity >= 10 ? (int)JsMath.Round(quantity / 10.0) * 10
        : (int)Math.Max(1, JsMath.Round(quantity));

    /// <summary>v1's median: the upper middle element.</summary>
    public static long Median(IEnumerable<long> values)
    {
        var sorted = values.Order().ToList();
        return sorted.Count > 0 ? sorted[sorted.Count / 2] : 0;
    }

    private static double SoldQuantity(IReadOnlyList<MarketSale> sales) => sales.Sum(sale => (double)sale.Quantity);

    /// <summary>Days between the newest and oldest sale (at least 0.05), or 1 with fewer than two sales.</summary>
    private static double SpanDays(IReadOnlyList<MarketSale> sales) =>
        sales.Count > 1 ? Math.Max((sales[0].SoldAt.ToUnixTimeSeconds() - sales[^1].SoldAt.ToUnixTimeSeconds()) / 86400.0, 0.05) : 1;
}
