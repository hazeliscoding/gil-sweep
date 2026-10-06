using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GilSweep.Core;
using GilSweep.Core.Alerts;
using GilSweep.Core.Catalog;
using GilSweep.Core.Configuration;
using GilSweep.Core.Watchlist;
using GilSweep.Desktop.Services;

namespace GilSweep.Desktop.ViewModels;

/// <summary>One watched item: its figures and its notification switches.</summary>
public sealed partial class WatchRowViewModel : ObservableObject
{
    private readonly IWatchlistService _watchlist;
    private readonly ProblemReporter _problems;
    private bool _syncing;

    public WatchRowViewModel(int itemId, string name, bool timed, IWatchlistService watchlist, ProblemReporter problems)
    {
        ItemId = itemId;
        Name = name;
        Timed = timed;
        _watchlist = watchlist;
        _problems = problems;
    }

    public int ItemId { get; }

    public string Name { get; }

    /// <summary>Only timed nodes have a window to be reminded about.</summary>
    public bool Timed { get; }

    [ObservableProperty]
    public partial string Price { get; set; } = "—";

    [ObservableProperty]
    public partial string Change { get; set; } = "";

    [ObservableProperty]
    public partial string ChangeBrushKey { get; set; } = "Text3";

    [ObservableProperty]
    public partial string LastEvent { get; set; } = "";

    [ObservableProperty]
    public partial bool NodeOpens { get; set; }

    [ObservableProperty]
    public partial bool PriceSpike { get; set; }

    [ObservableProperty]
    public partial bool PriceCrash { get; set; }

    [ObservableProperty]
    public partial bool Undercut { get; set; }

    [ObservableProperty]
    public partial bool Favorite { get; set; }

    public void Sync(WatchEntry entry)
    {
        _syncing = true;
        NodeOpens = entry.NodeOpens;
        PriceSpike = entry.PriceSpike;
        PriceCrash = entry.PriceCrash;
        Undercut = entry.Undercut;
        Favorite = entry.Favorite;
        _syncing = false;
    }

    partial void OnNodeOpensChanged(bool value) => Save(entry => entry.NodeOpens = value);

    partial void OnPriceSpikeChanged(bool value) => Save(entry => entry.PriceSpike = value);

    partial void OnPriceCrashChanged(bool value) => Save(entry => entry.PriceCrash = value);

    partial void OnUndercutChanged(bool value) => Save(entry => entry.Undercut = value);

    partial void OnFavoriteChanged(bool value) => Save(entry => entry.Favorite = value);

    private void Save(Action<WatchEntry> change)
    {
        if (!_syncing)
        {
            _problems.Run(() => _watchlist.Set(ItemId, change));
        }
    }
}

public sealed record AlertLineViewModel(string When, string Text);

/// <summary>Everything Gil Sweep keeps an eye on for you, and what it should tell you about.</summary>
public sealed partial class WatchlistViewModel : PageViewModel
{
    private readonly IWatchlistService _watchlist;
    private readonly ISettingsService _settings;
    private readonly IAlertService _alerts;
    private readonly IItemCatalog _catalog;
    private readonly AppSession _session;
    private readonly INavigator _navigator;
    private readonly IUiThread _ui;
    private readonly ProblemReporter _problems;

    public WatchlistViewModel(
        IWatchlistService watchlist,
        ISettingsService settings,
        IAlertService alerts,
        IItemCatalog catalog,
        AppSession session,
        INavigator navigator,
        IUiThread ui,
        ProblemReporter problems)
    {
        _problems = problems;
        _watchlist = watchlist;
        _settings = settings;
        _alerts = alerts;
        _catalog = catalog;
        _session = session;
        _navigator = navigator;
        _ui = ui;
        SpikeOptions = [.. new[] { 10, 15, 20, 25, 30, 40, 50 }.Select(p => new Option<int>(p, $"{p}%"))];
        CrashOptions = SpikeOptions;
        LeadOptions = [.. new[] { 0, 1, 2, 3, 5, 10 }.Select(m => new Option<int>(m, m == 0 ? "When it opens" : $"{m} min before"))];
        watchlist.Changed += (_, _) => Sync();
        alerts.Raised += (_, _) => _ui.Post(UpdateAlerts);
        session.PropertyChanged += OnSessionChanged;
        Sync();
        UpdateAlerts();
    }

    public override AppPage Page => AppPage.Watchlist;

    public ObservableCollection<WatchRowViewModel> Rows { get; } = [];

    public IReadOnlyList<ItemChoice> Choices => [.. _catalog.Items.Select(item => new ItemChoice(item.Id, item.Name)).OrderBy(choice => choice.Name, StringComparer.OrdinalIgnoreCase)];

    [ObservableProperty]
    public partial ItemChoice? ToWatch { get; set; }

    public IReadOnlyList<Option<int>> SpikeOptions { get; }

    public IReadOnlyList<Option<int>> CrashOptions { get; }

    public IReadOnlyList<Option<int>> LeadOptions { get; }

    [ObservableProperty]
    public partial Option<int>? Spike { get; set; }

    [ObservableProperty]
    public partial Option<int>? Crash { get; set; }

    [ObservableProperty]
    public partial Option<int>? Lead { get; set; }

    [ObservableProperty]
    public partial bool HasRows { get; private set; }

    [ObservableProperty]
    public partial bool NeedsRetainers { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<AlertLineViewModel> RecentAlerts { get; private set; } = [];

    partial void OnToWatchChanged(ItemChoice? value)
    {
        if (value is not null)
        {
            _problems.Run(() => _watchlist.Watch(value.Id));
            ToWatch = null;
        }
    }

    partial void OnSpikeChanged(Option<int>? value) => SaveThreshold(value, (alerts, percent) => alerts.SpikePercent = percent, _settings.Current.Alerts.SpikePercent);

    partial void OnCrashChanged(Option<int>? value) => SaveThreshold(value, (alerts, percent) => alerts.CrashPercent = percent, _settings.Current.Alerts.CrashPercent);

    partial void OnLeadChanged(Option<int>? value) => SaveThreshold(value, (alerts, minutes) => alerts.NodeLeadMinutes = minutes, _settings.Current.Alerts.NodeLeadMinutes);

    [RelayCommand]
    private void Remove(int itemId) => _problems.Run(() => _watchlist.Unwatch(itemId));

    [RelayCommand]
    private void Open(int itemId) => _navigator.OpenItem(itemId);

    [RelayCommand]
    private void OpenSettings() => _navigator.Navigate(AppPage.Settings);

    public override Task ActivateAsync()
    {
        Sync();
        UpdateAlerts();
        return Task.CompletedTask;
    }

    private void SaveThreshold(Option<int>? value, Action<AlertThresholds, int> apply, int current)
    {
        if (value is not null && value.Value != current)
        {
            _problems.Run(() => _settings.Update(settings => apply(settings.Alerts, value.Value)));
        }
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSession.Snapshot))
        {
            Sync();
        }
    }

    private void Sync()
    {
        var entries = _watchlist.Entries;
        if (!Rows.Select(row => row.ItemId).SequenceEqual(entries.Select(entry => entry.ItemId)))
        {
            Rows.Clear();
            foreach (var entry in entries)
            {
                var item = _catalog.Find(entry.ItemId);
                Rows.Add(new WatchRowViewModel(entry.ItemId, item?.Name ?? $"Item {entry.ItemId}", item?.IsTimed ?? false, _watchlist, _problems));
            }
        }

        var snapshot = _session.Snapshot;
        var recent = _alerts.Recent;
        for (var i = 0; i < Rows.Count; i++)
        {
            var row = Rows[i];
            row.Sync(entries[i]);
            if (snapshot?.Find(row.ItemId) is { } market)
            {
                row.Price = Formatting.Gil(market.Avg);
                row.Change = market.AvgChangePct is { } change ? $"{Presentation.ChangeArrow(change)} {Formatting.SignedPercent(change)}" : "";
                row.ChangeBrushKey = Presentation.ChangeBrushKey(market.AvgChangePct);
            }

            var last = recent.FirstOrDefault(alert => alert.ItemId == row.ItemId);
            row.LastEvent = last is null ? "no alerts yet" : $"{Describe(last.Kind)} {AlertService.When(last.At, _session.Now, TimeZoneInfo.Local)}";
        }

        HasRows = Rows.Count > 0;
        var thresholds = _settings.Current.Alerts;
        Spike = SpikeOptions.FirstOrDefault(option => option.Value == thresholds.SpikePercent) ?? new Option<int>(thresholds.SpikePercent, $"{thresholds.SpikePercent}%");
        Crash = CrashOptions.FirstOrDefault(option => option.Value == thresholds.CrashPercent) ?? new Option<int>(thresholds.CrashPercent, $"{thresholds.CrashPercent}%");
        Lead = LeadOptions.FirstOrDefault(option => option.Value == thresholds.NodeLeadMinutes) ?? new Option<int>(thresholds.NodeLeadMinutes, $"{thresholds.NodeLeadMinutes} min before");
        NeedsRetainers = Rows.Any(row => row.Undercut) && _settings.Current.RetainerNames.Count == 0;
    }

    private void UpdateAlerts()
    {
        var now = _session.Now;
        RecentAlerts = [.. _alerts.Recent.Take(8).Select(alert => new AlertLineViewModel(AlertService.When(alert.At, now, TimeZoneInfo.Local), alert.Title))];
        Sync();
    }

    private static string Describe(AlertKind kind) => kind switch
    {
        AlertKind.NodeOpening or AlertKind.NodeOpen => "node reminder",
        AlertKind.PriceSpike => "spike alert",
        AlertKind.PriceCrash => "crash alert",
        _ => "undercut",
    };
}
