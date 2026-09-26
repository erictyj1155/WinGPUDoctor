using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WinGPUDoctor.Desktop.Views;

// Follows the Windows "Animation effects" setting, which WPF exposes as SystemParameters.ClientAreaAnimation.
// When it is off, nothing in the window animates; the scanning screen then shows steady indicators.
public static class Motion
{
    static Motion() => SystemParameters.StaticPropertyChanged += (_, e) =>
    {
        if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation)) Refresh();
    };

    public static event EventHandler? Changed;

    public static bool IsEnabled => SystemParameters.ClientAreaAnimation;

    public static bool ShouldAnimate(bool isVisible, bool animationsEnabled) => isVisible && animationsEnabled;

    internal static void Refresh() => Changed?.Invoke(null, EventArgs.Empty);
}

// An element whose animation runs only while it is visible and Windows animations are on.
public abstract class MotionElement : FrameworkElement
{
    protected MotionElement()
    {
        IsVisibleChanged += (_, _) => Update();
        Loaded += (_, _) =>
        {
            Motion.Changed += OnMotionChanged;
            Update();
        };
        Unloaded += (_, _) =>
        {
            Motion.Changed -= OnMotionChanged;
            Update();
        };
    }

    protected bool IsAnimating { get; private set; }

    private void OnMotionChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(Update);

    private void Update()
    {
        var animate = IsLoaded && Motion.ShouldAnimate(IsVisible, Motion.IsEnabled);
        if (animate == IsAnimating) return;
        IsAnimating = animate;
        if (animate) Start();
        else Stop();
        InvalidateVisual();
    }

    protected abstract void Start();
    protected abstract void Stop();
}

// Indeterminate progress: a glow sweeping along a thin track. Without animation it is a steady bar.
public sealed class SweepBar : MotionElement
{
    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(nameof(Track), typeof(Brush),
        typeof(SweepBar), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GlowProperty = DependencyProperty.Register(nameof(Glow), typeof(Brush),
        typeof(SweepBar), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly DependencyProperty OffsetProperty = DependencyProperty.Register("Offset", typeof(double),
        typeof(SweepBar), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush? Track { get => (Brush?)GetValue(TrackProperty); set => SetValue(TrackProperty, value); }
    public Brush? Glow { get => (Brush?)GetValue(GlowProperty); set => SetValue(GlowProperty, value); }

    protected override void Start() => BeginAnimation(OffsetProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(1.6))
    {
        RepeatBehavior = RepeatBehavior.Forever,
        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
    });

    protected override void Stop() => BeginAnimation(OffsetProperty, null);

    protected override Size MeasureOverride(Size availableSize) => new(0, 3);

    protected override void OnRender(DrawingContext drawingContext)
    {
        var (width, height) = (ActualWidth, ActualHeight);
        if (width <= 0 || height <= 0) return;
        var bounds = new Rect(0, 0, width, height);
        drawingContext.PushClip(new RectangleGeometry(bounds, height / 2, height / 2));
        drawingContext.DrawRectangle(Track, null, bounds);
        if (Glow is SolidColorBrush glow)
        {
            if (IsAnimating)
            {
                var glowWidth = width * 0.4;
                var x = -glowWidth + (double)GetValue(OffsetProperty) * (width + glowWidth);
                var clear = Color.FromArgb(0, glow.Color.R, glow.Color.G, glow.Color.B);
                var brush = new LinearGradientBrush(new GradientStopCollection
                {
                    new(clear, 0), new(glow.Color, 0.5), new(clear, 1)
                }, new Point(0, 0.5), new Point(1, 0.5));
                drawingContext.DrawRectangle(brush, null, new Rect(x, 0, glowWidth, height));
            }
            else
            {
                drawingContext.PushOpacity(0.6);
                drawingContext.DrawRectangle(glow, null, bounds);
                drawingContext.Pop();
            }
        }
        drawingContext.Pop();
    }
}

// A placeholder line for a value that is being read. It pulses only while animations run.
public sealed class PulseLine : MotionElement
{
    private const double LineHeight = 10;

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(nameof(Fill), typeof(Brush),
        typeof(PulseLine), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FractionProperty = DependencyProperty.Register(nameof(Fraction), typeof(double),
        typeof(PulseLine), new FrameworkPropertyMetadata(0.6, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DelayProperty = DependencyProperty.Register(nameof(Delay), typeof(double),
        typeof(PulseLine), new PropertyMetadata(0.0));

    public PulseLine() => Opacity = 0.7;

    public Brush? Fill { get => (Brush?)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public double Fraction { get => (double)GetValue(FractionProperty); set => SetValue(FractionProperty, value); }
    public double Delay { get => (double)GetValue(DelayProperty); set => SetValue(DelayProperty, value); }

    protected override void Start() => BeginAnimation(OpacityProperty, new DoubleAnimation(0.35, 0.85, TimeSpan.FromSeconds(0.9))
    {
        AutoReverse = true,
        RepeatBehavior = RepeatBehavior.Forever,
        BeginTime = TimeSpan.FromSeconds(Delay)
    });

    protected override void Stop() => BeginAnimation(OpacityProperty, null);

    protected override Size MeasureOverride(Size availableSize) => new(0, LineHeight);

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (ActualWidth <= 0) return;
        drawingContext.DrawRoundedRectangle(Fill, null,
            new Rect(0, (ActualHeight - LineHeight) / 2, ActualWidth * Math.Clamp(Fraction, 0, 1), LineHeight), 3, 3);
    }
}
