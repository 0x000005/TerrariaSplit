using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed partial class DebugSettingsPage : SettingsPageBase
{
    private static void SetValue(Label label, DebugSettingsDisplayValue value)
    {
        SetValue(label, value.Text, value.Color);
    }

    private static void SetValue(Label label, string text)
    {
        SetValue(label, text, UiTheme.Text);
    }

    private static void SetValue(Label label, string text, Color color)
    {
        if (!string.Equals(label.Text, text, StringComparison.Ordinal))
        {
            label.Text = text;
        }

        if (label.ForeColor != color)
        {
            label.ForeColor = color;
        }
    }

    private static void SetSequenceText(TextBox textBox, string text)
    {
        if (!string.Equals(textBox.Text, text, StringComparison.Ordinal))
        {
            textBox.Text = text;
        }
    }

    private static TableLayoutPanel CreateSection(SettingsForm owner, string title)
    {
        return SettingsUiFactory.For(owner).CreateSection(title);
    }

    private static FlowLayoutPanel CreateActionBar(SettingsForm owner)
    {
        return SettingsUiFactory.For(owner).CreateActionBar();
    }

    private static TableLayoutPanel CreateGrid(SettingsForm owner)
    {
        SettingsUiFactory factory = SettingsUiFactory.For(owner);
        return factory.CreateGrid(
            SettingsUiFactory.ColumnStyleAbsolute(290f),
            SettingsUiFactory.ColumnStylePercent(100f));
    }

    private static void AddValueRow(TableLayoutPanel grid, SettingsForm owner, string label, Label valueLabel)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));
        grid.Controls.Add(CreateRowLabel(owner, label), 0, row);
        grid.Controls.Add(valueLabel, 1, row);
    }

    private static Label CreateRowLabel(SettingsForm owner, string text)
    {
        return SettingsUiFactory.For(owner).CreateRowLabel(text);
    }

    private static Label CreateValueLabel()
    {
        return new SettingsUiFactory(static key => key).CreateValueLabel();
    }

    private static Label CreateMutedLabel(SettingsForm owner, string text)
    {
        return SettingsUiFactory.For(owner).CreateMutedLabel(text);
    }

    private static TextBox CreateMultilineValueBox(int height)
    {
        return new SettingsUiFactory(static key => key).CreateMultilineValueBox(height);
    }

    private static Button CreateActionButton(SettingsForm owner, string text)
    {
        return SettingsUiFactory.For(owner).CreateActionButton(text);
    }

    private static void AddSection(TableLayoutPanel parent, Control section)
    {
        SettingsUiFactory.AddSection(parent, section);
    }

    private static void AddSectionControl(TableLayoutPanel section, Control control)
    {
        SettingsUiFactory.AddSectionControl(section, control);
    }
}
