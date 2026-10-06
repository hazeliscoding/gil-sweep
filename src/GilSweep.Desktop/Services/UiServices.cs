namespace GilSweep.Desktop.Services;

/// <summary>A confirmation the user must answer, shown as an in-window modal.</summary>
public sealed record ConfirmRequest(
    string Title,
    string Message,
    string ConfirmText,
    string CancelText = "Cancel")
{
    public bool Danger { get; init; }

    public string? ConfirmIcon { get; init; }
}

public interface IDialogService
{
    Task<bool> ConfirmAsync(ConfirmRequest request);
}

public interface IShellService
{
    void OpenUrl(string url);

    /// <summary>Opens a folder in File Explorer.</summary>
    void OpenFolder(string path);
}

public interface IFilePicker
{
    /// <summary>Asks where to save a CSV file. Null when cancelled.</summary>
    Task<string?> SaveCsvAsync(string suggestedName);
}

/// <summary>Runs updates on the UI thread. Tests run them inline.</summary>
public interface IUiThread
{
    void Post(Action action);
}

/// <summary>Beats once a second for the Eorzea clock and countdowns. Tests beat it by hand.</summary>
public interface ITicker
{
    event EventHandler? Tick;
}

/// <summary>Windows notifications (toasts). Clicking one brings the window back.</summary>
public interface INotifier
{
    void Show(string title, string body);
}

public enum AppPage
{
    Sweep,
    Market,
    Craft,
    Watchlist,
    History,
    Settings,
}

public interface INavigator
{
    void Navigate(AppPage page);

    /// <summary>Opens an item on the Market screen.</summary>
    void OpenItem(int itemId);
}

public sealed class Navigator : INavigator
{
    public event Action<AppPage, int?>? Navigated;

    public void Navigate(AppPage page) => Navigated?.Invoke(page, null);

    public void OpenItem(int itemId) => Navigated?.Invoke(AppPage.Market, itemId);
}
