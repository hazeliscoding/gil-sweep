using System.Globalization;

namespace GilSweep.Core;

/// <summary>Numbers and durations as the UI shows them: "1,842g", "1.06M", "7m 32s", "+12%".</summary>
public static class Formatting
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Gil(long gil) => gil.ToString("N0", Invariant) + "g";

    public static string Number(double value, int decimals = 0) => value.ToString("N" + decimals.ToString(Invariant), Invariant);

    /// <summary>Velocities: whole numbers from 10 up, one decimal below.</summary>
    public static string Rate(double perDay) => perDay >= 10 ? Number(perDay) : Number(perDay, 1);

    /// <summary>Large amounts in short form: 980, 12.4K, 1.06M.</summary>
    public static string Compact(double value) => Math.Abs(value) switch
    {
        >= 1_000_000_000 => (value / 1_000_000_000).ToString("0.##", Invariant) + "B",
        >= 1_000_000 => (value / 1_000_000).ToString("0.##", Invariant) + "M",
        >= 10_000 => (value / 1_000).ToString("0.#", Invariant) + "K",
        _ => value.ToString("N0", Invariant),
    };

    public static string SignedPercent(double percent) =>
        (percent > 0 ? "+" : percent < 0 ? "−" : "") + Math.Abs(percent).ToString(Math.Abs(percent) >= 10 ? "0" : "0.#", Invariant) + "%";

    /// <summary>A countdown: "7m 32s" under an hour, "1h 05m" from an hour.</summary>
    public static string Countdown(TimeSpan time)
    {
        var seconds = Math.Max(0, (long)Math.Round(time.TotalSeconds));
        return seconds >= 3600
            ? string.Create(Invariant, $"{seconds / 3600}h {seconds % 3600 / 60:00}m")
            : string.Create(Invariant, $"{seconds / 60}m {seconds % 60:00}s");
    }

    /// <summary>A short age: "just now", "4 min ago", "2h 14m ago", "3 days ago".</summary>
    public static string Ago(TimeSpan age)
    {
        if (age < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (age < TimeSpan.FromHours(1))
        {
            return string.Create(Invariant, $"{(int)age.TotalMinutes} min ago");
        }

        if (age < TimeSpan.FromDays(2))
        {
            return string.Create(Invariant, $"{(int)age.TotalHours}h {age.Minutes:00}m ago");
        }

        return string.Create(Invariant, $"{(int)age.TotalDays} days ago");
    }

    public static string Ordinal(int rank) => "#" + rank.ToString(Invariant);
}
