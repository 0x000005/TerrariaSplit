using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed partial class AnimationSettingsPage
{
    internal IReadOnlyDictionary<string, CompletionTextControls> CompletionTextOptions => completionTextControls;

    private TableLayoutPanel CreateCompletionTextGrid()
    {
        TableLayoutPanel grid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f), SettingsUiFactory.ColumnStyleAbsolute(220f),
            SettingsUiFactory.ColumnStyleAbsolute(70f), SettingsUiFactory.ColumnStyleAbsolute(70f),
            SettingsUiFactory.ColumnStyleAbsolute(100f), SettingsUiFactory.ColumnStyleAbsolute(100f),
            SettingsUiFactory.ColumnStyleAbsolute(100f));
        Factory.AddHeaderRow(grid, ContentAlignment.MiddleLeft,
            "Section", "Font family", "Bold", "Italic", "Opacity %", "Shadow %", "Outline %");
        foreach ((string key, string label, SplitCompletionTextStyle style) in TextParts(Draft.Overlay.SplitCompletionText))
        {
            var font = new FontFamilySelector();
            font.SetSelectedFontFamily(style.FontFamily);
            var bold = CreateComparisonCheckBox(style.Bold);
            var italic = CreateComparisonCheckBox(style.Italic);
            TextBox opacity = Factory.CreateNumberBox(style.OpacityPercent, 0, 100);
            TextBox shadow = Factory.CreateNumberBox(style.ShadowPercent, 0, 100);
            TextBox outline = Factory.CreateNumberBox(style.OutlineThicknessPercent, 0, 100);
            completionTextControls[key] = new(font, bold, italic, opacity, shadow, outline);
            font.SelectedIndexChanged += (_, _) => outlineStylePreview.Invalidate();
            bold.CheckedChanged += (_, _) => outlineStylePreview.Invalidate();
            italic.CheckedChanged += (_, _) => outlineStylePreview.Invalidate();
            opacity.TextChanged += (_, _) => outlineStylePreview.Invalidate();
            shadow.TextChanged += (_, _) => outlineStylePreview.Invalidate();
            outline.TextChanged += (_, _) => outlineStylePreview.Invalidate();
            int row = Factory.AddGridRow(grid);
            grid.Controls.Add(Factory.CreateRowLabel(label), 0, row);
            grid.Controls.Add(Factory.CreateCenteredCell(font, 210), 1, row);
            grid.Controls.Add(Factory.CreateCenteredCell(bold, 28), 2, row);
            grid.Controls.Add(Factory.CreateCenteredCell(italic, 28), 3, row);
            grid.Controls.Add(Factory.CreateCenteredCell(opacity, 72), 4, row);
            grid.Controls.Add(Factory.CreateCenteredCell(shadow, 72), 5, row);
            grid.Controls.Add(Factory.CreateCenteredCell(outline, 72), 6, row);
        }
        return grid;
    }

    private void ApplyCompletionText(SplitCompletionTextSettings settings)
    {
        foreach ((string key, _, SplitCompletionTextStyle style) in TextParts(settings))
        {
            ReadCompletionTextControls(key, style);
        }
    }

    private void ReadCompletionTextControls(string key, SplitCompletionTextStyle style)
    {
        CompletionTextControls controls = completionTextControls[key];
        style.FontFamily = controls.Font.SelectedFontFamily;
        style.Bold = controls.Bold.Checked;
        style.Italic = controls.Italic.Checked;
        style.OpacityPercent = SettingsValueParser.ParseIntBox(controls.Opacity, style.OpacityPercent, 0, 100);
        style.ShadowPercent = SettingsValueParser.ParseIntBox(controls.Shadow, style.ShadowPercent, 0, 100);
        style.OutlineThicknessPercent = SettingsValueParser.ParseIntBox(controls.Outline, style.OutlineThicknessPercent, 0, 100);
    }

    private static IEnumerable<(string Key, string Label, SplitCompletionTextStyle Style)> TextParts(SplitCompletionTextSettings settings)
    {
        yield return (nameof(settings.Time), "Time", settings.Time);
        yield return (nameof(settings.Hint), "Hint text", settings.Hint);
        yield return (nameof(settings.Delta), "Delta time", settings.Delta);
    }

    internal sealed record CompletionTextControls(FontFamilySelector Font, CheckBox Bold, CheckBox Italic,
        TextBox Opacity, TextBox Shadow, TextBox Outline);
}
