using System.Text.Json.Serialization;

namespace GilSweep.Core.Market.Universalis;

// Universalis response shapes. Only the fields Gil Sweep reads; never used outside this folder.

internal sealed class AggregatedResponse
{
    public List<AggregatedResult>? Results { get; set; }
}

internal sealed class AggregatedResult
{
    public int ItemId { get; set; }

    public AggregatedQuality? Nq { get; set; }

    public AggregatedQuality? Hq { get; set; }
}

internal sealed class AggregatedQuality
{
    public AggregatedScopes? MinListing { get; set; }

    public AggregatedScopes? AverageSalePrice { get; set; }

    public AggregatedScopes? DailySaleVelocity { get; set; }
}

internal sealed class AggregatedScopes
{
    public AggregatedFigure? World { get; set; }

    public AggregatedFigure? Dc { get; set; }

    public AggregatedFigure? Region { get; set; }
}

internal sealed class AggregatedFigure
{
    public double? Price { get; set; }

    public double? Quantity { get; set; }
}

internal sealed class CurrentData
{
    [JsonPropertyName("itemID")]
    public int ItemId { get; set; }

    public long? LastUploadTime { get; set; }

    public List<ListingData>? Listings { get; set; }

    public List<SaleData>? RecentHistory { get; set; }

    /// <summary>
    /// Stack size → number of listings, over every listing on the world. listingsCount and
    /// unitsForSale only count the listings returned, so depth comes from here.
    /// </summary>
    public Dictionary<string, int>? StackSizeHistogram { get; set; }
}

internal sealed class MultiCurrentData
{
    public Dictionary<string, CurrentData>? Items { get; set; }
}

internal sealed class ListingData
{
    public double PricePerUnit { get; set; }

    public int Quantity { get; set; }

    public bool Hq { get; set; }

    public string? RetainerName { get; set; }
}

internal sealed class SaleData
{
    public double PricePerUnit { get; set; }

    public int Quantity { get; set; }

    public bool Hq { get; set; }

    /// <summary>Unix seconds.</summary>
    public long Timestamp { get; set; }
}

internal sealed class HistoryData
{
    [JsonPropertyName("itemID")]
    public int ItemId { get; set; }

    public List<SaleData>? Entries { get; set; }
}

internal sealed class MultiHistoryData
{
    public Dictionary<string, HistoryData>? Items { get; set; }
}

internal sealed class WorldData
{
    public int Id { get; set; }

    public string Name { get; set; } = "";
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AggregatedResponse))]
[JsonSerializable(typeof(CurrentData))]
[JsonSerializable(typeof(MultiCurrentData))]
[JsonSerializable(typeof(HistoryData))]
[JsonSerializable(typeof(MultiHistoryData))]
[JsonSerializable(typeof(List<WorldData>))]
internal sealed partial class UniversalisJsonContext : JsonSerializerContext;
