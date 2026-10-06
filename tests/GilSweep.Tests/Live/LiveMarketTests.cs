using GilSweep.Core;
using GilSweep.Core.Configuration;
using GilSweep.Core.Market;
using GilSweep.Core.Platform;
using GilSweep.Core.Sweep;
using GilSweep.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace GilSweep.Tests.Live;

/// <summary>
/// Against the real Universalis and Saddlebag Exchange. Explicit: never part of a normal run.
/// dotnet test --project tests/GilSweep.Tests -- --filter-trait "Category=Live" --explicit only
/// </summary>
public sealed class LiveMarketTests
{
    [Fact(Explicit = true)]
    [Trait("Category", "Live")]
    public async Task A_live_sweep_ranks_farms_for_an_endgame_character()
    {
        using var environment = new TestEnvironment();
        var services = new ServiceCollection()
            .AddSingleton<IAppEnvironment>(environment)
            .AddGilSweepCore()
            .BuildServiceProvider();
        var settings = new GilSweepSettings
        {
            Levels = new GathererLevels { Miner = 100, Botanist = 100 },
            MsqExpansion = GilSweep.Core.Catalog.Expansion.DT,
        };

        var snapshot = await services.GetRequiredService<ISweepEngine>().RunAsync(settings, null, cancellationToken: TestContext.Current.CancellationToken);
        var board = OpportunityBoard.Build(snapshot, null, settings, DateTimeOffset.UtcNow);

        Assert.True(snapshot.Rows.Count(row => row.VelScope == MarketScope.World) > 60);
        Assert.True(snapshot.Rows.Count(row => row.Listings is not null) > 60, string.Join(" ", snapshot.Warnings ?? []));
        Assert.NotNull(board.Best);
        TestContext.Current.SendDiagnosticMessage($"Best: {board.Best.Name} {board.Best.Score.Total} ({board.Best.Grade}); warnings: {string.Join(" ", snapshot.Warnings ?? [])}");
    }
}
