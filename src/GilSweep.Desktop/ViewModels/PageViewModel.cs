using CommunityToolkit.Mvvm.ComponentModel;
using GilSweep.Desktop.Services;

namespace GilSweep.Desktop.ViewModels;

public abstract class PageViewModel : ObservableObject
{
    public abstract AppPage Page { get; }

    /// <summary>Called each time the screen is shown, so it reflects changes made elsewhere.</summary>
    public virtual Task ActivateAsync() => Task.CompletedTask;
}

/// <summary>A choice in a segmented control or a select. ToString gives the label the control shows.</summary>
public sealed record Option<T>(T Value, string Label)
{
    public override string ToString() => Label;
}
