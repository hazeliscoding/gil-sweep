using CommunityToolkit.Mvvm.ComponentModel;
using GilSweep.Core;
using GilSweep.Core.Catalog;
using GilSweep.Core.Sweep;
using GilSweep.Core.Time;
using GilSweep.Desktop.Controls;

namespace GilSweep.Desktop.ViewModels;

public sealed record ReasonViewModel(string Glyph, string Text, Tone Tone);

public sealed record SignalViewModel(string Label, string Points, double Fraction, string Detail, Tone Tone);

/// <summary>
/// One opportunity as the screens show it. Updated in place as the clock moves, so countdowns tick
/// without rebuilding lists.
/// </summary>
public sealed partial class OpportunityItemViewModel(int itemId) : ObservableObject
{
    public int ItemId { get; } = itemId;

    [ObservableProperty]
    public partial MarketOpportunity? Opportunity { get; private set; }

    [ObservableProperty]
    public partial int Rank { get; set; }

    [ObservableProperty]
    public partial string Name { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GradeMarket), nameof(GradeOpportunity))]
    public partial string Grade { get; private set; } = "";

    [ObservableProperty]
    public partial Tone GradeTone { get; private set; }

    [ObservableProperty]
    public partial string JobLevel { get; private set; } = "";

    [ObservableProperty]
    public partial string Where { get; private set; } = "";

    [ObservableProperty]
    public partial string Zone { get; private set; } = "";

    [ObservableProperty]
    public partial string Price { get; private set; } = "";

    [ObservableProperty]
    public partial string PriceNote { get; private set; } = "";

    [ObservableProperty]
    public partial string Sales { get; private set; } = "";

    [ObservableProperty]
    public partial string SalesNote { get; private set; } = "";

    /// <summary>"92/day · Miner 100 · Heritage Found": the ranked list's second line.</summary>
    [ObservableProperty]
    public partial string Subline { get; private set; } = "";

    [ObservableProperty]
    public partial string Competition { get; private set; } = "";

    [ObservableProperty]
    public partial string CompetitionShort { get; private set; } = "";

    [ObservableProperty]
    public partial string CompetitionNote { get; private set; } = "";

    [ObservableProperty]
    public partial string Trend { get; private set; } = "";

    [ObservableProperty]
    public partial string TrendNote { get; private set; } = "";

    [ObservableProperty]
    public partial Tone TrendTone { get; private set; }

    [ObservableProperty]
    public partial string Node { get; private set; } = "";

    [ObservableProperty]
    public partial string NodeTiny { get; private set; } = "";

    [ObservableProperty]
    public partial string NodeNote { get; private set; } = "";

    [ObservableProperty]
    public partial Tone NodeTone { get; private set; }

    [ObservableProperty]
    public partial string NodeBrushKey { get; private set; } = "Text2";

    /// <summary>The Available now / Soon line: "18 Eorzea minutes remaining · ET 10:00–12:00".</summary>
    [ObservableProperty]
    public partial string WindowLine { get; private set; } = "";

    [ObservableProperty]
    public partial string Countdown { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScoreText))]
    public partial int Score { get; private set; }

    public string ScoreText => $"{Score} / 100";

    [ObservableProperty]
    public partial IReadOnlyList<ReasonViewModel> Reasons { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<SignalViewModel> Signals { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FavoriteMark))]
    public partial bool IsFavorite { get; set; }

    /// <summary>A star after a favorite's name.</summary>
    public string FavoriteMark => IsFavorite ? "  ★" : "";

    [ObservableProperty]
    public partial bool IsWatched { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public string WatchLabel => IsWatched ? "Watching" : "Watch";

    /// <summary>"excellent market", as Available now words it.</summary>
    public string GradeMarket => Grade.ToLowerInvariant() + " market";

    public string GradeOpportunity => Grade.ToLowerInvariant() + " opportunity";

    partial void OnIsWatchedChanged(bool value) => OnPropertyChanged(nameof(WatchLabel));

    public void Update(MarketOpportunity opportunity)
    {
        var row = opportunity.Row;
        Opportunity = opportunity;
        Name = row.Name;
        Grade = opportunity.Grade.ToString();
        GradeTone = Presentation.GradeTone(opportunity.Grade);
        JobLevel = Presentation.JobLevel(row);
        Where = row.Where;
        Zone = row.Node.Zone;
        Price = Formatting.Gil(row.Avg);
        PriceNote = row.Min > 0 ? $"cheapest listing {Formatting.Gil(row.Min)}" : "nothing listed";
        Sales = Formatting.Rate(row.VelDay) + "/day";
        SalesNote = Formatting.Number(row.VelDay * 7) + " this week";
        Subline = $"{Formatting.Rate(row.VelDay)}/day · {JobLevel} · {Zone}";
        Competition = opportunity.Competition.Word;
        CompetitionShort = opportunity.Competition.Level switch
        {
            CompetitionLevel.Few => "Few",
            CompetitionLevel.Moderate => "Moderate",
            CompetitionLevel.Many => "Many",
            _ => "—",
        };
        CompetitionNote = opportunity.Competition switch
        {
            { Listings: null } => "no listing data",
            { Stale: true } => "listings out of date",
            { Listings: var count } => $"{count} listings",
        };
        Trend = $"{opportunity.Trend.Arrow} {opportunity.Trend.Word}";
        TrendNote = opportunity.Trend.Percent is { } percent ? $"{Formatting.SignedPercent(percent)} {opportunity.Trend.Basis}" : opportunity.Trend.Basis;
        TrendTone = Presentation.TrendTone(opportunity.Trend.Direction);
        UpdateNode(opportunity.Node, row);
        Score = opportunity.Score.Total;
        Reasons = [.. opportunity.Reasons.Select(reason => new ReasonViewModel(reason.Glyph, reason.Text, Presentation.ReasonTone(reason.Tone)))];
        Signals = [.. opportunity.Score.Signals.Select(signal => new SignalViewModel(
            signal.Label,
            signal.Kind == SignalKind.Node ? $"×{opportunity.Score.Availability:0.##}" : $"{signal.Points:0} / {signal.Max:0}",
            signal.Fraction,
            signal.Detail,
            signal.Fraction >= 0.66 ? Tone.Healthy : signal.Fraction >= 0.4 ? Tone.Paused : Tone.Unknown))];
    }

    private void UpdateNode(NodeAvailability node, CatalogItem item)
    {
        Node = Presentation.NodeWord(node);
        NodeTiny = Presentation.NodeTiny(node);
        NodeNote = Presentation.NodeNote(node);
        NodeTone = Presentation.NodeTone(node);
        NodeBrushKey = Presentation.NodeBrushKey(node);
        Countdown = node.State == NodeState.Closed ? Formatting.Countdown(node.RealRemaining) : "";
        WindowLine = node.State switch
        {
            NodeState.Open => $"{Math.Ceiling(node.EtMinutes):0} Eorzea minutes remaining · ET {node.SpawnHour:00}:00–{(node.SpawnHour + node.UptimeEtMinutes / 60) % 24:00}:00",
            NodeState.AlwaysAvailable => $"Always available · {item.Node.Zone}",
            _ => $"{item.Node.Zone} · opens ET {node.SpawnHour:00}:00",
        };
    }
}
