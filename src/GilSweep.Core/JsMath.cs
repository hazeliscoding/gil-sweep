using System.Globalization;

namespace GilSweep.Core;

/// <summary>
/// v1 rounded with JavaScript's Math.round and Number.toFixed. Stored figures (prices, velocities,
/// percentages) must round the same way, or v1 and v2 snapshots would disagree by a gil.
/// </summary>
internal static class JsMath
{
    /// <summary>Math.round: halves round up, toward positive infinity.</summary>
    public static long Round(double value) => (long)Math.Floor(value + 0.5);

    /// <summary>+value.toFixed(digits): rounds the exact binary value, halves away from zero.</summary>
    public static double ToFixed(double value, int digits) =>
        double.Parse(value.ToString("F" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
}
