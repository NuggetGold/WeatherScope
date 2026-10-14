using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WeatherScope.Services;

namespace WeatherScope.Controls;

public sealed class CompassControl : FrameworkElement
{
    public static readonly DependencyProperty WindSpeedProperty = DependencyProperty.Register(
        nameof(WindSpeed), typeof(double), typeof(CompassControl),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty WindGustProperty = DependencyProperty.Register(
        nameof(WindGust), typeof(double?), typeof(CompassControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ArrowDegreesProperty = DependencyProperty.Register(
        nameof(ArrowDegrees), typeof(double?), typeof(CompassControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnArrowChanged));

    public static readonly DependencyProperty IsCalmProperty = DependencyProperty.Register(
        nameof(IsCalm), typeof(bool), typeof(CompassControl),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentColorProperty = DependencyProperty.Register(
        nameof(AccentColor), typeof(Color), typeof(CompassControl),
        new FrameworkPropertyMetadata(Color.FromRgb(0x4E, 0xCD, 0xC4), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DisplayAngleProperty = DependencyProperty.Register(
        nameof(DisplayAngle), typeof(double), typeof(CompassControl),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Typeface Face = new("Segoe UI");
    private static readonly Typeface FaceBold = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Brush BgOuter = Frozen(Color.FromRgb(0x13, 0x1B, 0x2E));
    private static readonly Brush BgInner = Frozen(Color.FromRgb(0x1E, 0x29, 0x3B));
    private static readonly Brush TextBrush = Frozen(Color.FromRgb(0xE2, 0xE8, 0xF0));
    private static readonly Brush MutedBrush = Frozen(Color.FromRgb(0x94, 0xA3, 0xB8));
    private static readonly Pen BorderPen = FrozenPen(Color.FromRgb(0x33, 0x41, 0x55), 2);
    private static readonly Pen MinorPen = FrozenPen(Color.FromRgb(0x47, 0x55, 0x69), 1);
    private static readonly Pen MidPen = FrozenPen(Color.FromRgb(0x94, 0xA3, 0xB8), 1.5);
    private static readonly Pen MajorPen = FrozenPen(Color.FromRgb(0xE2, 0xE8, 0xF0), 2.5);
    private static readonly Brush BadgeBrush = Frozen(Color.FromRgb(0x13, 0x1B, 0x2E));

    private static readonly string[] Labels = { "С", "СВ", "В", "ЮВ", "Ю", "ЮЗ", "З", "СЗ" };

    private Color _cachedAccent;
    private Brush? _accentBrush;
    private Brush? _tailBrush;
    private Pen? _accentPen;

    public double WindSpeed
    {
        get => (double)GetValue(WindSpeedProperty);
        set => SetValue(WindSpeedProperty, value);
    }

    public double? WindGust
    {
        get => (double?)GetValue(WindGustProperty);
        set => SetValue(WindGustProperty, value);
    }

    public double? ArrowDegrees
    {
        get => (double?)GetValue(ArrowDegreesProperty);
        set => SetValue(ArrowDegreesProperty, value);
    }

    public bool IsCalm
    {
        get => (bool)GetValue(IsCalmProperty);
        set => SetValue(IsCalmProperty, value);
    }

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    public double DisplayAngle
    {
        get => (double)GetValue(DisplayAngleProperty);
        set => SetValue(DisplayAngleProperty, value);
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

    private static void OnArrowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (CompassControl)d;
        if (e.NewValue is not double target)
        {
            return;
        }

        if (e.OldValue is not double || !SystemParameters.ClientAreaAnimation)
        {
            c.BeginAnimation(DisplayAngleProperty, null);
            c.DisplayAngle = target;
            return;
        }

        double current = c.DisplayAngle;
        double delta = ((target - current) % 360 + 540) % 360 - 180;
        c.BeginAnimation(DisplayAngleProperty, new DoubleAnimation
        {
            To = current + delta,
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

    private static Point Polar(Point c, double radius, double deg)
    {
        double a = deg * Math.PI / 180;
        return new Point(c.X + radius * Math.Sin(a), c.Y - radius * Math.Cos(a));
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
        var tail = Color.FromArgb(0xB0, 0x94, 0xA3, 0xB8);
        _tailBrush = Frozen(tail);
        var p = new Pen(_accentBrush, 1) { LineJoin = PenLineJoin.Round };
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

        double tickOuter = r - side * 0.025;
        for (int i = 0; i < 72; i++)
        {
            double deg = i * 5;
            Pen pen;
            double len;
            if (i % 9 == 0)
            {
                pen = MajorPen;
                len = side * 0.07;
            }
            else if (i % 3 == 0)
            {
                pen = MidPen;
                len = side * 0.05;
            }
            else
            {
                pen = MinorPen;
                len = side * 0.03;
            }

            dc.DrawLine(pen, Polar(center, tickOuter, deg), Polar(center, tickOuter - len, deg));
        }

        double labelR = r - side * 0.145;
        for (int i = 0; i < 8; i++)
        {
            bool cardinal = i % 2 == 0;
            Brush brush = i == 0 ? _accentBrush! : cardinal ? TextBrush : MutedBrush;
            Centered(dc, Labels[i], Polar(center, labelR, i * 45), side * (cardinal ? 0.085 : 0.058), brush, cardinal);
        }

        if (!IsCalm && ArrowDegrees.HasValue)
        {
            double len = r * 0.66;
            double half = side * 0.045;
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(center.X, center.Y - len), true, true);
                ctx.LineTo(new Point(center.X + half, center.Y), true, false);
                ctx.LineTo(new Point(center.X - half, center.Y), true, false);
            }

            geo.Freeze();
            var tailGeo = new StreamGeometry();
            using (var ctx = tailGeo.Open())
            {
                ctx.BeginFigure(new Point(center.X + half, center.Y), true, true);
                ctx.LineTo(new Point(center.X, center.Y + len * 0.7), true, false);
                ctx.LineTo(new Point(center.X - half, center.Y), true, false);
            }

            tailGeo.Freeze();
            dc.PushTransform(new RotateTransform(DisplayAngle, center.X, center.Y));
            dc.DrawGeometry(_tailBrush, null, tailGeo);
            dc.DrawGeometry(_accentBrush, _accentPen, geo);
            dc.Pop();
        }

        double br = side * 0.19;
        dc.DrawEllipse(BadgeBrush, BorderPen, center, br, br);
        if (IsCalm)
        {
            Centered(dc, "Штиль", center, side * 0.07, TextBrush, true);
            return;
        }

        bool gust = WindGust.HasValue && WindGust.Value > 0;
        double dy = gust ? side * 0.025 : 0;
        Centered(dc, Formatting.Number(WindSpeed), new Point(center.X, center.Y - side * 0.035 - dy * 0.2), side * 0.105, TextBrush, true);
        Centered(dc, "м/с", new Point(center.X, center.Y + side * 0.045 - dy), side * 0.05, MutedBrush);
        if (gust)
        {
            Centered(dc, "порывы " + Formatting.Number(WindGust!.Value), new Point(center.X, center.Y + side * 0.115), side * 0.04, _accentBrush!);
        }
    }
}
