using System.Globalization;
using GilSweep.Core.Catalog;
using GilSweep.Core.Configuration;
using GilSweep.Core.Crafting;
using GilSweep.Core.Time;

namespace GilSweep.Core.Sweep;

public enum SessionTiming
{
    Now,
    Later,
    Fallback,
}

/// <summary>One stop in a farm session. Travel time and yield are never estimated.</summary>
public sealed record SessionStep(SessionTiming Timing, TimeSpan StartsIn, MarketOpportunity Opportunity, string Note)
{
    public string When => Timing switch
    {
        SessionTiming.Now => "Now",
        SessionTiming.Later => "In " + Math.Max(1, (int)Math.Round(StartsIn.TotalMinutes)).ToString(CultureInfo.InvariantCulture) + " min",
        _ => "After",
    };
}

/// <summary>
/// The answer to "what should I farm right now?" at one moment: the best farm you can gather now,
/// every candidate ranked, what is open now and what opens soon. Rebuilt as the Eorzea clock moves.
/// </summary>
public sealed record OpportunityBoard(
    MarketOpportunity? Best,
    IReadOnlyList<MarketOpportunity> Ranked,
    IReadOnlyList<MarketOpportunity> AvailableNow,
    IReadOnlyList<MarketOpportunity> Soon,
    SnapshotRow? MapPick,
    CraftingOpportunity? WorthProcessing,
    DateTimeOffset At)
{
    public static OpportunityBoard Empty(DateTimeOffset at) => new(null, [], [], [], null, null, at);

    public MarketOpportunity? Find(int itemId) => Ranked.FirstOrDefault(opportunity => opportunity.ItemId == itemId);

    /// <summary>1-based place in the ranking, or null for items that aren't candidates.</summary>
    public int? RankOf(int itemId)
    {
        for (var i = 0; i < Ranked.Count; i++)
        {
            if (Ranked[i].ItemId == itemId)
            {
                return i + 1;
            }
        }

        return null;
    }

    /// <summary>
    /// v1's farm candidates: what this character can gather (story, level, never trap kinds) that
    /// sells on their own world, minus maps and crystals, which have their own picks. Unlike v1,
    /// items either gatherer can get (aetherial reduction) count too; v1 only showed per-job tables.
    /// </summary>
    public static IEnumerable<SnapshotRow> Candidates(MarketSnapshot snapshot, GilSweepSettings settings) =>
        Ranking.Farmable(snapshot.Rows, settings).Where(row => row.Kind is not (ItemKind.Map or ItemKind.Crystal));

    public static OpportunityBoard Build(MarketSnapshot snapshot, MarketSnapshot? baseline, GilSweepSettings settings, DateTimeOffset now)
    {
        var baselineById = baseline?.Rows.GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.Last());
        var ranked = Candidates(snapshot, settings)
            .Select(row => Evaluate(row, baselineById?.GetValueOrDefault(row.Id), baseline, snapshot, settings, now))
            .OrderByDescending(opportunity => opportunity.Score.Total)
            .ThenByDescending(opportunity => opportunity.Row.Throughput)
            .ToList();

        var best = ranked.FirstOrDefault(opportunity => opportunity.Node.IsGatherableNow) ?? ranked.FirstOrDefault();
        var openNow = ranked.Where(opportunity => opportunity.Node.State == NodeState.Open);
        var always = ranked.Where(opportunity => opportunity.Node.State == NodeState.AlwaysAvailable).Take(2);
        return new OpportunityBoard(
            best,
            ranked,
            [.. openNow.Concat(always).Take(4)],
            [.. ranked.Where(opportunity => opportunity.Node.State == NodeState.Closed).OrderBy(opportunity => opportunity.Node.EtMinutes).Take(3)],
            Ranking.BestMap(snapshot.Rows, settings),
            CraftingAdvisor.Best(snapshot, settings),
            now);
    }

    public static MarketOpportunity Evaluate(SnapshotRow row, SnapshotRow? before, MarketSnapshot? baseline, MarketSnapshot snapshot, GilSweepSettings settings, DateTimeOffset now)
    {
        var node = EorzeaTime.Availability(row, now);
        var competition = CompetitionOf(row, snapshot.TakenAt);
        var trend = TrendOf(row, before, baseline);
        var folklore = Ranking.NeedsFolklore(row, settings);
        return new MarketOpportunity(
            row,
            OpportunityScorer.Score(row, node, competition, trend),
            node,
            competition,
            trend,
            folklore,
            OpportunityScorer.Reasons(row, node, competition, trend, folklore));
    }

    public static CompetitionInfo CompetitionOf(SnapshotRow row, DateTimeOffset sweptAt)
    {
        if (row.Listings is not { } listings)
        {
            return CompetitionInfo.Unknown;
        }

        var stale = listings.UpdatedAt is { } updated && sweptAt - updated > OpportunityScorer.StaleAfter;
        double? days = row.VelDay > 0 ? listings.UnitsForSale / row.VelDay : null;
        return new CompetitionInfo(listings.ListingsCount, listings.UnitsForSale, days, stale, listings.UpdatedAt);
    }

    /// <summary>
    /// Change in average sale price against the trend baseline (about a week back, see
    /// <see cref="TrendBaseline"/>); without one, Saddlebag Exchange's trend state; otherwise unknown.
    /// </summary>
    public static TrendInfo TrendOf(SnapshotRow row, SnapshotRow? before, MarketSnapshot? baseline)
    {
        if (before is { Avg: > 0 } && row.Avg > 0 && baseline is not null)
        {
            var percent = (double)(row.Avg - before.Avg) / before.Avg * 100;
            var direction = percent >= 3 ? TrendDirection.Rising : percent <= -3 ? TrendDirection.Falling : TrendDirection.Stable;
            return new TrendInfo(direction, Math.Round(percent, 1), "since " + baseline.TakenAt.ToString("MMM d", CultureInfo.InvariantCulture));
        }

        return row.SbState?.ToLowerInvariant() switch
        {
            "spiking" or "increasing" => new TrendInfo(TrendDirection.Rising, null, "Saddlebag Exchange"),
            "decreasing" or "crashing" => new TrendInfo(TrendDirection.Falling, null, "Saddlebag Exchange"),
            "stable" => new TrendInfo(TrendDirection.Stable, null, "Saddlebag Exchange"),
            _ => TrendInfo.Unknown,
        };
    }

    /// <summary>
    /// The snapshot trends are measured against: the newest at least 5 days older than the latest
    /// (the digest's rule), or failing that the oldest at least 20 hours older. Hourly sweeps
    /// differ too little from each other to show a trend.
    /// </summary>
    public static MarketSnapshot? TrendBaseline(IReadOnlyList<MarketSnapshot> history, MarketSnapshot latest)
    {
        var older = history.Where(snapshot => snapshot.World == latest.World && snapshot.TakenAt < latest.TakenAt).ToList();
        return older.LastOrDefault(snapshot => latest.TakenAt - snapshot.TakenAt >= TimeSpan.FromDays(5))
            ?? older.FirstOrDefault(snapshot => latest.TakenAt - snapshot.TakenAt >= TimeSpan.FromHours(20));
    }

    /// <summary>
    /// A farm session: what to gather now, timed nodes opening within the session, and an
    /// always-available fallback. An opportunity queue, not a route.
    /// </summary>
    public IReadOnlyList<SessionStep> Session(TimeSpan length)
    {
        var steps = new List<SessionStep>();
        var open = Ranked.FirstOrDefault(opportunity => opportunity.Node.State == NodeState.Open && opportunity.Grade >= OpportunityGrade.Fair);
        var always = Ranked.Where(opportunity => opportunity.Node.State == NodeState.AlwaysAvailable).Take(2).ToList();
        if (open is not null)
        {
            steps.Add(new SessionStep(SessionTiming.Now, TimeSpan.Zero, open,
                $"{open.Grade} market, {Math.Ceiling(open.Node.EtMinutes).ToString(CultureInfo.InvariantCulture)} Eorzea minutes left"));
        }
        else if (always.Count > 0)
        {
            steps.Add(new SessionStep(SessionTiming.Now, TimeSpan.Zero, always[0], "Strong, always-available market"));
        }

        // A node that opens later is judged by its market (the grade before the node multiplier):
        // the multiplier keeps a closed node out of "right now", not out of a session that reaches it.
        // The best markets opening within the session make the queue, listed in the order they open;
        // the soonest few would always be the next half hour's, however long the session.
        var stops = length >= TimeSpan.FromHours(3) ? 5 : length >= TimeSpan.FromHours(1) ? 3 : 2;
        var later = Ranked
            .Where(opportunity => opportunity.Node.State == NodeState.Closed && opportunity.Node.RealRemaining <= length && opportunity.Score.BaseGrade >= OpportunityGrade.Fair)
            .OrderByDescending(opportunity => opportunity.Score.Base)
            .ThenBy(opportunity => opportunity.Node.EtMinutes)
            .Take(stops)
            .OrderBy(opportunity => opportunity.Node.EtMinutes);
        steps.AddRange(later.Select(opportunity => new SessionStep(SessionTiming.Later, opportunity.Node.RealRemaining, opportunity,
            $"Timed node opens, {opportunity.Score.BaseGrade.ToString().ToLowerInvariant()} market")));

        if (always.FirstOrDefault(candidate => steps.All(step => step.Opportunity.ItemId != candidate.ItemId)) is { } fallback)
        {
            steps.Add(new SessionStep(SessionTiming.Fallback, TimeSpan.Zero, fallback, "Reliable fallback while you wait"));
        }

        return steps;
    }
}
