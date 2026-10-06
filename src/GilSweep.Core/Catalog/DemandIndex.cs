using System.Globalization;
using GilSweep.Core.Sweep;

namespace GilSweep.Core.Catalog;

/// <summary>
/// Why an item sells, from the bundled Garland Tools signals. Consumers rank by
/// units sold per day × quantity per craft: roughly how much of the item the market burns daily.
/// </summary>
public sealed class DemandIndex
{
    private readonly Dictionary<int, DemandSignals> _byId;
    private readonly IReadOnlyList<int> _consumerIds;

    public DemandIndex(IReadOnlyDictionary<string, DemandSignals> byId)
    {
        // v1 walked this object with Object.values: integer keys in ascending order.
        var ordered = byId.OrderBy(pair => int.Parse(pair.Key, CultureInfo.InvariantCulture)).ToList();
        _byId = ordered.ToDictionary(pair => int.Parse(pair.Key, CultureInfo.InvariantCulture), pair => pair.Value);
        _consumerIds = [.. ordered.SelectMany(pair => pair.Value.Consumers).Select(consumer => consumer.Id).Distinct()];
    }

    public DemandSignals? Get(int itemId) => _byId.GetValueOrDefault(itemId);

    /// <summary>Every consumer id, priced in the same pass as the tracked items.</summary>
    public IReadOnlyList<int> ConsumerIds() => _consumerIds;

    /// <summary>The "why it sells" line and the top five consumers (v1's <c>summarizeDemand</c>).</summary>
    public static (string Why, DemandInfo? Demand) Summarize(CatalogItem item, DemandSignals? signals, Func<int, double> velocityOf)
    {
        if (item.Kind == ItemKind.Crystal)
        {
            return ("universal craft mat", signals is null ? null : ToInfo(signals, []));
        }

        if (signals is null)
        {
            return ("", null);
        }

        var ranked = signals.Consumers
            .Select(consumer => new DemandConsumer { Id = consumer.Id, Name = consumer.Name, Qty = consumer.Qty, VelDay = velocityOf(consumer.Id) })
            .OrderByDescending(consumer => consumer.VelDay * consumer.Qty)
            .ThenByDescending(consumer => consumer.Qty)
            .ToList();

        var parts = ranked.Take(2)
            .Select(consumer => $"{consumer.Name} ×{consumer.Qty}{(consumer.VelDay != 0 ? $" ({JsNumber(consumer.VelDay)}/d)" : "")}")
            .ToList();
        var more = signals.RecipeCount - 2;
        if (more > 0)
        {
            parts.Add($"+{more} more recipe{(more == 1 ? "" : "s")}");
        }

        if (signals.Leves != 0)
        {
            parts.Add($"leves ×{signals.Leves}");
        }

        if (signals.Supply is not null)
        {
            parts.Add("GC supply");
        }

        if (signals.Quests != 0)
        {
            parts.Add($"quests ×{signals.Quests}");
        }

        var why = string.Join(" · ", parts);
        if (why.Length == 0 && item.Kind == ItemKind.Diadem)
        {
            why = "Ishgardian Restoration turn-in";
        }

        return (why, ToInfo(signals, [.. ranked.Take(5)]));
    }

    /// <summary>Formats a number the way JavaScript prints it in a template string.</summary>
    internal static string JsNumber(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static DemandInfo ToInfo(DemandSignals signals, List<DemandConsumer> top) => new()
    {
        RecipeCount = signals.RecipeCount,
        Leves = signals.Leves,
        Supply = signals.Supply is { } supply ? new SupplyMission { Count = supply.Count, Seals = supply.Seals } : null,
        Quests = signals.Quests,
        TopConsumers = top,
    };
}
