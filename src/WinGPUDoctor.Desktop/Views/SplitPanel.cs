using System.Windows;
using System.Windows.Controls;

namespace WinGPUDoctor.Desktop.Views;

// Two columns side by side when the panel is at least StackWidth wide; otherwise the second child goes
// below the first. Both columns are at least MinHeight tall (bound to the page's visible height), and
// the second is measured at that final height, so a panel such as the report preview can fill it and
// scroll inside. When stacked, the second child gets StackedSecondHeight, or its content height if NaN.
public sealed class SplitPanel : Panel
{
    public static readonly DependencyProperty StackWidthProperty = DependencyProperty.Register(nameof(StackWidth),
        typeof(double), typeof(SplitPanel), new FrameworkPropertyMetadata(720.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty FirstShareProperty = DependencyProperty.Register(nameof(FirstShare),
        typeof(double), typeof(SplitPanel), new FrameworkPropertyMetadata(0.52, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty StackedSecondHeightProperty = DependencyProperty.Register(nameof(StackedSecondHeight),
        typeof(double), typeof(SplitPanel), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure));

    private static readonly DependencyPropertyKey IsStackedKey = DependencyProperty.RegisterReadOnly(nameof(IsStacked),
        typeof(bool), typeof(SplitPanel), new PropertyMetadata(false));

    public static readonly DependencyProperty IsStackedProperty = IsStackedKey.DependencyProperty;

    public double StackWidth { get => (double)GetValue(StackWidthProperty); set => SetValue(StackWidthProperty, value); }
    public double FirstShare { get => (double)GetValue(FirstShareProperty); set => SetValue(FirstShareProperty, value); }
    public double StackedSecondHeight { get => (double)GetValue(StackedSecondHeightProperty); set => SetValue(StackedSecondHeightProperty, value); }
    public bool IsStacked => (bool)GetValue(IsStackedProperty);

    // Pure layout rule, kept separate so tests can pin it.
    public static bool Stacks(double width, double stackWidth) => width < stackWidth;

    private UIElement? First => InternalChildren.Count > 0 ? InternalChildren[0] : null;
    private UIElement? Second => InternalChildren.Count > 1 ? InternalChildren[1] : null;
    private double Floor => double.IsInfinity(MinHeight) || double.IsNaN(MinHeight) ? 0 : MinHeight;

    protected override Size MeasureOverride(Size available)
    {
        var width = double.IsInfinity(available.Width) ? StackWidth : available.Width;
        var stacked = Stacks(width, StackWidth);
        SetValue(IsStackedKey, stacked);
        if (stacked)
        {
            First?.Measure(new Size(width, double.PositiveInfinity));
            Second?.Measure(new Size(width, double.IsNaN(StackedSecondHeight) ? double.PositiveInfinity : StackedSecondHeight));
            return new Size(width, Math.Max(Floor, DesiredHeight(First) + DesiredHeight(Second)));
        }
        var firstWidth = Math.Round(width * FirstShare);
        First?.Measure(new Size(firstWidth, double.PositiveInfinity));
        var height = Math.Max(Floor, DesiredHeight(First));
        Second?.Measure(new Size(width - firstWidth, height));
        return new Size(width, Math.Max(height, DesiredHeight(Second)));
    }

    protected override Size ArrangeOverride(Size final)
    {
        if (IsStacked)
        {
            var top = DesiredHeight(First);
            First?.Arrange(new Rect(0, 0, final.Width, top));
            Second?.Arrange(new Rect(0, top, final.Width, Math.Max(DesiredHeight(Second), final.Height - top)));
            return final;
        }
        var firstWidth = Math.Round(final.Width * FirstShare);
        First?.Arrange(new Rect(0, 0, firstWidth, final.Height));
        Second?.Arrange(new Rect(firstWidth, 0, final.Width - firstWidth, final.Height));
        return final;
    }

    private static double DesiredHeight(UIElement? child) => child?.DesiredSize.Height ?? 0;
}
