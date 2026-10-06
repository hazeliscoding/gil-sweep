using GilSweep.Core.Platform;
using GilSweep.Core.Watchlist;
using Microsoft.Extensions.Logging;

namespace GilSweep.Core.Configuration;

/// <summary>What came over from v1.</summary>
public sealed record LegacyImport(bool Settings, int Watched, int Snapshots, bool CustomItems);

/// <summary>
/// The first time v2 starts on a PC that ran v1, copies v1's settings, stars, snapshots, custom
/// items and history backfill from %APPDATA%\gil-sweep. v1's files are left where they are.
/// </summary>
public sealed class LegacyImporter(IAppEnvironment environment, IConfigStore configStore, ILogger<LegacyImporter> logger)
{
    public LegacyImport? ImportIfNeeded()
    {
        var legacy = environment.LegacyDataDirectory;
        if (configStore.Exists || !Directory.Exists(legacy))
        {
            return null;
        }

        var settings = false;
        var watched = 0;
        var legacyConfig = Path.Combine(legacy, "config.json");
        if (File.Exists(legacyConfig))
        {
            try
            {
                var imported = ConfigStore.Read(File.ReadAllText(legacyConfig));
                imported.Watchlist = [.. (imported.LegacyWatched ?? []).Distinct().Select(id => new WatchEntry
                {
                    ItemId = id,

                    // v1 stars sent a node toast and a ±25% price toast.
                    NodeOpens = true,
                    PriceSpike = true,
                    PriceCrash = true,
                })];
                watched = imported.Watchlist.Count;

                // v1 kept every snapshot; keep doing that until the user picks a limit.
                imported.HistoryRetentionDays = null;
                configStore.Save(imported);
                settings = true;
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or GilSweepException or IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not import the v1 settings");
            }
        }

        var snapshots = Copy(Path.Combine(legacy, "snapshots"), Path.Combine(environment.DataDirectory, "snapshots"), "*.json");
        var customItems = Copy(legacy, environment.DataDirectory, "custom-items.json") > 0;
        Copy(legacy, environment.DataDirectory, "history-backfill-*.json");
        if (!settings && snapshots == 0 && !customItems)
        {
            return null;
        }

        logger.LogInformation("Imported from v1: settings {Settings}, {Watched} watched items, {Snapshots} snapshots", settings, watched, snapshots);
        return new LegacyImport(settings, watched, snapshots, customItems);
    }

    private int Copy(string from, string to, string pattern)
    {
        if (!Directory.Exists(from))
        {
            return 0;
        }

        var count = 0;
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from, pattern))
        {
            var target = Path.Combine(to, Path.GetFileName(file));
            if (File.Exists(target))
            {
                continue;
            }

            try
            {
                File.Copy(file, target);
                count++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not import {File}", Path.GetFileName(file));
            }
        }

        return count;
    }
}
