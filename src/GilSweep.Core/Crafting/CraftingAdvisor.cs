using GilSweep.Core.Configuration;
using GilSweep.Core.Market;
using GilSweep.Core.Sweep;

namespace GilSweep.Core.Crafting;

/// <summary>
/// Sell a gathered material raw, or craft it first? One recipe that uses something you farm, at
/// sweep-time prices, per craft.
/// </summary>
/// <param name="Raw">The farmed ingredient with the largest share of the cost.</param>
/// <param name="SellRaw">What that ingredient would fetch sold as-is (its quantity at the cheapest listing).</param>
/// <param name="Processed">Sale price × yield.</param>
/// <param name="OtherMaterials">Everything else the recipe needs, crystals included.</param>
/// <param name="Difference">Processed − other materials − raw: v1's margin.</param>
/// <param name="CrafterLevel">The configured level of the recipe's job.</param>
public sealed record CraftingOpportunity(
    CraftValue Craft,
    CraftIngredient Raw,
    long SellRaw,
    long Processed,
    long OtherMaterials,
    long Difference,
    bool Locked,
    int CrafterLevel)
{
    public bool WorthIt => !Locked && Difference > 0;

    /// <summary>Ingredients cheaper to craft than to buy (v1's one level of min(buy, craft)).</summary>
    public IEnumerable<CraftIngredient> CraftedIngredients => Craft.Ingredients.Where(ingredient => ingredient.ViaCraft);
}

public static class CraftingAdvisor
{
    public const int Limit = 40;

    /// <summary>
    /// Recipes with complete costs on world sales that use something this character can farm,
    /// in v1's order (margin × daily sales). Unlike v1's page, losing recipes and recipes above
    /// the crafter levels are kept, so the answer can be "sell raw" or "level a crafter".
    /// </summary>
    public static List<CraftingOpportunity> Build(MarketSnapshot snapshot, GilSweepSettings settings)
    {
        var mine = Ranking.FarmableMaterialIds(snapshot.Rows, settings);
        return [.. (snapshot.Crafts ?? [])
            .Where(craft => craft.CostComplete && craft.VelScope == MarketScope.World)
            .Where(craft => craft.UsesTracked.Any(mine.Contains))
            .Select(craft => Advise(craft, mine, settings))
            .OfType<CraftingOpportunity>()];
    }

    /// <summary>The best recipe worth processing for, or null.</summary>
    public static CraftingOpportunity? Best(MarketSnapshot snapshot, GilSweepSettings settings) =>
        Build(snapshot, settings).FirstOrDefault(opportunity => opportunity.WorthIt);

    private static CraftingOpportunity? Advise(CraftValue craft, HashSet<int> mine, GilSweepSettings settings)
    {
        var raw = craft.Ingredients
            .Where(ingredient => mine.Contains(ingredient.Id))
            .OrderByDescending(ingredient => ingredient.UnitPrice * ingredient.Qty)
            .FirstOrDefault();
        if (raw is null)
        {
            return null;
        }

        var sellRaw = raw.UnitPrice * raw.Qty;
        var processed = craft.SalePrice * craft.Yield;
        return new CraftingOpportunity(
            craft,
            raw,
            sellRaw,
            processed,
            craft.Cost - sellRaw,
            craft.Margin,
            !Ranking.CraftableBy(craft, settings),
            Ranking.CrafterCap(craft.Job, settings));
    }
}
