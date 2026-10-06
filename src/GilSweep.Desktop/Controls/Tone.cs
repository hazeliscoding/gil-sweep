using Avalonia;
using Avalonia.Media;

namespace GilSweep.Desktop.Controls;

/// <summary>Semantic colors from the design system. Each tone has a main, text, subtle and border brush.</summary>
public enum Tone
{
    Healthy,
    Warning,
    Critical,
    Info,
    Unknown,
    Paused,
    Queued,
    Accent,

    /// <summary>A weak grade: no fill, faint text, a plain border.</summary>
    Weak,
}

internal static class ToneBrushes
{
    public static IBrush Base(Tone tone) => Find(Key(tone));

    public static IBrush Text(Tone tone) => Find(tone == Tone.Accent ? "AccentText" : Key(tone) + "Text");

    public static IBrush Subtle(Tone tone) => Find(Key(tone) + "Subtle");

    public static IBrush Border(Tone tone) => Find(Key(tone) + "Border");

    public static IBrush Named(string key) => Find(key);

    private static string Key(Tone tone) => tone.ToString();

    private static IBrush Find(string key) =>
        Application.Current?.TryGetResource(key, null, out var value) == true && value is IBrush brush
            ? brush
            : Brushes.Gray;
}
