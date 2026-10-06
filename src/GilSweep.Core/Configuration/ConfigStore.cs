using System.Text.Json;
using GilSweep.Core.Platform;
using GilSweep.Core.Serialization;

namespace GilSweep.Core.Configuration;

public interface IConfigStore
{
    string ConfigPath { get; }

    /// <summary>False until settings are saved once; the first run asks for world and levels.</summary>
    bool Exists { get; }

    /// <summary>Loads the settings. A missing file gives the defaults.</summary>
    GilSweepSettings Load();

    void Save(GilSweepSettings settings);
}

public sealed class ConfigStore(IAppEnvironment environment) : IConfigStore
{
    public string ConfigPath => Path.Combine(environment.DataDirectory, "config.json");

    public bool Exists => File.Exists(ConfigPath);

    public GilSweepSettings Load()
    {
        if (!File.Exists(ConfigPath))
        {
            return new GilSweepSettings();
        }

        try
        {
            var settings = Read(AtomicFile.ReadAllText(ConfigPath));
            Validate(settings);
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new GilSweepException(
                GilSweepErrorKind.InvalidSettings,
                $"The settings file {ConfigPath} could not be read ({ex.Message}). Saving settings replaces it.",
                ex);
        }
    }

    public void Save(GilSweepSettings settings)
    {
        Validate(settings);
        settings.LegacyWatched = null;
        AtomicFile.WriteAllText(ConfigPath, JsonSerializer.Serialize(settings, GilSweepJsonContext.Default.GilSweepSettings));
    }

    /// <summary>Parses settings JSON, from this version or from v1.</summary>
    public static GilSweepSettings Read(string json) =>
        JsonSerializer.Deserialize(json, GilSweepJsonContext.Default.GilSweepSettings)
        ?? throw new JsonException("The file is empty.");

    public static void Validate(GilSweepSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.World))
        {
            throw new GilSweepException(GilSweepErrorKind.InvalidSettings, "Choose a world to price.");
        }

        if (settings.Levels.Miner is < 1 or > GilSweepSettings.MaxLevel || settings.Levels.Botanist is < 1 or > GilSweepSettings.MaxLevel)
        {
            throw new GilSweepException(GilSweepErrorKind.InvalidSettings, $"Gatherer levels must be between 1 and {GilSweepSettings.MaxLevel}.");
        }

        if (settings.Crafters.Values.Any(level => level is < 1 or > GilSweepSettings.MaxLevel))
        {
            throw new GilSweepException(GilSweepErrorKind.InvalidSettings, $"Crafter levels must be between 1 and {GilSweepSettings.MaxLevel}.");
        }

        if (settings.Alerts.SpikePercent is < 1 or > 1000 || settings.Alerts.CrashPercent is < 1 or > 100 || settings.Alerts.NodeLeadMinutes is < 0 or > 30)
        {
            throw new GilSweepException(GilSweepErrorKind.InvalidSettings, "Alert thresholds are out of range.");
        }

        if (settings.HistoryRetentionDays is < 7)
        {
            throw new GilSweepException(GilSweepErrorKind.InvalidSettings, "Keep at least a week of history.");
        }
    }
}
