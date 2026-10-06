using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GilSweep.Core;
using Microsoft.Extensions.Logging;

namespace GilSweep.Desktop.Services;

/// <summary>
/// Turns a failed action (a locked settings file, a browser that won't open) into a message in the
/// window instead of a crash. The window shows <see cref="Message"/> until it is dismissed.
/// </summary>
public sealed partial class ProblemReporter(ILogger<ProblemReporter> logger) : ObservableObject
{
    [ObservableProperty]
    public partial string? Message { get; private set; }

    public void Report(Exception exception)
    {
        logger.LogError(exception, "Action failed");
        Message = exception is GilSweepException known
            ? known.Message
            : $"Something went wrong: {exception.Message} The log in the data folder has the details.";
    }

    /// <summary>Runs an action the user started. Returns false, with the message shown, when it failed.</summary>
    public bool Run(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex) when (ex is GilSweepException or IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        {
            Report(ex);
            return false;
        }
    }

    [RelayCommand]
    private void Dismiss() => Message = null;
}
