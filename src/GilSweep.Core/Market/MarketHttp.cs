using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;

namespace GilSweep.Core.Market;

/// <summary>How hard to try a market API before giving up. Tests shrink the delays to zero.</summary>
public sealed class MarketHttpOptions
{
    public int MaxAttempts { get; set; } = 3;

    /// <summary>First retry delay; doubles each attempt. A 429's Retry-After wins when it is longer.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Longest wait honored from a Retry-After header.</summary>
    public TimeSpan MaxRetryAfter { get; set; } = TimeSpan.FromSeconds(15);

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// GET/POST with retries for the community market APIs. Rate limits (429), gateway errors (the
/// current-data endpoint returns 504 under load), network failures and timeouts are retried with
/// backoff; anything else, or running out of attempts, becomes a <see cref="GilSweepException"/>
/// with a message for the user. Cancellation by the caller is never retried.
/// </summary>
internal sealed class MarketHttp(HttpClient http, MarketHttpOptions options, TimeProvider clock, ILogger logger, string provider)
{
    public async Task<T> GetAsync<T>(string url, JsonTypeInfo<T> type, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, url), cancellationToken).ConfigureAwait(false);
        return await ReadAsync(response, type, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TResult> PostAsync<TBody, TResult>(string url, TBody body, JsonTypeInfo<TBody> bodyType, JsonTypeInfo<TResult> resultType, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(JsonSerializer.Serialize(body, bodyType), System.Text.Encoding.UTF8, "application/json") },
            cancellationToken).ConfigureAwait(false);
        return await ReadAsync(response, resultType, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> request, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            TimeSpan? retryAfter = null;
            string failure;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.RequestTimeout);
            try
            {
                using var message = request();
                var response = await http.SendAsync(message, HttpCompletionOption.ResponseContentRead, timeout.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                var status = (int)response.StatusCode;
                failure = $"HTTP {status}";
                retryAfter = response.Headers.RetryAfter?.Delta;
                response.Dispose();
                if (!IsTransient(response.StatusCode))
                {
                    throw Unavailable($"{provider} answered {failure}.");
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                failure = "no answer within " + options.RequestTimeout.TotalSeconds + " seconds";
            }
            catch (HttpRequestException ex)
            {
                failure = ex.HttpRequestError == HttpRequestError.NameResolutionError ? "no internet connection" : ex.Message;
            }

            if (attempt >= options.MaxAttempts)
            {
                throw Unavailable($"{provider} is not responding ({failure}).");
            }

            var delay = options.RetryDelay * Math.Pow(2, attempt - 1);
            if (retryAfter is { } wait && wait > delay)
            {
                delay = wait < options.MaxRetryAfter ? wait : options.MaxRetryAfter;
            }

            logger.LogInformation("{Provider} request failed ({Failure}); retrying in {Delay} ms", provider, failure, (int)delay.TotalMilliseconds);
            await Task.Delay(delay, clock, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<T> ReadAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> type, CancellationToken cancellationToken)
    {
        try
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                return await JsonSerializer.DeserializeAsync(stream, type, cancellationToken).ConfigureAwait(false)
                    ?? throw new JsonException("The response was empty.");
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "{Provider} sent a response that could not be read", provider);
            throw Unavailable($"{provider} sent a response Gil Sweep couldn't read.", ex);
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout
            or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private static GilSweepException Unavailable(string message, Exception? inner = null) =>
        new(GilSweepErrorKind.MarketUnavailable, message, inner);
}
