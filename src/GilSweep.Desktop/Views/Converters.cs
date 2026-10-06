using Avalonia.Data.Converters;
using Avalonia.Media;
using GilSweep.Desktop.Controls;

namespace GilSweep.Desktop.Views;

public static class Converters
{
    /// <summary>A brush from the theme by its token name, such as "HealthyText".</summary>
    public static readonly IValueConverter Brush = new FuncValueConverter<string?, IBrush>(key => ToneBrushes.Named(key ?? "Text2"));
}
