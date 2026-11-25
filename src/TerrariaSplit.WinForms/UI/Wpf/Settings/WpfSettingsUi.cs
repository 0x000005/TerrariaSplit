using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using TerrariaSplit.UI.Wpf.Controls;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfColor = System.Windows.Media.Color;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfPanel = System.Windows.Controls.Panel;
using WpfRadioButton = System.Windows.Controls.RadioButton;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace TerrariaSplit.UI.Wpf.Settings;

internal static class WpfSettingsUi
{
    public const double NumericFieldWidth = 104;
    public const double TimeFieldWidth = 140;
    public const double StandardFieldWidth = 220;
    public const double WideFieldWidth = 260;

    private static readonly SolidColorBrush WarningTextBrush = Freeze(WpfColor.FromRgb(0xF5, 0xC9, 0x7B));

    public static ScrollViewer CreateScrollPage(Action<StackPanel> buildSections)
    {
        var content = new StackPanel
        {
            Orientation = WpfOrientation.Vertical,
            Margin = new Thickness(16, 10, 16, 16)
        };
        buildSections(content);

        return new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            CanContentScroll = false,
            Content = content
        };
    }

    public static SettingCard CreateCard(
        string? header,
        out StackPanel body,
        string? subtitle = null,
        object? headerAction = null,
        Thickness? contentPadding = null)
    {
        body = new StackPanel
        {
            Orientation = WpfOrientation.Vertical
        };
        var card = new SettingCard
        {
            Header = header ?? string.Empty,
            Subtitle = subtitle ?? string.Empty,
            HeaderAction = headerAction,
            ContentPadding = contentPadding ?? new Thickness(14, 4, 14, 4),
            Content = body
        };
        return card;
    }

    public static SettingRow AddSettingRow(
        WpfPanel container,
        string title,
        FrameworkElement control,
        bool isLast = false,
        string? description = null)
    {
        var row = new SettingRow
        {
            Title = title,
            Description = description ?? string.Empty,
            ShowDivider = !isLast,
            Content = control
        };
        container.Children.Add(row);
        return row;
    }

    public static TextBlock CreateSubHeader(string text, Thickness? margin = null)
    {
        var label = new TextBlock
        {
            Text = text,
            FontWeight = FontWeights.SemiBold,
            Margin = margin ?? new Thickness(0, 10, 0, 5)
        };
        label.SetResourceReference(TextBlock.FontSizeProperty, "FontSizeBody");
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        return label;
    }

    public static TextBlock CreateMutedText(string text, Thickness? margin = null)
    {
        var label = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 17,
            Margin = margin ?? new Thickness(0, 4, 0, 6)
        };
        label.SetResourceReference(TextBlock.FontSizeProperty, "FontSizeSub");
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
        return label;
    }

    public static Border CreateNoticeBanner(string text, bool isWarning = false, FrameworkElement? action = null)
    {
        return CreateNoticeGroup((text, isWarning, action));
    }

    public static Border CreateNoticeGroup(params (string Text, bool IsWarning, FrameworkElement? Action)[] items)
    {
        var stack = new StackPanel { Orientation = WpfOrientation.Vertical };
        for (int i = 0; i < items.Length; i++)
        {
            (string text, bool isWarning, FrameworkElement? action) = items[i];
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            if (action is not null)
            {
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }

            if (i > 0)
            {
                bool hasAction = action is not null || items[i - 1].Action is not null;
                row.Margin = new Thickness(0, hasAction ? 6 : 4, 0, 0);
            }

            var textBlock = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                LineHeight = 18
            };
            textBlock.SetResourceReference(TextBlock.FontSizeProperty, "FontSizeSub");
            if (isWarning)
            {
                textBlock.Foreground = WarningTextBrush;
            }
            else
            {
                textBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            }

            Grid.SetColumn(textBlock, 0);
            row.Children.Add(textBlock);

            if (action is not null)
            {
                action.Margin = new Thickness(12, 0, 0, 0);
                action.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(action, 1);
                row.Children.Add(action);
            }

            stack.Children.Add(row);
        }

        var border = new Border
        {
            Padding = new Thickness(0, 4, 0, 4),
            Margin = new Thickness(0, 2, 0, 2),
            Child = stack
        };
        return border;
    }

    public static WpfCheckBox CreateToggleSwitch(bool isChecked)
    {
        var toggle = new WpfCheckBox
        {
            IsChecked = isChecked,
            VerticalAlignment = VerticalAlignment.Center
        };
        toggle.SetResourceReference(FrameworkElement.StyleProperty, "ToggleSwitchStyle");
        return toggle;
    }

    public static WpfCheckBox CreateCheckBox(bool isChecked, bool centered = false)
    {
        return new WpfCheckBox
        {
            IsChecked = isChecked,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = centered ? WpfHorizontalAlignment.Center : WpfHorizontalAlignment.Left
        };
    }

    public static ToggleButton CreateChipToggle(string text, bool isChecked)
    {
        var chip = new ToggleButton
        {
            Content = new TextBlock
            {
                Text = text,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            },
            IsChecked = isChecked,
            MinHeight = 26,
            HorizontalAlignment = WpfHorizontalAlignment.Stretch,
            HorizontalContentAlignment = WpfHorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        chip.SetResourceReference(FrameworkElement.StyleProperty, "ChipToggleStyle");
        return chip;
    }

    public static WpfTextBox CreateTextBox(
        string text,
        string? placeholder = null,
        double? width = null,
        bool mono = false,
        bool rightAlign = false,
        bool centerAlign = false)
    {
        TextAlignment textAlign = centerAlign
            ? TextAlignment.Center
            : rightAlign
                ? TextAlignment.Right
                : TextAlignment.Left;
        WpfHorizontalAlignment contentAlign = centerAlign
            ? WpfHorizontalAlignment.Center
            : rightAlign
                ? WpfHorizontalAlignment.Right
                : WpfHorizontalAlignment.Left;

        var box = new WpfTextBox
        {
            Text = text ?? string.Empty,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = textAlign,
            HorizontalContentAlignment = contentAlign
        };
        if (width.HasValue)
        {
            box.Width = width.Value;
        }

        if (!string.IsNullOrEmpty(placeholder))
        {
            TextBoxHelper.SetPlaceholder(box, placeholder);
        }

        if (mono)
        {
            box.SetResourceReference(System.Windows.Controls.Control.FontFamilyProperty, "MonoFontFamily");
        }

        return box;
    }

    public static WpfTextBox CreateNumberBox(int value, int minimum, int maximum, double width = NumericFieldWidth)
    {
        WpfTextBox box = CreateTextBox(
            Math.Clamp(value, minimum, maximum).ToString(CultureInfo.InvariantCulture),
            width: width,
            mono: true,
            centerAlign: true);
        box.Padding = width <= 76 ? new Thickness(5, 0, 5, 0) : new Thickness(8, 0, 8, 0);
        return box;
    }

    public static WpfTextBox CreateDecimalBox(float value, decimal minimum, decimal maximum, double width = NumericFieldWidth)
    {
        WpfTextBox box = CreateTextBox(
            Math.Clamp((decimal)value, minimum, maximum).ToString("0.#", CultureInfo.InvariantCulture),
            width: width,
            mono: true,
            centerAlign: true);
        box.Padding = width <= 76 ? new Thickness(5, 0, 5, 0) : new Thickness(8, 0, 8, 0);
        return box;
    }

    public static WpfTextBox CreateTimeBox(string text, string? placeholder = null, double width = TimeFieldWidth)
    {
        WpfTextBox box = CreateTextBox(
            text,
            placeholder: placeholder,
            width: width,
            mono: true,
            centerAlign: true);
        box.Padding = new Thickness(8, 0, 8, 0);
        return box;
    }

    public static WpfTextBox CreateMultilineTextBox(string text, string? placeholder = null, double height = 92)
    {
        WpfTextBox box = CreateTextBox(text, placeholder: placeholder);
        box.AcceptsReturn = true;
        box.TextWrapping = TextWrapping.Wrap;
        box.VerticalContentAlignment = VerticalAlignment.Top;
        box.Padding = new Thickness(10, 7, 10, 7);
        box.Height = height;
        box.Margin = new Thickness(0, 4, 0, 6);
        ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);
        return box;
    }

    public static WpfComboBox CreateComboBox(double? width = null)
    {
        var combo = new WpfComboBox
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        if (width.HasValue)
        {
            combo.Width = width.Value;
        }

        return combo;
    }

    public static WpfButton CreateButton(string text, bool accent = false, bool ghost = false, double? minWidth = null)
    {
        var button = new WpfButton
        {
            Content = text,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (minWidth.HasValue)
        {
            button.MinWidth = minWidth.Value;
        }

        if (accent)
        {
            button.SetResourceReference(FrameworkElement.StyleProperty, "AccentButtonStyle");
        }
        else if (ghost)
        {
            button.SetResourceReference(FrameworkElement.StyleProperty, "GhostButtonStyle");
        }

        return button;
    }

    public static HotkeyCaptureBox CreateHotkeyBox(Keys selected, double width = StandardFieldWidth)
    {
        return new HotkeyCaptureBox
        {
            Hotkey = selected,
            Width = width,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public static WpfRadioButton CreateNavRadio(string text, string iconGeometryData)
    {
        var icon = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(iconGeometryData),
            Width = 14,
            Height = 14,
            Stretch = Stretch.Uniform,
            StrokeThickness = 1.25,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Margin = new Thickness(0, 0, 9, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var label = new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var content = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(icon);
        content.Children.Add(label);

        var radio = new WpfRadioButton
        {
            GroupName = "SettingsNav",
            Content = content,
            Margin = new Thickness(0, 1, 0, 1)
        };
        radio.SetResourceReference(FrameworkElement.StyleProperty, "NavRadioStyle");
        icon.SetBinding(
            System.Windows.Shapes.Shape.StrokeProperty,
            new System.Windows.Data.Binding(nameof(System.Windows.Controls.Control.Foreground)) { Source = radio });
        return radio;
    }

    public static TextBlock CreateTableHeaderText(string text, TextAlignment alignment = TextAlignment.Left)
    {
        var header = new TextBlock
        {
            Text = text,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = text
        };
        header.SetResourceReference(TextBlock.FontSizeProperty, "FontSizeSub");
        header.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
        return header;
    }

    public static TextBlock CreateRowLabel(string text)
    {
        var label = new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = text
        };
        label.SetResourceReference(TextBlock.FontSizeProperty, "FontSizeBody");
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        return label;
    }

    public static Grid CreateColumnGrid(params GridLength[] columns)
    {
        var grid = new Grid();
        foreach (GridLength length in columns)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = length });
        }

        return grid;
    }

    public static Border CreateRowDividerContainer(
        UIElement child,
        bool showBottomBorder = true,
        Thickness? padding = null,
        string? minHeightResourceKey = "SettingRowHeight")
    {
        var border = new Border
        {
            Padding = padding ?? new Thickness(0, 4, 0, 4),
            BorderThickness = showBottomBorder ? new Thickness(0, 0, 0, 1) : new Thickness(0),
            Child = child
        };
        if (!string.IsNullOrEmpty(minHeightResourceKey))
        {
            border.SetResourceReference(FrameworkElement.MinHeightProperty, minHeightResourceKey);
        }

        border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        return border;
    }

    public static Border CreateTableHeaderContainer(UIElement child)
    {
        return CreateRowDividerContainer(
            child,
            showBottomBorder: true,
            padding: new Thickness(0, 4, 0, 4),
            minHeightResourceKey: "TableHeaderHeight");
    }

    public static Border CreateTableRowContainer(UIElement child, bool showBottomBorder = true)
    {
        return CreateRowDividerContainer(
            child,
            showBottomBorder: showBottomBorder,
            padding: new Thickness(0, 3, 0, 3),
            minHeightResourceKey: "TableRowHeight");
    }

    public static SolidColorBrush Freeze(WpfColor color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
