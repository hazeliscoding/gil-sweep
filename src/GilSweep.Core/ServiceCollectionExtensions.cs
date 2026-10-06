using System.Net.Http.Headers;
using GilSweep.Core.Alerts;
using GilSweep.Core.Catalog;
using GilSweep.Core.Configuration;
using GilSweep.Core.History;
using GilSweep.Core.Market;
using GilSweep.Core.Market.Saddlebag;
using GilSweep.Core.Market.Universalis;
using GilSweep.Core.Platform;
using GilSweep.Core.Sweep;
using GilSweep.Core.Watchlist;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace GilSweep.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything the app needs below the UI. Platform services use TryAdd, so tests
    /// and the screenshot tool can register their own first.
    /// </summary>
    public static IServiceCollection AddGilSweepCore(this IServiceCollection services)
    {
        // Every market request is logged by HttpClient at Information; only its problems are worth keeping.
        services.AddLogging(logging => logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning));
        services.TryAddSingleton<IAppEnvironment, SystemAppEnvironment>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<MarketHttpOptions>();

        services.AddHttpClient<IUniversalisClient, UniversalisClient>(client => Configure(client, UniversalisClient.BaseUrl));
        services.AddHttpClient<ISaddlebagClient, SaddlebagClient>(client => Configure(client, null));
        services.AddHttpClient<IItemVerifier, ItemVerifier>(client => Configure(client, null));

        services.TryAddSingleton<IConfigStore, ConfigStore>();
        services.TryAddSingleton<LegacyImporter>();
        services.TryAddSingleton<ISettingsService, SettingsService>();
        services.TryAddSingleton<IItemCatalog, ItemCatalog>();
        services.TryAddSingleton<IMarketSnapshotStore, MarketSnapshotStore>();
        services.TryAddSingleton<IHistoryService, HistoryService>();
        services.TryAddSingleton<ISweepEngine, SweepEngine>();
        services.TryAddSingleton<ISweepService, SweepService>();
        services.TryAddSingleton<IWatchlistService, WatchlistService>();
        services.TryAddSingleton<IAlertService, AlertService>();
        return services;
    }

    private static void Configure(HttpClient client, string? baseUrl)
    {
        if (baseUrl is not null)
        {
            client.BaseAddress = new Uri(baseUrl);
        }

        // Each request has its own timeout and retries (MarketHttp); the client's would cut across them.
        client.Timeout = Timeout.InfiniteTimeSpan;
        client.DefaultRequestHeaders.UserAgent.ParseAdd(GilSweepInfo.UserAgent);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
}
