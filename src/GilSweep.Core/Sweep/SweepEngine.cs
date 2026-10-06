using GilSweep.Core.Catalog;
using GilSweep.Core.Configuration;
using GilSweep.Core.Crafting;
using GilSweep.Core.Market;
using GilSweep.Core.Market.Saddlebag;
using GilSweep.Core.Market.Universalis;
using Microsoft.Extensions.Logging;

namespace GilSweep.Core.Sweep;

public enum SweepStage
{
    Prices,
    Trends,
    Listings,
    Crafts,
}

public sealed record SweepProgress(SweepStage Stage, int Done, int Total);

/// <summary>Prices every tracked item on the configured world and builds a snapshot. It never writes files.</summary>
public interface ISweepEngine
{
    /// <summary>
    /// Runs a sweep. <paramref name="previous"/> is the last snapshot the user saw on this world;
    /// price changes are measured against it.
    /// </summary>
    Task<MarketSnapshot> RunAsync(GilSweepSettings settings, MarketSnapshot? previous, IProgress<SweepProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>About how many market requests one sweep makes, for the settings screen.</summary>
    int EstimateRequests(GilSweepSettings settings);
}

/// <summary>
/// v1's sweep, ported: one aggregated pricing pass covers the tracked items, the recipes that
/// consume them (demand) and every recipe ingredient (craft margins); Saddlebag adds trend
/// states; rows are sorted by throughput. v2 adds one more pass: current listings for the
/// farmable and watched items, which gives listing depth (competition) and spots undercuts.
/// </summary>
public sealed class SweepEngine(
    IItemCatalog catalog,
    IUniversalisClient universalis,
    ISaddlebagClient saddlebag,
    TimeProvider clock,
    ILogger<SweepEngine> logger) : ISweepEngine
{
    public const int ListingsPerItem = 20;

    public async Task<MarketSnapshot> RunAsync(GilSweepSettings settings, MarketSnapshot? previous, IProgress<SweepProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var items = catalog.Items;
        var ids = PricingIds();
        var chunks = ids.Chunk(UniversalisClient.MaxAggregatedIds).ToList();
        var trackedIds = items.Select(item => item.Id).ToHashSet();
        var listingIds = ListingIds(settings);
        var listingChunks = listingIds.Chunk(UniversalisClient.MaxCurrentIds).ToList();
        var total = chunks.Count + 1 + listingChunks.Count;
        var done = 0;
        var warnings = new List<string>();

        var prices = new Dictionary<int, ItemPrices>();
        var missedPrices = 0;
        foreach (var chunk in chunks)
        {
            progress?.Report(new SweepProgress(SweepStage.Prices, done, total));
            try
            {
                foreach (var (id, price) in await universalis.GetPricesAsync(settings.World, chunk, cancellationToken).ConfigureAwait(false))
                {
                    prices[id] = price;
                }
            }
            catch (GilSweepException) when (!chunk.Any(trackedIds.Contains))
            {
                // Demand consumers and recipe ingredients only refine the picture; the farm list
                // still stands without them.
                missedPrices += chunk.Length;
            }

            done++;
        }

        if (missedPrices > 0)
        {
            warnings.Add($"Universalis didn't answer for {missedPrices} recipe items, so some craft margins and demand figures are missing.");
        }

        progress?.Report(new SweepProgress(SweepStage.Trends, done, total));
        var trends = settings.UseSaddlebag
            ? await saddlebag.GetMarketShareAsync(settings.World, settings.Saddlebag, cancellationToken).ConfigureAwait(false)
            : [];
        if (trends is null)
        {
            warnings.Add("Saddlebag Exchange didn't answer, so trend states are missing from this sweep.");
        }

        done++;

        var markets = new Dictionary<int, ItemMarket>();
        var missedListings = 0;
        foreach (var chunk in listingChunks)
        {
            progress?.Report(new SweepProgress(SweepStage.Listings, done, total));
            try
            {
                foreach (var (id, market) in await universalis.GetMarketsAsync(settings.World, chunk, ListingsPerItem, 0, cancellationToken).ConfigureAwait(false))
                {
                    markets[id] = market;
                }
            }
            catch (GilSweepException ex)
            {
                logger.LogWarning("Listings for {Count} items unavailable: {Message}", chunk.Length, ex.Message);
                missedListings += chunk.Length;
            }

            done++;
        }

        if (missedListings > 0)
        {
            warnings.Add($"Listing depth is missing for {missedListings} items, so competition is unknown for them.");
        }

        progress?.Report(new SweepProgress(SweepStage.Crafts, done, total));
        var snapshot = Build(items, prices, trends ?? [], markets, settings, previous, clock.GetUtcNow(), catalog.Recipes, catalog.Demand);
        snapshot.Warnings = warnings.Count > 0 ? warnings : null;
        logger.LogInformation("Swept {World}: {Rows} items, {Crafts} crafts, {Warnings} warnings", settings.World, snapshot.Rows.Count, snapshot.Crafts?.Count ?? 0, warnings.Count);
        return snapshot;
    }

    public int EstimateRequests(GilSweepSettings settings) =>
        (int)Math.Ceiling(PricingIds().Count / (double)UniversalisClient.MaxAggregatedIds)
        + (int)Math.Ceiling(ListingIds(settings).Count / (double)UniversalisClient.MaxCurrentIds)
        + (settings.UseSaddlebag ? 1 : 0);

    /// <summary>Tracked items, then demand consumers, then recipe ingredients, each id once (v1's order).</summary>
    public List<int> PricingIds() =>
        [.. catalog.Items.Select(item => item.Id)
            .Concat(catalog.Demand.ConsumerIds())
            .Concat(catalog.Recipes.SelectMany(recipe => recipe.Ingredients).Select(ingredient => ingredient.Id))
            .Distinct()];

    /// <summary>Items worth checking the listings of: anything you might farm, plus everything you watch.</summary>
    public List<int> ListingIds(GilSweepSettings settings) =>
        [.. catalog.Items.Where(item => !item.Kind.IsTrap() && item.Kind != ItemKind.Crystal).Select(item => item.Id)
            .Concat(settings.Watchlist.Select(entry => entry.ItemId))
            .Distinct()];

    /// <summary>
    /// The snapshot from fetched market data (v1's <c>run</c> after its fetches). Pure, so tests
    /// can feed it recorded responses.
    /// </summary>
    public static MarketSnapshot Build(
        IReadOnlyList<CatalogItem> items,
        IReadOnlyDictionary<int, ItemPrices> prices,
        IReadOnlyList<TrendSignal> trends,
        IReadOnlyDictionary<int, ItemMarket> markets,
        GilSweepSettings settings,
        MarketSnapshot? previous,
        DateTimeOffset now,
        IReadOnlyList<CraftRecipe>? recipes = null,
        DemandIndex? demand = null)
    {
        recipes ??= ItemCatalog.LoadBundledRecipes();
        demand ??= ItemCatalog.LoadBundledDemand();
        ItemPrices Prices(int id) => prices.TryGetValue(id, out var found) ? found : new ItemPrices(id, PriceSet.Empty, PriceSet.Empty);
        double VelocityOf(int id) => JsMath.ToFixed(Prices(id).Nq.DailySaleVelocity.Value, 1);

        var trendById = new Dictionary<int, TrendSignal>();
        foreach (var trend in trends)
        {
            trendById[trend.ItemId] = trend;
        }

        var previousById = previous is not null && previous.World == settings.World
            ? previous.Rows.GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.Last())
            : null;
        var owners = settings.RetainerNames.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rows = new List<SnapshotRow>();
        foreach (var item in items)
        {
            var p = Prices(item.Id);
            var avg = JsMath.Round(p.Nq.AverageSalePrice.Value);
            var velDay = VelocityOf(item.Id);
            var trend = trendById.GetValueOrDefault(item.Id);
            var before = previousById?.GetValueOrDefault(item.Id);
            var (why, demandInfo) = DemandIndex.Summarize(item, demand.Get(item.Id), VelocityOf);
            rows.Add(new SnapshotRow
            {
                Name = item.Name,
                Id = item.Id,
                Job = item.Job,
                Level = item.Level,
                Where = item.Where,
                Kind = item.Kind,
                Expansion = item.Expansion,
                Note = item.Note,
                Spawns = item.Spawns,
                Uptime = item.Uptime,
                Custom = item.Custom,
                Avg = avg,
                Min = JsMath.Round(p.Nq.MinListing.Value),
                VelDay = velDay,
                VelScope = p.Nq.DailySaleVelocity.Scope,
                Throughput = JsMath.Round(avg * velDay),
                SbState = trend?.State,
                SbSoldWeek = trend?.SoldWeek,
                AvgChangePct = before is { Avg: not 0 } ? JsMath.ToFixed((double)(avg - before.Avg) / before.Avg * 100, 1) : null,
                Why = why,
                Demand = demandInfo,
                Listings = markets.TryGetValue(item.Id, out var market) ? Depth(market, owners) : null,
            });
        }

        var known = items.Select(item => item.Id).ToHashSet();
        return new MarketSnapshot
        {
            Date = now.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            Timestamp = now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture),
            World = settings.World,

            // Stable, like v1's Array.sort: ties keep database order.
            Rows = [.. rows.OrderByDescending(row => row.Throughput)],
            SbUnknown = [.. trends.Where(trend => !known.Contains(trend.ItemId)).Take(15).Select(trend => new SaddlebagCandidate
            {
                Name = trend.Name,
                Id = trend.ItemId,
                Avg = trend.Average,
                SoldWeek = trend.SoldWeek,
                State = trend.State,
            })],
            Crafts = CraftCalculator.Compute(recipes, prices, items.Where(item => item.Kind != ItemKind.Crystal).Select(item => item.Id).ToHashSet()),
        };
    }

    private static ListingDepth Depth(ItemMarket market, HashSet<string> owners)
    {
        var listings = market.Listings.OrderBy(listing => listing.PricePerUnit).ToList();
        var own = listings.Where(listing => listing.RetainerName is { } name && owners.Contains(name)).ToList();
        return new ListingDepth
        {
            ListingsCount = market.ListingsCount,
            UnitsForSale = market.UnitsForSale,
            Cheapest = listings.Count > 0 ? listings[0].PricePerUnit : 0,
            OwnCheapest = own.Count > 0 ? own[0].PricePerUnit : null,
            CheapestIsOwn = listings.Count > 0 && listings[0].RetainerName is { } first && owners.Contains(first),
            UpdatedAt = market.LastUploadedAt,
        };
    }
}
