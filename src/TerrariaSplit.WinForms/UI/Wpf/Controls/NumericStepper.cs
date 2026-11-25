using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using WpfControl = System.Windows.Controls.Control;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace TerrariaSplit.UI.Wpf.Controls;

internal sealed class NumericStepper : WpfControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(NumericStepper),
        new FrameworkPropertyMetadata(
            0d,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnValueChanged,
            CoerceValue));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum),
        typeof(double),
        typeof(NumericStepper),
        new PropertyMetadata(0d, OnRangeChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum),
        typeof(double),
        typeof(NumericStepper),
        new PropertyMetadata(100d, OnRangeChanged));

    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
        nameof(Step),
        typeof(double),
        typeof(NumericStepper),
        new PropertyMetadata(1d));

    public static readonly DependencyProperty DecimalPlacesProperty = DependencyProperty.Register(
        nameof(DecimalPlaces),
        typeof(int),
        typeof(NumericStepper),
        new PropertyMetadata(0, OnFormatChanged));

    public static readonly DependencyProperty SuffixProperty = DependencyProperty.Register(
        nameof(Suffix),
        typeof(string),
        typeof(NumericStepper),
        new PropertyMetadata(string.Empty));

    private WpfTextBox? textBox;
    private RepeatButton? incrementButton;
    private RepeatButton? decrementButton;
    private bool updatingText;

    public event RoutedPropertyChangedEventHandler<double>? ValueChanged;

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Step
    {
        get => (double)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public int DecimalPlaces
    {
        get => (int)GetValue(DecimalPlacesProperty);
        set => SetValue(DecimalPlacesProperty, value);
    }

    public string Suffix
    {
        get => (string)GetValue(SuffixProperty);
        set => SetValue(SuffixProperty, value);
    }

    public override void OnApplyTemplate()
    {
        DetachHandlers();
        base.OnApplyTemplate();

        textBox = GetTemplateChild("PART_TextBox") as WpfTextBox;
        incrementButton = GetTemplateChild("PART_IncrementButton") as RepeatButton;
        decrementButton = GetTemplateChild("PART_DecrementButton") as RepeatButton;

        if (textBox is not null)
        {
            textBox.LostKeyboardFocus += OnTextBoxLostFocus;
            textBox.PreviewKeyDown += OnTextBoxPreviewKeyDown;
            textBox.PreviewMouseWheel += OnPreviewMouseWheel;
        }

        if (incrementButton is not null)
        {
            incrementButton.Click += OnIncrementClick;
        }

        if (decrementButton is not null)
        {
            decrementButton.Click += OnDecrementClick;
        }

        SyncTextFromValue();
    }

    private static object CoerceValue(DependencyObject d, object baseValue)
    {
        var control = (NumericStepper)d;
        double value = (double)baseValue;
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            value = control.Minimum;
        }

        double clamped = Math.Clamp(value, control.Minimum, Math.Max(control.Minimum, control.Maximum));
        int decimals = Math.Clamp(control.DecimalPlaces, 0, 6);
        return Math.Round(clamped, decimals, MidpointRounding.AwayFromZero);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NumericStepper)d;
        control.SyncTextFromValue();
        control.ValueChanged?.Invoke(control, new RoutedPropertyChangedEventArgs<double>((double)e.OldValue, (double)e.NewValue));
    }

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        d.CoerceValue(ValueProperty);
    }

    private static void OnFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NumericStepper)d;
        control.CoerceValue(ValueProperty);
        control.SyncTextFromValue();
    }

    private void OnIncrementClick(object sender, RoutedEventArgs e)
    {
        AdjustBy(Step);
    }

    private void OnDecrementClick(object sender, RoutedEventArgs e)
    {
        AdjustBy(-Step);
    }

    private void OnTextBoxLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        CommitTextInput();
    }

    private void OnTextBoxPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Up)
        {
            AdjustBy(Step);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            AdjustBy(-Step);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            CommitTextInput();
            e.Handled = true;
        }
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (textBox is null || !textBox.IsKeyboardFocusWithin)
        {
            return;
        }

        AdjustBy(e.Delta > 0 ? Step : -Step);
        e.Handled = true;
    }

    private void AdjustBy(double delta)
    {
        CommitTextInput();
        Value += delta;
    }

    private void CommitTextInput()
    {
        if (textBox is null || updatingText)
        {
            return;
        }

        string raw = textBox.Text.Trim();
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ||
            double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
        {
            Value = parsed;
        }

        SyncTextFromValue();
    }

    private void SyncTextFromValue()
    {
        if (textBox is null)
        {
            return;
        }

        updatingText = true;
        try
        {
            int decimals = Math.Clamp(DecimalPlaces, 0, 6);
            string format = decimals == 0 ? "0" : "0." + new string('#', decimals);
            textBox.Text = Value.ToString(format, CultureInfo.InvariantCulture);
        }
        finally
        {
            updatingText = false;
        }
    }

    private void DetachHandlers()
    {
        if (textBox is not null)
        {
            textBox.LostKeyboardFocus -= OnTextBoxLostFocus;
            textBox.PreviewKeyDown -= OnTextBoxPreviewKeyDown;
            textBox.PreviewMouseWheel -= OnPreviewMouseWheel;
        }

        if (incrementButton is not null)
        {
            incrementButton.Click -= OnIncrementClick;
        }

        if (decrementButton is not null)
        {
            decrementButton.Click -= OnDecrementClick;
        }
    }
}
