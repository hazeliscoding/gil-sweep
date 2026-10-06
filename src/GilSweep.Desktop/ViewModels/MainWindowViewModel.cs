using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GilSweep.Core;
using GilSweep.Core.Catalog;
using GilSweep.Core.Configuration;
using GilSweep.Core.Time;
using GilSweep.Core.Watchlist;
using GilSweep.Desktop.Controls;
using GilSweep.Desktop.Services;

namespace GilSweep.Desktop.ViewModels;

public sealed partial class NavItemViewModel(AppPage page, string label, string icon) : ObservableObject
{
    public AppPage Page { get; } = page;

    public string Label { get; } = label;

    public string Icon { get; } = icon;

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    /// <summary>A count beside the label (watched items); 0 hides it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCount))]
    public partial int Count { get; set; }

    public bool HasCount => Count > 0;
}

/// <summary>The window: navigation, the Eorzea clock, connection state, the offline banner and first run.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly Dictionary<AppPage, PageViewModel> _pages;
    private readonly AppSession _session;
    private readonly ISettingsService _settings;
    private readonly IWatchlistService _watchlist;
    private readonly IItemCatalog _catalog;
    private readonly IShellService _shell;

    public MainWindowViewModel(
        IEnumerable<PageViewModel> pages,
        Navigator navigator,
        AppSession session,
        ISettingsService settings,
        IWatchlistService watchlist,
        IItemCatalog catalog,
        OverlayDialogService dialogs,
        UpdatesViewModel updates,
        OnboardingViewModel onboarding,
        IShellService shell)
    {
        _pages = pages.ToDictionary(page => page.Page);
        _session = session;
        _settings = settings;
        _watchlist = watchlist;
        _catalog = catalog;
        _shell = shell;
        Dialogs = dialogs;
        Updates = updates;
        Onboarding = onboarding;
        NavItems =
        [
            new(AppPage.Sweep, "Sweep", "Compass"),
            new(AppPage.Market, "Market", "Store"),
            new(AppPage.Craft, "Craft", "Hammer"),
            new(AppPage.Watchlist, "Watchlist", "Bookmark"),
            new(AppPage.History, "History", "History"),
            new(AppPage.Settings, "Settings", "Settings"),
        ];
        navigator.Navigated += (page, item) => _ = ShowAsync(page, item);
        session.PropertyChanged += OnSessionChanged;
        session.Ticked += (_, _) => UpdateClock();
        watchlist.Changed += (_, _) => UpdateWatchCount();
        CurrentPage = _pages[AppPage.Sweep];
        NavItems[0].IsActive = true;
        UpdateWatchCount();
        UpdateClock();
        UpdateConnection();
    }

    public ObservableCollection<NavItemViewModel> NavItems { get; }

    public OverlayDialogService Dialogs { get; }

    public UpdatesViewModel Updates { get; }

    public OnboardingViewModel Onboarding { get; }

    public string VersionLabel { get; } = "Gil Sweep " + GilSweepInfo.Version;

    [ObservableProperty]
    public partial PageViewModel CurrentPage { get; private set; }

    [ObservableProperty]
    public partial string EorzeaClock { get; private set; } = "";

    [ObservableProperty]
    public partial string NextWindowLine { get; private set; } = "";

    [ObservableProperty]
    public partial Tone ConnectionTone { get; private set; } = Tone.Unknown;

    [ObservableProperty]
    public partial string ConnectionLabel { get; private set; } = "";

    [ObservableProperty]
    public partial bool ShowOfflineBanner { get; private set; }

    [ObservableProperty]
    public partial string OfflineTitle { get; private set; } = "";

    [ObservableProperty]
    public partial string OfflineDescription { get; private set; } = "";

    [ObservableProperty]
    public partial string OfflineMeta { get; private set; } = "";

    /// <summary>Settings couldn't be read, or v1 data was just imported: a one-time notice.</summary>
    [ObservableProperty]
    public partial string? Notice { get; private set; }

    public async Task InitializeAsync()
    {
        Notice = _settings.LoadError is { } error
            ? error
            : _settings.Imported is { } imported
                ? $"Imported your Gil Sweep 1 settings{(imported.Watched > 0 ? $", {imported.Watched} watched items" : "")} and {imported.Snapshots} snapshots."
                : null;
        Onboarding.IsOpen = _settings.IsFirstRun;
        _session.Initialize();
        await ShowAsync(AppPage.Sweep, null);
        await Updates.CheckOnStartupAsync();
    }

    [RelayCommand]
    private Task NavigateAsync(AppPage page) => ShowAsync(page, null);

    [RelayCommand]
    private Task OpenUpdatesAsync() => ShowAsync(AppPage.Settings, null);

    [RelayCommand]
    private void OpenGitHub() => _shell.OpenUrl(GilSweepInfo.RepositoryUrl);

    [RelayCommand]
    private Task RetryAsync() => _session.SweepAsync();

    [RelayCommand]
    private void DismissNotice() => Notice = null;

    public async Task ShowAsync(AppPage page, int? itemId)
    {
        foreach (var item in NavItems)
        {
            item.IsActive = item.Page == page;
        }

        var target = _pages[page];
        if (itemId is { } id && target is MarketViewModel market)
        {
            market.Select(id);
        }

        CurrentPage = target;
        await target.ActivateAsync();
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSession.SweepError) or nameof(AppSession.Snapshot) or nameof(AppSession.IsSweeping) or nameof(AppSession.NextRetryAt))
        {
            UpdateConnection();
        }
    }

    private void UpdateWatchCount() => NavItems[3].Count = _watchlist.Entries.Count;

    private void UpdateClock()
    {
        var now = _session.Now;
        EorzeaClock = EorzeaTime.Clock(now);
        var next = NodeWindows.Next(_catalog.Items, _settings.Current, now, limit: 20).FirstOrDefault(window => !window.Up);
        NextWindowLine = next is null
            ? "No timed nodes at your level"
            : $"{Short(next.Item.Name)} opens in {Formatting.Countdown(next.Availability.RealRemaining)}";
        if (ShowOfflineBanner)
        {
            UpdateConnection();
        }
    }

    private void UpdateConnection()
    {
        var world = _settings.Current.World;
        if (_session.IsOffline)
        {
            ConnectionTone = Tone.Warning;
            ConnectionLabel = "Universalis unreachable";
            ShowOfflineBanner = true;
            var age = _session.SnapshotAge;
            OfflineTitle = "Universalis is not responding";
            OfflineDescription = age is { } cached
                ? $"Showing the cached sweep from {Formatting.Ago(cached)}. Prices may have moved since then; recommendations use the last known values."
                : "There is no earlier sweep for this world to fall back on. Gil Sweep tries again on its own.";
            var retry = _session.NextRetryAt is { } at ? $"Retrying in {Formatting.Countdown(at - _session.Now)}" : "Retrying shortly";
            var last = _session.Snapshot is { } snapshot ? $"Last successful sweep {snapshot.TakenAt:yyyy-MM-dd HH:mm} UTC · " : "";
            OfflineMeta = last + retry;
            return;
        }

        ShowOfflineBanner = false;
        ConnectionTone = _session.HasData || _session.IsSweeping ? Tone.Healthy : Tone.Unknown;
        ConnectionLabel = _session.IsSweeping ? $"Sweeping {world}" : $"Universalis · {world}";
    }

    /// <summary>The sidebar fits about two words: "Dense Aluminum".</summary>
    private static string Short(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length <= 2 ? name : string.Join(' ', words.Take(2));
    }
}
