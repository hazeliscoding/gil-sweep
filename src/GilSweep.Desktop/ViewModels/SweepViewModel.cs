using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GilSweep.Core;
using GilSweep.Core.Catalog;
using GilSweep.Core.Sweep;
using GilSweep.Core.Time;
using GilSweep.Core.Watchlist;
using GilSweep.Desktop.Controls;
using GilSweep.Desktop.Services;

namespace GilSweep.Desktop.ViewModels;

public sealed record SessionStepViewModel(string When, Tone WhenTone, string Name, string Note, int ItemId);

/// <summary>
/// The home screen: the best farm right now and why, more opportunities, what is open now and
/// soon, and an optional farm session queue.
/// </summary>
public sealed partial class SweepViewModel : PageViewModel
{
    public const int RankedRows = 12;

    private readonly AppSession _session;
    private readonly IWatchlistService _watchlist;
    private readonly INavigator _navigator;

    public SweepViewModel(AppSession session, IWatchlistService watchlist, INavigator navigator)
    {
        _session = session;
        _watchlist = watchlist;
        _navigator = navigator;
        SessionLengths =
        [
            new(TimeSpan.Zero, "Off"),
            new(TimeSpan.FromMinutes(15), "15 min"),
            new(TimeSpan.FromMinutes(30), "30 min"),
            new(TimeSpan.FromHours(1), "1 hour"),
            new(TimeSpan.FromHours(3), "Chill"),
        ];
        SessionLength = SessionLengths[0];
        session.PropertyChanged += OnSessionChanged;
        session.Ticked += (_, _) => UpdateClock();
        watchlist.Changed += (_, _) => Refresh();
        Refresh();
    }

    public override AppPage Page => AppPage.Sweep;

    public AppSession Session => _session;

    public IReadOnlyList<Option<TimeSpan>> SessionLengths { get; }

    [ObservableProperty]
    public partial Option<TimeSpan> SessionLength { get; set; }

    public bool SessionOn => SessionLength.Value > TimeSpan.Zero;

    public string SessionTitle => $"YOUR {SessionLength.Label.ToUpperInvariant()} SWEEP";

    [ObservableProperty]
    public partial IReadOnlyList<SessionStepViewModel> SessionSteps { get; private set; } = [];

    [ObservableProperty]
    public partial OpportunityItemViewModel? Best { get; private set; }

    public ObservableCollection<OpportunityItemViewModel> Ranked { get; } = [];

    public ObservableCollection<OpportunityItemViewModel> AvailableNow { get; } = [];

    public ObservableCollection<OpportunityItemViewModel> Soon { get; } = [];

    [ObservableProperty]
    public partial string WorldLine { get; private set; } = "";

    [ObservableProperty]
    public partial string Freshness { get; private set; } = "";

    [ObservableProperty]
    public partial Tone FreshnessTone { get; private set; } = Tone.Unknown;

    [ObservableProperty]
    public partial string RankedFor { get; private set; } = "";

    [ObservableProperty]
    public partial string LoadingLine { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EorzeaClockLabel))]
    public partial string EorzeaClock { get; private set; } = "";

    public string EorzeaClockLabel => "ET " + EorzeaClock;

    [ObservableProperty]
    public partial bool HasMapPick { get; private set; }

    [ObservableProperty]
    public partial string MapName { get; private set; } = "";

    [ObservableProperty]
    public partial string MapPrice { get; private set; } = "";

    [ObservableProperty]
    public partial string MapNote { get; private set; } = "";

    [ObservableProperty]
    public partial bool HasCraftPick { get; private set; }

    [ObservableProperty]
    public partial string CraftName { get; private set; } = "";

    [ObservableProperty]
    public partial string CraftDifference { get; private set; } = "";

    [ObservableProperty]
    public partial string CraftNote { get; private set; } = "";

    /// <summary>Why this sweep is incomplete, when a provider failed part-way.</summary>
    [ObservableProperty]
    public partial string? Warning { get; private set; }

    /// <summary>Data, but nothing this character can farm (low levels, or nothing sells).</summary>
    [ObservableProperty]
    public partial bool NothingFarmable { get; private set; }

    partial void OnSessionLengthChanged(Option<TimeSpan> value)
    {
        OnPropertyChanged(nameof(SessionOn));
        OnPropertyChanged(nameof(SessionTitle));
        UpdateSession();
    }

    [RelayCommand]
    private Task RunSweepAsync() => _session.SweepAsync();

    [RelayCommand]
    private void Open(int itemId) => _navigator.OpenItem(itemId);

    [RelayCommand]
    private void ToggleWatch(int itemId) => _watchlist.Toggle(itemId);

    [RelayCommand]
    private void ClearSession() => SessionLength = SessionLengths[0];

    [RelayCommand]
    private void OpenCraft() => _navigator.Navigate(AppPage.Craft);

    [RelayCommand]
    private void OpenMap()
    {
        if (_session.Board.MapPick is { } map)
        {
            _navigator.OpenItem(map.Id);
        }
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSession.Board) or nameof(AppSession.Snapshot) or nameof(AppSession.IsSweeping)
            or nameof(AppSession.SweepError) or nameof(AppSession.Progress))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var board = _session.Board;
        var settings = _session.Settings;
        WorldLine = settings.World;
        RankedFor = $"Ranked for Miner {settings.Levels.Miner} · Botanist {settings.Levels.Botanist} · {settings.MsqExpansion.Name()}";
        Warning = _session.Snapshot?.Warnings is { Count: > 0 } warnings ? string.Join(" ", warnings) : null;
        NothingFarmable = _session.HasData && board.Ranked.Count == 0;
        LoadingLine = _session.Progress is { } progress
            ? $"Pricing on {settings.World} · Universalis · {progress.Done} of {progress.Total} requests"
            : $"Pricing on {settings.World} · Universalis";

        Best = Sync(board.Best, Best);
        SyncList(Ranked, board.Ranked.Where(opportunity => opportunity.ItemId != board.Best?.ItemId).Take(RankedRows).ToList(), rankFrom: 2);
        SyncList(AvailableNow, board.AvailableNow);
        SyncList(Soon, board.Soon);

        HasMapPick = board.MapPick is not null;
        if (board.MapPick is { } map)
        {
            MapName = map.Name;
            MapPrice = Formatting.Gil(map.Avg);
            MapNote = $"{Formatting.Rate(map.VelDay)} sold/day · one per 18h per character · level {map.Level} map";
        }

        HasCraftPick = board.WorthProcessing is not null;
        if (board.WorthProcessing is { } craft)
        {
            CraftName = $"{craft.Raw.Name} → {craft.Craft.Name}";
            CraftDifference = "+" + Formatting.Gil(craft.Difference);
            CraftNote = $"per craft · {CrafterName(craft.Craft.Job)} {craft.Craft.Lvl}";
        }

        UpdateClock();
        UpdateSession();
    }

    private void UpdateClock()
    {
        EorzeaClock = EorzeaTime.Clock(_session.Now);
        if (_session.IsSweeping)
        {
            Freshness = "sweeping";
            FreshnessTone = Tone.Accent;
        }
        else if (_session.IsOffline)
        {
            Freshness = _session.SnapshotAge is { } age ? "cached · " + Formatting.Ago(age) : "no data";
            FreshnessTone = Tone.Warning;
        }
        else
        {
            Freshness = _session.SnapshotAge is { } age ? "updated " + Formatting.Ago(age) : "not swept yet";
            FreshnessTone = _session.HasData ? Tone.Healthy : Tone.Unknown;
        }
    }

    private void UpdateSession()
    {
        SessionSteps = SessionOn
            ? [.. _session.Board.Session(SessionLength.Value).Select(step => new SessionStepViewModel(
                step.When.ToUpperInvariant(),
                step.Timing switch { SessionTiming.Now => Tone.Healthy, SessionTiming.Later => Tone.Queued, _ => Tone.Unknown },
                step.Opportunity.Name,
                step.Note,
                step.Opportunity.ItemId))]
            : [];
    }

    private OpportunityItemViewModel? Sync(MarketOpportunity? opportunity, OpportunityItemViewModel? current)
    {
        if (opportunity is null)
        {
            return null;
        }

        var item = current?.ItemId == opportunity.ItemId ? current : new OpportunityItemViewModel(opportunity.ItemId);
        item.Update(opportunity);
        Mark(item);
        return item;
    }

    /// <summary>Updates rows in place when the order is unchanged; otherwise replaces them.</summary>
    private void SyncList(ObservableCollection<OpportunityItemViewModel> target, IReadOnlyList<MarketOpportunity> source, int rankFrom = 1)
    {
        if (target.Count != source.Count || target.Select(item => item.ItemId).Where((id, i) => id != source[i].ItemId).Any())
        {
            target.Clear();
            foreach (var opportunity in source)
            {
                target.Add(new OpportunityItemViewModel(opportunity.ItemId));
            }
        }

        for (var i = 0; i < source.Count; i++)
        {
            target[i].Update(source[i]);
            target[i].Rank = rankFrom + i;
            Mark(target[i]);
        }
    }

    private void Mark(OpportunityItemViewModel item)
    {
        var entry = _watchlist.Get(item.ItemId);
        item.IsWatched = entry is not null;
        item.IsFavorite = entry?.Favorite == true;
    }

    internal static string CrafterName(string job) => job switch
    {
        "CRP" => "Carpenter",
        "BSM" => "Blacksmith",
        "ARM" => "Armorer",
        "GSM" => "Goldsmith",
        "LTW" => "Leatherworker",
        "WVR" => "Weaver",
        "ALC" => "Alchemist",
        "CUL" => "Culinarian",
        _ => "Any crafter",
    };
}
