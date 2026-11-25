using System.Windows;
using System.Windows.Input;
using WpfControl = System.Windows.Controls.Control;
using FormsKeys = System.Windows.Forms.Keys;

namespace TerrariaSplit.UI.Wpf.Controls;

internal sealed class HotkeyCaptureBox : WpfControl
{
    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey),
        typeof(FormsKeys),
        typeof(HotkeyCaptureBox),
        new FrameworkPropertyMetadata(
            FormsKeys.None,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnHotkeyChanged));

    public static readonly DependencyProperty DisplayTextProperty = DependencyProperty.Register(
        nameof(DisplayText),
        typeof(string),
        typeof(HotkeyCaptureBox),
        new PropertyMetadata("None"));

    public static readonly DependencyProperty EmptyPlaceholderProperty = DependencyProperty.Register(
        nameof(EmptyPlaceholder),
        typeof(string),
        typeof(HotkeyCaptureBox),
        new PropertyMetadata("None", OnHotkeyChanged));

    public static readonly DependencyProperty IsCapturingProperty = DependencyProperty.Register(
        nameof(IsCapturing),
        typeof(bool),
        typeof(HotkeyCaptureBox),
        new PropertyMetadata(false));

    public event EventHandler? HotkeyCaptured;

    public HotkeyCaptureBox()
    {
        Focusable = true;
        UpdateDisplayText();
    }

    public FormsKeys Hotkey
    {
        get => (FormsKeys)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, Normalize(value));
    }

    public string DisplayText
    {
        get => (string)GetValue(DisplayTextProperty);
        private set => SetValue(DisplayTextProperty, value);
    }

    public string EmptyPlaceholder
    {
        get => (string)GetValue(EmptyPlaceholderProperty);
        set => SetValue(EmptyPlaceholderProperty, value);
    }

    public bool IsCapturing
    {
        get => (bool)GetValue(IsCapturingProperty);
        private set => SetValue(IsCapturingProperty, value);
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton == MouseButton.Left)
        {
            Focus();
            IsCapturing = true;
            e.Handled = true;
        }
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        IsCapturing = true;
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        IsCapturing = false;
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        Key wpfKey = e.Key == Key.System ? e.SystemKey : e.Key;
        CaptureKey(wpfKey, Keyboard.Modifiers);
        e.Handled = true;
    }

    internal void CaptureKey(Key wpfKey, ModifierKeys modifiers)
    {
        if (wpfKey == Key.Escape && modifiers == ModifierKeys.None)
        {
            Hotkey = FormsKeys.None;
            HotkeyCaptured?.Invoke(this, EventArgs.Empty);
            return;
        }

        int virtualKey = KeyInterop.VirtualKeyFromKey(wpfKey);
        if (virtualKey <= 0 || virtualKey > byte.MaxValue)
        {
            return;
        }

        FormsKeys combined = (FormsKeys)virtualKey;
        if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            combined |= FormsKeys.Control;
        }

        if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
        {
            combined |= FormsKeys.Alt;
        }

        if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            combined |= FormsKeys.Shift;
        }

        if (HotkeyKeyValidator.TryNormalize(combined, out FormsKeys normalized))
        {
            Hotkey = normalized;
            HotkeyCaptured?.Invoke(this, EventArgs.Empty);
        }
    }

    private static FormsKeys Normalize(FormsKeys value)
    {
        if (value == FormsKeys.None)
        {
            return FormsKeys.None;
        }

        return HotkeyKeyValidator.TryNormalize(value, out FormsKeys normalized)
            ? normalized
            : FormsKeys.None;
    }

    private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyCaptureBox box)
        {
            box.UpdateDisplayText();
        }
    }

    private void UpdateDisplayText()
    {
        DisplayText = Hotkey == FormsKeys.None
            ? EmptyPlaceholder
            : HotkeyKeyValidator.Format(Hotkey);
    }
}
