using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using GilSweep.Core.Market;
using Microsoft.Extensions.Logging;

namespace GilSweep.Core.Catalog;

/// <param name="Reason">v1's wording, kept for tests and the log.</param>
/// <param name="Message">What the user reads.</param>
public sealed record VerifyResult(bool Found, int? Id, string? Name, bool Gatherable, bool AlreadyTracked, string Reason, string Message);

/// <summary>
/// Adds an item to the database by name, the way the curated list was built: XIVAPI finds the
/// item, and Garland Tools says whether the item itself has gathering nodes. Node partials alone
/// can belong to a crafted item's ingredients (v1's Desert Lapis lesson), so only the item's own
/// node list counts. Top sellers with no nodes are classified: crafted ones are left to the Craft
/// screen, vendor and voyage items are tracked as never-farm traps so they stop resurfacing.
/// </summary>
public interface IItemVerifier
{
    Task<(VerifyResult Result, CatalogItem? Item)> VerifyAsync(string query, IReadOnlySet<int> tracked, CancellationToken cancellationToken = default);
}

public sealed class ItemVerifier : IItemVerifier
{
    private const string Garland = "https://garlandtools.org/db/doc/";
    private readonly MarketHttp _xivapi;
    private readonly MarketHttp _garland;
    private Dictionary<string, GarlandLocation>? _locations;

    public ItemVerifier(HttpClient http, MarketHttpOptions options, TimeProvider clock, ILogger<ItemVerifier> logger)
    {
        _xivapi = new MarketHttp(http, options, clock, logger, "XIVAPI");
        _garland = new MarketHttp(http, options, clock, logger, "Garland Tools");
    }

    public static Expansion ExpansionOf(int level) => level switch
    {
        <= 50 => Expansion.ARR,
        <= 60 => Expansion.HW,
        <= 70 => Expansion.StB,
        <= 80 => Expansion.ShB,
        <= 90 => Expansion.EW,
        _ => Expansion.DT,
    };

    public async Task<(VerifyResult Result, CatalogItem? Item)> VerifyAsync(string query, IReadOnlySet<int> tracked, CancellationToken cancellationToken = default)
    {
        query = query.Trim();

        // 1. The name to an item id: an exact match first, a partial one as a fallback.
        SearchHit? hit = null;
        foreach (var search in new[] { $"Name=\"{query}\"", $"Name~\"{query}\"" })
        {
            var response = await _xivapi.GetAsync(
                "https://v2.xivapi.com/api/search?sheets=Item&query=" + Uri.EscapeDataString(search) + "&fields=Name",
                VerifierJsonContext.Default.SearchResponse,
                cancellationToken).ConfigureAwait(false);
            hit = response.Results?.FirstOrDefault();
            if (hit is not null)
            {
                break;
            }
        }

        if (hit is null)
        {
            return (new VerifyResult(false, null, null, false, false, $"no item named \"{query}\"", $"No item is named \"{query}\"."), null);
        }

        var id = hit.RowId;
        var name = hit.Fields?.Name ?? query;
        if (tracked.Contains(id))
        {
            return (new VerifyResult(true, id, name, false, true, "already tracked", $"{name} is already tracked."), null);
        }

        // 2. Garland: does the item itself have gathering nodes?
        var document = await _garland.GetAsync(
            $"{Garland}item/en/3/{id.ToString(CultureInfo.InvariantCulture)}.json",
            VerifierJsonContext.Default.GarlandItemDocument,
            cancellationToken).ConfigureAwait(false);
        var item = document.Item ?? new GarlandItem();
        var nodeIds = (item.Nodes ?? []).Select(node => node.ToString(CultureInfo.InvariantCulture)).ToHashSet();
        List<GarlandNode> nodes;
        try
        {
            nodes = [.. (document.Partials ?? [])
                .Where(partial => partial.Type == "node" && nodeIds.Contains(partial.Id.ToString()))
                .Select(partial => partial.Obj.Deserialize(VerifierJsonContext.Default.GarlandNode))
                .OfType<GarlandNode>()];
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            throw new GilSweepException(GilSweepErrorKind.MarketUnavailable, "Garland Tools sent node data Gil Sweep couldn't read.", ex);
        }

        if (nodes.Count > 0)
        {
            var node = nodes[0];
            var label = (node.Lt ?? "").ToLowerInvariant();
            var kind = label.Contains("legendary", StringComparison.Ordinal) ? ItemKind.Legendary
                : label.Contains("unspoiled", StringComparison.Ordinal) ? ItemKind.Unspoiled
                : label.Contains("ephemeral", StringComparison.Ordinal) ? ItemKind.Ephemeral
                : ItemKind.Node;
            var spawns = nodes.SelectMany(each => each.Ti ?? []).Distinct().Order().ToList();
            var zone = await ZoneNameAsync(node.Z, cancellationToken).ConfigureAwait(false);
            var where = string.Join(", ", new[] { node.N, zone }.Where(part => !string.IsNullOrEmpty(part)));
            var level = node.L ?? 0;
            var added = new CatalogItem
            {
                Name = name,
                Id = id,
                Job = (node.T ?? 0) <= 1 ? Job.Miner : Job.Botanist,
                Level = level,
                Where = where.Length > 0 ? where : "unverified",
                Kind = kind,
                Expansion = ExpansionOf(level),
                Spawns = spawns.Count > 0 ? spawns : null,
                Note = "added in-app (Garland-verified nodes)",
            };
            var kindWord = kind.ToString().ToLowerInvariant();
            var timing = spawns.Count > 0 ? $" · spawns {string.Join('/', spawns)} ET" : "";
            return (new VerifyResult(true, id, name, true, false,
                $"{kindWord} lv{node.L?.ToString(CultureInfo.InvariantCulture) ?? "undefined"}{timing} · {added.Where}",
                $"Tracked {name}: {added.Kind.Describe().ToLowerInvariant()}, level {level}{(spawns.Count > 0 ? $", windows at {string.Join(" and ", spawns.Select(hour => $"{hour:00}:00"))} ET" : "")}, {added.Where}."), added);
        }

        // 3. No nodes: classify the trap so it stops resurfacing.
        if (item.Craft is { Count: > 0 })
        {
            return (new VerifyResult(true, id, name, false, false,
                "crafted item — the Crafting page covers these; not tracked",
                $"{name} is crafted, not gathered. The Craft screen covers recipes, so it isn't tracked."), null);
        }

        var vendor = item.Vendors is { Count: > 0 } || item.TradeShops is { Count: > 0 };
        var trap = new CatalogItem
        {
            Name = name,
            Id = id,
            Job = Job.Either,
            Level = 0,
            Where = vendor ? "vendor purchase" : "unknown source (no nodes/recipe/vendor)",
            Kind = vendor ? ItemKind.Vendor : ItemKind.Submarine,
            Expansion = Expansion.ARR,
            Note = "added in-app - NOT gatherable" + (vendor ? "" : "; likely FC voyage loot"),
        };
        return (new VerifyResult(true, id, name, false, false,
            vendor ? "vendor item — tracked as a non-farm so it stops resurfacing" : "no nodes, recipe, or vendor — likely FC voyage loot; tracked as a non-farm",
            vendor
                ? $"{name} is sold by vendors, not gathered. It is tracked as a non-farm so it is never recommended."
                : $"{name} has no nodes, recipe or vendor; it is probably Free Company voyage loot. It is tracked as a non-farm so it is never recommended."), trap);
    }

    /// <summary>Garland's zone names come in one large file, fetched once per session. Without it the location is just the node name.</summary>
    private async Task<string> ZoneNameAsync(int? zone, CancellationToken cancellationToken)
    {
        if (zone is not { } id || id == 0)
        {
            return "";
        }

        try
        {
            _locations ??= (await _garland.GetAsync($"{Garland}core/en/3/data.json", VerifierJsonContext.Default.GarlandCore, cancellationToken).ConfigureAwait(false)).LocationIndex ?? [];
            return _locations.GetValueOrDefault(id.ToString(CultureInfo.InvariantCulture))?.Name ?? "";
        }
        catch (GilSweepException)
        {
            return "";
        }
    }
}

internal sealed class SearchResponse
{
    public List<SearchHit>? Results { get; set; }
}

internal sealed class SearchHit
{
    [JsonPropertyName("row_id")]
    public int RowId { get; set; }

    public SearchFields? Fields { get; set; }
}

internal sealed class SearchFields
{
    [JsonPropertyName("Name")]
    public string? Name { get; set; }
}

internal sealed class GarlandItemDocument
{
    public GarlandItem? Item { get; set; }

    public List<GarlandPartial>? Partials { get; set; }
}

internal sealed class GarlandItem
{
    public List<int>? Nodes { get; set; }

    public List<JsonElement>? Craft { get; set; }

    public List<JsonElement>? Vendors { get; set; }

    public List<JsonElement>? TradeShops { get; set; }
}

internal sealed class GarlandPartial
{
    public string? Type { get; set; }

    /// <summary>Sent as a string ("155").</summary>
    public JsonElement Id { get; set; }

    /// <summary>Its shape depends on the partial's type; only node partials are read.</summary>
    public JsonElement Obj { get; set; }
}

/// <summary>A gathering node partial: name, level, type (0–1 mining, 2–3 logging), zone, label, spawn hours.</summary>
internal sealed class GarlandNode
{
    public string? N { get; set; }

    public int? L { get; set; }

    public int? T { get; set; }

    public int? Z { get; set; }

    public string? Lt { get; set; }

    public List<int>? Ti { get; set; }
}

internal sealed class GarlandCore
{
    public Dictionary<string, GarlandLocation>? LocationIndex { get; set; }
}

internal sealed class GarlandLocation
{
    public string? Name { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(SearchResponse))]
[JsonSerializable(typeof(GarlandItemDocument))]
[JsonSerializable(typeof(GarlandNode))]
[JsonSerializable(typeof(GarlandCore))]
internal sealed partial class VerifierJsonContext : JsonSerializerContext;
