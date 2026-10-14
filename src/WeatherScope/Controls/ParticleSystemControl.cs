using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using WeatherScope.Models;

namespace WeatherScope.Controls;

public sealed class ParticleSystemControl : FrameworkElement
{
    private const int MaxParticles = 560;
    private const int MaxClouds = 10;
    private const int LayerCount = 3;
    private const double FrameInterval = 1.0 / 62.0;

    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode), typeof(ParticleMode), typeof(ParticleSystemControl),
        new PropertyMetadata(ParticleMode.None, OnConfigChanged));

    public static readonly DependencyProperty IntensityProperty = DependencyProperty.Register(
        nameof(Intensity), typeof(double), typeof(ParticleSystemControl),
        new PropertyMetadata(0.5, OnConfigChanged));

    public static readonly DependencyProperty WindDriftProperty = DependencyProperty.Register(
        nameof(WindDrift), typeof(double), typeof(ParticleSystemControl),
        new PropertyMetadata(0.0, OnConfigChanged));

    public static readonly DependencyProperty IsThunderProperty = DependencyProperty.Register(
        nameof(IsThunder), typeof(bool), typeof(ParticleSystemControl),
        new PropertyMetadata(false, OnConfigChanged));

    public static readonly DependencyProperty ReducedMotionProperty = DependencyProperty.Register(
        nameof(ReducedMotion), typeof(bool), typeof(ParticleSystemControl),
        new PropertyMetadata(false, OnConfigChanged));

    private struct Particle
    {
        public bool Alive;
        public float X;
        public float Y;
        public float Speed;
        public float Size;
        public float Alpha;
        public float Phase;
        public float Sway;
        public byte Layer;
    }

    private struct Cloud
    {
        public float X;
        public float Y;
        public float W;
        public float H;
        public float Speed;
        public float Alpha;
        public bool Alive;
    }

    private static readonly Brush[] SnowBrushes = BuildSnowBrushes();
    private static readonly Pen[][] RainPens = BuildRainPens();
    private static readonly Brush CloudBrush = BuildCloudBrush();
    private static readonly Pen BoltCorePen = BuildBoltPen(0xF8, 0xFA, 0xFC, 255, 2.5);
    private static readonly Pen BoltGlowPen = BuildBoltPen(0xF8, 0xFA, 0xFC, 90, 7);

    private const int AlphaSteps = 4;

    private readonly Particle[] _particles = new Particle[MaxParticles];
    private readonly Cloud[] _clouds = new Cloud[MaxClouds];
    private readonly Stopwatch _clock = new();
    private readonly Random _rng = new();
    private readonly Point[] _bolt = new Point[13];

    private bool _subscribed;
    private bool _loaded;
    private Window? _window;
    private double _lastTime;
    private double _sinceFrame;
    private double _fade = 1;
    private double _w;
    private double _h;
    private int _activeCount;
    private int _cloudCount;
    private int _boltPoints;
    private double _flash;
    private double _boltLife;
    private double _nextLightning;
    private double _elapsed;
    private ParticleMode _builtMode = ParticleMode.None;

    public ParticleSystemControl()
    {
        IsHitTestVisible = false;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += (_, _) => UpdateSubscription();
        SizeChanged += OnSizeChanged;
    }

    public ParticleMode Mode
    {
        get => (ParticleMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double Intensity
    {
        get => (double)GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }

    public double WindDrift
    {
        get => (double)GetValue(WindDriftProperty);
        set => SetValue(WindDriftProperty, value);
    }

    public bool IsThunder
    {
        get => (bool)GetValue(IsThunderProperty);
        set => SetValue(IsThunderProperty, value);
    }

    public bool ReducedMotion
    {
        get => (bool)GetValue(ReducedMotionProperty);
        set => SetValue(ReducedMotionProperty, value);
    }

    private static Brush[] BuildSnowBrushes()
    {
        var arr = new Brush[AlphaSteps];
        for (var i = 0; i < AlphaSteps; i++)
        {
            var b = new SolidColorBrush(Color.FromArgb((byte)(80 + i * 50), 0xF1, 0xF5, 0xF9));
            b.Freeze();
            arr[i] = b;
        }
        return arr;
    }

    private static Pen[][] BuildRainPens()
    {
        var res = new Pen[LayerCount][];
        var widths = new[] { 0.8, 1.2, 1.8 };
        for (var l = 0; l < LayerCount; l++)
        {
            res[l] = new Pen[AlphaSteps];
            for (var a = 0; a < AlphaSteps; a++)
            {
                var alpha = (byte)(64 + a * 29);
                var br = new SolidColorBrush(Color.FromArgb(alpha, 0xA5, 0xC8, 0xE4));
                br.Freeze();
                var p = new Pen(br, widths[l]) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                p.Freeze();
                res[l][a] = p;
            }
        }
        return res;
    }

    private static Brush BuildCloudBrush()
    {
        var g = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5
        };
        g.GradientStops.Add(new GradientStop(Color.FromArgb(0xB0, 0xCB, 0xD5, 0xE1), 0));
        g.GradientStops.Add(new GradientStop(Color.FromArgb(0x60, 0x94, 0xA3, 0xB8), 0.55));
        g.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0x94, 0xA3, 0xB8), 1));
        g.Freeze();
        return g;
    }

    private static Pen BuildBoltPen(byte r, byte g, byte b, byte a, double t)
    {
        var br = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        br.Freeze();
        var p = new Pen(br, t) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        p.Freeze();
        return p;
    }

    private static void OnConfigChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ParticleSystemControl)d).Reconfigure();
    }

    private bool Disabled => ReducedMotion || !SystemParameters.ClientAreaAnimation || Mode == ParticleMode.None;

    private void Reconfigure()
    {
        if (_builtMode != Mode)
        {
            _builtMode = Mode;
            _fade = 0;
            for (var i = 0; i < _particles.Length; i++)
            {
                _particles[i].Alive = false;
            }
            for (var i = 0; i < _clouds.Length; i++)
            {
                _clouds[i].Alive = false;
            }
            _activeCount = 0;
            _cloudCount = 0;
            _flash = 0;
            _boltLife = 0;
            ScheduleLightning();
        }
        ApplyCounts();
        UpdateSubscription();
        InvalidateVisual();
    }

    private int TargetParticles()
    {
        var k = Math.Clamp(Intensity, 0, 1);
        return Mode switch
        {
            ParticleMode.Rain => (int)(80 + 320 * k),
            ParticleMode.Storm => (int)(180 + 320 * k),
            ParticleMode.Snow => (int)(60 + 180 * k),
            _ => 0
        };
    }

    private void ApplyCounts()
    {
        if (_w < 1 || _h < 1)
        {
            return;
        }
        var target = TargetParticles();
        if (target > MaxParticles)
        {
            target = MaxParticles;
        }
        for (var i = 0; i < _particles.Length; i++)
        {
            if (i < target)
            {
                if (!_particles[i].Alive)
                {
                    Spawn(ref _particles[i], true);
                }
            }
            else
            {
                _particles[i].Alive = false;
            }
        }
        _activeCount = target;

        if (Mode == ParticleMode.Clouds)
        {
            var want = Math.Clamp(6 + (int)Math.Round(4 * Math.Clamp(Intensity, 0, 1)), 6, MaxClouds);
            for (var i = 0; i < _clouds.Length; i++)
            {
                if (i < want)
                {
                    if (!_clouds[i].Alive)
                    {
                        SpawnCloud(ref _clouds[i], true);
                    }
                }
                else
                {
                    _clouds[i].Alive = false;
                }
            }
            _cloudCount = want;
        }
        else
        {
            for (var i = 0; i < _clouds.Length; i++)
            {
                _clouds[i].Alive = false;
            }
            _cloudCount = 0;
        }
    }

    private float Rand(double min, double max)
    {
        return (float)(min + _rng.NextDouble() * (max - min));
    }

    private void Spawn(ref Particle p, bool anywhere)
    {
        p.Alive = true;
        p.Layer = (byte)_rng.Next(LayerCount);
        p.Phase = Rand(0, Math.PI * 2);
        var depth = (p.Layer + 1) / (double)LayerCount;
        if (Mode == ParticleMode.Snow)
        {
            p.Size = Rand(1.5, 4) * (float)(0.6 + 0.4 * depth);
            if (p.Size > 4) p.Size = 4;
            if (p.Size < 1.5f) p.Size = 1.5f;
            p.Speed = Rand(40, 120) * (float)(0.5 + 0.5 * depth);
            if (p.Speed > 120) p.Speed = 120;
            p.Sway = Rand(8, 28);
            p.Alpha = Rand(0.35, 0.95) * (float)(0.6 + 0.4 * depth);
        }
        else
        {
            p.Size = Rand(10, 26) * (float)(0.5 + 0.5 * depth);
            p.Speed = Rand(600, 1100) * (float)(0.6 + 0.4 * depth);
            if (p.Speed > 1100) p.Speed = 1100;
            p.Sway = 0;
            p.Alpha = Rand(0.25, 0.6);
        }
        p.X = Rand(-60, _w + 60);
        p.Y = anywhere ? Rand(-20, _h) : Rand(-60, -5);
    }

    private void SpawnCloud(ref Cloud c, bool anywhere)
    {
        c.Alive = true;
        c.W = Rand(Math.Max(200, _w * 0.35), Math.Max(320, _w * 0.7));
        c.H = c.W * Rand(0.35, 0.55);
        c.Y = Rand(-c.H * 0.3, Math.Max(10, _h * 0.55));
        var speed = Rand(8, 25);
        var dir = WindDrift < -0.01 ? -1f : 1f;
        c.Speed = speed * dir;
        c.Alpha = Rand(0.35, 0.8);
        if (anywhere)
        {
            c.X = Rand(-c.W * 0.5, (float)_w);
        }
        else
        {
            c.X = dir > 0 ? -c.W : (float)_w;
        }
    }

    private void ScheduleLightning()
    {
        _nextLightning = _elapsed + 4 + _rng.NextDouble() * 5;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _w = e.NewSize.Width;
        _h = e.NewSize.Height;
        if (_w < 1 || _h < 1)
        {
            return;
        }
        if (_activeCount == 0 && _cloudCount == 0)
        {
            ApplyCounts();
            return;
        }
        for (var i = 0; i < _particles.Length; i++)
        {
            ref var p = ref _particles[i];
            if (p.Alive && (p.X < -80 || p.X > _w + 80 || p.Y > _h + 40))
            {
                Spawn(ref p, true);
            }
        }
        for (var i = 0; i < _clouds.Length; i++)
        {
            ref var c = ref _clouds[i];
            if (c.Alive && (c.X > _w || c.X + c.W < 0))
            {
                SpawnCloud(ref c, true);
            }
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        _w = ActualWidth;
        _h = ActualHeight;
        _window = Window.GetWindow(this);
        if (_window != null)
        {
            _window.StateChanged += OnWindowStateChanged;
            _window.Activated += OnWindowActivity;
            _window.Deactivated += OnWindowActivity;
        }
        _builtMode = Mode;
        ApplyCounts();
        UpdateSubscription();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _loaded = false;
        if (_window != null)
        {
            _window.StateChanged -= OnWindowStateChanged;
            _window.Activated -= OnWindowActivity;
            _window.Deactivated -= OnWindowActivity;
            _window = null;
        }
        UpdateSubscription();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        UpdateSubscription();
    }

    private void OnWindowActivity(object? sender, EventArgs e)
    {
        UpdateSubscription();
    }

    private bool ShouldRun()
    {
        if (!_loaded || !IsVisible || Disabled)
        {
            return false;
        }
        if (_window != null && (_window.WindowState == WindowState.Minimized || !_window.IsActive))
        {
            return false;
        }
        return true;
    }

    private void UpdateSubscription()
    {
        var run = ShouldRun();
        if (run && !_subscribed)
        {
            _subscribed = true;
            _lastTime = 0;
            _sinceFrame = 0;
            _clock.Restart();
            CompositionTarget.Rendering += OnRendering;
        }
        else if (!run && _subscribed)
        {
            _subscribed = false;
            CompositionTarget.Rendering -= OnRendering;
            _clock.Stop();
        }
        if (!run)
        {
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        var h = double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height;
        return new Size(w, h);
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed.TotalSeconds;
        var dt = now - _lastTime;
        _lastTime = now;
        _sinceFrame += dt;
        if (_sinceFrame < FrameInterval)
        {
            return;
        }
        dt = Math.Min(_sinceFrame, 0.05);
        _sinceFrame = 0;
        Step(dt);
        InvalidateVisual();
    }

    private void Step(double dt)
    {
        _elapsed += dt;
        if (_fade < 1)
        {
            _fade = Math.Min(1, _fade + dt / 0.4);
        }
        if (_w < 1 || _h < 1)
        {
            return;
        }
        var wind = Math.Clamp(WindDrift, -1, 1);
        var mode = Mode;
        var f = (float)dt;

        if (mode == ParticleMode.Rain || mode == ParticleMode.Storm)
        {
            var slant = (float)(wind * 0.45);
            for (var i = 0; i < _particles.Length; i++)
            {
                ref var p = ref _particles[i];
                if (!p.Alive) continue;
                p.Y += p.Speed * f;
                p.X += p.Speed * slant * f;
                if (p.Y > _h + 30 || p.X < -120 || p.X > _w + 120)
                {
                    Spawn(ref p, false);
                    if (slant > 0) p.X -= (float)(_h * slant * 0.5);
                    else if (slant < 0) p.X += (float)(_h * -slant * 0.5);
                }
            }
        }
        else if (mode == ParticleMode.Snow)
        {
            for (var i = 0; i < _particles.Length; i++)
            {
                ref var p = ref _particles[i];
                if (!p.Alive) continue;
                p.Y += p.Speed * f;
                p.Phase += f * (0.8f + p.Layer * 0.3f);
                p.X += ((float)Math.Cos(p.Phase) * p.Sway + (float)wind * 60f * (0.6f + p.Layer * 0.3f)) * f;
                if (p.Y > _h + 10 || p.X < -40 || p.X > _w + 40)
                {
                    Spawn(ref p, false);
                    if (wind > 0.05) p.X = Rand(-60, _w * 0.9);
                }
            }
        }
        else if (mode == ParticleMode.Clouds)
        {
            for (var i = 0; i < _clouds.Length; i++)
            {
                ref var c = ref _clouds[i];
                if (!c.Alive) continue;
                var dir = wind < -0.01 ? -1f : 1f;
                c.Speed = Math.Abs(c.Speed) * dir;
                c.X += c.Speed * f;
                if ((dir > 0 && c.X > _w) || (dir < 0 && c.X + c.W < 0))
                {
                    SpawnCloud(ref c, false);
                }
            }
        }

        if (mode == ParticleMode.Storm || IsThunder)
        {
            if (_elapsed >= _nextLightning)
            {
                _flash = 1;
                if (_rng.NextDouble() < 0.6)
                {
                    BuildBolt();
                    _boltLife = 1;
                }
                ScheduleLightning();
            }
        }
        if (_flash > 0)
        {
            _flash = Math.Max(0, _flash - dt / 0.35);
        }
        if (_boltLife > 0)
        {
            _boltLife = Math.Max(0, _boltLife - dt / 0.35);
        }
    }

    private void BuildBolt()
    {
        var segs = _rng.Next(8, 13);
        _boltPoints = segs + 1;
        var x = _w * (0.15 + _rng.NextDouble() * 0.7);
        var stepY = _h * 0.75 / segs;
        for (var i = 0; i < _boltPoints; i++)
        {
            _bolt[i] = new Point(x, i * stepY);
            x += (_rng.NextDouble() - 0.5) * Math.Max(30, _w * 0.07);
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_w < 1 || _h < 1 || Disabled || !_subscribed)
        {
            return;
        }
        var fade = _fade;
        var mode = Mode;

        if (mode == ParticleMode.Clouds)
        {
            DrawClouds(dc, fade);
        }
        else if (mode == ParticleMode.Snow)
        {
            DrawSnow(dc, fade);
        }
        else if (mode == ParticleMode.Rain || mode == ParticleMode.Storm)
        {
            DrawRain(dc, fade);
        }

        if (_flash > 0)
        {
            var a = (byte)(255 * 0.35 * _flash * _flash);
            var br = new SolidColorBrush(Color.FromArgb(a, 255, 255, 255));
            br.Freeze();
            dc.DrawRectangle(br, null, new Rect(0, 0, _w, _h));
        }
        if (_boltLife > 0 && _boltPoints > 1)
        {
            DrawBolt(dc);
        }
    }

    private void DrawBolt(DrawingContext dc)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(_bolt[0], false, false);
            for (var i = 1; i < _boltPoints; i++)
            {
                ctx.LineTo(_bolt[i], true, true);
            }
        }
        g.Freeze();
        dc.PushOpacity(Math.Clamp(_boltLife * 1.5, 0, 1));
        dc.DrawGeometry(null, BoltGlowPen, g);
        dc.DrawGeometry(null, BoltCorePen, g);
        dc.Pop();
    }

    private void DrawClouds(DrawingContext dc, double fade)
    {
        for (var i = 0; i < _clouds.Length; i++)
        {
            ref var c = ref _clouds[i];
            if (!c.Alive) continue;
            dc.PushOpacity(c.Alpha * fade);
            dc.DrawEllipse(CloudBrush, null, new Point(c.X + c.W / 2, c.Y + c.H / 2), c.W / 2, c.H / 2);
            dc.Pop();
        }
    }

    private void DrawSnow(DrawingContext dc, double fade)
    {
        var count = Math.Min(_activeCount, _particles.Length);
        for (var step = 0; step < AlphaSteps; step++)
        {
            var brush = SnowBrushes[step];
            for (var i = 0; i < count; i++)
            {
                ref var p = ref _particles[i];
                if (!p.Alive) continue;
                var bucket = Math.Min(AlphaSteps - 1, (int)(p.Alpha * AlphaSteps));
                if (bucket != step) continue;
                var cx = p.X;
                dc.DrawEllipse(brush, null, new Point(cx, p.Y), p.Size * fade + 0.2, p.Size * fade + 0.2);
            }
        }
    }

    private void DrawRain(DrawingContext dc, double fade)
    {
        var wind = Math.Clamp(WindDrift, -1, 1);
        var slant = wind * 0.45;
        var count = Math.Min(_activeCount, _particles.Length);
        dc.PushOpacity(fade);
        for (var layer = 0; layer < LayerCount; layer++)
        {
            for (var step = 0; step < AlphaSteps; step++)
            {
                var pen = RainPens[layer][step];
                for (var i = 0; i < count; i++)
                {
                    ref var p = ref _particles[i];
                    if (!p.Alive || p.Layer != layer) continue;
                    var bucket = Math.Min(AlphaSteps - 1, (int)((p.Alpha - 0.25) / 0.35 * AlphaSteps));
                    if (bucket < 0) bucket = 0;
                    if (bucket != step) continue;
                    var len = p.Size;
                    dc.DrawLine(pen, new Point(p.X, p.Y), new Point(p.X - len * slant, p.Y - len));
                }
            }
        }
        dc.Pop();
    }
}
