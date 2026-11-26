using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace TerrariaSplit.UI.Wpf.Settings;

internal sealed class WpfColorSettingsPage : WpfSettingsPageBase
{
    private const double SwatchColumnWidth = 156;
    private readonly Dictionary<string, WpfTextBox> colorTextBoxes = new(StringComparer.Ordinal);

    public override SettingsPageId Id => SettingsPageId.Colors;

    internal IReadOnlyDictionary<string, WpfTextBox> ColorTextBoxes => colorTextBoxes;

    protected override FrameworkElement BuildPage(WpfSettingsPageContext context)
    {
        return WpfSettingsUi.CreateScrollPage(BuildSections);
    }

    public override void Apply(AppSettings settings)
    {
        WpfSettingsBinder.ApplyColors(settings, colorTextBoxes);
    }

    private void BuildSections(StackPanel parent)
    {
        Draft.Overlay.Colors ??= new UiColorSettings();

        // UI Colors
        var uiCard = WpfSettingsUi.CreateCard(Localize("UI Colors"), out StackPanel uiBody);
        uiBody.Children.Add(CreateColorTableHeader(Localize("Text type"), Localize("Text"), Localize("Outline"), Localize("Shadow")));
        for (int i = 0; i < SettingsDescriptors.TextColors.Count; i++)
        {
            TextColorDescriptor desc = SettingsDescriptors.TextColors[i];
            uiBody.Children.Add(CreateTextColorRow(
                desc.Label,
                desc.TextKey, desc.GetText(Draft.Overlay.Colors),
                desc.OutlineKey, desc.GetOutline(Draft.Overlay.Colors),
                desc.ShadowKey, desc.GetShadow(Draft.Overlay.Colors),
                isLast: i == SettingsDescriptors.TextColors.Count - 1));
        }

        parent.Children.Add(uiCard);

        // Icon Colors
        var iconCard = WpfSettingsUi.CreateCard(Localize("Icon Colors"), out StackPanel iconBody);
        iconBody.Children.Add(CreateTwoColorTableHeader(Localize("Icon type"), Localize("Outline"), Localize("Shadow")));
        ColorDescriptor outline = GetIconColorDescriptor(nameof(UiColorSettings.IconOutline));
        ColorDescriptor shadow = GetIconColorDescriptor(nameof(UiColorSettings.IconShadow));
        iconBody.Children.Add(CreateIconColorRow(
            outline.Key, outline.GetValue(Draft.Overlay.Colors),
            shadow.Key, shadow.GetValue(Draft.Overlay.Colors)));
        parent.Children.Add(iconCard);

        // Animation Colors
        var animCard = WpfSettingsUi.CreateCard(Localize("Animation Colors"), out StackPanel animBody);
        animBody.Children.Add(CreateColorTableHeader(Localize("Text type"), Localize("Text"), Localize("Outline"), Localize("Shadow")));
        for (int i = 0; i < SettingsDescriptors.AnimationColors.Count; i++)
        {
            TextColorDescriptor desc = SettingsDescriptors.AnimationColors[i];
            animBody.Children.Add(CreateTextColorRow(
                desc.Label,
                desc.TextKey, desc.GetText(Draft.Overlay.Colors),
                desc.OutlineKey, desc.GetOutline(Draft.Overlay.Colors),
                desc.ShadowKey, desc.GetShadow(Draft.Overlay.Colors),
                isLast: i == SettingsDescriptors.AnimationColors.Count - 1));
        }

        parent.Children.Add(animCard);
    }

    private static Border CreateColorTableHeader(string col0, string col1, string col2, string col3)
    {
        Grid grid = WpfSettingsUi.CreateColumnGrid(
            new GridLength(1, GridUnitType.Star),
            new GridLength(SwatchColumnWidth),
            new GridLength(SwatchColumnWidth),
            new GridLength(SwatchColumnWidth));

        AddHeaderCell(grid, col0, 0, new Thickness(0, 0, 8, 0), TextAlignment.Left);
        AddHeaderCell(grid, col1, 1, new Thickness(6, 0, 0, 0), TextAlignment.Center);
        AddHeaderCell(grid, col2, 2, new Thickness(6, 0, 0, 0), TextAlignment.Center);
        AddHeaderCell(grid, col3, 3, new Thickness(6, 0, 0, 0), TextAlignment.Center);

        return WpfSettingsUi.CreateTableHeaderContainer(grid);
    }

    private static Border CreateTwoColorTableHeader(string col0, string col1, string col2)
    {
        Grid grid = WpfSettingsUi.CreateColumnGrid(
            new GridLength(1, GridUnitType.Star),
            new GridLength(SwatchColumnWidth),
            new GridLength(SwatchColumnWidth));

        AddHeaderCell(grid, col0, 0, new Thickness(0, 0, 8, 0), TextAlignment.Left);
        AddHeaderCell(grid, col1, 1, new Thickness(6, 0, 0, 0), TextAlignment.Center);
        AddHeaderCell(grid, col2, 2, new Thickness(6, 0, 0, 0), TextAlignment.Center);

        return WpfSettingsUi.CreateTableHeaderContainer(grid);
    }

    private static void AddHeaderCell(Grid grid, string text, int column, Thickness margin, TextAlignment alignment)
    {
        TextBlock header = WpfSettingsUi.CreateTableHeaderText(text, alignment);
        header.Margin = margin;
        Grid.SetColumn(header, column);
        grid.Children.Add(header);
    }

    private Border CreateTextColorRow(
        string labelKey,
        string textKey, string textValue,
        string outlineKey, string outlineValue,
        string shadowKey, string shadowValue,
        bool isLast)
    {
        Grid grid = WpfSettingsUi.CreateColumnGrid(
            new GridLength(1, GridUnitType.Star),
            new GridLength(SwatchColumnWidth),
            new GridLength(SwatchColumnWidth),
            new GridLength(SwatchColumnWidth));

        TextBlock label = WpfSettingsUi.CreateRowLabel(Localize(labelKey));
        label.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        FrameworkElement textEditor = CreateColorEditor(textKey, textValue);
        textEditor.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(textEditor, 1);
        grid.Children.Add(textEditor);

        FrameworkElement outlineEditor = CreateColorEditor(outlineKey, outlineValue);
        outlineEditor.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(outlineEditor, 2);
        grid.Children.Add(outlineEditor);

        FrameworkElement shadowEditor = CreateColorEditor(shadowKey, shadowValue);
        shadowEditor.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(shadowEditor, 3);
        grid.Children.Add(shadowEditor);

        return WpfSettingsUi.CreateTableRowContainer(grid, showBottomBorder: !isLast);
    }

    private Border CreateIconColorRow(string outlineKey, string outlineValue, string shadowKey, string shadowValue)
    {
        Grid grid = WpfSettingsUi.CreateColumnGrid(
            new GridLength(1, GridUnitType.Star),
            new GridLength(SwatchColumnWidth),
            new GridLength(SwatchColumnWidth));

        TextBlock label = WpfSettingsUi.CreateRowLabel(Localize("Icon"));
        label.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        FrameworkElement outlineEditor = CreateColorEditor(outlineKey, outlineValue);
        outlineEditor.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(outlineEditor, 1);
        grid.Children.Add(outlineEditor);

        FrameworkElement shadowEditor = CreateColorEditor(shadowKey, shadowValue);
        shadowEditor.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(shadowEditor, 2);
        grid.Children.Add(shadowEditor);

        return WpfSettingsUi.CreateTableRowContainer(grid, showBottomBorder: false);
    }

    private Grid CreateColorEditor(string key, string value)
    {
        Grid grid = WpfSettingsUi.CreateColumnGrid(
            new GridLength(1, GridUnitType.Star),
            GridLength.Auto);

        WpfTextBox textBox = WpfSettingsUi.CreateTextBox(value, mono: true, centerAlign: true);
        textBox.Padding = new Thickness(6, 0, 6, 0);
        textBox.Margin = new Thickness(0, 0, 5, 0);
        colorTextBoxes[key] = textBox;

        var swatchBorder = new Border
        {
            Width = 14,
            Height = 14,
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        swatchBorder.SetResourceReference(Border.BorderBrushProperty, "BorderStrongBrush");

        var transparentGlyph = new TextBlock
        {
            Text = "T",
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        transparentGlyph.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        swatchBorder.Child = transparentGlyph;

        var pickButton = new WpfButton
        {
            Width = 26,
            Height = 26,
            MinHeight = 26,
            Padding = new Thickness(0),
            Content = swatchBorder,
            VerticalAlignment = VerticalAlignment.Center
        };
        pickButton.Click += (_, _) => Dialogs.PickColor(textBox);

        UpdateSwatch(swatchBorder, transparentGlyph, value);
        textBox.TextChanged += (_, _) =>
        {
            UpdateSwatch(swatchBorder, transparentGlyph, textBox.Text);
            SyncDraftColor(key, textBox.Text);
        };

        Grid.SetColumn(textBox, 0);
        Grid.SetColumn(pickButton, 1);
        grid.Children.Add(textBox);
        grid.Children.Add(pickButton);
        return grid;
    }

    private static void UpdateSwatch(Border swatchBorder, TextBlock transparentGlyph, string colorText)
    {
        System.Drawing.Color parsed = ColorText.Parse(colorText, System.Drawing.Color.White);
        bool transparent = parsed.A == 0;
        if (transparent)
        {
            swatchBorder.SetResourceReference(Border.BackgroundProperty, "FieldBgBrush");
            transparentGlyph.Visibility = Visibility.Visible;
        }
        else
        {
            swatchBorder.Background = WpfSettingsUi.Freeze(WpfColor.FromRgb(parsed.R, parsed.G, parsed.B));
            transparentGlyph.Visibility = Visibility.Collapsed;
        }
    }

    private void SyncDraftColor(string key, string colorText)
    {
        Draft.Overlay.Colors ??= new UiColorSettings();
        string normalized = ColorText.Format(ColorText.Parse(colorText, System.Drawing.Color.White));
        foreach (TextColorDescriptor descriptor in SettingsDescriptors.TextColors.Concat(SettingsDescriptors.AnimationColors))
        {
            if (descriptor.TextKey == key)
            {
                descriptor.SetText(Draft.Overlay.Colors, normalized);
                return;
            }

            if (descriptor.OutlineKey == key)
            {
                descriptor.SetOutline(Draft.Overlay.Colors, normalized);
                return;
            }

            if (descriptor.ShadowKey == key)
            {
                descriptor.SetShadow(Draft.Overlay.Colors, normalized);
                return;
            }
        }

        foreach (ColorDescriptor descriptor in SettingsDescriptors.IconColors)
        {
            if (descriptor.Key == key)
            {
                descriptor.SetValue(Draft.Overlay.Colors, normalized);
                return;
            }
        }
    }

    private static ColorDescriptor GetIconColorDescriptor(string key)
    {
        foreach (ColorDescriptor descriptor in SettingsDescriptors.IconColors)
        {
            if (string.Equals(descriptor.Key, key, StringComparison.Ordinal))
            {
                return descriptor;
            }
        }

        throw new InvalidOperationException($"Missing icon color descriptor: {key}");
    }
}
