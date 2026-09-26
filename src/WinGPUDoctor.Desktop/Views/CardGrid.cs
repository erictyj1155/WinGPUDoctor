using System.Windows;
using System.Windows.Controls;

namespace WinGPUDoctor.Desktop.Views;

// Cards in equal columns: as many as fit at MinColumnWidth, up to MaxColumns, in reading order.
// Cards in the same row share the row's height.
public sealed class CardGrid : Panel
{
    public static readonly DependencyProperty MinColumnWidthProperty = DependencyProperty.Register(nameof(MinColumnWidth),
        typeof(double), typeof(CardGrid), new FrameworkPropertyMetadata(260.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty MaxColumnsProperty = DependencyProperty.Register(nameof(MaxColumns),
        typeof(int), typeof(CardGrid), new FrameworkPropertyMetadata(3, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(nameof(Spacing),
        typeof(double), typeof(CardGrid), new FrameworkPropertyMetadata(14.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double MinColumnWidth { get => (double)GetValue(MinColumnWidthProperty); set => SetValue(MinColumnWidthProperty, value); }
    public int MaxColumns { get => (int)GetValue(MaxColumnsProperty); set => SetValue(MaxColumnsProperty, value); }
    public double Spacing { get => (double)GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    public static int Columns(double width, double minColumnWidth, int maxColumns, double spacing) =>
        Math.Clamp((int)Math.Floor((width + spacing) / (minColumnWidth + spacing)), 1, Math.Max(1, maxColumns));

    private double[] _rows = [];
    private int _columns;

    protected override Size MeasureOverride(Size available)
    {
        var width = double.IsInfinity(available.Width) ? MaxColumns * (MinColumnWidth + Spacing) - Spacing : available.Width;
        var columns = Columns(width, MinColumnWidth, MaxColumns, Spacing);
        var columnWidth = Math.Max(0, (width - Spacing * (columns - 1)) / columns);
        foreach (UIElement child in InternalChildren) child.Measure(new Size(columnWidth, double.PositiveInfinity));
        RowsFromDesiredHeights(columns);
        return new Size(width, _rows.Sum() + Spacing * Math.Max(0, _rows.Length - 1));
    }

    private void RowsFromDesiredHeights(int columns)
    {
        _columns = columns;
        _rows = new double[(InternalChildren.Count + columns - 1) / columns];
        for (var i = 0; i < InternalChildren.Count; i++)
            _rows[i / columns] = Math.Max(_rows[i / columns], InternalChildren[i].DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var columns = Columns(final.Width, MinColumnWidth, MaxColumns, Spacing);
        var columnWidth = Math.Max(0, (final.Width - Spacing * (columns - 1)) / columns);
        if (columns != _columns) RowsFromDesiredHeights(columns);
        var top = 0.0;
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var row = i / columns;
            if (i > 0 && i % columns == 0) top += _rows[row - 1] + Spacing;
            var height = _rows[row];
            InternalChildren[i].Arrange(new Rect((i % columns) * (columnWidth + Spacing), top, columnWidth, height));
        }
        return final;
    }
}
