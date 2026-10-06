using System.Text.Json.Serialization;
using GilSweep.Core.Catalog;
using GilSweep.Core.Watchlist;

namespace GilSweep.Core.Configuration;

/// <summary>
/// Everything the user tells Gil Sweep about their character and how the app should behave.
/// Keys shared with v1's config.json keep v1's names, so a v1 file loads as-is. Properties use
/// setters with defaults: a key missing from an older file keeps its default.
/// </summary>
public sealed class GilSweepSettings
{
    public const int MaxLevel = 100;

    public static IReadOnlyList<string> CrafterJobs { get; } = ["CRP", "BSM", "ARM", "GSM", "LTW", "WVR", "ALC", "CUL"];

    public string World { get; set; } = "Cactuar";

    public GathererLevels Levels { get; set; } = new();

    public Expansion MsqExpansion { get; set; } = Expansion.EW;

    /// <summary>Expansions whose folklore book the character owns; legendary nodes need it.</summary>
    public List<Expansion> Folklore { get; set; } = [];

    /// <summary>Crafter levels by job abbreviation. A job missing here counts as level 100, as in v1.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, int> Crafters { get; set; } = CrafterJobs.ToDictionary(job => job, _ => MaxLevel);

    /// <summary>Closing the window keeps Gil Sweep in the tray, where node and price alerts keep working.</summary>
    public bool CloseToTray { get; set; } = true;

    /// <summary>Sweep again every hour while the app runs.</summary>
    public bool AutoSweep { get; set; } = true;

    /// <summary>Ask Saddlebag Exchange for trend states. Off means Universalis only.</summary>
    public bool UseSaddlebag { get; set; } = true;

    public SaddlebagQuery Saddlebag { get; set; } = new();

    public List<WatchEntry> Watchlist { get; set; } = [];

    public AlertThresholds Alerts { get; set; } = new();

    /// <summary>Your retainers' names, to notice when someone lists below you. Stays on this PC.</summary>
    public List<string> RetainerNames { get; set; } = [];

    public bool CheckForUpdates { get; set; } = true;

    /// <summary>Snapshots older than this many days are deleted. Null keeps everything.</summary>
    public int? HistoryRetentionDays { get; set; } = 90;

    /// <summary>v1's starred item ids. Read once when a v1 config is imported, never written.</summary>
    [JsonPropertyName("watched")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? LegacyWatched { get; set; }

    /// <summary>The level that counts for an item: the job's level, or the higher of the two for "either".</summary>
    public int LevelFor(Job job) => job switch
    {
        Job.Miner => Levels.Miner,
        Job.Botanist => Levels.Botanist,
        _ => Math.Max(Levels.Miner, Levels.Botanist),
    };

    public int CrafterLevel(string job) => Crafters.TryGetValue(job, out var level) ? level : MaxLevel;

    public WatchEntry? WatchOf(int itemId) => Watchlist.FirstOrDefault(entry => entry.ItemId == itemId);

    public GilSweepSettings Clone() => new()
    {
        World = World,
        Levels = new GathererLevels { Miner = Levels.Miner, Botanist = Levels.Botanist },
        MsqExpansion = MsqExpansion,
        Folklore = [.. Folklore],
        Crafters = new Dictionary<string, int>(Crafters),
        CloseToTray = CloseToTray,
        AutoSweep = AutoSweep,
        UseSaddlebag = UseSaddlebag,
        Saddlebag = new SaddlebagQuery
        {
            TimePeriod = Saddlebag.TimePeriod,
            SalesAmount = Saddlebag.SalesAmount,
            AveragePrice = Saddlebag.AveragePrice,
            Filters = [.. Saddlebag.Filters],
        },
        Watchlist = [.. Watchlist.Select(entry => entry.Clone())],
        Alerts = new AlertThresholds
        {
            SpikePercent = Alerts.SpikePercent,
            CrashPercent = Alerts.CrashPercent,
            NodeLeadMinutes = Alerts.NodeLeadMinutes,
        },
        RetainerNames = [.. RetainerNames],
        CheckForUpdates = CheckForUpdates,
        HistoryRetentionDays = HistoryRetentionDays,
    };
}

public sealed class GathererLevels
{
    [JsonPropertyName("MIN")]
    public int Miner { get; set; } = 89;

    [JsonPropertyName("BTN")]
    public int Botanist { get; set; } = 70;
}

/// <summary>The Saddlebag Exchange market-share query (v1's defaults).</summary>
public sealed class SaddlebagQuery
{
    /// <summary>Hours of sales to consider.</summary>
    public int TimePeriod { get; set; } = 168;

    public int SalesAmount { get; set; } = 2;

    public int AveragePrice { get; set; } = 50;

    /// <summary>Item search categories: 47 ores, 48 logs, 49 reagents.</summary>
    public List<int> Filters { get; set; } = [47, 48, 49];
}

/// <summary>When a watched item is worth a notification.</summary>
public sealed class AlertThresholds
{
    /// <summary>A rise of at least this much since the previous sweep (v1: 25%).</summary>
    public int SpikePercent { get; set; } = 25;

    /// <summary>A drop of at least this much since the previous sweep (v1: 25%).</summary>
    public int CrashPercent { get; set; } = 25;

    /// <summary>How many real minutes before a watched node opens to send the reminder. 0 = when it opens, as in v1.</summary>
    public int NodeLeadMinutes { get; set; } = 5;
}
