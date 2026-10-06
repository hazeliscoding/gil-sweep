using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace GilSweep.Desktop.Services;

public sealed class AvaloniaUiThread : IUiThread
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action);
}

public sealed class DispatcherTicker : ITicker
{
    private readonly DispatcherTimer _timer;

    public DispatcherTicker()
    {
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Tick?.Invoke(this, EventArgs.Empty));
        _timer.Start();
    }

    public event EventHandler? Tick;
}

public sealed class WindowsShellService : IShellService
{
    public void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    public void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = false });
    }
}

/// <summary>File pickers need the window; it is attached once it exists.</summary>
public sealed class WindowServices : IFilePicker
{
    public TopLevel? TopLevel { get; set; }

    public async Task<string?> SaveCsvAsync(string suggestedName)
    {
        if (TopLevel?.StorageProvider is not { } storage)
        {
            return null;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export history",
            SuggestedFileName = suggestedName,
            DefaultExtension = "csv",
            FileTypeChoices = [new FilePickerFileType("CSV file") { Patterns = ["*.csv"] }],
        });
        return file?.TryGetLocalPath();
    }
}
