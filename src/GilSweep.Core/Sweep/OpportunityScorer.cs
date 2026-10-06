using System.Globalization;
using GilSweep.Core.Catalog;
using GilSweep.Core.Time;

namespace GilSweep.Core.Sweep;

/// <summary>
/// The opportunity score, 0–100. Gil Sweep doesn't know yield, travel or gathering speed, so it
/// ranks by how good the market is for a gatherer and whether the node can be reached now,
/// never by gil per hour. See docs/opportunity-scoring.md.
/// </summary>
public static class OpportunityScorer
{
    public const double PointsPerDecade = 12;
    public const double MarketMax = 70;
    public const double CompetitionMax = 20;
    public const double TrendMax = 10;

    /// <summary>Bar widths on screen: 10,000 sold a day and 10,000 gil fill them.</summary>
    public const double SalesBarMax = 48;
    public const double PriceBarMax = 36;

    public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(3);

    /// <summary>
    /// 12 points per tenfold increase in gil changing hands per day (v1's throughput), from 10 a
    /// day. One curve over throughput keeps v1's order whenever the other signals are equal.
    /// </summary>
    public static double MarketPoints(long throughput) =>
        throughput < 10 ? 0 : Math.Min(MarketMax, PointsPerDecade * Math.Log10(throughput / 10.0));

    /// <summary>The part of the market points that comes from sales volume (the rest is price).</summary>
    public static double SalesPoints(double velDay, double market) =>
        velDay < 1 ? 0 : Math.Clamp(PointsPerDecade * Math.Log10(velDay), 0, market);

    /// <summary>Fewer days of stock on the board means your listings sell sooner. Unknown counts as average.</summary>
    public static double CompetitionPoints(CompetitionInfo competition) =>
        !competition.IsKnown || competition.DaysOfSupply is not { } days ? 11
        : days < 1 ? 20
        : days < 3 ? 16
        : days < 7 ? 11
        : days < 14 ? 6
        : 2;

    /// <summary>A rising price helps, a falling one hurts. Unknown counts as stable.</summary>
    public static double TrendPoints(TrendInfo trend) => trend.Percent switch
    {
        null => trend.Direction switch
        {
            TrendDirection.Rising => 8,
            TrendDirection.Falling => 3,
            _ => 6,
        },
        >= 10 => 10,
        >= 3 => 8,
        > -3 => 6,
        > -10 => 3,
        _ => 0,
    };

    /// <summary>The score's multiplier: a closed node is worth less now, more the sooner it opens.</summary>
    public static double AvailabilityFactor(NodeAvailability node) => node.State switch
    {
        NodeState.Closed when node.RealRemaining <= TimeSpan.FromMinutes(10) => 0.9,
        NodeState.Closed when node.RealRemaining <= TimeSpan.FromMinutes(30) => 0.75,
        NodeState.Closed => 0.55,
        _ => 1,
    };

    public static OpportunityGrade GradeOf(int score) => score switch
    {
        >= 75 => OpportunityGrade.Excellent,
        >= 62 => OpportunityGrade.Good,
        >= 48 => OpportunityGrade.Fair,
        _ => OpportunityGrade.Weak,
    };

    public static OpportunityScore Score(SnapshotRow row, NodeAvailability node, CompetitionInfo competition, TrendInfo trend)
    {
        var market = MarketPoints(row.Throughput);
        var sales = SalesPoints(row.VelDay, market);
        var price = market - sales;
        var competitionPoints = CompetitionPoints(competition);
        var trendPoints = TrendPoints(trend);
        var availability = AvailabilityFactor(node);
        var baseScore = (int)Math.Round(market + competitionPoints + trendPoints, MidpointRounding.AwayFromZero);
        var total = (int)Math.Round((market + competitionPoints + trendPoints) * availability, MidpointRounding.AwayFromZero);
        var signals = new List<ScoreSignal>
        {
            new(SignalKind.Sales, "Sale velocity", sales, SalesBarMax, $"{Formatting.Rate(row.VelDay)} sold per day: {SalesWord(row.VelDay).ToLowerInvariant()}"),
            new(SignalKind.Price, "Price", price, PriceBarMax, $"{Formatting.Gil(row.Avg)} average sale, {Formatting.Compact(row.Throughput)} gil a day changes hands"),
            new(SignalKind.Competition, "Competition", competitionPoints, CompetitionMax, CompetitionDetail(competition)),
            new(SignalKind.Trend, "Trend", trendPoints, TrendMax, TrendDetail(trend)),
            new(SignalKind.Node, "Node availability", availability * 10, 10, NodeDetail(node, availability)),
        };
        return new OpportunityScore(total, baseScore, market, sales, price, competitionPoints, trendPoints, availability, GradeOf(total), signals);
    }

    public static string SalesWord(double velDay) => velDay >= 100 ? "Selling quickly" : velDay >= 30 ? "Sells steadily" : "Sells slowly";

    /// <summary>Reasons for the headline, one per signal, each with a glyph so none relies on color.</summary>
    public static List<Reason> Reasons(SnapshotRow row, NodeAvailability node, CompetitionInfo competition, TrendInfo trend, bool needsFolklore)
    {
        var reasons = new List<Reason>
        {
            row.VelDay >= 100 ? new("↑", "Selling quickly", ReasonTone.Positive)
                : row.VelDay >= 30 ? new("·", "Sells steadily", ReasonTone.Neutral)
                : new("↓", "Sells slowly", ReasonTone.Negative),
            competition switch
            {
                { IsKnown: false } => new("·", "Competition unknown", ReasonTone.Neutral),
                { Level: CompetitionLevel.Few } => new("↑", "Few sellers", ReasonTone.Positive),
                { Level: CompetitionLevel.Moderate } => new("·", "Moderate competition", ReasonTone.Neutral),
                _ => new("↓", "Many sellers", ReasonTone.Negative),
            },
            trend.Direction switch
            {
                TrendDirection.Rising => new("↑", "Price rising", ReasonTone.Positive),
                TrendDirection.Falling => new("↓", "Price falling", ReasonTone.Negative),
                TrendDirection.Stable => new("→", "Price stable", ReasonTone.Neutral),
                _ => new("·", "No price history yet", ReasonTone.Neutral),
            },
            node.State switch
            {
                NodeState.Open => new("●", "Available now", ReasonTone.Positive),
                NodeState.AlwaysAvailable => new("●", "Always available", ReasonTone.Positive),
                _ => new("○", "Opens in " + Formatting.Countdown(node.RealRemaining), ReasonTone.Waiting),
            },
        };
        if (needsFolklore)
        {
            reasons.Add(new("!", $"Needs the {row.Expansion.Name()} folklore book", ReasonTone.Negative));
        }

        return reasons;
    }

    private static string CompetitionDetail(CompetitionInfo competition)
    {
        if (competition.Listings is not { } listings)
        {
            return "No listing data from this sweep; counted as average";
        }

        if (competition.Stale)
        {
            return $"Listings were last updated {competition.UpdatedAt:MMM d}; counted as average";
        }

        if (listings == 0)
        {
            return "Nothing is listed: an empty market";
        }

        var days = competition.DaysOfSupply is { } d ? $", {d.ToString(d >= 10 ? "0" : "0.#", CultureInfo.InvariantCulture)} days of stock" : "";
        return $"{listings} listings{days}: {competition.Word.ToLowerInvariant()}";
    }

    private static string TrendDetail(TrendInfo trend) => trend switch
    {
        { Percent: { } percent } => $"{Formatting.SignedPercent(percent)} {trend.Basis}: {trend.Word.ToLowerInvariant()}",
        { Direction: TrendDirection.Unknown } => "A few days of sweeps will show the trend; counted as stable",
        _ => $"{trend.Basis}: {trend.Word.ToLowerInvariant()}",
    };

    private static string NodeDetail(NodeAvailability node, double factor) => node.State switch
    {
        NodeState.AlwaysAvailable => "A regular node, always there",
        NodeState.Open => $"Open now, {Math.Ceiling(node.EtMinutes).ToString(CultureInfo.InvariantCulture)} Eorzea minutes left ({Formatting.Countdown(node.RealRemaining)})",
        _ => $"Opens in {Formatting.Countdown(node.RealRemaining)}, counted at {factor.ToString("0.##", CultureInfo.InvariantCulture)}×",
    };
}
