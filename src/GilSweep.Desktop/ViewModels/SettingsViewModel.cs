using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GilSweep.Core;
using GilSweep.Core.Catalog;
using GilSweep.Core.Configuration;
using GilSweep.Core.History;
using GilSweep.Core.Platform;
using GilSweep.Core.Sweep;
using GilSweep.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace GilSweep.Desktop.ViewModels;

public sealed partial class FolkloreViewModel(Expansion expansion, bool owned, Action<Expansion, bool> changed) : ObservableObject
{
    public Expansion Expansion { get; } = expansion;

    public string Label { get; } = expansion.Name();

    [ObservableProperty]
    public partial bool Owned { get; set; } = owned;

    partial void OnOwnedChanged(bool value) => changed(Expansion, value);
}

public sealed partial class CrafterViewModel(string job, int level, Action<CrafterViewModel> changed) : ObservableObject
{
    public string Job { get; } = job;

    public string Label { get; } = SweepViewModel.CrafterName(job);

    [ObservableProperty]
    public partial string Level { get; set; } = level.ToString(CultureInfo.InvariantCulture);

    partial void OnLevelChanged(string value) => changed(this);
}

public sealed record CustomItemViewModel(int Id, string Name, string Detail);

/// <summary>
/// Who your character is and how the app behaves. Every change applies at once: levels and story
/// progress re-rank what's loaded, a new world sweeps right away.
/// </summary>
public sealed partial class SettingsViewModel : PageViewModel
{
    private readonly ISettingsService _settings;
    private readonly AppSession _session;
    private readonly IItemCatalog _catalog;
    private readonly IItemVerifier _verifier;
    private readonly IMarketSnapshotStore _store;
    private readonly ISweepEngine _engine;
    private readonly IAppEnvironment _environment;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly ProblemReporter _problems;
    private readonly ILogger<SettingsViewModel> _logger;
    private bool _loading;

    public SettingsViewModel(
        ISettingsService settings,
        AppSession session,
        IItemCatalog catalog,
        IItemVerifier verifier,
        IMarketSnapshotStore store,
        ISweepEngine engine,
        IAppEnvironment environment,
        IDialogService dialogs,
        IShellService shell,
        UpdatesViewModel updates,
        ProblemReporter problems,
        ILogger<SettingsViewModel> logger)
    {
        _problems = problems;
        _settings = settings;
        _session = session;
        _catalog = catalog;
        _verifier = verifier;
        _store = store;
        _engine = engine;
        _environment = environment;
        _dialogs = dialogs;
        _shell = shell;
        _logger = logger;
        Updates = updates;
        Expansions = [.. Core.Catalog.Expansions.All.Select(expansion => new Option<Expansion>(expansion, expansion.Name()))];
        Retentions =
        [
            new(30, "30 days"),
            new(90, "90 days"),
            new(365, "A year"),
            new(null, "Keep everything"),
        ];
        session.PropertyChanged += OnSessionChanged;
        settings.Changed += (_, _) => Load();
        catalog.Changed += (_, _) => LoadCustomItems();
        store.Changed += (_, _) => LoadStats();
        Load();
    }

    public override AppPage Page => AppPage.Settings;

    public UpdatesViewModel Updates { get; }

    public IReadOnlyList<Option<Expansion>> Expansions { get; }

    public IReadOnlyList<Option<int?>> Retentions { get; }

    public IReadOnlyList<string> Worlds => _session.Worlds.Count > 0 ? _session.Worlds : [_settings.Current.World];

    public ObservableCollection<FolkloreViewModel> Folklore { get; } = [];

    public ObservableCollection<CrafterViewModel> Crafters { get; } = [];

    public ObservableCollection<CustomItemViewModel> CustomItems { get; } = [];

    [ObservableProperty]
    public partial string? World { get; set; }

    [ObservableProperty]
    public partial Option<Expansion>? Story { get; set; }

    [ObservableProperty]
    public partial string Miner { get; set; } = "";

    [ObservableProperty]
    public partial string Botanist { get; set; } = "";

    [ObservableProperty]
    public partial string? CharacterError { get; private set; }

    [ObservableProperty]
    public partial string? CrafterError { get; private set; }

    [ObservableProperty]
    public partial bool CloseToTray { get; set; }

    [ObservableProperty]
    public partial bool AutoSweep { get; set; }

    [ObservableProperty]
    public partial bool UseSaddlebag { get; set; }

    [ObservableProperty]
    public partial bool CheckForUpdates { get; set; }

    [ObservableProperty]
    public partial string RequestNote { get; private set; } = "";

    [ObservableProperty]
    public partial string RetainerNames { get; set; } = "";

    [ObservableProperty]
    public partial Option<int?>? Retention { get; set; }

    [ObservableProperty]
    public partial string DataLine { get; private set; } = "";

    [ObservableProperty]
    public partial string? DataResult { get; private set; }

    [ObservableProperty]
    public partial string TrackQuery { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TrackCommand))]
    public partial bool IsTracking { get; private set; }

    [ObservableProperty]
    public partial string? TrackResult { get; private set; }

    public string DataFolder => _environment.DataDirectory;

    partial void OnWorldChanged(string? value)
    {
        if (!_loading && value is not null && value != _settings.Current.World)
        {
            Apply(settings => settings.World = value, error => CharacterError = error);
        }
    }

    partial void OnStoryChanged(Option<Expansion>? value)
    {
        if (!_loading && value is not null)
        {
            Apply(settings => settings.MsqExpansion = value.Value, error => CharacterError = error);
        }
    }

    partial void OnMinerChanged(string value) => ApplyLevel(value, (settings, level) => settings.Levels.Miner = level);

    partial void OnBotanistChanged(string value) => ApplyLevel(value, (settings, level) => settings.Levels.Botanist = level);

    partial void OnCloseToTrayChanged(bool value) => ApplySwitch(settings => settings.CloseToTray = value);

    partial void OnAutoSweepChanged(bool value) => ApplySwitch(settings => settings.AutoSweep = value);

    partial void OnUseSaddlebagChanged(bool value) => ApplySwitch(settings => settings.UseSaddlebag = value);

    partial void OnCheckForUpdatesChanged(bool value) => ApplySwitch(settings => settings.CheckForUpdates = value);

    partial void OnRetainerNamesChanged(string value)
    {
        if (_loading)
        {
            return;
        }

        var names = value.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Apply(settings => settings.RetainerNames = names, error => DataResult = error);
    }

    partial void OnRetentionChanged(Option<int?>? value)
    {
        if (!_loading && value is not null)
        {
            Apply(settings => settings.HistoryRetentionDays = value.Value, error => DataResult = error);
        }
    }

    public override Task ActivateAsync()
    {
        Load();
        LoadStats();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void OpenFolder() => _problems.Run(() => _shell.OpenFolder(DataFolder));

    [RelayCommand]
    private void KeepOnePerDay()
    {
        var deleted = _store.PruneToOnePerDay();
        DataResult = deleted == 0 ? "Already one snapshot per day." : $"Removed {deleted} snapshots; the newest of each day is kept.";
    }

    [RelayCommand]
    private async Task ClearSnapshotsAsync()
    {
        var stats = _store.Stats();
        var confirmed = await _dialogs.ConfirmAsync(new ConfirmRequest(
            "Clear all snapshots?",
            $"This deletes {stats.Count} snapshots from this computer. History, trends and movers start again from the next sweep. Settings and watched items stay.",
            "Clear snapshots")
        { Danger = true, ConfirmIcon = "Trash2" });
        if (!confirmed)
        {
            return;
        }

        var deleted = _store.Clear();
        DataResult = $"Deleted {deleted} snapshots.";
    }

    private bool CanTrack => !IsTracking;

    [RelayCommand(CanExecute = nameof(CanTrack))]
    private async Task TrackAsync()
    {
        var query = TrackQuery.Trim();
        if (query.Length == 0)
        {
            return;
        }

        IsTracking = true;
        TrackResult = null;
        try
        {
            var tracked = _catalog.Items.Select(item => item.Id).ToHashSet();
            var (result, item) = await _verifier.VerifyAsync(query, tracked);
            if (item is not null)
            {
                _catalog.AddCustom(item);
            }

            TrackResult = result.Message;
            TrackQuery = "";
            if (result.Gatherable)
            {
                // Price the newcomer, as v1 did; a sweep already running started without it.
                _session.SweepAfterCurrent();
            }
        }
        catch (GilSweepException ex)
        {
            _logger.LogWarning("Verification failed: {Message}", ex.Message);
            TrackResult = "Couldn't check that item: " + ex.Message;
        }
        finally
        {
            IsTracking = false;
        }
    }

    [RelayCommand]
    private void RemoveCustom(int itemId) => _problems.Run(() => _catalog.RemoveCustom(itemId));

    private void ApplyLevel(string value, Action<GilSweepSettings, int> apply)
    {
        if (_loading)
        {
            return;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) || level is < 1 or > GilSweepSettings.MaxLevel)
        {
            CharacterError = "Gatherer levels go from 1 to 100.";
            return;
        }

        Apply(settings => apply(settings, level), error => CharacterError = error);
    }

    private void ApplyCrafter(CrafterViewModel crafter)
    {
        if (_loading)
        {
            return;
        }

        if (!int.TryParse(crafter.Level, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) || level is < 1 or > GilSweepSettings.MaxLevel)
        {
            CrafterError = $"{crafter.Label}: levels go from 1 to 100.";
            return;
        }

        Apply(settings => settings.Crafters[crafter.Job] = level, error => CrafterError = error);
    }

    private void ApplyFolklore(Expansion expansion, bool owned)
    {
        if (_loading)
        {
            return;
        }

        Apply(settings =>
        {
            settings.Folklore.Remove(expansion);
            if (owned)
            {
                settings.Folklore.Add(expansion);
            }
        }, error => CharacterError = error);
    }

    private void ApplySwitch(Action<GilSweepSettings> apply)
    {
        if (!_loading)
        {
            Apply(apply, error => DataResult = error);
        }
    }

    private void Apply(Action<GilSweepSettings> change, Action<string?> report)
    {
        try
        {
            _settings.Update(change);
            report(null);
        }
        catch (GilSweepException ex)
        {
            report(ex.Message);
        }
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSession.Worlds))
        {
            OnPropertyChanged(nameof(Worlds));
            _loading = true;
            World = _settings.Current.World;
            _loading = false;
        }
    }

    private void Load()
    {
        _loading = true;
        try
        {
            var current = _settings.Current;
            World = current.World;
            Story = Expansions.First(option => option.Value == current.MsqExpansion);
            Miner = current.Levels.Miner.ToString(CultureInfo.InvariantCulture);
            Botanist = current.Levels.Botanist.ToString(CultureInfo.InvariantCulture);
            CloseToTray = current.CloseToTray;
            AutoSweep = current.AutoSweep;
            UseSaddlebag = current.UseSaddlebag;
            CheckForUpdates = current.CheckForUpdates;
            RetainerNames = string.Join(", ", current.RetainerNames);
            Retention = Retentions.FirstOrDefault(option => option.Value == current.HistoryRetentionDays) ?? new Option<int?>(current.HistoryRetentionDays, $"{current.HistoryRetentionDays} days");
            RequestNote = $"About {_engine.EstimateRequests(current)} Universalis and Saddlebag requests per sweep.";

            // ARR has no folklore books; they start with Heavensward.
            if (Folklore.Count == 0)
            {
                foreach (var expansion in Core.Catalog.Expansions.All.Where(expansion => expansion != Expansion.ARR))
                {
                    Folklore.Add(new FolkloreViewModel(expansion, current.Folklore.Contains(expansion), ApplyFolklore));
                }

                foreach (var job in GilSweepSettings.CrafterJobs)
                {
                    Crafters.Add(new CrafterViewModel(job, current.CrafterLevel(job), ApplyCrafter));
                }
            }
            else
            {
                foreach (var folklore in Folklore)
                {
                    folklore.Owned = current.Folklore.Contains(folklore.Expansion);
                }

                foreach (var crafter in Crafters)
                {
                    crafter.Level = current.CrafterLevel(crafter.Job).ToString(CultureInfo.InvariantCulture);
                }
            }
        }
        finally
        {
            _loading = false;
        }

        LoadCustomItems();
        LoadStats();
    }

    private void LoadCustomItems()
    {
        CustomItems.Clear();
        foreach (var item in _catalog.Items.Where(item => item.Custom == true))
        {
            var detail = item.Kind.IsTrap() ? $"{item.Kind.Describe()} · never recommended" : $"{Presentation.JobLevel(item)} · {item.Where}";
            CustomItems.Add(new CustomItemViewModel(item.Id, item.Name, detail));
        }
    }

    private void LoadStats()
    {
        var stats = _store.Stats();
        DataLine = $"{stats.Count} snapshots · {stats.Bytes / 1048576.0:0.0} MB · {_environment.DataDirectoryLabel}";
    }
}
