using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WeatherScope.Models;
using WeatherScope.Services;

namespace WeatherScope.Controls;

public sealed class HourlyChartControl : FrameworkElement
{
    private const int MaxPoints = 24;
    private const double PadLeft = 18;
    private const double PadRight = 18;
    private const double PadTop = 30;
    private const double PadBottom = 26;

    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points), typeof(IReadOnlyList<ChartPoint>), typeof(HourlyChartControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnPointsChanged));

    public static readonly DependencyProperty AccentColorProperty = DependencyProperty.Register(
        nameof(AccentColor), typeof(Color), typeof(HourlyChartControl),
        new FrameworkPropertyMetadata(Color.FromRgb(0x4E, 0xCD, 0xC4), FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly DependencyProperty RevealProperty = DependencyProperty.Register(
        "Reveal", typeof(double), typeof(HourlyChartControl),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Typeface Regular = new("Segoe UI");
    private static readonly Typeface Bold = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("ru-RU");

    private static readonly Brush TextBrush = Make(Color.FromRgb(0xE2, 0xE8, 0xF0));
    private static readonly Brush MutedBrush = Make(Color.FromRgb(0x94, 0xA3, 0xB8));
    private static readonly Brush TooltipBrush = Make(Color.FromArgb(0xEE, 0x1E, 0x29, 0x3B));
    private static readonly Brush PointCoreBrush = Make(Color.FromRgb(0x13, 0x1B, 0x2E));
    private static readonly Pen GridPen = MakePen(Color.FromArgb(0x70, 0x33, 0x41, 0x55), 1);
    private static readonly Pen TooltipPen = MakePen(Color.FromRgb(0x33, 0x41, 0x55), 1);
    private static readonly Pen GuidePen = MakePen(Color.FromArgb(0xA0, 0x94, 0xA3, 0xB8), 1, true);

    private Color _brushColor;
    private bool _brushesReady;
    private Brush _accentBrush = Brushes.Transparent;
    private Brush _fillBrush = Brushes.Transparent;
    private Pen _linePen = new(Brushes.Transparent, 1);
    private Pen _pointPen = new(Brushes.Transparent, 1);

    private Point[] _pts = Array.Empty<Point>();
    private StreamGeometry? _line;
    private StreamGeometry? _fill;
    private Size _geoSize;
    private bool _geoDirty = true;
    private int _hover = -1;

    public HourlyChartControl()
    {
        SnapsToDevicePixels = false;
        ClipToBounds = true;
    }

    public IReadOnlyList<ChartPoint>? Points
    {
        get => (IReadOnlyList<ChartPoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    private double Reveal
    {
        get => (double)GetValue(RevealProperty);
        set => SetValue(RevealProperty, value);
    }

    private static Brush Make(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static Pen MakePen(Color c, double thickness, bool dashed = false)
    {
        var p = new Pen(Make(c), thickness);
        if (dashed)
        {
            p.DashStyle = new DashStyle(new double[] { 3, 3 }, 0);
        }
        p.Freeze();
        return p;
    }

    private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((HourlyChartControl)d).Restart();
    }

    private void Restart()
    {
        _geoDirty = true;
        _hover = -1;
        if (!SystemParameters.ClientAreaAnimation)
        {
            BeginAnimation(RevealProperty, null);
            Reveal = 1;
            return;
        }
        var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(700))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(RevealProperty, anim);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 480 : availableSize.Width;
        var h = double.IsInfinity(availableSize.Height) ? 220 : availableSize.Height;
        return new Size(w, h);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        _geoDirty = true;
    }

    private void EnsureBrushes()
    {
        var c = AccentColor;
        if (_brushesReady && c == _brushColor)
        {
            return;
        }
        _brushColor = c;
        _brushesReady = true;
        _accentBrush = Make(c);
        var grad = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        grad.GradientStops.Add(new GradientStop(Color.FromArgb(0x80, c.R, c.G, c.B), 0));
        grad.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, c.R, c.G, c.B), 1));
        grad.Freeze();
        _fillBrush = grad;
        var lp = new Pen(_accentBrush, 2.5) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        lp.Freeze();
        _linePen = lp;
        var pp = new Pen(_accentBrush, 2);
        pp.Freeze();
        _pointPen = pp;
    }

    private FormattedText Text(string s, double size, Brush brush, bool bold = false)
    {
        return new FormattedText(s, Culture, FlowDirection.LeftToRight, bold ? Bold : Regular, size, brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    private Rect PlotRect(Size size)
    {
        return new Rect(PadLeft, PadTop, Math.Max(1, size.Width - PadLeft - PadRight), Math.Max(1, size.Height - PadTop - PadBottom));
    }

    private void BuildGeometry(Size size, IReadOnlyList<ChartPoint> data)
    {
        var n = Math.Min(data.Count, MaxPoints);
        var plot = PlotRect(size);
        double min = double.MaxValue, max = double.MinValue;
        for (var i = 0; i < n; i++)
        {
            min = Math.Min(min, data[i].Value);
            max = Math.Max(max, data[i].Value);
        }
        if (max - min < 1e-6)
        {
            min -= 1;
            max += 1;
        }
        var pad = (max - min) * 0.15;
        min -= pad;
        max += pad;

        _pts = new Point[n];
        for (var i = 0; i < n; i++)
        {
            var x = n == 1 ? plot.Left + plot.Width / 2 : plot.Left + plot.Width * i / (n - 1);
            var y = plot.Top + (1 - (data[i].Value - min) / (max - min)) * plot.Height;
            _pts[i] = new Point(x, y);
        }

        _line = null;
        _fill = null;
        if (n >= 2)
        {
            var line = new StreamGeometry();
            using (var ctx = line.Open())
            {
                ctx.BeginFigure(_pts[0], false, false);
                for (var i = 0; i < n - 1; i++)
                {
                    Segment(i, out var c1, out var c2);
                    ctx.BezierTo(c1, c2, _pts[i + 1], true, true);
                }
            }
            line.Freeze();

            var fill = new StreamGeometry();
            using (var ctx = fill.Open())
            {
                ctx.BeginFigure(new Point(_pts[0].X, plot.Bottom), true, true);
                ctx.LineTo(_pts[0], false, false);
                for (var i = 0; i < n - 1; i++)
                {
                    Segment(i, out var c1, out var c2);
                    ctx.BezierTo(c1, c2, _pts[i + 1], false, false);
                }
                ctx.LineTo(new Point(_pts[n - 1].X, plot.Bottom), false, false);
            }
            fill.Freeze();
            _line = line;
            _fill = fill;
        }
        _geoSize = size;
        _geoDirty = false;
    }

    private void Segment(int i, out Point c1, out Point c2)
    {
        var n = _pts.Length;
        var p0 = _pts[Math.Max(i - 1, 0)];
        var p1 = _pts[i];
        var p2 = _pts[i + 1];
        var p3 = _pts[Math.Min(i + 2, n - 1)];
        var lo = Math.Min(p1.Y, p2.Y);
        var hi = Math.Max(p1.Y, p2.Y);
        var y1 = Math.Clamp(p1.Y + (p2.Y - p0.Y) / 6, lo, hi);
        var y2 = Math.Clamp(p2.Y - (p3.Y - p1.Y) / 6, lo, hi);
        c1 = new Point(p1.X + (p2.X - p0.X) / 6, y1);
        c2 = new Point(p2.X - (p3.X - p1.X) / 6, y2);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = new Size(ActualWidth, ActualHeight);
        if (size.Width < 2 || size.Height < 2)
        {
            return;
        }
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(size));
        EnsureBrushes();

        var data = Points;
        if (data == null || data.Count == 0)
        {
            var t = Text("Нет данных", 14, MutedBrush);
            dc.DrawText(t, new Point((size.Width - t.Width) / 2, (size.Height - t.Height) / 2));
            return;
        }

        if (_geoDirty || _geoSize != size || _pts.Length != Math.Min(data.Count, MaxPoints))
        {
            BuildGeometry(size, data);
        }

        var plot = PlotRect(size);
        var n = _pts.Length;

        for (var i = 0; i <= 3; i++)
        {
            var y = plot.Top + plot.Height * i / 3;
            dc.DrawLine(GridPen, new Point(plot.Left, y), new Point(plot.Right, y));
        }

        dc.PushClip(new RectangleGeometry(new Rect(0, 0, size.Width * Math.Clamp(Reveal, 0, 1), size.Height)));

        if (_fill != null)
        {
            dc.DrawGeometry(_fillBrush, null, _fill);
        }
        if (_line != null)
        {
            dc.DrawGeometry(null, _linePen, _line);
        }

        var spacing = n > 1 ? plot.Width / (n - 1) : plot.Width;
        var step = Math.Max(1, (int)Math.Ceiling(38 / Math.Max(spacing, 1)));
        for (var i = 0; i < n; i++)
        {
            dc.DrawEllipse(PointCoreBrush, _pointPen, _pts[i], 3.5, 3.5);
            if (i % step == 0 && i != _hover)
            {
                var t = Text(Formatting.Temperature(data[i].Value), 11, TextBrush);
                dc.DrawText(t, new Point(_pts[i].X - t.Width / 2, _pts[i].Y - 10 - t.Height));
            }
            if (i % 3 == 0)
            {
                var t = Text(data[i].Label, 11, MutedBrush);
                dc.DrawText(t, new Point(Math.Clamp(_pts[i].X - t.Width / 2, 0, Math.Max(0, size.Width - t.Width)), plot.Bottom + 6));
            }
        }

        dc.Pop();

        if (_hover >= 0 && _hover < n)
        {
            DrawHover(dc, size, plot, data[_hover], _pts[_hover]);
        }
    }

    private void DrawHover(DrawingContext dc, Size size, Rect plot, ChartPoint cp, Point p)
    {
        dc.DrawLine(GuidePen, new Point(p.X, plot.Top - 6), new Point(p.X, plot.Bottom));
        dc.DrawEllipse(_accentBrush, null, p, 7, 7);
        dc.DrawEllipse(PointCoreBrush, _pointPen, p, 4.5, 4.5);

        var t1 = Text(cp.Label, 12, TextBrush, true);
        FormattedText? t2 = string.IsNullOrWhiteSpace(cp.Detail) ? null : Text(cp.Detail, 11, MutedBrush);
        var w = Math.Max(t1.Width, t2?.Width ?? 0) + 20;
        var h = t1.Height + (t2?.Height ?? 0) + 14;

        var x = p.X + 14;
        if (x + w > size.Width - 4)
        {
            x = p.X - 14 - w;
        }
        x = Math.Clamp(x, 4, Math.Max(4, size.Width - w - 4));
        var y = p.Y - h - 12;
        if (y < 4)
        {
            y = p.Y + 14;
        }
        y = Math.Clamp(y, 4, Math.Max(4, size.Height - h - 4));

        dc.DrawRoundedRectangle(TooltipBrush, TooltipPen, new Rect(x, y, w, h), 8, 8);
        dc.DrawText(t1, new Point(x + 10, y + 7));
        if (t2 != null)
        {
            dc.DrawText(t2, new Point(x + 10, y + 7 + t1.Height));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_pts.Length == 0 || Points == null)
        {
            return;
        }
        var mx = e.GetPosition(this).X;
        var best = 0;
        var bestD = double.MaxValue;
        for (var i = 0; i < _pts.Length; i++)
        {
            var d = Math.Abs(_pts[i].X - mx);
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }
        if (best != _hover)
        {
            _hover = best;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover != -1)
        {
            _hover = -1;
            InvalidateVisual();
        }
    }
}
