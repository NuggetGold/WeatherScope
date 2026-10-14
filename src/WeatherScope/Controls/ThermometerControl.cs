using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WeatherScope.Services;

namespace WeatherScope.Controls;

public sealed class ThermometerControl : FrameworkElement
{
    private const double Min = -40;
    private const double Max = 50;

    public static readonly DependencyProperty TemperatureProperty = DependencyProperty.Register(
        nameof(Temperature), typeof(double), typeof(ThermometerControl),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnTemperatureChanged));

    public static readonly DependencyProperty FeelsLikeProperty = DependencyProperty.Register(
        nameof(FeelsLike), typeof(double?), typeof(ThermometerControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentColorProperty = DependencyProperty.Register(
        nameof(AccentColor), typeof(Color), typeof(ThermometerControl),
        new FrameworkPropertyMetadata(Color.FromRgb(0x4E, 0xCD, 0xC4), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DisplayTemperatureProperty = DependencyProperty.Register(
        nameof(DisplayTemperature), typeof(double), typeof(ThermometerControl),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Typeface Face = new("Segoe UI");
    private static readonly Typeface FaceBold = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Brush TextBrush = Frozen(Color.FromRgb(0xE2, 0xE8, 0xF0));
    private static readonly Brush MutedBrush = Frozen(Color.FromRgb(0x94, 0xA3, 0xB8));
    private static readonly Brush GlassBrush = Frozen(Color.FromArgb(0x40, 0x1E, 0x29, 0x3B));
    private static readonly Brush HighlightBrush = Frozen(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
    private static readonly Pen BorderPen = FrozenPen(Color.FromRgb(0x33, 0x41, 0x55), 2);
    private static readonly Pen MinorPen = FrozenPen(Color.FromRgb(0x47, 0x55, 0x69), 1);
    private static readonly Pen MajorPen = FrozenPen(Color.FromRgb(0x94, 0xA3, 0xB8), 1.5);
    private static readonly Pen ZeroPen = FrozenPen(Color.FromRgb(0xE2, 0xE8, 0xF0), 2.5);

    private Color _cachedAccent;
    private Brush? _accentBrush;
    private Brush? _mercuryBrush;
    private double _cachedTop;
    private double _cachedBottom;

    public double Temperature
    {
        get => (double)GetValue(TemperatureProperty);
        set => SetValue(TemperatureProperty, value);
    }

    public double? FeelsLike
    {
        get => (double?)GetValue(FeelsLikeProperty);
        set => SetValue(FeelsLikeProperty, value);
    }

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    public double DisplayTemperature
    {
        get => (double)GetValue(DisplayTemperatureProperty);
        set => SetValue(DisplayTemperatureProperty, value);
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

    private static void OnTemperatureChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (ThermometerControl)d;
        double target = Math.Clamp((double)e.NewValue, Min, Max);
        if (!SystemParameters.ClientAreaAnimation)
        {
            c.BeginAnimation(DisplayTemperatureProperty, null);
            c.DisplayTemperature = target;
            return;
        }

        c.BeginAnimation(DisplayTemperatureProperty, new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(600),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsInfinity(availableSize.Width) ? 90 : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? 260 : availableSize.Height;
        return new Size(w, h);
    }

    private FormattedText Text(string s, double size, Brush brush, bool bold = false)
    {
        return new FormattedText(s, CultureInfo.GetCultureInfo("ru-RU"), FlowDirection.LeftToRight,
            bold ? FaceBold : Face, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    private static string Signed(int v)
    {
        if (v > 0)
        {
            return "+" + v.ToString(CultureInfo.InvariantCulture);
        }

        return v < 0 ? "−" + (-v).ToString(CultureInfo.InvariantCulture) : "0";
    }

    private void EnsureAccent(double top, double bottom)
    {
        var color = AccentColor;
        if (_mercuryBrush != null && _cachedAccent == color && _cachedTop == top && _cachedBottom == bottom)
        {
            return;
        }

        _cachedAccent = color;
        _cachedTop = top;
        _cachedBottom = bottom;
        _accentBrush = Frozen(color);
        var light = Color.FromRgb(
            (byte)Math.Min(255, color.R + (255 - color.R) * 0.55),
            (byte)Math.Min(255, color.G + (255 - color.G) * 0.55),
            (byte)Math.Min(255, color.B + (255 - color.B) * 0.55));
        var g = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(0, bottom),
            EndPoint = new Point(0, top)
        };
        g.GradientStops.Add(new GradientStop(color, 0));
        g.GradientStops.Add(new GradientStop(light, 1));
        g.Freeze();
        _mercuryBrush = g;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth;
        double h = ActualHeight;
        if (w < 30 || h < 100)
        {
            return;
        }

        double textH = Math.Min(h * 0.12, 30);
        double bulbR = Math.Min(w * 0.17, h * 0.07);
        double tubeW = bulbR * 1.0;
        double tubeR = tubeW / 2;
        double cx = w * 0.42;
        double scaleTop = textH + 14;
        double bulbCy = h - bulbR - 4;
        double scaleBottom = bulbCy - bulbR * 0.6;
        if (scaleBottom - scaleTop < 40)
        {
            return;
        }

        double YOf(double t) => scaleBottom - (Math.Clamp(t, Min, Max) - Min) / (Max - Min) * (scaleBottom - scaleTop);

        EnsureAccent(scaleTop, scaleBottom);

        var tubeRect = new Rect(cx - tubeR, scaleTop - tubeR, tubeW, bulbCy - scaleTop + tubeR);
        var glass = new CombinedGeometry(GeometryCombineMode.Union,
            new RectangleGeometry(tubeRect, tubeR, tubeR),
            new EllipseGeometry(new Point(cx, bulbCy), bulbR, bulbR));
        glass.Freeze();
        dc.DrawGeometry(GlassBrush, BorderPen, glass);

        double innerR = tubeR * 0.55;
        double bulbInner = bulbR * 0.8;
        double mercuryTop = YOf(DisplayTemperature);
        var mercury = new CombinedGeometry(GeometryCombineMode.Union,
            new RectangleGeometry(new Rect(cx - innerR, mercuryTop, innerR * 2, Math.Max(0, bulbCy - mercuryTop)), innerR, innerR),
            new EllipseGeometry(new Point(cx, bulbCy), bulbInner, bulbInner));
        mercury.Freeze();
        dc.DrawGeometry(_mercuryBrush, null, mercury);

        var hl = new Rect(cx - tubeR * 0.6, scaleTop + tubeR * 0.5, tubeR * 0.3, scaleBottom - scaleTop - tubeR);
        dc.DrawRoundedRectangle(HighlightBrush, null, hl, tubeR * 0.15, tubeR * 0.15);
        dc.DrawEllipse(HighlightBrush, null, new Point(cx - bulbR * 0.4, bulbCy - bulbR * 0.35), bulbR * 0.2, bulbR * 0.2);

        double fs = Math.Clamp(w * 0.11, 8, 12);
        double tickX = cx + tubeR + 4;
        for (int t = (int)Min; t <= (int)Max; t += 5)
        {
            double y = YOf(t);
            bool major = t % 10 == 0;
            Pen pen = t == 0 ? ZeroPen : major ? MajorPen : MinorPen;
            double len = t == 0 ? 11 : major ? 8 : 4;
            dc.DrawLine(pen, new Point(tickX, y), new Point(tickX + len, y));
            if (major)
            {
                var label = Text(Signed(t), fs, t == 0 ? TextBrush : MutedBrush, t == 0);
                dc.DrawText(label, new Point(tickX + 14, y - label.Height / 2));
            }
        }

        if (FeelsLike.HasValue)
        {
            double fy = YOf(FeelsLike.Value);
            double tx = cx - tubeR - 3;
            var tri = new StreamGeometry();
            using (var ctx = tri.Open())
            {
                ctx.BeginFigure(new Point(tx, fy), true, true);
                ctx.LineTo(new Point(tx - 8, fy - 5), true, false);
                ctx.LineTo(new Point(tx - 8, fy + 5), true, false);
            }

            tri.Freeze();
            dc.DrawGeometry(_accentBrush, null, tri);
            var fl = Text("ощущ. " + Formatting.Temperature(FeelsLike.Value).Replace("°", "") + "°", Math.Max(7, fs - 2), MutedBrush);
            double lx = Math.Max(0, tx - 10 - fl.Width);
            dc.DrawText(fl, new Point(lx, Math.Clamp(fy - fl.Height / 2, 0, h - fl.Height)));
        }

        var big = Text(Formatting.Temperature(Temperature), Math.Clamp(textH * 0.9, 12, 26), TextBrush, true);
        dc.DrawText(big, new Point((w - big.Width) / 2, 0));
    }
}
