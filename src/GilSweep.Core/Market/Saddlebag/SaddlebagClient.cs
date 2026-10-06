using System.Text.Json.Serialization;
using GilSweep.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace GilSweep.Core.Market.Saddlebag;

/// <summary>
/// Saddlebag Exchange's market-share report: a week's sales and a trend state per item. Optional:
/// a sweep completes without it, so a failure returns null instead of throwing (as in v1).
/// </summary>
public interface ISaddlebagClient
{
    Task<IReadOnlyList<TrendSignal>?> GetMarketShareAsync(string world, SaddlebagQuery query, CancellationToken cancellationToken = default);
}

public sealed class SaddlebagClient : ISaddlebagClient
{
    public const string Url = "https://api.saddlebagexchange.com/api/ffxivmarketshare";

    private readonly MarketHttp _http;
    private readonly ILogger<SaddlebagClient> _logger;

    public SaddlebagClient(HttpClient http, MarketHttpOptions options, TimeProvider clock, ILogger<SaddlebagClient> logger)
    {
        _logger = logger;
        _http = new MarketHttp(http, new MarketHttpOptions
        {
            // A second source isn't worth holding up a sweep for: one try.
            MaxAttempts = 1,
            RequestTimeout = options.RequestTimeout,
            RetryDelay = options.RetryDelay,
            MaxRetryAfter = options.MaxRetryAfter,
        }, clock, logger, "Saddlebag Exchange");
    }

    public async Task<IReadOnlyList<TrendSignal>?> GetMarketShareAsync(string world, SaddlebagQuery query, CancellationToken cancellationToken = default)
    {
        var body = new MarketShareRequest
        {
            Server = world,
            TimePeriod = query.TimePeriod,
            SalesAmount = query.SalesAmount,
            AveragePrice = query.AveragePrice,
            Filters = query.Filters,
        };
        try
        {
            var response = await _http.PostAsync(Url, body, SaddlebagJsonContext.Default.MarketShareRequest, SaddlebagJsonContext.Default.MarketShareResponse, cancellationToken).ConfigureAwait(false);
            if (response.Data is not { } rows)
            {
                return null;
            }

            return [.. rows.Select(row => new TrendSignal(row.ItemId, row.Name ?? "", row.Avg ?? 0, row.QuantitySold ?? 0, row.State ?? "", row.PercentChange))];
        }
        catch (GilSweepException ex)
        {
            _logger.LogWarning("Saddlebag Exchange unavailable: {Message}", ex.Message);
            return null;
        }
    }
}

internal sealed class MarketShareRequest
{
    [JsonPropertyName("server")]
    public string Server { get; set; } = "";

    [JsonPropertyName("time_period")]
    public int TimePeriod { get; set; }

    [JsonPropertyName("sales_amount")]
    public int SalesAmount { get; set; }

    [JsonPropertyName("average_price")]
    public int AveragePrice { get; set; }

    [JsonPropertyName("filters")]
    public List<int> Filters { get; set; } = [];

    [JsonPropertyName("sort_by")]
    public string SortBy { get; set; } = "marketValue";
}

internal sealed class MarketShareResponse
{
    public List<MarketShareRow>? Data { get; set; }
}

internal sealed class MarketShareRow
{
    public string? Name { get; set; }

    /// <summary>Sent as a string ("35792").</summary>
    [JsonPropertyName("itemID")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int ItemId { get; set; }

    public double? Avg { get; set; }

    public double? QuantitySold { get; set; }

    public string? State { get; set; }

    public double? PercentChange { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(MarketShareRequest))]
[JsonSerializable(typeof(MarketShareResponse))]
internal sealed partial class SaddlebagJsonContext : JsonSerializerContext;
