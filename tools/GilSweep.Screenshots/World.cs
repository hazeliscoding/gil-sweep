using System.Text.Json;
using GilSweep.Core.Alerts;
using GilSweep.Core.Catalog;
using GilSweep.Core.Configuration;
using GilSweep.Core.History;
using GilSweep.Core.Market;
using GilSweep.Core.Platform;
using GilSweep.Core.Serialization;
using GilSweep.Core.Sweep;
using GilSweep.Desktop;
using GilSweep.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GilSweep.Screenshots;

public enum WorldKind
{
    /// <summary>An endgame character on Cactuar with two weeks of sweeps and a few watched items.</summary>
    Seasoned,

    /// <summary>Nobody has opened Gil Sweep here before.</summary>
    FirstRun,

    /// <summary>Set up, but the first sweep is still running.</summary>
    Loading,
}

/// <summary>A believable PC for the screenshots, from recorded market data and a fixed clock.</summary>
internal sealed class World : IDisposable
{
    /// <summary>The recordings were made around 03:33 UTC on 2026-10-05.</summary>
    private static readonly DateTimeOffset Recorded = DateTimeOffset.FromUnixTimeMilliseconds(1791260018186);

    private readonly ServiceProvider _services;

    private World(string root, WorldKind kind)
    {
        Root = root;
        var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        Market = new FixtureMarket(fixtures);
        var collection = new ServiceCollection();
        collection.AddSingleton<IAppEnvironment>(new FakeEnvironment(root));
        collection.AddSingleton<TimeProvider>(Clock);
        collection.AddSingleton(new MarketHttpOptions { RetryDelay = TimeSpan.Zero, MaxAttempts = 1 });
        collection.AddGilSweepDesktop();
        collection.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => Market));

        // The desktop registers its own UI services, so these replace them afterwards.
        collection.AddSingleton<IMotionSettings, StillMotion>();
        collection.AddSingleton<IShellService, NoShell>();
        collection.AddSingleton<IAppUpdater>(Updater);
        collection.AddSingleton<IAppInstances, NoInstances>();
        collection.AddSingleton<ITicker>(Ticker);
        collection.AddSingleton<INotifier, NoNotifier>();
        _services = collection.BuildServiceProvider();

        if (kind == WorldKind.FirstRun)
        {
            return;
        }

        var settings = _services.GetRequiredService<ISettingsService>();
        settings.Update(s =>
        {
            s.World = "Cactuar";
            s.Levels = new GathererLevels { Miner = 100, Botanist = 100 };
            s.MsqExpansion = Expansion.DT;
            s.Folklore = [Expansion.HW, Expansion.StB, Expansion.ShB, Expansion.EW, Expansion.DT];
            s.Crafters = new Dictionary<string, int> { ["CRP"] = 100, ["BSM"] = 92, ["ARM"] = 90, ["GSM"] = 100, ["LTW"] = 85, ["WVR"] = 100, ["ALC"] = 90, ["CUL"] = 100 };
            s.RetainerNames = ["Mochi", "Tansy"];
            s.Watchlist =
            [
                new() { ItemId = 49208, NodeOpens = true, PriceSpike = true, Favorite = true },
                new() { ItemId = 45968, NodeOpens = true },
                new() { ItemId = 46246, PriceCrash = true },
                new() { ItemId = 36612, Undercut = true },
            ];
        });

        if (kind == WorldKind.Loading)
        {
            Market.Hold = new TaskCompletionSource();
            return;
        }

        CreateHistory();
    }

    public string Root { get; }

    public FakeClock Clock { get; } = new(new DateTimeOffset(2026, 10, 5, 19, 10, 0, TimeSpan.Zero));

    public FixtureMarket Market { get; }

    public FakeUpdater Updater { get; } = new();

    public ManualTicker Ticker { get; } = new();

    public IServiceProvider Services => _services;

    public static World Create(string root, WorldKind kind) => new(root, kind);

    public void Dispose()
    {
        Market.Hold?.TrySetResult();
        _services.Dispose();
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Two weeks of daily sweeps (the tests' archive), then today's real sweep four minutes ago.</summary>
    private void CreateHistory()
    {
        var store = _services.GetRequiredService<IMarketSnapshotStore>();
        Directory.CreateDirectory(store.Folder);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "V1", "archive"), "*Cactuar.json"))
        {
            // The archive's newest day is today; today's sweep below takes its place.
            if (!Path.GetFileName(file).StartsWith("sweep-2026-10-05", StringComparison.Ordinal))
            {
                File.Copy(file, Path.Combine(store.Folder, Path.GetFileName(file)));
            }
        }

        var sweepAt = Clock.Now - TimeSpan.FromMinutes(4);
        Market.Shift = sweepAt - Recorded;
        var now = Clock.Now;
        Clock.Now = sweepAt;
        _services.GetRequiredService<ISweepService>().RunAsync().GetAwaiter().GetResult();
        Clock.Now = now;

        var log = new List<AlertEvent>
        {
            new(AlertKind.NodeOpening, 49208, "Dense Aluminum Ore", "Dense Aluminum Ore opens in 5m 00s", "Heritage Found. The window lasts 5m 50s.", now - TimeSpan.FromMinutes(78)),
            new(AlertKind.PriceCrash, 46246, "Levinchrome Aethersand", "Levinchrome Aethersand dropped −31%", "Now 1,028g on Cactuar, since the previous sweep.", now - TimeSpan.FromHours(5)),
            new(AlertKind.Undercut, 36612, "Timeworn Kumbhiraskin Map", "Timeworn Kumbhiraskin Map: undercut by 400g", "Cheapest is now 41,200g; yours is 41,600g.", now - TimeSpan.FromHours(26)),
        };
        File.WriteAllText(
            Path.Combine(_services.GetRequiredService<IAppEnvironment>().DataDirectory, "alerts.json"),
            JsonSerializer.Serialize(log, GilSweepJsonContext.Compact.ListAlertEvent));
    }
}
