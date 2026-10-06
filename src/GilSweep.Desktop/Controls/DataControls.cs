using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace GilSweep.Desktop.Controls;

/// <summary>A small tinted label: a grade (EXCELLENT) or a lock reason. Shown in capitals; read as written.</summary>
public sealed class Pill : ToneControl
{
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<Pill, string?>(nameof(Label));

    public static readonly StyledProperty<string?> IconProperty = AvaloniaProperty.Register<Pill, string?>(nameof(Icon));

    public static readonly DirectProperty<Pill, string?> TextProperty =
        AvaloniaProperty.RegisterDirect<Pill, string?>(nameof(Text), pill => pill.Text);

    private string? _text;

    public Pill() => AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Text);

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Text
    {
        get => _text;
        private set => SetAndRaise(TextProperty, ref _text, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            Text = Label?.ToUpperInvariant();
            AutomationProperties.SetName(this, Label);
        }
    }
}

/// <summary>A thin bar showing how much of a signal's maximum an item earned.</summary>
public sealed class Meter : TemplatedControl
{
    public static readonly StyledProperty<double> FractionProperty = AvaloniaProperty.Register<Meter, double>(nameof(Fraction));

    static Meter() => AffectsRender<Meter>(FractionProperty, BackgroundProperty, ForegroundProperty);

    public double Fraction
    {
        get => GetValue(FractionProperty);
        set => SetValue(FractionProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.DrawRectangle(Background, null, bounds, 2, 2);
        var width = bounds.Width * Math.Clamp(Fraction, 0, 1);
        if (width > 0)
        {
            context.DrawRectangle(Foreground, null, new Rect(0, 0, width, bounds.Height), 2, 2);
        }
    }
}

/// <summary>A loading placeholder that shimmers while data arrives.</summary>
public sealed class Skeleton : TemplatedControl;

/// <summary>A tiny chart: a line (price history) or bars (sales by hour).</summary>
public sealed class Sparkline : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<Sparkline, IReadOnlyList<double>?>(nameof(Values));

    public static readonly StyledProperty<bool> BarsProperty = AvaloniaProperty.Register<Sparkline, bool>(nameof(Bars));

    public static readonly StyledProperty<IBrush?> StrokeProperty = AvaloniaProperty.Register<Sparkline, IBrush?>(nameof(Stroke));

    /// <summary>Bars at these indexes are drawn in <see cref="HighlightProperty"/> (the peak hours).</summary>
    public static readonly StyledProperty<IReadOnlyList<int>?> HighlightedProperty =
        AvaloniaProperty.Register<Sparkline, IReadOnlyList<int>?>(nameof(Highlighted));

    public static readonly StyledProperty<IBrush?> HighlightProperty = AvaloniaProperty.Register<Sparkline, IBrush?>(nameof(Highlight));

    static Sparkline() => AffectsRender<Sparkline>(ValuesProperty, BarsProperty, StrokeProperty, HighlightedProperty, HighlightProperty);

    public IReadOnlyList<double>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public bool Bars
    {
        get => GetValue(BarsProperty);
        set => SetValue(BarsProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public IReadOnlyList<int>? Highlighted
    {
        get => GetValue(HighlightedProperty);
        set => SetValue(HighlightedProperty, value);
    }

    public IBrush? Highlight
    {
        get => GetValue(HighlightProperty);
        set => SetValue(HighlightProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Values is not { Count: > 0 } values || Stroke is not { } stroke)
        {
            return;
        }

        var width = Bounds.Width;
        var height = Bounds.Height;
        if (Bars)
        {
            var max = Math.Max(values.Max(), 1e-9);
            var slot = width / values.Count;
            var gap = Math.Min(2, slot * 0.25);
            for (var i = 0; i < values.Count; i++)
            {
                // A sliver for empty hours, so the axis still reads as 24 slots.
                var barHeight = Math.Max(2, values[i] / max * height);
                var brush = Highlighted?.Contains(i) == true && Highlight is { } highlight ? highlight : stroke;
                context.DrawRectangle(brush, null, new Rect(i * slot + gap / 2, height - barHeight, slot - gap, barHeight), 1, 1);
            }

            return;
        }

        if (values.Count < 2)
        {
            return;
        }

        var low = values.Min();
        var high = values.Max();
        var span = high - low < 1e-9 ? 1 : high - low;
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            for (var i = 0; i < values.Count; i++)
            {
                var point = new Point(i * (width - 2) / (values.Count - 1) + 1, height - 1 - (values[i] - low) / span * (height - 2));
                if (i == 0)
                {
                    path.BeginFigure(point, false);
                }
                else
                {
                    path.LineTo(point);
                }
            }

            path.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(stroke, 1.25, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
    }
}

/// <summary>One point of a price chart.</summary>
public sealed record ChartPoint(DateTimeOffset At, double Value);

/// <summary>
/// The Market screen's price history: a gold line with a faint fill, three gridlines with gil
/// labels, dates along the bottom, and a crosshair with the day's value under the pointer.
/// </summary>
public sealed class PriceChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<ChartPoint>?> PointsProperty =
        AvaloniaProperty.Register<PriceChart, IReadOnlyList<ChartPoint>?>(nameof(Points));

    private const double AxisWidth = 64;
    private const double LabelHeight = 18;
    private int? _hover;

    static PriceChart() => AffectsRender<PriceChart>(PointsProperty);

    public PriceChart() => ClipToBounds = true;

    public IReadOnlyList<ChartPoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Points is not { Count: > 1 } points)
        {
            return;
        }

        var x = e.GetPosition(this).X - AxisWidth;
        var plotWidth = Math.Max(1, Bounds.Width - AxisWidth - 8);
        var index = (int)Math.Round(x / plotWidth * (points.Count - 1));
        index = Math.Clamp(index, 0, points.Count - 1);
        if (index != _hover)
        {
            _hover = index;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = null;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (Points is not { Count: > 1 } points)
        {
            return;
        }

        var series = Brush("Series1");
        var grid = Brush("ChartGrid");
        var axis = Brush("ChartAxis");
        var text1 = Brush("Text1");
        var mono = Application.Current?.TryGetResource("MonoFont", null, out var font) == true && font is FontFamily family
            ? new Typeface(family)
            : new Typeface("Consolas");

        var plot = new Rect(AxisWidth, 6, Math.Max(1, Bounds.Width - AxisWidth - 8), Math.Max(1, Bounds.Height - LabelHeight - 10));
        var low = points.Min(point => point.Value);
        var high = points.Max(point => point.Value);
        var pad = Math.Max((high - low) * 0.12, Math.Max(high * 0.01, 1));
        low = Math.Max(0, low - pad);
        high += pad;

        double X(int i) => plot.X + i * plot.Width / (points.Count - 1);
        double Y(double value) => plot.Bottom - (value - low) / (high - low) * plot.Height;

        for (var line = 0; line < 3; line++)
        {
            var value = low + (high - low) * line / 2;
            var y = Y(value);
            context.DrawLine(new Pen(grid, 1), new Point(plot.X, y), new Point(plot.Right, y));
            var label = Text(Gil(value), mono, 11, axis);
            context.DrawText(label, new Point(AxisWidth - 10 - label.Width, y - label.Height / 2));
        }

        foreach (var i in new[] { 0, points.Count / 2, points.Count - 1 }.Distinct())
        {
            var label = Text(points[i].At.ToLocalTime().ToString("MMM d", CultureInfo.InvariantCulture), mono, 11, axis);
            var x = Math.Clamp(X(i) - label.Width / 2, plot.X, plot.Right - label.Width);
            context.DrawText(label, new Point(x, plot.Bottom + 6));
        }

        var line2 = new StreamGeometry();
        var fill = new StreamGeometry();
        using (var path = line2.Open())
        using (var area = fill.Open())
        {
            area.BeginFigure(new Point(X(0), plot.Bottom), true);
            for (var i = 0; i < points.Count; i++)
            {
                var point = new Point(X(i), Y(points[i].Value));
                if (i == 0)
                {
                    path.BeginFigure(point, false);
                }
                else
                {
                    path.LineTo(point);
                }

                area.LineTo(point);
            }

            path.EndFigure(false);
            area.LineTo(new Point(X(points.Count - 1), plot.Bottom));
            area.EndFigure(true);
        }

        var color = (series as ISolidColorBrush)?.Color ?? Colors.Goldenrod;
        context.DrawGeometry(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.FromArgb(56, color.R, color.G, color.B), 0), new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1) },
        }, null, fill);
        context.DrawGeometry(null, new Pen(series, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), line2);

        if (_hover is { } hover)
        {
            var at = new Point(X(hover), Y(points[hover].Value));
            context.DrawLine(new Pen(Brush("Border3"), 1, new DashStyle([3, 3], 0)), new Point(at.X, plot.Y), new Point(at.X, plot.Bottom));
            context.DrawEllipse(series, new Pen(Brush("Bg1"), 2), at, 4, 4);
            var label = Text($"{points[hover].At.ToLocalTime():MMM d} · {Gil(points[hover].Value)}", mono, 11, text1);
            var box = new Rect(Math.Clamp(at.X - label.Width / 2 - 6, plot.X, plot.Right - label.Width - 12), plot.Y, label.Width + 12, label.Height + 6);
            context.DrawRectangle(Brush("Bg3"), new Pen(Brush("Border2"), 1), box, 4, 4);
            context.DrawText(label, new Point(box.X + 6, box.Y + 3));
        }
    }

    private static string Gil(double value) => value.ToString("N0", CultureInfo.InvariantCulture) + "g";

    private static FormattedText Text(string text, Typeface typeface, double size, IBrush brush) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, brush);

    private static IBrush Brush(string key) =>
        Application.Current?.TryGetResource(key, null, out var value) == true && value is IBrush brush ? brush : Brushes.Gray;
}
