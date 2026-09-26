using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace WinGPUDoctor.Desktop.Views;

// An (i) button for secondary text. It is a Tab stop; the text appears on mouse hover, on a focus
// change caused by keyboard input from another element in the window (Tab, Shift+Tab or other
// keyboard navigation; the rule does not identify the key), and on Enter/Space or click. It closes
// with Escape or when focus leaves. Focus that returns when the window is reactivated (for example
// after Alt+Tab) does not open it. Screen readers get the name from Label and the text as HelpText.
public sealed class InfoTip : Button
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string),
        typeof(InfoTip), new PropertyMetadata(null, (d, e) => ((InfoTip)d).OnTextChanged((string?)e.NewValue)));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string),
        typeof(InfoTip), new PropertyMetadata(null, (d, e) => AutomationProperties.SetName(d, (string?)e.NewValue ?? "")));

    private readonly TextBlock _body = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 380 };
    private readonly ToolTip _tip;

    public InfoTip()
    {
        Content = "\uE946";
        _tip = new ToolTip { Content = _body, Placement = PlacementMode.Bottom };
        ToolTip = _tip;
        // The control decides itself when keyboard focus opens the tip (see ShowsOnFocusChange).
        ToolTipService.SetShowsToolTipOnKeyboardFocus(this, false);
        ToolTipService.SetInitialShowDelay(this, 250);
        ToolTipService.SetShowDuration(this, int.MaxValue);
    }

    public string? Text { get => (string?)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    private void OnTextChanged(string? text)
    {
        _body.Text = text;
        AutomationProperties.SetHelpText(this, text ?? "");
    }

    protected override void OnClick()
    {
        base.OnClick();
        Open();
    }

    // Keyboard users see the text as soon as keyboard navigation lands here, without hover timing.
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        if (ShowsOnFocusChange(InputManager.Current.MostRecentInputDevice is KeyboardDevice, e.OldFocus is not null)) Open();
    }

    // True for a focus change caused by keyboard input that moves focus from another element, as Tab,
    // Shift+Tab and other keyboard navigation do. Focus restored on window reactivation arrives from no
    // element, and mouse focus is handled by hover and click.
    public static bool ShowsOnFocusChange(bool keyboardInput, bool fromAnotherElement) =>
        keyboardInput && fromAnotherElement;

    private void Open()
    {
        _tip.PlacementTarget = this;
        _tip.IsOpen = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _tip.IsOpen)
        {
            _tip.IsOpen = false;
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        _tip.IsOpen = false;
        base.OnLostKeyboardFocus(e);
    }
}
