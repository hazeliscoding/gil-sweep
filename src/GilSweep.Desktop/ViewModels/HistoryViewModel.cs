using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GilSweep.Core;
using GilSweep.Core.History;
using GilSweep.Core.Watchlist;
using GilSweep.Desktop.Controls;
using GilSweep.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace GilSweep.Desktop.ViewModels;

public sealed record SweepLogRowViewModel(string When, string Best, string BestPrice, string Items, string Movers, int? BestId);

public sealed record MoverRowViewModel(int ItemId, string Name, IReadOnlyList<double> History, Tone Tone, string Then, string Now, string Change, string ChangeBrushKey);

public sealed record WatchChangeViewModel(string Name, string Change, string ChangeBrushKey);

/// <summary>Your local market memory: past sweeps, what moved this week, and a summary. Nothing leaves the PC.</summary>
public sealed partial class HistoryViewModel : PageViewModel
{
    private readonly AppSession _session;
    private readonly IMarketSnapshotStore _store;
    private readonly IHistoryService _history;
    private readonly IWatchlistService _watchlist;
    private readonly IFilePicker _picker;
    private readonly INavigator _navigator;
    private readonly ILogger<HistoryViewModel> _logger;

    public HistoryViewModel(
        AppSession session,
        IMarketSnapshotStore store,
        IHistoryService history,
        IWatchlistService watchlist,
        IFilePicker picker,
        INavigator navigator,
        ILogger<HistoryViewModel> logger)
    {
        _session = session;
        _store = store;
        _history = history;
        _watchlist = watchlist;
        _picker = picker;
        _navigator = navigator;
        _logger = logger;
        session.PropertyChanged += OnSessionChanged;
    }

    public override AppPage Page => AppPage.History;

    [ObservableProperty]
    public partial string Subtitle { get; private set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<SweepLogRowViewModel> Sweeps { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<MoverRowViewModel> Movers { get; private set; } = [];

    [ObservableProperty]
    public partial string MoversTitle { get; private set; } = "Movers this week";

    [ObservableProperty]
    public partial string WeekTitle { get; private set; } = "";

    [ObservableProperty]
    public partial string WeekText { get; private set; } = "";

    [ObservableProperty]
    public partial string SweepsRecorded { get; private set; } = "0";

    [ObservableProperty]
    public partial string ItemsTracked { get; private set; } = "0";

    [ObservableProperty]
    public partial string SnapshotsStored { get; private set; } = "0";

    [ObservableProperty]
    public partial string OldestSnapshot { get; private set; } = "—";

    [ObservableProperty]
    public partial IReadOnlyList<WatchChangeViewModel> WatchedChanges { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<string> Slowing { get; private set; } = [];

    [ObservableProperty]
    public partial bool HasHistory { get; private set; }

    [ObservableProperty]
    public partial string? ExportResult { get; private set; }

    public override Task ActivateAsync()
    {
        Refresh();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void Open(int itemId) => _navigator.OpenItem(itemId);

    [RelayCommand]
    private async Task ExportAsync()
    {
        var world = _session.Settings.World;
        var path = await _picker.SaveCsvAsync($"gil-sweep-{world.ToLowerInvariant()}-{_session.Now:yyyy-MM-dd}.csv");
        if (path is null)
        {
            return;
        }

        try
        {
            using (var writer = new StreamWriter(path))
            {
                HistoryReport.WriteCsv(_store.Load(world), writer);
            }

            ExportResult = $"Saved {Path.GetFileName(path)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not export history");
            ExportResult = "Could not save the file: " + ex.Message;
        }
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSession.Snapshot))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var settings = _session.Settings;
        var world = settings.World;
        var snapshots = _store.Load(world);
        var stats = _store.Stats();
        var digest = _history.Digest(world);
        var now = _session.Now;
        HasHistory = snapshots.Count > 0;
        Subtitle = $"Your local record of sweeps on {world}. Nothing leaves this computer.";
        WeekTitle = $"THIS WEEK ON {world.ToUpperInvariant()}";

        Sweeps = [.. HistoryReport.Log(snapshots, settings).Select(entry => new SweepLogRowViewModel(
            When(entry.At, now),
            entry.BestName ?? "Nothing farmable",
            entry.BestName is null ? "" : Formatting.Gil(entry.BestPrice),
            entry.Items.ToString(CultureInfo.InvariantCulture),
            entry.Movers.ToString(CultureInfo.InvariantCulture),
            entry.BestId))];

        var series = _history.Series(world);
        MoversTitle = digest.BaselineDate is { } baseline
            ? $"Movers since {DateTimeOffset.Parse(baseline, CultureInfo.InvariantCulture):MMM d}"
            : "Movers this week";
        Movers = [.. digest.Changes.Take(6).Select(change => new MoverRowViewModel(
            change.Id,
            change.Name,
            [.. (series.GetValueOrDefault(change.Id) ?? []).TakeLast(14).Select(point => (double)point.Avg)],
            change.AvgNow >= change.AvgThen ? Tone.Healthy : Tone.Critical,
            Formatting.Number(change.AvgThen),
            Formatting.Number(change.AvgNow),
            change.AvgPct is { } pct ? Formatting.SignedPercent(pct) : "—",
            Presentation.ChangeBrushKey(change.AvgPct)))];

        var summary = HistoryReport.Summarize(snapshots, digest, settings, stats, now);
        WeekText = summary.Text;
        SweepsRecorded = summary.SweepsThisWeek.ToString(CultureInfo.InvariantCulture);
        ItemsTracked = summary.ItemsTracked.ToString(CultureInfo.InvariantCulture);
        SnapshotsStored = $"{stats.Count} · {stats.Bytes / 1048576.0:0.0} MB";
        OldestSnapshot = stats.Oldest is { } oldest ? oldest.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "—";
        Slowing = [.. digest.Prune.Select(item => $"{item.Name}: {string.Join(", ", item.RecentVel.Select(v => v.ToString("0.#", CultureInfo.InvariantCulture)))} a day")];

        var week = snapshots.LastOrDefault(snapshot => now - snapshot.TakenAt >= TimeSpan.FromDays(6)) ?? snapshots.FirstOrDefault();
        var latest = snapshots.LastOrDefault();
        WatchedChanges = [.. _watchlist.Entries.Select(entry =>
        {
            var before = week?.Find(entry.ItemId);
            var after = latest?.Find(entry.ItemId);
            double? pct = before is { Avg: > 0 } && after is not null && week != latest ? (double)(after.Avg - before.Avg) / before.Avg * 100 : null;
            return new WatchChangeViewModel(after?.Name ?? $"Item {entry.ItemId}", pct is { } p ? Formatting.SignedPercent(Math.Round(p, 1)) : "—", Presentation.ChangeBrushKey(pct));
        })];
    }

    private static string When(DateTimeOffset at, DateTimeOffset now)
    {
        var local = at.ToLocalTime();
        var today = now.ToLocalTime().Date;
        return local.Date == today ? $"Today {local:HH:mm}"
            : local.Date == today.AddDays(-1) ? $"Yesterday {local:HH:mm}"
            : local.ToString("MMM d · HH:mm", CultureInfo.InvariantCulture);
    }
}
