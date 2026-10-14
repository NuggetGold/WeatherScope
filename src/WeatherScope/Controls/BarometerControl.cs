using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WeatherScope.Controls;

public sealed class BarometerControl : FrameworkElement
{
    private const double Min = 700;
    private const double Max = 800;
    private const double StartAngle = 150;
    private const double SweepAngle = 240;

    public static readonly DependencyProperty PressureProperty = DependencyProperty.Register(
        nameof(Pressure), typeof(double), typeof(BarometerControl),
        new FrameworkPropertyMetadata(750.0, FrameworkPropertyMetadataOptions.AffectsRender, OnPressureChanged));

    public static readonly DependencyProperty TrendTextProperty = DependencyProperty.Register(
        nameof(TrendText), typeof(string), typeof(BarometerControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentColorProperty = DependencyProperty.Register(
        nameof(AccentColor), typeof(Color), typeof(BarometerControl),
        new FrameworkPropertyMetadata(Color.FromRgb(0x4E, 0xCD, 0xC4), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DisplayPressureProperty = DependencyProperty.Register(
        nameof(DisplayPressure), typeof(double), typeof(BarometerControl),
        new FrameworkPropertyMetadata(750.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Typeface Face = new("Segoe UI");
    private static readonly Typeface FaceBold = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Brush TextBrush = Frozen(Color.FromRgb(0xE2, 0xE8, 0xF0));
    private static readonly Brush MutedBrush = Frozen(Color.FromRgb(0x94, 0xA3, 0xB8));
    private static readonly Brush HubBrush = Frozen(Color.FromRgb(0x13, 0x1B, 0x2E));
    private static readonly Pen BorderPen = FrozenPen(Color.FromRgb(0x33, 0x41, 0x55), 2);
    private static readonly Pen MinorPen = FrozenPen(Color.FromRgb(0x64, 0x74, 0x8B), 1);
    private static readonly Pen MajorPen = FrozenPen(Color.FromRgb(0xE2, 0xE8, 0xF0), 2);

    private static readonly Color LowColor = Color.FromRgb(0x46, 0xB4, 0xDC);
    private static readonly Color NormalColor = Color.FromRgb(0xA8, 0xE0, 0x63);
    private static readonly Color HighColor = Color.FromRgb(0xFF, 0x9A, 0x3C);

    private static readonly (double From, double To, Color Color, string Label)[] Zones =
    {
        (700, 750, LowColor, "низкое"),
        (750, 765, NormalColor, "норма"),
        (765, 800, HighColor, "повышенное")
    };

    private Color _cachedAccent;
    private Brush? _accentBrush;
    private Pen? _accentPen;

    public double Pressure
    {
        get => (double)GetValue(PressureProperty);
        set => SetValue(PressureProperty, value);
    }

    public string? TrendText
    {
        get => (string?)GetValue(TrendTextProperty);
        set => SetValue(TrendTextProperty, value);
    }

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    public double DisplayPressure
    {
        get => (double)GetValue(DisplayPressureProperty);
        set => SetValue(DisplayPressureProperty, value);
    }

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static Pen FrozenPen(Color c, double w)
    {
        var p = new Pen(Frozen(c), w) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        p.Freeze();
        return p;
    }

    private static void OnPressureChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (BarometerControl)d;
        double target = Math.Clamp((double)e.NewValue, Min, Max);
        if (!SystemParameters.ClientAreaAnimation)
        {
            c.BeginAnimation(DisplayPressureProperty, null);
            c.DisplayPressure = target;
            return;
        }

        c.BeginAnimation(DisplayPressureProperty, new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(600),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? 200 : availableSize.Height;
        return new Size(w, h);
    }

    private FormattedText Text(string s, double size, Brush brush, bool bold = false)
    {
        return new FormattedText(s, CultureInfo.GetCultureInfo("ru-RU"), FlowDirection.LeftToRight,
            bold ? FaceBold : Face, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    private void Centered(DrawingContext dc, string s, Point p, double size, Brush brush, bool bold = false)
    {
        var t = Text(s, size, brush, bold);
        dc.DrawText(t, new Point(p.X - t.Width / 2, p.Y - t.Height / 2));
    }

    private static double AngleOf(double value)
    {
        return StartAngle + (Math.Clamp(value, Min, Max) - Min) / (Max - Min) * SweepAngle;
    }

    private static Point At(Point c, double radius, double deg)
    {
        double a = deg * Math.PI / 180;
        return new Point(c.X + radius * Math.Cos(a), c.Y + radius * Math.Sin(a));
    }

    private static Geometry Arc(Point c, double radius, double from, double to)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(At(c, radius, from), false, false);
            ctx.ArcTo(At(c, radius, to), new Size(radius, radius), 0, to - from > 180, SweepDirection.Clockwise, true, false);
        }

        geo.Freeze();
        return geo;
    }

    private void EnsureAccent()
    {
        var color = AccentColor;
        if (_accentBrush != null && _cachedAccent == color)
        {
            return;
        }

        _cachedAccent = color;
        _accentBrush = Frozen(color);
        var p = new Pen(_accentBrush, 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Triangle };
        p.Freeze();
        _accentPen = p;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double side = Math.Min(ActualWidth, ActualHeight);
        if (side < 20)
        {
            return;
        }

        EnsureAccent();
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        double r = side / 2 - 3;

        var bg = new RadialGradientBrush(Color.FromRgb(0x1E, 0x29, 0x3B), Color.FromRgb(0x13, 0x1B, 0x2E));
        bg.Freeze();
        dc.DrawEllipse(bg, BorderPen, center, r, r);

        double zoneR = r * 0.80;
        double zoneW = side * 0.035;
        foreach (var z in Zones)
        {
            var pen = new Pen(Frozen(z.Color), zoneW) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
            pen.Freeze();
            dc.DrawGeometry(null, pen, Arc(center, zoneR, AngleOf(z.From), AngleOf(z.To)));
        }

        foreach (var z in Zones)
        {
            double mid = AngleOf((z.From + z.To) / 2);
            Centered(dc, z.Label, At(center, r * 0.93 - side * 0.0, mid), side * 0.032, Frozen(z.Color));
        }

        double innerR = zoneR - zoneW * 0.7;
        for (int v = 700; v <= 800; v += 5)
        {
            bool major = v % 10 == 0;
            double a = AngleOf(v);
            double len = major ? side * 0.05 : side * 0.028;
            dc.DrawLine(major ? MajorPen : MinorPen, At(center, innerR, a), At(center, innerR - len, a));
            if (major)
            {
                Centered(dc, v.ToString(CultureInfo.InvariantCulture), At(center, innerR - side * 0.095, a), side * 0.04, MutedBrush);
            }
        }

        double angle = AngleOf(DisplayPressure);
        double needleLen = zoneR - side * 0.02;
        var tip = At(center, needleLen, angle);
        var tailEnd = At(center, -side * 0.09, angle);
        dc.DrawLine(_accentPen!, tailEnd, tip);
        dc.DrawEllipse(_accentBrush, null, tailEnd, side * 0.026, side * 0.026);
        dc.DrawEllipse(HubBrush, BorderPen, center, side * 0.035, side * 0.035);
        dc.DrawEllipse(_accentBrush, null, center, side * 0.014, side * 0.014);

        Centered(dc, Pressure.ToString("0", CultureInfo.InvariantCulture) + " мм рт. ст.",
            new Point(center.X, center.Y + side * 0.2), side * 0.065, TextBrush, true);
        if (!string.IsNullOrWhiteSpace(TrendText))
        {
            Centered(dc, TrendText!, new Point(center.X, center.Y + side * 0.28), side * 0.045, MutedBrush);
        }
    }
}
