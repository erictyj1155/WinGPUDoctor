using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WinGPUDoctor.Desktop.Views;
using Xunit;

namespace WinGPUDoctor.Tests;

public class DesktopLayoutTests
{
    // WPF elements need an STA thread.
    internal static void OnSta(Action action)
    {
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }

    private static Rect Layout(SplitPanel panel, double width)
    {
        panel.Measure(new Size(width, double.PositiveInfinity));
        panel.Arrange(new Rect(new Point(), panel.DesiredSize));
        return LayoutInformation.GetLayoutSlot((FrameworkElement)panel.Children[1]);
    }

    [Fact]
    public void WindowStartsInsideTheWorkAreaAndItsMinimumSizeStacksTwoColumnPages()
    {
        var xaml = File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../..", "src", "WinGPUDoctor.Desktop", "MainWindow.xaml"));
        var window = Regex.Match(xaml, @"<Window\b[^>]*>").Value;
        double Attribute(string name) =>
            double.Parse(Regex.Match(window, $@"\s{name}=""(\d+)""").Groups[1].Value, CultureInfo.InvariantCulture);
        var (preferred, minimum) = (new Size(Attribute("Width"), Attribute("Height")), new Size(Attribute("MinWidth"), Attribute("MinHeight")));
        // 1366 × 768 at 150% scaling leaves about 910 × 480 device-independent pixels above the taskbar.
        var small = new Size(910, 480);
        Assert.True(minimum.Width <= small.Width && minimum.Height <= small.Height);
        Assert.True(minimum.Width >= 480 && minimum.Height >= 400); // Still room for a column of content and its actions.
        OnSta(() =>
        {
            Assert.Equal(small, WinGPUDoctor.Desktop.MainWindow.FitToWorkArea(preferred, minimum, small));
            Assert.Equal(preferred, WinGPUDoctor.Desktop.MainWindow.FitToWorkArea(preferred, minimum, new Size(1920, 1040)));
            Assert.Equal(minimum, WinGPUDoctor.Desktop.MainWindow.FitToWorkArea(preferred, minimum, new Size(400, 300)));
            // At the minimum width the two columns are stacked; at the preferred width they sit side by side.
            var stackWidth = (double)SplitPanel.StackWidthProperty.DefaultMetadata.DefaultValue;
            Assert.True(SplitPanel.Stacks(minimum.Width, stackWidth));
            Assert.False(SplitPanel.Stacks(preferred.Width, stackWidth));
        });
    }

    [Fact]
    public void AnimationsRunOnlyWhileVisibleAndWhenWindowsAnimationEffectsAreOn()
    {
        Assert.True(Motion.ShouldAnimate(isVisible: true, animationsEnabled: true));
        Assert.False(Motion.ShouldAnimate(isVisible: true, animationsEnabled: false)); // "Animation effects" off
        Assert.False(Motion.ShouldAnimate(isVisible: false, animationsEnabled: true));
        Assert.Equal(SystemParameters.ClientAreaAnimation, Motion.IsEnabled);
    }

    [Fact]
    public void TwoColumnPagesStackTheSecondColumnBelowTheFirstWhenNarrow()
    {
        Assert.True(SplitPanel.Stacks(719, 720));
        Assert.False(SplitPanel.Stacks(720, 720));
        OnSta(() =>
        {
            var panel = new SplitPanel { StackWidth = 720, FirstShare = 0.5, MinHeight = 500 };
            panel.Children.Add(new Border { Height = 300 });
            panel.Children.Add(new Border { MinHeight = 100 });

            var side = Layout(panel, 1000);
            Assert.False(panel.IsStacked);
            Assert.Equal(new Rect(500, 0, 500, 500), side); // Beside the first column, filling the page height.

            var below = Layout(panel, 600);
            Assert.True(panel.IsStacked);
            Assert.Equal(new Rect(0, 300, 600, 200), below); // Below the first column, filling what is left.
        });
    }
}
