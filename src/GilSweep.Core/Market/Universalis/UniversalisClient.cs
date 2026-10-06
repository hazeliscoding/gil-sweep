using System.Globalization;
using Microsoft.Extensions.Logging;

namespace GilSweep.Core.Market.Universalis;

/// <summary>
/// Universalis, the community market board API. Its response shapes stay inside this adapter; the
/// rest of Gil Sweep sees <see cref="ItemPrices"/>, <see cref="ItemMarket"/> and <see cref="SaleHistory"/>.
/// </summary>
public interface IUniversalisClient
{
    /// <summary>Aggregated prices and velocities for up to <see cref="UniversalisClient.MaxAggregatedIds"/> items.</summary>
    Task<IReadOnlyDictionary<int, ItemPrices>> GetPricesAsync(string world, IReadOnlyList<int> itemIds, CancellationToken cancellationToken = default);

    /// <summary>Current listings and recent sales for up to <see cref="UniversalisClient.MaxCurrentIds"/> items.</summary>
    Task<IReadOnlyDictionary<int, ItemMarket>> GetMarketsAsync(string world, IReadOnlyList<int> itemIds, int listings, int sales, CancellationToken cancellationToken = default);

    /// <summary>Sale history for up to <see cref="UniversalisClient.MaxHistoryIds"/> items, newest first.</summary>
    Task<IReadOnlyDictionary<int, SaleHistory>> GetHistoryAsync(string world, IReadOnlyList<int> itemIds, int entries, TimeSpan? within, CancellationToken cancellationToken = default);

    /// <summary>Every public world name, sorted.</summary>
    Task<IReadOnlyList<string>> GetWorldsAsync(CancellationToken cancellationToken = default);
}

public sealed class UniversalisClient : IUniversalisClient
{
    public const string BaseUrl = "https://universalis.app/api/v2/";

    /// <summary>The aggregated endpoint's limit per request.</summary>
    public const int MaxAggregatedIds = 100;

    /// <summary>The current-data endpoint answers 504 for large batches, so ask for a few at a time.</summary>
    public const int MaxCurrentIds = 20;

    public const int MaxHistoryIds = 25;

    private readonly MarketHttp _http;

    public UniversalisClient(HttpClient http, MarketHttpOptions options, TimeProvider clock, ILogger<UniversalisClient> logger)
    {
        http.BaseAddress ??= new Uri(BaseUrl);
        _http = new MarketHttp(http, options, clock, logger, "Universalis");
    }

    public async Task<IReadOnlyDictionary<int, ItemPrices>> GetPricesAsync(string world, IReadOnlyList<int> itemIds, CancellationToken cancellationToken = default)
    {
        Check(itemIds, MaxAggregatedIds);
        var response = await _http.GetAsync($"aggregated/{Uri.EscapeDataString(world)}/{Join(itemIds)}", UniversalisJsonContext.Default.AggregatedResponse, cancellationToken).ConfigureAwait(false);
        var prices = new Dictionary<int, ItemPrices>();
        foreach (var result in response.Results ?? [])
        {
            prices[result.ItemId] = new ItemPrices(result.ItemId, ToPriceSet(result.Nq), ToPriceSet(result.Hq));
        }

        return prices;
    }

    public async Task<IReadOnlyDictionary<int, ItemMarket>> GetMarketsAsync(string world, IReadOnlyList<int> itemIds, int listings, int sales, CancellationToken cancellationToken = default)
    {
        Check(itemIds, MaxCurrentIds);
        var url = $"{Uri.EscapeDataString(world)}/{Join(itemIds)}?listings={listings}&entries={sales}";

        // A single id comes back unwrapped; several come back under "items".
        if (itemIds.Count == 1)
        {
            var one = await _http.GetAsync(url, UniversalisJsonContext.Default.CurrentData, cancellationToken).ConfigureAwait(false);
            return one.ItemId == 0 ? new Dictionary<int, ItemMarket>() : new Dictionary<int, ItemMarket> { [itemIds[0]] = ToMarket(itemIds[0], one) };
        }

        var many = await _http.GetAsync(url, UniversalisJsonContext.Default.MultiCurrentData, cancellationToken).ConfigureAwait(false);
        return (many.Items ?? []).ToDictionary(pair => int.Parse(pair.Key, CultureInfo.InvariantCulture), pair => ToMarket(int.Parse(pair.Key, CultureInfo.InvariantCulture), pair.Value));
    }

    public async Task<IReadOnlyDictionary<int, SaleHistory>> GetHistoryAsync(string world, IReadOnlyList<int> itemIds, int entries, TimeSpan? within, CancellationToken cancellationToken = default)
    {
        Check(itemIds, MaxHistoryIds);
        var url = $"history/{Uri.EscapeDataString(world)}/{Join(itemIds)}?entriesToReturn={entries}"
            + (within is { } span ? $"&entriesWithin={(long)span.TotalSeconds}" : "");
        if (itemIds.Count == 1)
        {
            var one = await _http.GetAsync(url, UniversalisJsonContext.Default.HistoryData, cancellationToken).ConfigureAwait(false);
            return one.ItemId == 0 ? new Dictionary<int, SaleHistory>() : new Dictionary<int, SaleHistory> { [itemIds[0]] = ToHistory(itemIds[0], one) };
        }

        var many = await _http.GetAsync(url, UniversalisJsonContext.Default.MultiHistoryData, cancellationToken).ConfigureAwait(false);
        return (many.Items ?? []).ToDictionary(pair => int.Parse(pair.Key, CultureInfo.InvariantCulture), pair => ToHistory(int.Parse(pair.Key, CultureInfo.InvariantCulture), pair.Value));
    }

    public async Task<IReadOnlyList<string>> GetWorldsAsync(CancellationToken cancellationToken = default)
    {
        var worlds = await _http.GetAsync("worlds", UniversalisJsonContext.Default.ListWorldData, cancellationToken).ConfigureAwait(false);
        return [.. worlds.Select(world => world.Name).Where(name => !string.IsNullOrWhiteSpace(name)).Order(StringComparer.Ordinal)];
    }

    private static void Check(IReadOnlyList<int> itemIds, int max)
    {
        if (itemIds.Count == 0 || itemIds.Count > max)
        {
            throw new ArgumentException($"Ask for between 1 and {max} items per request.", nameof(itemIds));
        }
    }

    private static string Join(IReadOnlyList<int> itemIds) => string.Join(',', itemIds.Select(id => id.ToString(CultureInfo.InvariantCulture)));

    private static PriceSet ToPriceSet(AggregatedQuality? quality) => quality is null
        ? PriceSet.Empty
        : new PriceSet(Price(quality.MinListing), Price(quality.AverageSalePrice), Quantity(quality.DailySaleVelocity));

    private static Scoped Price(AggregatedScopes? scopes) => scopes is null
        ? Scoped.Empty
        : new Scoped(scopes.World is { } w ? new ScopeValue(w.Price) : null, scopes.Dc is { } d ? new ScopeValue(d.Price) : null, scopes.Region is { } r ? new ScopeValue(r.Price) : null);

    private static Scoped Quantity(AggregatedScopes? scopes) => scopes is null
        ? Scoped.Empty
        : new Scoped(scopes.World is { } w ? new ScopeValue(w.Quantity) : null, scopes.Dc is { } d ? new ScopeValue(d.Quantity) : null, scopes.Region is { } r ? new ScopeValue(r.Quantity) : null);

    private static ItemMarket ToMarket(int itemId, CurrentData data)
    {
        var listings = (data.Listings ?? []).Select(listing => new MarketListing((long)Math.Round(listing.PricePerUnit), listing.Quantity, listing.Hq, listing.RetainerName)).ToList();
        var histogram = (data.StackSizeHistogram ?? [])
            .Select(pair => (Size: long.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size) ? size : 0, Count: pair.Value))
            .ToList();
        var count = histogram.Count > 0 ? histogram.Sum(entry => entry.Count) : listings.Count;
        var units = histogram.Count > 0 ? histogram.Sum(entry => entry.Size * entry.Count) : listings.Sum(listing => (long)listing.Quantity);
        return new ItemMarket(
            itemId,
            listings,
            [.. (data.RecentHistory ?? []).Select(ToSale)],
            Math.Max(count, listings.Count),
            Math.Max(units, listings.Sum(listing => (long)listing.Quantity)),
            data.LastUploadTime is > 0 and var ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null);
    }

    private static SaleHistory ToHistory(int itemId, HistoryData data) => new(itemId, [.. (data.Entries ?? []).Select(ToSale)]);

    private static MarketSale ToSale(SaleData sale) =>
        new((long)Math.Round(sale.PricePerUnit), sale.Quantity, sale.Hq, DateTimeOffset.FromUnixTimeSeconds(sale.Timestamp));
}
