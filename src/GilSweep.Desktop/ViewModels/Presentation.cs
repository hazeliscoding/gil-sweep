using GilSweep.Core;
using GilSweep.Core.Catalog;
using GilSweep.Core.Sweep;
using GilSweep.Core.Time;
using GilSweep.Desktop.Controls;

namespace GilSweep.Desktop.ViewModels;

/// <summary>How domain states look: which tone, glyph and words. Shared by every screen so they agree.</summary>
public static class Presentation
{
    public static Tone GradeTone(OpportunityGrade grade) => grade switch
    {
        OpportunityGrade.Excellent => Tone.Healthy,
        OpportunityGrade.Good => Tone.Info,
        OpportunityGrade.Fair => Tone.Unknown,
        _ => Tone.Weak,
    };

    public static Tone TrendTone(TrendDirection direction) => direction switch
    {
        TrendDirection.Rising => Tone.Healthy,
        TrendDirection.Falling => Tone.Critical,
        _ => Tone.Unknown,
    };

    /// <summary>Text color for a percentage change: green up, red down, muted when flat.</summary>
    public static string ChangeBrushKey(double? percent) => percent switch
    {
        >= 3 => "HealthyText",
        <= -3 => "CriticalText",
        _ => "Text2",
    };

    public static string ChangeArrow(double? percent) => percent switch
    {
        >= 3 => "↑",
        <= -3 => "↓",
        null => "·",
        _ => "→",
    };

    public static Tone ReasonTone(ReasonTone tone) => tone switch
    {
        Core.Sweep.ReasonTone.Positive => Tone.Healthy,
        Core.Sweep.ReasonTone.Negative => Tone.Critical,
        Core.Sweep.ReasonTone.Waiting => Tone.Queued,
        _ => Tone.Unknown,
    };

    /// <summary>A node's dot: green when you can gather it, purple when it opens soon, a dashed ring when closed.</summary>
    public static Tone NodeTone(NodeAvailability node) => node.State switch
    {
        NodeState.Closed when node.RealRemaining <= TimeSpan.FromMinutes(20) => Tone.Queued,
        NodeState.Closed => Tone.Unknown,
        _ => Tone.Healthy,
    };

    public static string NodeBrushKey(NodeAvailability node) => node.State switch
    {
        NodeState.Open => "HealthyText",
        NodeState.AlwaysAvailable => "Text2",
        NodeState.Closed when node.RealRemaining <= TimeSpan.FromMinutes(20) => "QueuedText",
        _ => "Text3",
    };

    /// <summary>The node in a few words: "Available now", "Always available", "Opens in 7m 32s".</summary>
    public static string NodeWord(NodeAvailability node) => node.State switch
    {
        NodeState.Open => "Available now",
        NodeState.AlwaysAvailable => "Always available",
        _ => "Opens in " + Formatting.Countdown(node.RealRemaining),
    };

    /// <summary>The ranked list's short node column: "Always", "Open", "7m 32s".</summary>
    public static string NodeTiny(NodeAvailability node) => node.State switch
    {
        NodeState.Open => "Open",
        NodeState.AlwaysAvailable => "Always",
        _ => Formatting.Countdown(node.RealRemaining),
    };

    public static string NodeNote(NodeAvailability node) => node.State switch
    {
        NodeState.Open => $"{Math.Ceiling(node.EtMinutes):0} Eorzea min left · {Formatting.Countdown(node.RealRemaining)} real",
        NodeState.AlwaysAvailable => "No spawn window",
        _ => $"Next window ET {node.SpawnHour:00}:00",
    };

    /// <summary>"ET 10:00 and ET 22:00 · 2 ET hours (5m 50s)".</summary>
    public static string WindowText(CatalogItem item)
    {
        if (!item.IsTimed)
        {
            return "Regular node, always there";
        }

        var uptime = item.Uptime ?? SpawnSchedule.DefaultUptime;
        var hours = string.Join(" and ", item.Spawns!.Select(hour => $"ET {hour:00}:00"));
        return $"{hours} · {uptime / 60.0:0.#} ET hours ({Formatting.Countdown(EorzeaTime.RealDuration(uptime))})";
    }

    public static string Requirement(CatalogItem item)
    {
        var job = item.Job == Job.Either ? "Miner or Botanist" : item.Job.Name();
        var extra = item.Kind switch
        {
            ItemKind.Legendary => $" · {item.Expansion.Name()} folklore",
            ItemKind.Reduction => " · Aetherial Reduction",
            _ => "",
        };
        return $"{job} {item.Level}{extra}";
    }

    public static string JobLevel(CatalogItem item) => item.Job switch
    {
        Job.Either => $"Either job {item.Level}",
        _ => $"{item.Job.Name()} {item.Level}",
    };
}
