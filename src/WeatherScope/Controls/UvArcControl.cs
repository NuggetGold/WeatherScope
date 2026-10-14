using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WeatherScope.Services;

namespace WeatherScope.Controls;

public sealed class UvArcControl : FrameworkElement
{
    private const double StartAngle = 160;
    private const double SweepAngle = 220;
    private const double MaxScale = 12;
    private const double GapDegrees = 2.2;

    public static readonly DependencyProperty UvIndexProperty = DependencyProperty.Register(
        nameof(UvIndex), typeof(double?), typeof(UvArcControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnUvChanged));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(UvArcControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AdviceProperty = DependencyProperty.Register(
        nameof(Advice), typeof(string), typeof(UvArcControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentColorProperty = DependencyProperty.Register(
        nameof(AccentColor), typeof(Color), typeof(UvArcControl),
        new FrameworkPropertyMetadata(Color.FromRgb(0x4E, 0xCD, 0xC4), FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly DependencyProperty DisplayValueProperty = DependencyProperty.Register(
        "DisplayValue", typeof(double), typeof(UvArcControl),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Typeface Regular = new("Segoe UI");
    private static readonly Typeface Bold = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("ru-RU");

    private static readonly Brush TextBrush = Make(Color.FromRgb(0xE2, 0xE8, 0xF0));
    private static readonly Brush MutedBrush = Make(Color.FromRgb(0x94, 0xA3, 0xB8));
    private static readonly Brush MarkerCoreBrush = Make(Color.FromRgb(0x13, 0x1B, 0x2E));
    private static readonly Pen TrackPen = MakeRoundPen(Color.FromRgb(0x1E, 0x29, 0x3B), 18);

    private static readonly (double From, double To, Color Color)[] Sectors =
    {
        (0, 3, Color.FromRgb(0x4E, 0xCD, 0xC4)),
        (3, 6, Color.FromRgb(0xF7, 0xD0, 0x60)),
        (6, 8, Color.FromRgb(0xFF, 0x9A, 0x3C)),
        (8, 11, Color.FromRgb(0xFF, 0x47, 0x57)),
        (11, 12, Color.FromRgb(0xA8, 0x55, 0xF7))
    };

    private static readonly Pen[] SectorPens = BuildSectorPens(1.0);
    private static readonly Pen[] DimPens = BuildSectorPens(0.35);

    private double _cachedSide;
    private StreamGeometry[]? _arcs;
    private Point _center;
    private double _radius;

    public UvArcControl()
    {
        IsHitTestVisible = false;
    }

    public double? UvIndex
    {
        get => (double?)GetValue(UvIndexProperty);
        set => SetValue(UvIndexProperty, value);
    }

    public string? Label
    {
        get => (string?)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Advice
    {
        get => (string?)GetValue(AdviceProperty);
        set => SetValue(AdviceProperty, value);
    }

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    private static Brush Make(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static Pen MakeRoundPen(Color c, double thickness)
    {
        var p = new Pen(Make(c), thickness) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
        p.Freeze();
        return p;
    }

    private static Pen[] BuildSectorPens(double opacity)
    {
        var pens = new Pen[Sectors.Length];
        for (var i = 0; i < Sectors.Length; i++)
        {
            var c = Sectors[i].Color;
            c.A = (byte)(255 * opacity);
            pens[i] = MakeRoundPen(c, 14);
        }
        return pens;
    }

    private static void OnUvChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (UvArcControl)d;
        var target = Math.Clamp(((double?)e.NewValue) ?? 0, 0, MaxScale);
        if (!SystemParameters.ClientAreaAnimation)
        {
            self.BeginAnimation(DisplayValueProperty, null);
            self.SetValue(DisplayValueProperty, target);
            return;
        }
        var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(600))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        self.BeginAnimation(DisplayValueProperty, anim);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 260 : availableSize.Width;
        var h = double.IsInfinity(availableSize.Height) ? 240 : availableSize.Height;
        return new Size(w, h);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        _arcs = null;
    }

    private FormattedText Text(string s, double size, Brush brush, Typeface face)
    {
        return new FormattedText(s, Culture, FlowDirection.LeftToRight, face, size, brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    private static Point OnCircle(Point c, double r, double deg)
    {
        var a = deg * Math.PI / 180;
        return new Point(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a));
    }

    private static double ValueToAngle(double v)
    {
        return StartAngle + SweepAngle * Math.Clamp(v, 0, MaxScale) / MaxScale;
    }

    private static StreamGeometry ArcGeometry(Point c, double r, double a0, double a1)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(OnCircle(c, r, a0), false, false);
            ctx.ArcTo(OnCircle(c, r, a1), new Size(r, r), 0, a1 - a0 > 180, SweepDirection.Clockwise, true, false);
        }
        g.Freeze();
        return g;
    }

    private void BuildArcs(Size size, double adviceHeight)
    {
        var availH = Math.Max(40, size.Height - adviceHeight);
        var radius = Math.Min(size.Width / 2 - 14, availH / 1.5 - 8);
        radius = Math.Max(radius, 20);
        var cy = 14 + radius;
        _center = new Point(size.Width / 2, Math.Min(cy, availH - radius * 0.35));
        if (_center.Y - radius < 10)
        {
            _center = new Point(_center.X, radius + 10);
        }
        _radius = radius;
        _arcs = new StreamGeometry[Sectors.Length];
        for (var i = 0; i < Sectors.Length; i++)
        {
            var a0 = ValueToAngle(Sectors[i].From) + (i == 0 ? 0 : GapDegrees / 2);
            var a1 = ValueToAngle(Sectors[i].To) - (i == Sectors.Length - 1 ? 0 : GapDegrees / 2);
            _arcs[i] = ArcGeometry(_center, radius, a0, a1);
        }
        _cachedSide = size.Width;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = new Size(ActualWidth, ActualHeight);
        if (size.Width < 40 || size.Height < 40)
        {
            return;
        }

        FormattedText? advice = null;
        var adviceText = Advice;
        var adviceHeight = 0.0;
        if (!string.IsNullOrWhiteSpace(adviceText))
        {
            advice = Text(adviceText, 12, MutedBrush, Regular);
            advice.MaxTextWidth = Math.Max(10, size.Width - 24);
            advice.MaxTextHeight = Math.Max(14, size.Height * 0.3);
            advice.Trimming = TextTrimming.CharacterEllipsis;
            advice.TextAlignment = TextAlignment.Center;
            adviceHeight = advice.Height + 10;
        }

        BuildArcs(size, adviceHeight);
        var arcs = _arcs!;
        var hasValue = UvIndex.HasValue;
        var shown = Math.Clamp(hasValue ? DisplayedValue() : 0, 0, MaxScale);

        dc.DrawRectangle(Brushes.Transparent, null, new Rect(size));

        for (var i = 0; i < arcs.Length; i++)
        {
            dc.DrawGeometry(null, hasValue ? SectorPens[i] : DimPens[i], arcs[i]);
        }

        var numText = hasValue ? Formatting.Number(UvIndex!.Value, "0.#") : "—";
        var num = Text(numText, Math.Clamp(_radius * 0.62, 20, 64), TextBrush, Bold);
        var label = Text(hasValue ? (string.IsNullOrWhiteSpace(Label) ? "" : Label!) : "Нет данных", Math.Clamp(_radius * 0.2, 11, 16), MutedBrush, Regular);
        var blockH = num.Height + label.Height;
        var top = _center.Y - blockH / 2 + _radius * 0.05;
        dc.DrawText(num, new Point(_center.X - num.Width / 2, top));
        dc.DrawText(label, new Point(_center.X - label.Width / 2, top + num.Height - 2));

        if (hasValue)
        {
            var angle = ValueToAngle(shown);
            var p = OnCircle(_center, _radius, angle);
            var color = ColorFor(shown);
            var ring = new SolidColorBrush(color);
            ring.Freeze();
            dc.DrawEllipse(Make(Color.FromArgb(0x55, color.R, color.G, color.B)), null, p, 13, 13);
            dc.DrawEllipse(MarkerCoreBrush, new Pen(ring, 3), p, 8, 8);
        }

        if (advice != null)
        {
            dc.DrawText(advice, new Point(12, size.Height - adviceHeight + 2));
        }
    }

    private double DisplayedValue()
    {
        return (double)GetValue(DisplayValueProperty);
    }

    private static Color ColorFor(double v)
    {
        for (var i = 0; i < Sectors.Length; i++)
        {
            if (v < Sectors[i].To)
            {
                return Sectors[i].Color;
            }
        }
        return Sectors[^1].Color;
    }
}
