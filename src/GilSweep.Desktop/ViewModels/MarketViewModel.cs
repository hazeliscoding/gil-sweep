using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GilSweep.Core;
using GilSweep.Core.Catalog;
using GilSweep.Core.History;
using GilSweep.Core.Market;
using GilSweep.Core.Market.Universalis;
using GilSweep.Core.Sweep;
using GilSweep.Core.Watchlist;
using GilSweep.Desktop.Controls;
using GilSweep.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace GilSweep.Desktop.ViewModels;

public sealed record TableRow(string Price, string Quantity, string Third);

/// <summary>An item the search box can find.</summary>
public sealed record ItemChoice(int Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// One item in depth: live listings and sales, two weeks of prices, the hours it sells, why it
/// ranks where it does, where to gather it, who buys it and how to list it.
/// </summary>
public sealed partial class MarketViewModel : PageViewModel
{
    private readonly AppSession _session;
    private readonly IItemCatalog _catalog;
    private readonly IUniversalisClient _universalis;
    private readonly IHistoryService _history;
    private readonly IWatchlistService _watchlist;
    private readonly IShellService _shell;
    private readonly INavigator _navigator;
    private readonly IUiThread _ui;
    private readonly ILogger<MarketViewModel> _logger;
    private CancellationTokenSource? _loading;
    private int? _loadedId;

    public MarketViewModel(
        AppSession session,
        IItemCatalog catalog,
        IUniversalisClient universalis,
        IHistoryService history,
        IWatchlistService watchlist,
        IShellService shell,
        INavigator navigator,
        IUiThread ui,
        ILogger<MarketViewModel> logger)
    {
        _session = session;
        _catalog = catalog;
        _universalis = universalis;
        _history = history;
        _watchlist = watchlist;
        _shell = shell;
        _navigator = navigator;
        _ui = ui;
        _logger = logger;
        Item = new OpportunityItemViewModel(0);
        session.PropertyChanged += OnSessionChanged;
        watchlist.Changed += (_, _) => UpdateWatch();
        catalog.Changed += (_, _) => OnPropertyChanged(nameof(Choices));
    }

    public override AppPage Page => AppPage.Market;

    public IReadOnlyList<ItemChoice> Choices => [.. _catalog.Items.Select(item => new ItemChoice(item.Id, item.Name)).OrderBy(choice => choice.Name, StringComparer.OrdinalIgnoreCase)];

    [ObservableProperty]
    public partial int? SelectedId { get; private set; }

    [ObservableProperty]
    public partial ItemChoice? Search { get; set; }

    public bool HasItem => SelectedId is not null && Row is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItem))]
    public partial SnapshotRow? Row { get; private set; }

    /// <summary>The item's opportunity view, also for items that aren't ranked (scored the same way).</summary>
    public OpportunityItemViewModel Item { get; }

    [ObservableProperty]
    public partial bool IsRanked { get; private set; }

    [ObservableProperty]
    public partial string RankTitle { get; private set; } = "";

    [ObservableProperty]
    public partial string? LockText { get; private set; }

    [ObservableProperty]
    public partial string Subtitle { get; private set; } = "";

    [ObservableProperty]
    public partial string WatchLabel { get; private set; } = "Watch";

    [ObservableProperty]
    public partial string CheapestNow { get; private set; } = "—";

    [ObservableProperty]
    public partial string CheapestNote { get; private set; } = "";

    [ObservableProperty]
    public partial string SoldPerDay { get; private set; } = "—";

    [ObservableProperty]
    public partial string SoldNote { get; private set; } = "";

    [ObservableProperty]
    public partial string DaysOfSupply { get; private set; } = "—";

    [ObservableProperty]
    public partial string SupplyNote { get; private set; } = "";

    [ObservableProperty]
    public partial string TrendValue { get; private set; } = "—";

    [ObservableProperty]
    public partial string TrendNote { get; private set; } = "";

    [ObservableProperty]
    public partial string TrendBrushKey { get; private set; } = "Text1";

    [ObservableProperty]
    public partial IReadOnlyList<ChartPoint> PricePoints { get; private set; } = [];

    [ObservableProperty]
    public partial string ChartSubtitle { get; private set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<TableRow> Listings { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<TableRow> Sales { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<double> Hours { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<int> PeakHours { get; private set; } = [];

    [ObservableProperty]
    public partial string PeakLine { get; private set; } = "";

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial string? LoadError { get; private set; }

    [ObservableProperty]
    public partial string Advice { get; private set; } = "";

    [ObservableProperty]
    public partial string AdviceNote { get; private set; } = "";

    [ObservableProperty]
    public partial string Demand { get; private set; } = "";

    [ObservableProperty]
    public partial string WindowText { get; private set; } = "";

    [ObservableProperty]
    public partial string Requirement { get; private set; } = "";

    [ObservableProperty]
    public partial string SourceLine { get; private set; } = "";

    public void Select(int itemId)
    {
        SelectedId = itemId;
        Search = null;
        UpdateFromSnapshot();
        _ = LoadLiveAsync(itemId);
    }

    public override Task ActivateAsync()
    {
        if (SelectedId is null && _session.Board.Best is { } best)
        {
            Select(best.ItemId);
        }

        return Task.CompletedTask;
    }

    partial void OnSearchChanged(ItemChoice? value)
    {
        if (value is not null && value.Id != SelectedId)
        {
            Select(value.Id);
        }
    }

    [RelayCommand]
    private void ToggleWatch()
    {
        if (SelectedId is { } id)
        {
            _watchlist.Toggle(id);
        }
    }

    [RelayCommand]
    private void OpenUniversalis()
    {
        if (SelectedId is { } id)
        {
            _shell.OpenUrl($"https://universalis.app/market/{id}");
        }
    }

    [RelayCommand]
    private void Back() => _navigator.Navigate(AppPage.Sweep);

    [RelayCommand]
    private Task ReloadAsync() => SelectedId is { } id ? LoadLiveAsync(id) : Task.CompletedTask;

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSession.Board))
        {
            UpdateFromSnapshot();
        }
        else if (e.PropertyName == nameof(AppSession.Snapshot) && SelectedId is { } id && _loadedId != id)
        {
            _ = LoadLiveAsync(id);
        }
    }

    /// <summary>Everything that comes from the sweep: figures, score, node, demand. Cheap; runs every board refresh.</summary>
    private void UpdateFromSnapshot()
    {
        if (SelectedId is not { } id)
        {
            return;
        }

        var snapshot = _session.Snapshot;
        var item = _catalog.Find(id);
        var row = snapshot?.Find(id) ?? (item is null ? null : new SnapshotRow { Id = id, Name = item.Name, Job = item.Job, Level = item.Level, Where = item.Where, Kind = item.Kind, Expansion = item.Expansion, Spawns = item.Spawns, Uptime = item.Uptime });
        Row = row;
        if (row is null)
        {
            return;
        }

        var board = _session.Board;
        var opportunity = board.Find(id) ?? (snapshot is null ? null : OpportunityBoard.Evaluate(row, _session.Baseline?.Find(id), _session.Baseline, snapshot, _session.Settings, _session.Now));
        if (opportunity is not null)
        {
            Item.Update(opportunity);
        }

        var rank = board.RankOf(id);
        IsRanked = rank is not null;
        var settingsLock = Ranking.Lock(row, _session.Settings);
        LockText = settingsLock?.Text ?? (rank is null ? NotRankedReason(row) : null);
        RankTitle = rank is { } place ? $"WHY IT RANKS {Formatting.Ordinal(place)}" : "WHY IT ISN'T RANKED";
        Subtitle = $"{Presentation.JobLevel(row)} · {row.Where} · {row.Kind.Describe()} · {_session.Settings.World}";
        WindowText = Presentation.WindowText(row);
        Requirement = Presentation.Requirement(row);
        Demand = DemandText(row);
        SoldPerDay = row.VelDay > 0 ? Formatting.Rate(row.VelDay) : "—";
        SoldNote = row.VelScope == MarketScope.World ? $"{Formatting.Number(row.VelDay * 7)} this week" : "barely trades on this world";
        if (opportunity is not null)
        {
            TrendValue = opportunity.Trend.Percent is { } percent ? Formatting.SignedPercent(percent) : opportunity.Trend.Arrow + " " + opportunity.Trend.Word;
            TrendNote = opportunity.Trend.Percent is not null ? $"{opportunity.Trend.Word} · {opportunity.Trend.Basis}" : opportunity.Trend.Basis;
            TrendBrushKey = Presentation.ChangeBrushKey(opportunity.Trend.Percent ?? (opportunity.Trend.Direction switch { TrendDirection.Rising => 5, TrendDirection.Falling => -5, _ => 0 }));
            if (_loadedId != id || DaysOfSupply == "—")
            {
                DaysOfSupply = opportunity.Competition.DaysOfSupply is { } days ? days.ToString(days >= 10 ? "0" : "0.0", CultureInfo.InvariantCulture) : "—";
                SupplyNote = opportunity.Competition.Listings is { } listings
                    ? $"{Formatting.Number(opportunity.Competition.UnitsForSale ?? 0)} listed · {listings} sellers"
                    : "no listing data";
            }
        }

        if (_loadedId != id)
        {
            CheapestNow = row.Min > 0 ? Formatting.Gil(row.Min) : "—";
            CheapestNote = $"average sale {Formatting.Gil(row.Avg)}";
        }

        SourceLine = snapshot is null ? "Not swept yet" : $"Universalis · swept {Formatting.Ago(_session.Now - snapshot.TakenAt)}";
        UpdateWatch();
    }

    private async Task LoadLiveAsync(int itemId)
    {
        _loading?.Cancel();
        var cancel = _loading = new CancellationTokenSource();
        IsLoading = true;
        LoadError = null;
        var world = _session.Settings.World;
        try
        {
            var markets = await _universalis.GetMarketsAsync(world, [itemId], 30, 100, cancel.Token);
            var history = await LoadHistoryAsync(world, itemId, cancel.Token);
            if (cancel.IsCancellationRequested)
            {
                return;
            }

            _ui.Post(() => Apply(itemId, markets.GetValueOrDefault(itemId), history));
        }
        catch (OperationCanceledException)
        {
        }
        catch (GilSweepException ex)
        {
            _ui.Post(() =>
            {
                if (SelectedId != itemId)
                {
                    return;
                }

                LoadError = $"Live listings unavailable: {ex.Message} Figures above are from the last sweep.";
                ApplyLocalHistory(itemId);
                IsLoading = false;
            });
        }
    }

    private async Task<SaleHistory?> LoadHistoryAsync(string world, int itemId, CancellationToken cancellationToken)
    {
        try
        {
            var history = await _universalis.GetHistoryAsync(world, [itemId], 1000, TimeSpan.FromDays(14), cancellationToken);
            return history.GetValueOrDefault(itemId);
        }
        catch (GilSweepException ex)
        {
            // The chart falls back to local snapshots; listings may still have loaded.
            _logger.LogInformation("Sale history unavailable for {Item}: {Message}", itemId, ex.Message);
            return null;
        }
    }

    private void Apply(int itemId, ItemMarket? market, SaleHistory? history)
    {
        if (SelectedId != itemId)
        {
            return;
        }

        _loadedId = itemId;
        IsLoading = false;
        if (market is null)
        {
            LoadError = "Universalis has no market data for this item on " + _session.Settings.World + ".";
            ApplyLocalHistory(itemId);
            return;
        }

        var detail = MarketAnalyzer.Detail(market);
        CheapestNow = detail.CurMin > 0 ? Formatting.Gil(detail.CurMin) : "None listed";
        CheapestNote = detail.MedPpu > 0 ? $"median sale {Formatting.Gil(detail.MedPpu)}" : "no recent sales";
        Listings = [.. detail.Listings.Take(8).Select(listing => new TableRow(
            Formatting.Number(listing.PricePerUnit),
            listing.Quantity.ToString(CultureInfo.InvariantCulture),
            Formatting.Number(listing.PricePerUnit * listing.Quantity)))];
        var now = _session.Now;
        Sales = [.. detail.Sales.Take(8).Select(sale => new TableRow(
            Formatting.Number(sale.PricePerUnit),
            sale.Quantity.ToString(CultureInfo.InvariantCulture),
            Ago(now - sale.SoldAt)))];

        var row = Row;
        if (row is not null)
        {
            var advice = MarketAnalyzer.Advise(market, row.Kind);
            Advice = $"List at {Formatting.Gil(advice.ListPrice)} · stacks of {advice.Stack}";
            AdviceNote = advice.Text + (advice.DaysInv is { } days ? $" {days:0.#} days of stock at {advice.UnitsPerDay}/day." : "");
        }

        var sales = history?.Entries is { Count: > 0 } entries ? entries : market.RecentSales;
        var hours = MarketAnalyzer.Hours(sales, TimeZoneInfo.Local);
        Hours = [.. hours.Units.Select(units => (double)units)];
        PeakHours = hours.PeakHours;
        PeakLine = hours.HasData ? "peak " + string.Join(", ", hours.PeakHours.Select(hour => $"{hour:00}:00")) : "not enough sales yet";

        if (history?.Entries is { Count: > 1 } days2)
        {
            PricePoints = Daily(days2);
            ChartSubtitle = $"Median sale price per day · last 14 days · {_session.Settings.World}";
        }
        else
        {
            ApplyLocalHistory(itemId);
        }
    }

    /// <summary>Without live history, the chart draws this PC's own snapshots.</summary>
    private void ApplyLocalHistory(int itemId)
    {
        var series = _history.Series(_session.Settings.World).GetValueOrDefault(itemId) ?? [];
        PricePoints = [.. series.TakeLast(30).Select(point => new ChartPoint(DateTimeOffset.FromUnixTimeMilliseconds(point.T), point.Avg))];
        ChartSubtitle = PricePoints.Count > 1 ? "Average price at each of your sweeps · local history" : "Not enough history yet";
    }

    private static List<ChartPoint> Daily(IReadOnlyList<MarketSale> sales) =>
        [.. sales
            .GroupBy(sale => TimeZoneInfo.ConvertTime(sale.SoldAt, TimeZoneInfo.Local).Date)
            .OrderBy(group => group.Key)
            .Select(group => new ChartPoint(new DateTimeOffset(group.Key.AddHours(12), TimeZoneInfo.Local.GetUtcOffset(group.Key)), MarketAnalyzer.Median(group.Select(sale => sale.PricePerUnit))))];

    private void UpdateWatch() => WatchLabel = SelectedId is { } id && _watchlist.IsWatched(id) ? "Watching" : "Watch";

    private string NotRankedReason(SnapshotRow row) =>
        row.Kind switch
        {
            ItemKind.Map => "Maps have their own daily pick",
            ItemKind.Crystal => "Crystals aren't ranked as farms",
            _ when row.VelScope != MarketScope.World => "Barely trades on " + _session.Settings.World,
            _ when row.VelDay <= 0 => "No recent sales",
            _ => "Not ranked",
        };

    private static string DemandText(SnapshotRow row)
    {
        if (row.Demand is not { } demand)
        {
            return string.IsNullOrEmpty(row.Why) ? "No demand data for this item." : row.Why;
        }

        var parts = new List<string>();
        if (demand.RecipeCount > 0)
        {
            var top = demand.TopConsumers.Take(3).Select(consumer =>
                $"{consumer.Name} ×{consumer.Qty}" + (consumer.VelDay > 0 ? $" ({Formatting.Rate(consumer.VelDay)} sold a day)" : ""));
            parts.Add($"Used in {demand.RecipeCount} recipe{(demand.RecipeCount == 1 ? "" : "s")}: {string.Join(", ", top)}{(demand.RecipeCount > 3 ? ", and more" : "")}.");
        }

        if (demand.Leves > 0)
        {
            parts.Add($"Turned in for {demand.Leves} leve{(demand.Leves == 1 ? "" : "s")}.");
        }

        if (demand.Supply is { } supply)
        {
            parts.Add($"Grand Company supply missions ({supply.Count}).");
        }

        if (demand.Quests > 0)
        {
            parts.Add($"Needed for {demand.Quests} quest{(demand.Quests == 1 ? "" : "s")}.");
        }

        return parts.Count > 0 ? string.Join(" ", parts) : row.Why;
    }

    private static string Ago(TimeSpan age) =>
        age < TimeSpan.FromHours(1) ? $"{Math.Max(0, (int)age.TotalMinutes)}m ago"
        : age < TimeSpan.FromDays(2) ? $"{(int)age.TotalHours}h {age.Minutes:00}m"
        : $"{(int)age.TotalDays}d ago";
}
