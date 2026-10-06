using GilSweep.Core;
using GilSweep.Core.Catalog;
using GilSweep.Core.Configuration;
using GilSweep.Core.History;
using GilSweep.Core.Watchlist;
using GilSweep.Tests.Support;

namespace GilSweep.Tests.Core;

public sealed class SettingsTests
{
    private const string V1Config = """
        {
          "world": "Gilgamesh",
          "levels": { "MIN": 100, "BTN": 92 },
          "msqExpansion": "DT",
          "folklore": ["EW", "DT"],
          "closeToTray": false,
          "watched": [44012, 5121, 44012],
          "crafters": { "CRP": 100, "BSM": 90 },
          "saddlebag": { "timePeriod": 168, "salesAmount": 2, "averagePrice": 50, "filters": [47, 48, 49] }
        }
        """;

    [Fact]
    public void A_v1_config_file_loads_as_is()
    {
        var settings = ConfigStore.Read(V1Config);

        Assert.Equal("Gilgamesh", settings.World);
        Assert.Equal(100, settings.Levels.Miner);
        Assert.Equal(92, settings.Levels.Botanist);
        Assert.Equal(Expansion.DT, settings.MsqExpansion);
        Assert.Equal([Expansion.EW, Expansion.DT], settings.Folklore);
        Assert.False(settings.CloseToTray);
        Assert.Equal([44012, 5121, 44012], settings.LegacyWatched);
    }

    [Fact]
    public void Keys_missing_from_a_file_keep_their_defaults()
    {
        var settings = ConfigStore.Read("""{ "world": "Siren", "levels": { "MIN": 50 }, "crafters": { "GSM": 70 } }""");

        Assert.Equal(50, settings.Levels.Miner);
        Assert.Equal(70, settings.Levels.Botanist);
        Assert.Equal(70, settings.CrafterLevel("GSM"));
        Assert.Equal(100, settings.CrafterLevel("CRP"));
        Assert.Equal(25, settings.Alerts.SpikePercent);
        Assert.True(settings.AutoSweep);
        Assert.Equal([47, 48, 49], settings.Saddlebag.Filters);
    }

    [Fact]
    public void Saved_settings_load_back_the_same()
    {
        using var environment = new TestEnvironment();
        var store = new ConfigStore(environment);
        var settings = new GilSweepSettings { World = "Siren", RetainerNames = ["Mochi"], HistoryRetentionDays = null };
        settings.Watchlist.Add(new WatchEntry { ItemId = 5121, NodeOpens = true, Undercut = true });

        store.Save(settings);
        var loaded = store.Load();

        Assert.True(store.Exists);
        Assert.Equal("Siren", loaded.World);
        Assert.Equal(["Mochi"], loaded.RetainerNames);
        Assert.Null(loaded.HistoryRetentionDays);
        Assert.True(Assert.Single(loaded.Watchlist).Undercut);
        Assert.DoesNotContain("watched", File.ReadAllText(store.ConfigPath), StringComparison.Ordinal);
    }

    [Fact]
    public void An_unreadable_file_is_reported_and_defaults_stand_in()
    {
        using var host = new CoreHost();
        File.WriteAllText(Path.Combine(host.Environment.DataDirectory, "config.json"), "{ not json");

        var settings = host.Get<ISettingsService>();

        Assert.NotNull(settings.LoadError);
        Assert.Equal("Cactuar", settings.Current.World);
        settings.Update(s => s.World = "Siren");
        Assert.Null(settings.LoadError);
        Assert.Equal("Siren", new ConfigStore(host.Environment).Load().World);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(101, 50)]
    [InlineData(50, 0)]
    public void Levels_outside_1_to_100_are_refused(int miner, int botanist)
    {
        var settings = new GilSweepSettings { Levels = new GathererLevels { Miner = miner, Botanist = botanist } };
        Assert.Equal(GilSweepErrorKind.InvalidSettings, Assert.Throws<GilSweepException>(() => ConfigStore.Validate(settings)).Kind);
    }

    [Fact]
    public void A_refused_change_leaves_the_current_settings_alone()
    {
        using var host = new CoreHost();
        var settings = host.Get<ISettingsService>();

        Assert.Throws<GilSweepException>(() => settings.Update(s => s.World = ""));

        Assert.Equal("Cactuar", settings.Current.World);
        Assert.True(settings.IsFirstRun);
    }

    [Fact]
    public void A_settings_file_that_cannot_be_written_is_reported_not_thrown_raw()
    {
        using var host = new CoreHost();
        var settings = host.Get<ISettingsService>();

        // A folder where config.json should be: the rename can never succeed.
        Directory.CreateDirectory(Path.Combine(host.Environment.DataDirectory, "config.json"));
        var error = Assert.Throws<GilSweepException>(() => settings.Update(s => s.World = "Siren"));

        Assert.Equal(GilSweepErrorKind.StorageFailed, error.Kind);
        Assert.StartsWith("Settings could not be saved", error.Message, StringComparison.Ordinal);
        Assert.Equal("Cactuar", settings.Current.World);
    }

    [Fact]
    public void The_first_run_ends_when_settings_are_saved()
    {
        using var host = new CoreHost();
        var settings = host.Get<ISettingsService>();
        Assert.True(settings.IsFirstRun);

        settings.Update(s => s.World = "Siren");

        Assert.False(settings.IsFirstRun);
    }

    [Fact]
    public void v1_settings_stars_and_history_are_imported_once()
    {
        using var host = new CoreHost();
        var legacy = host.Environment.LegacyDataDirectory;
        Directory.CreateDirectory(Path.Combine(legacy, "snapshots"));
        File.WriteAllText(Path.Combine(legacy, "config.json"), V1Config);
        File.WriteAllText(Path.Combine(legacy, "custom-items.json"), """[{"name":"Raw Ametrine","id":44012,"job":"MIN","level":95,"where":"Somewhere","kind":"node","expansion":"DT"}]""");
        foreach (var file in Directory.EnumerateFiles(Fixture.PathOf("V1", "archive")).Take(3))
        {
            File.Copy(file, Path.Combine(legacy, "snapshots", Path.GetFileName(file)));
        }

        var settings = host.Get<ISettingsService>();

        Assert.Equal(new LegacyImport(Settings: true, Watched: 2, Snapshots: 3, CustomItems: true), settings.Imported);
        Assert.False(settings.IsFirstRun);
        Assert.Equal("Gilgamesh", settings.Current.World);
        Assert.Null(settings.Current.HistoryRetentionDays);
        Assert.All(settings.Current.Watchlist, entry => Assert.True(entry.NodeOpens && entry.PriceSpike && entry.PriceCrash));
        Assert.Equal([44012, 5121], settings.Current.Watchlist.Select(entry => entry.ItemId));
        Assert.Equal(3, host.Get<IMarketSnapshotStore>().Stats().Count);
        Assert.True(File.Exists(Path.Combine(legacy, "config.json")));
        Assert.Null(new LegacyImporter(host.Environment, new ConfigStore(host.Environment), Microsoft.Extensions.Logging.Abstractions.NullLogger<LegacyImporter>.Instance).ImportIfNeeded());
    }

    [Fact]
    public void Without_v1_data_nothing_is_imported()
    {
        using var host = new CoreHost();
        Assert.Null(host.Get<ISettingsService>().Imported);
    }
}
