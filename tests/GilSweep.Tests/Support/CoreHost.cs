using GilSweep.Core;
using GilSweep.Core.Market;
using GilSweep.Core.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace GilSweep.Tests.Support;

/// <summary>Core's real services over a temp data folder, a hand-moved clock and recorded market data.</summary>
public sealed class CoreHost : IDisposable
{
    private readonly ServiceProvider _provider;

    public CoreHost(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAppEnvironment>(Environment);
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton(new MarketHttpOptions { RetryDelay = TimeSpan.Zero, RequestTimeout = TimeSpan.FromSeconds(5) });
        services.AddGilSweepCore();
        services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => Market));
        configure?.Invoke(services);
        _provider = services.BuildServiceProvider();
    }

    public TestEnvironment Environment { get; } = new();

    public TestClock Clock { get; } = new(new DateTimeOffset(2026, 10, 5, 18, 30, 0, TimeSpan.Zero));

    public FakeMarketHandler Market { get; } = new();

    public T Get<T>()
        where T : notnull => _provider.GetRequiredService<T>();

    public void Dispose()
    {
        _provider.Dispose();
        Environment.Dispose();
    }
}
