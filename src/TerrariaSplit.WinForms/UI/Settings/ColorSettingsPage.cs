using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class ColorSettingsPage : SettingsPageBase
{
    private readonly Dictionary<string, TextBox> colorTextBoxes = new();

    public override SettingsPageId Id => SettingsPageId.Colors;

    internal IReadOnlyDictionary<string, TextBox> ColorTextBoxes => colorTextBoxes;

    protected override Control BuildPage(SettingsPageContext context)
    {
        return context.BuildScrollPage(BuildSections);
    }

    public override void Apply(AppSettings settings)
    {
        SettingsBinder.ApplyColors(settings, colorTextBoxes);
    }

    private void BuildSections(TableLayoutPanel parent)
    {
        Draft.Overlay.Colors ??= new UiColorSettings();

        TableLayoutPanel textSection = Factory.CreateSection("UI Colors");
        TableLayoutPanel textGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(214f),
            SettingsUiFactory.ColumnStyleAbsolute(214f),
            SettingsUiFactory.ColumnStyleAbsolute(214f));

        Factory.AddHeaderRow(textGrid, "Text type", "Text", "Outline", "Shadow");
        foreach (TextColorDescriptor descriptor in SettingsDescriptors.TextColors)
        {
            AddTextColorRow(
                textGrid,
                descriptor.Label,
                descriptor.TextKey,
                descriptor.GetText(Draft.Overlay.Colors),
                descriptor.OutlineKey,
                descriptor.GetOutline(Draft.Overlay.Colors),
                descriptor.ShadowKey,
                descriptor.GetShadow(Draft.Overlay.Colors));
        }

        SettingsUiFactory.AddSectionControl(textSection, textGrid);
        SettingsUiFactory.AddSection(parent, textSection);

        TableLayoutPanel iconSection = Factory.CreateSection("Icon Colors");
        TableLayoutPanel iconGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(214f),
            SettingsUiFactory.ColumnStyleAbsolute(214f));

        Factory.AddHeaderRow(iconGrid, "Icon type", "Outline", "Shadow");
        AddIconColorRow(iconGrid);

        SettingsUiFactory.AddSectionControl(iconSection, iconGrid);
        SettingsUiFactory.AddSection(parent, iconSection);

        TableLayoutPanel animationSection = Factory.CreateSection("Animation Colors");
        TableLayoutPanel animationGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(214f));

        Factory.AddHeaderRow(animationGrid, "Text type", "Text");
        foreach (ColorDescriptor descriptor in SettingsDescriptors.AnimationColors)
        {
            AddColorRow(
                animationGrid,
                descriptor.Label,
                descriptor.Key,
                descriptor.GetValue(Draft.Overlay.Colors));
        }

        SettingsUiFactory.AddSectionControl(animationSection, animationGrid);
        SettingsUiFactory.AddSection(parent, animationSection);
    }

    private void AddTextColorRow(
        TableLayoutPanel grid,
        string label,
        string textKey,
        string textValue,
        string outlineKey,
        string outlineValue,
        string shadowKey,
        string shadowValue)
    {
        int row = Factory.AddGridRow(grid);
        grid.Controls.Add(Factory.CreateRowLabel(label), 0, row);
        grid.Controls.Add(CreateColorEditor(textKey, textValue), 1, row);
        grid.Controls.Add(CreateColorEditor(outlineKey, outlineValue), 2, row);
        grid.Controls.Add(CreateColorEditor(shadowKey, shadowValue), 3, row);
    }

    private void AddIconColorRow(TableLayoutPanel grid)
    {
        ColorDescriptor outline = GetIconColorDescriptor(nameof(UiColorSettings.IconOutline));
        ColorDescriptor shadow = GetIconColorDescriptor(nameof(UiColorSettings.IconShadow));

        int row = Factory.AddGridRow(grid);
        grid.Controls.Add(Factory.CreateRowLabel("Icon"), 0, row);
        grid.Controls.Add(CreateColorEditor(outline.Key, outline.GetValue(Draft.Overlay.Colors)), 1, row);
        grid.Controls.Add(CreateColorEditor(shadow.Key, shadow.GetValue(Draft.Overlay.Colors)), 2, row);
    }

    private void AddColorRow(TableLayoutPanel grid, string label, string key, string value)
    {
        int row = Factory.AddGridRow(grid);
        grid.Controls.Add(Factory.CreateRowLabel(label), 0, row);
        grid.Controls.Add(CreateColorEditor(key, value), 1, row);
    }

    private Control CreateColorEditor(string key, string value)
    {
        TextBox textBox = Factory.CreateTextBox(value);
        colorTextBoxes[key] = textBox;

        Button pickButton = CreateColorButton(textBox);
        textBox.TextChanged += (_, _) =>
        {
            UpdateColorButton(pickButton, textBox.Text);
            SyncDraftColor(key, textBox.Text);
        };

        var editor = new TableLayoutPanel
        {
            BackColor = UiTheme.Surface,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1
        };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58f));
        editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        editor.Controls.Add(textBox, 0, 0);
        editor.Controls.Add(pickButton, 1, 0);
        return editor;
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

    private Button CreateColorButton(TextBox textBox)
    {
        var button = new Button
        {
            Height = 36,
            Margin = new Padding(10, 8, 0, 8),
            Text = string.Empty,
            Width = 48
        };
        UiTheme.StyleButton(button, accent: false, minimumWidth: 48);
        button.MinimumSize = new Size(48, 36);
        button.Padding = Padding.Empty;
        button.FlatAppearance.BorderColor = UiTheme.Border;
        button.Click += (_, _) => Dialogs.PickColor(textBox);
        UpdateColorButton(button, textBox.Text);
        return button;
    }

    private void SyncDraftColor(string key, string colorText)
    {
        Draft.Overlay.Colors ??= new UiColorSettings();
        string normalized = ColorText.Format(ColorText.Parse(colorText, Color.White));
        foreach (TextColorDescriptor descriptor in SettingsDescriptors.TextColors)
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

        foreach (ColorDescriptor descriptor in SettingsDescriptors.AnimationColors)
        {
            if (descriptor.Key == key)
            {
                descriptor.SetValue(Draft.Overlay.Colors, normalized);
                return;
            }
        }
    }

    private static void UpdateColorButton(Button button, string colorText)
    {
        Color color = ColorText.Parse(colorText, UiTheme.Text);
        bool transparent = color.A == 0;
        Color previewColor = transparent ? UiTheme.Field : Color.FromArgb(color.R, color.G, color.B);
        button.Text = transparent ? "T" : string.Empty;
        button.ForeColor = UiTheme.Text;
        button.BackColor = previewColor;
        button.FlatAppearance.MouseDownBackColor = previewColor;
        button.FlatAppearance.MouseOverBackColor = previewColor;
    }
}
