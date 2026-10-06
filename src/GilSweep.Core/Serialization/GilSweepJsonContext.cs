using System.Text.Json;
using System.Text.Json.Serialization;
using GilSweep.Core.Alerts;
using GilSweep.Core.Catalog;
using GilSweep.Core.Configuration;
using GilSweep.Core.History;
using GilSweep.Core.Sweep;

namespace GilSweep.Core.Serialization;

/// <summary>Gil Sweep's own files: settings, the item database, snapshots. camelCase, as v1 wrote them.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UseStringEnumConverter = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(GilSweepSettings))]
[JsonSerializable(typeof(CatalogItem))]
[JsonSerializable(typeof(List<CatalogItem>))]
[JsonSerializable(typeof(Dictionary<string, CraftRecipe>))]
[JsonSerializable(typeof(Dictionary<string, DemandSignals>))]
[JsonSerializable(typeof(MarketSnapshot))]
[JsonSerializable(typeof(BackfillFile))]
[JsonSerializable(typeof(List<AlertEvent>))]
internal sealed partial class GilSweepJsonContext : JsonSerializerContext
{
    private static GilSweepJsonContext? _compact;

    /// <summary>Snapshots are written compact, like v1's; settings stay readable.</summary>
    public static GilSweepJsonContext Compact => _compact ??= new GilSweepJsonContext(new JsonSerializerOptions(Default.Options) { WriteIndented = false });
}
