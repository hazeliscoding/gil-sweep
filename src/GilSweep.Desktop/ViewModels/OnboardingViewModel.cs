using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GilSweep.Core;
using GilSweep.Core.Catalog;
using GilSweep.Core.Configuration;
using GilSweep.Core.Sweep;

namespace GilSweep.Desktop.ViewModels;

/// <summary>First run: three answers (world, gatherer levels, story progress), then the first sweep.</summary>
public sealed partial class OnboardingViewModel : ObservableValidator
{
    private readonly ISettingsService _settings;
    private readonly AppSession _session;

    public OnboardingViewModel(ISettingsService settings, AppSession session, ISweepEngine engine)
    {
        _settings = settings;
        _session = session;
        var current = settings.Current;

        // v1's first-run answers: 90/90 and the latest story, until the user says otherwise.
        World = current.World;
        Miner = settings.IsFirstRun ? "90" : current.Levels.Miner.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Botanist = settings.IsFirstRun ? "90" : current.Levels.Botanist.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Story = settings.IsFirstRun ? Expansions[^1] : Expansions.FirstOrDefault(option => option.Value == current.MsqExpansion) ?? Expansions[^1];
        RequestNote = $"Market data from Universalis · about {engine.EstimateRequests(current)} requests";
        session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSession.Worlds))
            {
                OnPropertyChanged(nameof(Worlds));
            }
        };
    }

    public IReadOnlyList<Option<Expansion>> Expansions { get; } =
        [.. Core.Catalog.Expansions.All.Select(expansion => new Option<Expansion>(expansion, expansion.Name()))];

    public IReadOnlyList<string> Worlds => _session.Worlds.Count > 0 ? _session.Worlds : [World];

    public string RequestNote { get; }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string World { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, GilSweepSettings.MaxLevel, ErrorMessage = "1 to 100")]
    [RegularExpression("^[0-9]+$", ErrorMessage = "1 to 100")]
    public partial string Miner { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, GilSweepSettings.MaxLevel, ErrorMessage = "1 to 100")]
    [RegularExpression("^[0-9]+$", ErrorMessage = "1 to 100")]
    public partial string Botanist { get; set; }

    [ObservableProperty]
    public partial Option<Expansion> Story { get; set; }

    [ObservableProperty]
    public partial string? Error { get; private set; }

    [RelayCommand]
    private async Task RunFirstSweepAsync()
    {
        ValidateAllProperties();
        if (HasErrors || !int.TryParse(Miner, out var miner) || !int.TryParse(Botanist, out var botanist))
        {
            Error = "Gatherer levels go from 1 to 100.";
            return;
        }

        try
        {
            _settings.Update(settings =>
            {
                settings.World = World;
                settings.Levels.Miner = miner;
                settings.Levels.Botanist = botanist;
                settings.MsqExpansion = Story.Value;
            });
        }
        catch (GilSweepException ex)
        {
            Error = ex.Message;
            return;
        }

        Error = null;
        IsOpen = false;
        await _session.SweepAsync();
    }
}
