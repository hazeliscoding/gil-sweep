using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace GilSweep.Desktop.Controls;

/// <summary>The small spinner used inside loading buttons: a ring with one open side.</summary>
public sealed class Spinner : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        Avalonia.Controls.Documents.TextElement.ForegroundProperty.AddOwner<Spinner>();

    static Spinner() => AffectsRender<Spinner>(ForegroundProperty);

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        var radius = (size - 1.5) / 2;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(new Point(center.X, center.Y - radius), false);
            path.ArcTo(new Point(center.X - radius, center.Y), new Size(radius, radius), 0, true, SweepDirection.Clockwise);
            path.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(Foreground ?? Brushes.Gray, 1.5), geometry);
    }
}
