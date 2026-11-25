using System.Windows;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;

namespace TerrariaSplit.UI.Wpf.Controls;

internal sealed class ColorSwatchButton : WpfButton
{
    public static readonly DependencyProperty ColorHexProperty = DependencyProperty.Register(
        nameof(ColorHex),
        typeof(string),
        typeof(ColorSwatchButton),
        new FrameworkPropertyMetadata(
            "#FFFFFF",
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnColorHexChanged));

    public static readonly DependencyProperty SwatchBrushProperty = DependencyProperty.Register(
        nameof(SwatchBrush),
        typeof(WpfBrush),
        typeof(ColorSwatchButton),
        new PropertyMetadata(WpfBrushes.White));

    public ColorSwatchButton()
    {
        UpdateSwatchBrush();
    }

    public string ColorHex
    {
        get => (string)GetValue(ColorHexProperty);
        set => SetValue(ColorHexProperty, value);
    }

    public WpfBrush SwatchBrush
    {
        get => (WpfBrush)GetValue(SwatchBrushProperty);
        private set => SetValue(SwatchBrushProperty, value);
    }

    private static void OnColorHexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorSwatchButton button)
        {
            button.UpdateSwatchBrush();
        }
    }

    private void UpdateSwatchBrush()
    {
        System.Drawing.Color parsed = ColorText.Parse(ColorHex, System.Drawing.Color.White);
        var brush = new SolidColorBrush(WpfColor.FromArgb(parsed.A, parsed.R, parsed.G, parsed.B));
        brush.Freeze();
        SwatchBrush = brush;
    }
}
