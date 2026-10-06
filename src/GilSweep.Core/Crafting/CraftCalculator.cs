using GilSweep.Core.Catalog;
using GilSweep.Core.Market;
using GilSweep.Core.Sweep;

namespace GilSweep.Core.Crafting;

/// <summary>
/// Craft margins at sweep-time prices (v1's two passes, unchanged):
/// first the cost of crafting each recipe from bought materials (cheapest normal-quality
/// listings), then each recipe's margin with every ingredient at min(buy, craft): one level
/// deep, so a nugget → ingot chain costs the nugget whichever way is cheaper. Outputs sell at
/// HQ when the HQ market trades more; materials stay NQ (gathered materials have no HQ since 6.0).
/// </summary>
public static class CraftCalculator
{
    public static List<CraftValue> Compute(IReadOnlyList<CraftRecipe> recipes, IReadOnlyDictionary<int, ItemPrices> prices, IReadOnlySet<int> trackedMaterialIds)
    {
        long BuyUnit(int id) => JsMath.Round(Prices(id).Nq.MinListing.Value);
        ItemPrices Prices(int id) => prices.TryGetValue(id, out var found) ? found : new ItemPrices(id, PriceSet.Empty, PriceSet.Empty);

        var craftUnit = new Dictionary<int, long>();
        foreach (var recipe in recipes)
        {
            long cost = 0;
            var complete = true;
            foreach (var ingredient in recipe.Ingredients)
            {
                var unit = BuyUnit(ingredient.Id);
                if (unit == 0)
                {
                    complete = false;
                    break;
                }

                cost += unit * ingredient.Qty;
            }

            if (complete)
            {
                craftUnit[recipe.Id] = (long)Math.Ceiling((double)cost / recipe.Yield);
            }
        }

        var crafts = new List<CraftValue>();
        foreach (var recipe in recipes)
        {
            var p = Prices(recipe.Id);
            var nqAvg = JsMath.Round(p.Nq.AverageSalePrice.Value);
            var nqVel = JsMath.ToFixed(p.Nq.DailySaleVelocity.Value, 1);
            var hqAvg = JsMath.Round(p.Hq.AverageSalePrice.Value);
            var hqVel = JsMath.ToFixed(p.Hq.DailySaleVelocity.Value, 1);
            var useHq = hqAvg > 0 && hqVel > nqVel;
            var salePrice = useHq ? hqAvg : nqAvg;
            var velDay = useHq ? hqVel : nqVel;
            if (salePrice == 0 || velDay == 0)
            {
                // A dead market isn't worth listing into.
                continue;
            }

            long cost = 0;
            var costComplete = true;
            var ingredients = new List<CraftIngredient>();
            foreach (var ingredient in recipe.Ingredients)
            {
                var buy = BuyUnit(ingredient.Id);
                var crafted = craftUnit.GetValueOrDefault(ingredient.Id);
                var unitPrice = buy != 0 && crafted != 0 ? Math.Min(buy, crafted) : buy != 0 ? buy : crafted;
                if (unitPrice == 0)
                {
                    costComplete = false;
                }

                cost += unitPrice * ingredient.Qty;
                ingredients.Add(new CraftIngredient
                {
                    Id = ingredient.Id,
                    Name = ingredient.Name,
                    Qty = ingredient.Qty,
                    UnitPrice = unitPrice,
                    ViaCraft = crafted != 0 && (buy == 0 || crafted < buy),
                });
            }

            var margin = salePrice * recipe.Yield - cost;
            crafts.Add(new CraftValue
            {
                Id = recipe.Id,
                Name = recipe.Name,
                Job = recipe.Job,
                Lvl = recipe.Lvl,
                Yield = recipe.Yield,
                SalePrice = salePrice,
                Hq = useHq,
                VelDay = velDay,
                VelScope = (useHq ? p.Hq : p.Nq).DailySaleVelocity.Scope,
                Cost = cost,
                CostComplete = costComplete,
                Margin = margin,
                MarginPct = cost > 0 ? JsMath.ToFixed((double)margin / cost * 100, 1) : null,
                UsesTracked = [.. recipe.Ingredients.Where(ingredient => trackedMaterialIds.Contains(ingredient.Id)).Select(ingredient => ingredient.Id)],
                Ingredients = ingredients,
            });
        }

        // Stable, like v1's Array.sort: ties keep recipe order.
        return [.. crafts.OrderByDescending(craft => craft.Margin * craft.VelDay)];
    }
}
