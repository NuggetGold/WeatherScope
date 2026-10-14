using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WeatherScope.Controls;

public sealed class ShimmerControl : FrameworkElement
{
    public static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
        nameof(Phase), typeof(double), typeof(ShimmerControl),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(double), typeof(ShimmerControl),
        new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush BaseBrush = Frozen(Color.FromRgb(0x1E, 0x29, 0x3B));
    private static readonly Pen BorderPen = FrozenPen(Color.FromRgb(0x33, 0x41, 0x55));

    public ShimmerControl()
    {
        Loaded += (_, _) => StartAnimation();
        Unloaded += (_, _) => BeginAnimation(PhaseProperty, null);
    }

    public double Phase
    {
        get => (double)GetValue(PhaseProperty);
        set => SetValue(PhaseProperty, value);
    }

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? 120 : availableSize.Height;
        return new Size(width, height);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var rect = new Rect(0, 0, ActualWidth, ActualHeight);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        dc.DrawRoundedRectangle(BaseBrush, BorderPen, rect, CornerRadius, CornerRadius);

        var center = Phase;
        var highlight = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0)
        };
        highlight.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0x94, 0xA3, 0xB8), center - 0.25));
        highlight.GradientStops.Add(new GradientStop(Color.FromArgb(0x38, 0x94, 0xA3, 0xB8), center));
        highlight.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0x94, 0xA3, 0xB8), center + 0.25));
        highlight.Freeze();
        dc.DrawRoundedRectangle(highlight, null, rect, CornerRadius, CornerRadius);
    }

    private void StartAnimation()
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            Phase = 0.5;
            return;
        }

        var animation = new DoubleAnimation(-0.25, 1.25, TimeSpan.FromSeconds(1.5))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        BeginAnimation(PhaseProperty, animation);
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        var pen = new Pen(brush, 1);
        pen.Freeze();
        return pen;
    }
}
