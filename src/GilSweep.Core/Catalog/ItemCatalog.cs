using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using GilSweep.Core.Platform;
using GilSweep.Core.Serialization;
using Microsoft.Extensions.Logging;

namespace GilSweep.Core.Catalog;

/// <summary>
/// The items Gil Sweep prices: the curated list shipped with the app (every node checked against
/// Garland Tools) plus items the user verified and added, and the recipes and demand data that
/// explain them.
/// </summary>
public interface IItemCatalog
{
    /// <summary>Bundled items, then custom ones (flagged <see cref="CatalogItem.Custom"/>).</summary>
    IReadOnlyList<CatalogItem> Items { get; }

    /// <summary>Recipes in ascending id order, the order v1 walked them in.</summary>
    IReadOnlyList<CraftRecipe> Recipes { get; }

    DemandIndex Demand { get; }

    event EventHandler? Changed;

    CatalogItem? Find(int itemId);

    /// <summary>Adds a verified item. Adding an id that is already tracked changes nothing.</summary>
    void AddCustom(CatalogItem item);

    void RemoveCustom(int itemId);
}

public sealed class ItemCatalog : IItemCatalog
{
    private readonly IReadOnlyList<CatalogItem> _bundled;
    private readonly string _customPath;
    private readonly ILogger<ItemCatalog> _logger;
    private readonly List<CatalogItem> _custom;
    private IReadOnlyList<CatalogItem> _items;

    public ItemCatalog(IAppEnvironment environment, ILogger<ItemCatalog> logger)
    {
        _logger = logger;
        _bundled = LoadBundledItems();
        Recipes = LoadBundledRecipes();
        Demand = LoadBundledDemand();
        _customPath = Path.Combine(environment.DataDirectory, "custom-items.json");
        _custom = LoadCustom();
        _items = Combine();
    }

    public IReadOnlyList<CatalogItem> Items => _items;

    public IReadOnlyList<CraftRecipe> Recipes { get; }

    public DemandIndex Demand { get; }

    public event EventHandler? Changed;

    public CatalogItem? Find(int itemId) => _items.FirstOrDefault(item => item.Id == itemId);

    public void AddCustom(CatalogItem item)
    {
        if (Find(item.Id) is not null)
        {
            return;
        }

        item.Custom = true;
        _custom.Add(item);
        SaveCustom();
    }

    public void RemoveCustom(int itemId)
    {
        if (_custom.RemoveAll(item => item.Id == itemId) > 0)
        {
            SaveCustom();
        }
    }

    public static IReadOnlyList<CatalogItem> LoadBundledItems() =>
        ReadResource("items.json", GilSweepJsonContext.Default.ListCatalogItem);

    /// <summary>
    /// crafts.json is keyed by recipe id. v1 iterated it with Object.values, which orders
    /// integer keys ascending; margins tie-break on that order, so v2 keeps it.
    /// </summary>
    public static IReadOnlyList<CraftRecipe> LoadBundledRecipes() =>
        [.. ReadResource("crafts.json", GilSweepJsonContext.Default.DictionaryStringCraftRecipe)
            .OrderBy(pair => int.Parse(pair.Key, CultureInfo.InvariantCulture))
            .Select(pair => pair.Value)];

    public static DemandIndex LoadBundledDemand() =>
        new(ReadResource("garland-demand.json", GilSweepJsonContext.Default.DictionaryStringDemandSignals));

    private static T ReadResource<T>(string name, JsonTypeInfo<T> type)
    {
        using var stream = typeof(ItemCatalog).Assembly.GetManifestResourceStream("GilSweep.Data." + name)
            ?? throw new InvalidOperationException($"The bundled {name} is missing from the build.");
        return JsonSerializer.Deserialize(stream, type) ?? throw new InvalidOperationException($"The bundled {name} is empty.");
    }

    private List<CatalogItem> LoadCustom()
    {
        if (!File.Exists(_customPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize(AtomicFile.ReadAllText(_customPath), GilSweepJsonContext.Default.ListCatalogItem) ?? [];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // v1 also fell back to no custom items; the bundled database still works.
            _logger.LogWarning(ex, "Could not read custom items from {Path}", _customPath);
            return [];
        }
    }

    private void SaveCustom()
    {
        try
        {
            AtomicFile.WriteAllText(_customPath, JsonSerializer.Serialize(_custom, GilSweepJsonContext.Default.ListCatalogItem));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new GilSweepException(GilSweepErrorKind.StorageFailed, $"Tracked items could not be saved ({ex.Message}).", ex);
        }

        _items = Combine();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<CatalogItem> Combine()
    {
        foreach (var item in _custom)
        {
            item.Custom = true;
        }

        // v1 appended custom items after the bundled ones; an id can only be tracked once.
        return [.. _bundled, .. _custom.Where(item => _bundled.All(bundled => bundled.Id != item.Id))];
    }
}
