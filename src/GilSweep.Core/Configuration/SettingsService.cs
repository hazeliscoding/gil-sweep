using Microsoft.Extensions.Logging;

namespace GilSweep.Core.Configuration;

/// <summary>The settings in use, shared by every screen. Changes are validated and saved at once.</summary>
public interface ISettingsService
{
    GilSweepSettings Current { get; }

    /// <summary>True until settings are saved once: the first run asks for world and levels.</summary>
    bool IsFirstRun { get; }

    /// <summary>Why the saved settings couldn't be read, while defaults stand in. Saving clears it.</summary>
    string? LoadError { get; }

    /// <summary>What was brought over from v1 on this start, if anything.</summary>
    LegacyImport? Imported { get; }

    event EventHandler? Changed;

    /// <summary>Applies a change to a copy, validates and saves it, then makes it current. Throws <see cref="GilSweepException"/> when invalid.</summary>
    void Update(Action<GilSweepSettings> change);
}

public sealed class SettingsService : ISettingsService
{
    private readonly IConfigStore _store;
    private readonly Lock _gate = new();

    public SettingsService(IConfigStore store, LegacyImporter importer, ILogger<SettingsService> logger)
    {
        _store = store;
        Imported = importer.ImportIfNeeded();
        try
        {
            Current = store.Load();
        }
        catch (GilSweepException ex)
        {
            logger.LogWarning(ex, "Using default settings");
            Current = new GilSweepSettings();
            LoadError = ex.Message;
        }

        IsFirstRun = !store.Exists;
    }

    public GilSweepSettings Current { get; private set; }

    public bool IsFirstRun { get; private set; }

    public string? LoadError { get; private set; }

    public LegacyImport? Imported { get; }

    public event EventHandler? Changed;

    public void Update(Action<GilSweepSettings> change)
    {
        lock (_gate)
        {
            var next = Current.Clone();
            change(next);
            try
            {
                _store.Save(next);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A sync tool or antivirus holding config.json must not take the app down.
                throw new GilSweepException(GilSweepErrorKind.StorageFailed, $"Settings could not be saved ({ex.Message}). Your change wasn't kept; try again.", ex);
            }

            Current = next;
            IsFirstRun = false;
            LoadError = null;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
