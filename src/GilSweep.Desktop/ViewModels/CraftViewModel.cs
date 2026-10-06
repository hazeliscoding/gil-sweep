using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GilSweep.Core;
using GilSweep.Core.Crafting;
using GilSweep.Desktop.Controls;
using GilSweep.Desktop.Services;

namespace GilSweep.Desktop.ViewModels;

public enum CraftFilter
{
    All,
    WorthIt,
    Locked,
}

public sealed record CraftStepViewModel(string Stage, string Name, string Value, bool Highlight, bool Arrow);

/// <summary>One recipe as a sell-raw-or-process card.</summary>
public sealed record CraftCardViewModel(
    int ItemId,
    int RawId,
    string Title,
    string JobLine,
    bool Locked,
    string LockReason,
    IReadOnlyList<CraftStepViewModel> Steps,
    string SellRaw,
    string Processed,
    bool Hq,
    string OtherMaterials,
    string Difference,
    Tone DifferenceTone,
    string Verdict,
    string Basis);

/// <summary>Should a gathered material be sold raw or processed first? Recipes that use what you farm.</summary>
public sealed partial class CraftViewModel : PageViewModel
{
    private readonly AppSession _session;
    private readonly INavigator _navigator;
    private List<CraftingOpportunity> _all = [];

    public CraftViewModel(AppSession session, INavigator navigator)
    {
        _session = session;
        _navigator = navigator;
        session.PropertyChanged += OnSessionChanged;
        Rebuild();
    }

    public override AppPage Page => AppPage.Craft;

    [ObservableProperty]
    public partial IReadOnlyList<Option<CraftFilter>> Filters { get; private set; } = [];

    [ObservableProperty]
    public partial Option<CraftFilter>? Filter { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<CraftCardViewModel> Cards { get; private set; } = [];

    [ObservableProperty]
    public partial bool HasCrafts { get; private set; }

    [ObservableProperty]
    public partial string EmptyTitle { get; private set; } = "";

    [ObservableProperty]
    public partial string EmptyDescription { get; private set; } = "";

    partial void OnFilterChanged(Option<CraftFilter>? value) => ApplyFilter();

    [RelayCommand]
    private void Open(int itemId) => _navigator.OpenItem(itemId);

    public override Task ActivateAsync()
    {
        Rebuild();
        return Task.CompletedTask;
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSession.Snapshot))
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        _all = _session.Snapshot is { } snapshot ? CraftingAdvisor.Build(snapshot, _session.Settings) : [];
        var current = Filter?.Value ?? CraftFilter.WorthIt;
        Filters =
        [
            new(CraftFilter.All, "All"),
            new(CraftFilter.WorthIt, $"Worth it ({Materials(_all.Where(c => c.WorthIt))})"),
            new(CraftFilter.Locked, $"Locked ({Materials(_all.Where(c => c.Locked))})"),
        ];
        Filter = Filters.First(option => option.Value == current);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filter = Filter?.Value ?? CraftFilter.WorthIt;
        var chosen = filter switch
        {
            CraftFilter.WorthIt => _all.Where(c => c.WorthIt),
            CraftFilter.Locked => _all.Where(c => c.Locked),
            _ => _all,
        };
        // One card per farmed material: its best recipe in v1's order (margin × daily sales).
        // Some materials feed a hundred recipes; the question is what to do with the material.
        Cards = [.. chosen
            .GroupBy(opportunity => opportunity.Raw.Id)
            .Select(group => Card(group.First(), group.Count() - 1))
            .Take(CraftingAdvisor.Limit)];
        HasCrafts = Cards.Count > 0;
        (EmptyTitle, EmptyDescription) = (_session.Snapshot, filter) switch
        {
            (null, _) => ("No craft margins yet", "Margins are worked out during a sweep. Run one from the Sweep screen."),
            (_, CraftFilter.WorthIt) => ("Selling raw wins right now", "No recipe that uses what you can farm beats selling the materials as they are."),
            (_, CraftFilter.Locked) => ("Nothing is out of reach", "Your crafter levels cover every recipe that uses what you farm."),
            _ => ("No recipes use what you can farm", "Raise your gatherer levels in Settings to see more."),
        };
    }

    private static int Materials(IEnumerable<CraftingOpportunity> opportunities) => opportunities.Select(c => c.Raw.Id).Distinct().Count();

    private CraftCardViewModel Card(CraftingOpportunity opportunity, int moreRecipes)
    {
        var craft = opportunity.Craft;
        var crafter = SweepViewModel.CrafterName(craft.Job);
        var jobLine = craft.Job == "any" ? crafter : $"{crafter} {craft.Lvl}";
        var steps = new List<CraftStepViewModel>
        {
            new("GATHERED", $"{opportunity.Raw.Name} ×{opportunity.Raw.Qty}", $"{Formatting.Gil(opportunity.SellRaw)} raw", false, true),
        };
        foreach (var crafted in opportunity.CraftedIngredients.DistinctBy(ingredient => ingredient.Id).Take(2))
        {
            steps.Add(new("CRAFT YOUR OWN", $"{crafted.Name} ×{crafted.Qty}", $"{Formatting.Gil(crafted.UnitPrice)} each, cheaper than buying", false, true));
        }

        steps.Add(new("CRAFTED", $"{craft.Name}{(craft.Yield > 1 ? $" ×{craft.Yield}" : "")}",
            $"{Formatting.Gil(craft.SalePrice)} each{(craft.Hq ? " (HQ)" : "")} · {Formatting.Rate(craft.VelDay)}/day", opportunity.WorthIt, false));

        var difference = opportunity.Difference;
        return new CraftCardViewModel(
            craft.Id,
            opportunity.Raw.Id,
            $"{opportunity.Raw.Name} → {craft.Name}",
            jobLine,
            opportunity.Locked,
            "needs " + jobLine,
            steps,
            Formatting.Gil(opportunity.SellRaw),
            Formatting.Gil(opportunity.Processed),
            craft.Hq,
            "−" + Formatting.Gil(opportunity.OtherMaterials),
            (difference >= 0 ? "+" : "−") + Formatting.Gil(Math.Abs(difference)),
            difference > 0 ? Tone.Healthy : Tone.Critical,
            opportunity.Locked ? "Level the crafter to profit" : difference > 0 ? "Process first" : "Sell raw",
            $"Per craft · {jobLine} · you are {opportunity.CrafterLevel}"
                + (moreRecipes > 0 ? $" · {moreRecipes} more recipe{(moreRecipes == 1 ? "" : "s")} use {opportunity.Raw.Name}" : ""));
    }
}
