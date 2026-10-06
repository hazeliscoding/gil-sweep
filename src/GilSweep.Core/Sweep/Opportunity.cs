using GilSweep.Core.Time;

namespace GilSweep.Core.Sweep;

public enum OpportunityGrade
{
    Weak,
    Fair,
    Good,
    Excellent,
}

public enum TrendDirection
{
    Unknown,
    Falling,
    Stable,
    Rising,
}

public enum CompetitionLevel
{
    Unknown,
    Few,
    Moderate,
    Many,
}

public enum SignalKind
{
    Sales,
    Price,
    Competition,
    Trend,
    Node,
}

/// <summary>Whether a reason counts for or against an item. Shown with a glyph as well as a color.</summary>
public enum ReasonTone
{
    Positive,
    Neutral,
    Negative,
    Waiting,
}

/// <summary>How crowded the market board is, from the listings fetched during the sweep.</summary>
/// <param name="DaysOfSupply">Units listed ÷ units sold per day: how long the stock on the board lasts.</param>
/// <param name="Stale">The listings were last uploaded to Universalis more than three days before the sweep.</param>
public sealed record CompetitionInfo(int? Listings, long? UnitsForSale, double? DaysOfSupply, bool Stale, DateTimeOffset? UpdatedAt)
{
    public static CompetitionInfo Unknown { get; } = new(null, null, null, false, null);

    public bool IsKnown => Listings is not null && !Stale;

    /// <summary>The mockup's wording: up to 10 listings is few, up to 35 moderate.</summary>
    public CompetitionLevel Level => Listings switch
    {
        null => CompetitionLevel.Unknown,
        <= 10 => CompetitionLevel.Few,
        <= 35 => CompetitionLevel.Moderate,
        _ => CompetitionLevel.Many,
    };

    public string Word => Level switch
    {
        CompetitionLevel.Few => "Few sellers",
        CompetitionLevel.Moderate => "Moderate",
        CompetitionLevel.Many => "Many sellers",
        _ => "Unknown",
    };
}

/// <summary>Which way the price is moving, and against what.</summary>
/// <param name="Percent">Change in average sale price; null when only a Saddlebag state is known.</param>
/// <param name="Basis">What the change is measured against, such as "since Sep 28" or "Saddlebag Exchange".</param>
public sealed record TrendInfo(TrendDirection Direction, double? Percent, string Basis)
{
    public static TrendInfo Unknown { get; } = new(TrendDirection.Unknown, null, "");

    public string Word => Direction switch
    {
        TrendDirection.Rising => "Rising",
        TrendDirection.Falling => "Falling",
        TrendDirection.Stable => "Stable",
        _ => "No history yet",
    };

    /// <summary>A data glyph, so direction never depends on color alone.</summary>
    public string Arrow => Direction switch
    {
        TrendDirection.Rising => "↑",
        TrendDirection.Falling => "↓",
        TrendDirection.Stable => "→",
        _ => "·",
    };
}

/// <summary>One contribution to the score, with the facts behind it.</summary>
/// <param name="Max">The bar's full width; points can't exceed it except for market points on extreme items.</param>
public sealed record ScoreSignal(SignalKind Kind, string Label, double Points, double Max, string Detail)
{
    public double Fraction => Max <= 0 ? 0 : Math.Clamp(Points / Max, 0, 1);
}

/// <param name="Total">0–100: (market + competition + trend) × availability, rounded.</param>
/// <param name="Base">The same before the node multiplier: how good the market is, whenever you get there.</param>
/// <param name="Market">0–70 from gil changing hands per day (sales × price).</param>
/// <param name="Availability">1 when the node can be gathered now; less while a timed node is closed.</param>
public sealed record OpportunityScore(
    int Total,
    int Base,
    double Market,
    double Sales,
    double Price,
    double Competition,
    double Trend,
    double Availability,
    OpportunityGrade Grade,
    IReadOnlyList<ScoreSignal> Signals)
{
    /// <summary>The market's grade before the node multiplier.</summary>
    public OpportunityGrade BaseGrade => OpportunityScorer.GradeOf(Base);
}

/// <summary>A short reason for or against an item, such as "↑ Selling quickly".</summary>
public sealed record Reason(string Glyph, string Text, ReasonTone Tone);

/// <summary>A farmable item with its score and everything needed to explain it.</summary>
public sealed record MarketOpportunity(
    SnapshotRow Row,
    OpportunityScore Score,
    NodeAvailability Node,
    CompetitionInfo Competition,
    TrendInfo Trend,
    bool NeedsFolklore,
    IReadOnlyList<Reason> Reasons)
{
    public int ItemId => Row.Id;

    public string Name => Row.Name;

    public OpportunityGrade Grade => Score.Grade;
}
