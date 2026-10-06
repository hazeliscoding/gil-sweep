using System.Text.Json;
using GilSweep.Core.Catalog;
using GilSweep.Core.Configuration;
using GilSweep.Core.Serialization;
using GilSweep.Core.Sweep;
using GilSweep.Core.Time;
using GilSweep.Tests.Support;

namespace GilSweep.Tests.Core;

public sealed class OpportunityScoringTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 18, 30, 0, TimeSpan.Zero);

    private static readonly GilSweepSettings Endgame = new()
    {
        Levels = new GathererLevels { Miner = 100, Botanist = 100 },
        MsqExpansion = Expansion.DT,
        Folklore = [Expansion.HW, Expansion.StB, Expansion.ShB, Expansion.EW, Expansion.DT],
    };

    [Theory]
    [MemberData(nameof(RankingCharacterizationTests.Cases), MemberType = typeof(RankingCharacterizationTests))]
    public void With_listings_trends_and_node_windows_set_aside_the_ranking_is_v1s_throughput_order(string source, string config)
    {
        var snapshot = Neutralize(source == "seed" ? SweepCharacterizationTests.Seed() : RecordedSweep());
        var settings = ConfigStore.Read(Fixture.Json("V1", "expected-ranking.json")["configs"]![config]!.ToJsonString());

        var board = OpportunityBoard.Build(snapshot, null, settings, Now);

        var v1Order = OpportunityBoard.Candidates(snapshot, settings).Select(row => row.Id);
        Assert.Equal(v1Order, board.Ranked.Select(opportunity => opportunity.ItemId));
        var v1Top = Ranking.TopFarm(snapshot.Rows, settings);
        Assert.Equal(v1Top?.Id, board.Ranked.FirstOrDefault(opportunity => opportunity.Row.Job != Job.Either)?.ItemId);
    }

    [Fact]
    public void Market_points_rise_twelve_per_tenfold_throughput()
    {
        Assert.Equal(0, OpportunityScorer.MarketPoints(9));
        Assert.Equal(12, OpportunityScorer.MarketPoints(100), 6);
        Assert.Equal(60, OpportunityScorer.MarketPoints(1_000_000), 6);
        Assert.Equal(OpportunityScorer.MarketMax, OpportunityScorer.MarketPoints(long.MaxValue));
    }

    [Theory]
    [InlineData(0.4, 20)]
    [InlineData(2, 16)]
    [InlineData(5, 11)]
    [InlineData(10, 6)]
    [InlineData(40, 2)]
    public void Fewer_days_of_stock_score_higher(double days, double points) =>
        Assert.Equal(points, OpportunityScorer.CompetitionPoints(new CompetitionInfo(10, 100, days, false, Now)));

    [Fact]
    public void Unknown_or_stale_listings_count_as_average()
    {
        Assert.Equal(11, OpportunityScorer.CompetitionPoints(CompetitionInfo.Unknown));
        Assert.Equal(11, OpportunityScorer.CompetitionPoints(new CompetitionInfo(2, 10, 0.1, Stale: true, Now.AddDays(-6))));
    }

    [Theory]
    [InlineData(25.0, 10)]
    [InlineData(5.0, 8)]
    [InlineData(0.0, 6)]
    [InlineData(-5.0, 3)]
    [InlineData(-30.0, 0)]
    public void A_rising_price_scores_higher(double percent, double points) =>
        Assert.Equal(points, OpportunityScorer.TrendPoints(new TrendInfo(TrendDirection.Stable, percent, "since Sep 28")));

    [Fact]
    public void Without_history_the_trend_counts_as_stable() =>
        Assert.Equal(OpportunityScorer.TrendPoints(new TrendInfo(TrendDirection.Stable, 0, "")), OpportunityScorer.TrendPoints(TrendInfo.Unknown));

    [Fact]
    public void A_closed_node_counts_for_less_the_longer_it_stays_closed()
    {
        Assert.Equal(1, OpportunityScorer.AvailabilityFactor(NodeAvailability.Always));
        Assert.Equal(1, OpportunityScorer.AvailabilityFactor(new NodeAvailability(NodeState.Open, 30, 10, 120)));
        Assert.Equal(0.9, OpportunityScorer.AvailabilityFactor(Closed(TimeSpan.FromMinutes(7))));
        Assert.Equal(0.75, OpportunityScorer.AvailabilityFactor(Closed(TimeSpan.FromMinutes(25))));
        Assert.Equal(0.55, OpportunityScorer.AvailabilityFactor(Closed(TimeSpan.FromHours(1))));
    }

    [Theory]
    [InlineData(80, OpportunityGrade.Excellent)]
    [InlineData(75, OpportunityGrade.Excellent)]
    [InlineData(70, OpportunityGrade.Good)]
    [InlineData(50, OpportunityGrade.Fair)]
    [InlineData(30, OpportunityGrade.Weak)]
    public void Scores_map_to_grades(int score, OpportunityGrade grade) => Assert.Equal(grade, OpportunityScorer.GradeOf(score));

    [Fact]
    public void The_best_farm_right_now_is_one_you_can_gather_now()
    {
        var snapshot = RecordedSweep();
        for (var minute = 0; minute < 70; minute += 3)
        {
            var at = Now.AddMinutes(minute);
            var board = OpportunityBoard.Build(snapshot, null, Endgame, at);
            Assert.NotNull(board.Best);
            Assert.True(board.Best.Node.IsGatherableNow, $"{board.Best.Name} is closed at {at:t}");
        }
    }

    [Fact]
    public void Every_recommendation_explains_itself()
    {
        var board = OpportunityBoard.Build(RecordedSweep(), null, Endgame, Now);
        foreach (var opportunity in board.Ranked)
        {
            Assert.Equal(5, opportunity.Score.Signals.Count);
            Assert.All(opportunity.Score.Signals, signal => Assert.False(string.IsNullOrWhiteSpace(signal.Detail)));
            Assert.True(opportunity.Reasons.Count >= 4);
            Assert.All(opportunity.Reasons, reason => Assert.False(string.IsNullOrEmpty(reason.Glyph)));
        }
    }

    [Fact]
    public void Trap_items_and_locked_items_are_never_recommended()
    {
        var snapshot = RecordedSweep();
        var fresh = new GilSweepSettings { Levels = new GathererLevels { Miner = 30, Botanist = 30 }, MsqExpansion = Expansion.ARR };
        foreach (var settings in new[] { Endgame, fresh })
        {
            var board = OpportunityBoard.Build(snapshot, null, settings, Now);
            Assert.All(board.Ranked, opportunity =>
            {
                Assert.False(opportunity.Row.Kind.IsTrap());
                Assert.Null(Ranking.Lock(opportunity.Row, settings));
            });
        }
    }

    [Fact]
    public void Available_now_lists_open_timed_nodes_first_and_soon_lists_the_next_to_open()
    {
        var board = OpportunityBoard.Build(RecordedSweep(), null, Endgame, Now);

        Assert.All(board.AvailableNow, opportunity => Assert.True(opportunity.Node.IsGatherableNow));
        Assert.Equal(board.AvailableNow.OrderBy(o => o.Node.State == NodeState.Open ? 0 : 1).Select(o => o.ItemId), board.AvailableNow.Select(o => o.ItemId));
        Assert.All(board.Soon, opportunity => Assert.Equal(NodeState.Closed, opportunity.Node.State));
        Assert.Equal(board.Soon.OrderBy(o => o.Node.EtMinutes).Select(o => o.ItemId), board.Soon.Select(o => o.ItemId));
    }

    [Fact]
    public void A_farm_session_queues_now_then_nodes_opening_within_it_then_a_fallback()
    {
        var board = OpportunityBoard.Build(RecordedSweep(), null, Endgame, Now);

        var session = board.Session(TimeSpan.FromMinutes(30));

        Assert.Equal(SessionTiming.Now, session[0].Timing);
        Assert.True(session[0].Opportunity.Node.IsGatherableNow);
        Assert.All(session.Where(step => step.Timing == SessionTiming.Later), step =>
            Assert.True(step.Opportunity.Node.RealRemaining <= TimeSpan.FromMinutes(30)));
        Assert.Equal(SessionTiming.Fallback, session[^1].Timing);
        Assert.Equal(NodeState.AlwaysAvailable, session[^1].Opportunity.Node.State);
        Assert.Equal(session.Count, session.Select(step => step.ItemId()).Distinct().Count());
    }

    [Fact]
    public void A_longer_session_reaches_further_ahead()
    {
        var board = OpportunityBoard.Build(RecordedSweep(), null, Endgame, Now);
        Assert.True(board.Session(TimeSpan.FromHours(3)).Count >= board.Session(TimeSpan.FromMinutes(15)).Count);
    }

    [Fact]
    public void The_trend_baseline_is_about_a_week_back_and_never_the_last_hourly_sweep()
    {
        var latest = At(Now);
        var history = new[] { At(Now.AddDays(-8)), At(Now.AddDays(-6)), At(Now.AddDays(-3)), At(Now.AddHours(-1)), latest };
        Assert.Equal(Now.AddDays(-6), OpportunityBoard.TrendBaseline(history, latest)!.TakenAt);

        var young = new[] { At(Now.AddDays(-2)), At(Now.AddDays(-1)), At(Now.AddHours(-1)), latest };
        Assert.Equal(Now.AddDays(-2), OpportunityBoard.TrendBaseline(young, latest)!.TakenAt);

        Assert.Null(OpportunityBoard.TrendBaseline([At(Now.AddHours(-2)), latest], latest));
    }

    [Fact]
    public void Without_a_baseline_the_trend_comes_from_saddlebag()
    {
        var row = new SnapshotRow { Avg = 100, SbState = "increasing" };
        Assert.Equal(TrendDirection.Rising, OpportunityBoard.TrendOf(row, null, null).Direction);
        row.SbState = "crashing";
        Assert.Equal(TrendDirection.Falling, OpportunityBoard.TrendOf(row, null, null).Direction);
        row.SbState = null;
        Assert.Equal(TrendDirection.Unknown, OpportunityBoard.TrendOf(row, null, null).Direction);
    }

    [Fact]
    public void A_baseline_price_gives_the_change_and_direction()
    {
        var trend = OpportunityBoard.TrendOf(new SnapshotRow { Avg = 112 }, new SnapshotRow { Avg = 100 }, At(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero)));
        Assert.Equal(TrendDirection.Rising, trend.Direction);
        Assert.Equal(12, trend.Percent);
        Assert.Equal("since Sep 28", trend.Basis);
    }

    internal static MarketSnapshot RecordedSweep() =>
        JsonSerializer.Deserialize(Fixture.Text("V1", "expected-sweep.json"), GilSweepJsonContext.Default.MarketSnapshot)!;

    private static MarketSnapshot At(DateTimeOffset at) => new()
    {
        World = "Cactuar",
        Date = at.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        Timestamp = at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture),
    };

    private static NodeAvailability Closed(TimeSpan until) => new(NodeState.Closed, until.TotalSeconds / EorzeaTime.RealSecondsPerEtMinute, 10, 120);

    /// <summary>Only the market measure left: no listings, no trend state, every node always open.</summary>
    private static MarketSnapshot Neutralize(MarketSnapshot snapshot)
    {
        foreach (var row in snapshot.Rows)
        {
            row.Listings = null;
            row.SbState = null;
            row.Spawns = null;
        }

        return snapshot;
    }
}

internal static class SessionStepExtensions
{
    public static int ItemId(this SessionStep step) => step.Opportunity.ItemId;
}
