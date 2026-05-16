using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed partial class SettingsForm : Form
{

    internal void AddColumnSettingsSection(TableLayoutPanel parent)
    {
        ConfigureCheckBox(enableDynamicDeltaTimeUnitsBox, settings.EnableDynamicDeltaTimeUnits);

        TableLayoutPanel section = CreateSection("Split display");
        TableLayoutPanel grid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(92f),
            ColumnStyleAbsolute(118f),
            ColumnStyleAbsolute(132f),
            ColumnStyleAbsolute(92f),
            ColumnStyleAbsolute(152f),
            ColumnStyleAbsolute(172f));

        AddHeaderRow(grid, ContentAlignment.MiddleLeft, "Column", "Show", "Width", "Font", "Bold", "Shadow %", "Outline %");
        AddColumnSettingsRow(grid, "Icon", "Icon", settings.Columns.Icon, showBold: false);
        AddColumnSettingsRow(
            grid,
            "Time",
            "Time",
            settings.Columns.Time,
            timeShadowBox,
            settings.TextEffects.TimeShadowPercent,
            timeOutlineThicknessBox,
            settings.TextEffects.TimeOutlineThicknessPercent);
        AddColumnSettingsRow(
            grid,
            "Delta",
            "Delta",
            settings.Columns.Delta,
            deltaShadowBox,
            settings.TextEffects.DeltaShadowPercent,
            deltaOutlineThicknessBox,
            settings.TextEffects.DeltaOutlineThicknessPercent);

        TableLayoutPanel optionsGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(optionsGrid, "Dynamic delta time units", enableDynamicDeltaTimeUnitsBox);

        AddSectionControl(section, grid);
        AddSectionControl(section, optionsGrid);
        AddSection(parent, section);
    }


    private void AddColumnSettingsRow(
        TableLayoutPanel grid,
        string label,
        string key,
        UiColumnSettings value,
        TextBox? shadowBox = null,
        int shadowPercent = 0,
        TextBox? outlineThicknessBox = null,
        int outlineThicknessPercent = 0,
        bool showBold = true)
    {
        var showBox = new CheckBox
        {
            Checked = value.Show,
            Dock = DockStyle.Fill,
            ForeColor = TextColor,
            TextAlign = ContentAlignment.MiddleCenter
        };
        UiTheme.StyleCheckBox(showBox);

        TextBox widthBox = CreateNumberBox(value.Width, 1, 1000);
        TextBox fontBox = CreateDecimalBox(value.FontSize, 6, 96);
        Control shadowControl = CreateEffectCell(shadowBox, shadowPercent);
        Control outlineThicknessControl = CreateEffectCell(outlineThicknessBox, outlineThicknessPercent);

        CheckBox? boldBox = null;
        Control boldControl = CreateEmptySettingsCell();
        if (showBold)
        {
            boldBox = new CheckBox
            {
                Checked = value.Bold,
                Dock = DockStyle.Fill,
                ForeColor = TextColor,
                TextAlign = ContentAlignment.MiddleCenter
            };
            UiTheme.StyleCheckBox(boldBox);
            boldControl = CreateCenteredCell(boldBox, 28);
        }

        columnControls[key] = new ColumnControls(showBox, widthBox, fontBox, boldBox);

        int row = AddGridRow(grid);
        grid.Controls.Add(CreateRowLabel(label), 0, row);
        grid.Controls.Add(CreateCenteredCell(showBox, 28), 1, row);
        grid.Controls.Add(CreateCenteredCell(widthBox, 86), 2, row);
        grid.Controls.Add(CreateCenteredCell(fontBox, 92), 3, row);
        grid.Controls.Add(boldControl, 4, row);
        grid.Controls.Add(shadowControl, 5, row);
        grid.Controls.Add(outlineThicknessControl, 6, row);
    }


    internal void AddTimerSettingsSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = CreateSection("Main timer");
        TableLayoutPanel grid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(92f),
            ColumnStyleAbsolute(132f),
            ColumnStyleAbsolute(92f),
            ColumnStyleAbsolute(152f),
            ColumnStyleAbsolute(172f));

        AddHeaderRow(grid, ContentAlignment.MiddleLeft, "Section", "Show", "Font", "Bold", "Shadow %", "Outline %");
        AddFontSettingsRow(
            grid,
            "Before decimal",
            "Timer",
            settings.Columns.Timer,
            timerShadowBox,
            settings.TextEffects.TimerShadowPercent,
            timerOutlineThicknessBox,
            settings.TextEffects.TimerOutlineThicknessPercent);
        AddFontSettingsRow(
            grid,
            "After decimal",
            "TimerMilliseconds",
            settings.Columns.TimerMilliseconds,
            timerMillisecondsShadowBox,
            settings.TextEffects.TimerMillisecondsShadowPercent,
            timerMillisecondsOutlineThicknessBox,
            settings.TextEffects.TimerMillisecondsOutlineThicknessPercent);

        ConfigureNumberBox(timerOffsetXBox, settings.Columns.TimerOffsetX, -2000, 2000);
        ConfigureNumberBox(timerOffsetYBox, settings.Columns.TimerOffsetY, -2000, 2000);
        TableLayoutPanel offsetGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(offsetGrid, "Offset X", timerOffsetXBox);
        AddSettingRow(offsetGrid, "Offset Y", timerOffsetYBox);

        AddSectionControl(section, grid);
        AddSectionControl(section, offsetGrid);
        AddSection(parent, section);
    }


    private Control CreateEffectCell(TextBox? textBox, int value)
    {
        if (textBox is null)
        {
            return new Panel
            {
                BackColor = SectionColor,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
        }

        ConfigureNumberBox(textBox, value, 0, 100);
        return CreateCenteredCell(textBox, 112);
    }


    private Control CreateEmptySettingsCell()
    {
        return new Panel
        {
            BackColor = SectionColor,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
    }


    private void AddFontSettingsRow(
        TableLayoutPanel grid,
        string label,
        string key,
        UiColumnSettings value,
        TextBox shadowBox,
        int shadowPercent,
        TextBox outlineThicknessBox,
        int outlineThicknessPercent)
    {
        var showBox = new CheckBox
        {
            Checked = value.Show,
            Dock = DockStyle.Fill,
            ForeColor = TextColor,
            TextAlign = ContentAlignment.MiddleCenter
        };
        UiTheme.StyleCheckBox(showBox);

        TextBox fontBox = CreateDecimalBox(value.FontSize, 6, 96);
        Control shadowControl = CreateEffectCell(shadowBox, shadowPercent);
        Control outlineThicknessControl = CreateEffectCell(outlineThicknessBox, outlineThicknessPercent);
        var boldBox = new CheckBox
        {
            Checked = value.Bold,
            Dock = DockStyle.Fill,
            ForeColor = TextColor,
            TextAlign = ContentAlignment.MiddleCenter
        };
        UiTheme.StyleCheckBox(boldBox);

        fontControls[key] = new FontControls(showBox, fontBox, boldBox);
        int row = AddGridRow(grid);
        grid.Controls.Add(CreateRowLabel(label), 0, row);
        grid.Controls.Add(CreateCenteredCell(showBox, 28), 1, row);
        grid.Controls.Add(CreateCenteredCell(fontBox, 92), 2, row);
        grid.Controls.Add(CreateCenteredCell(boldBox, 28), 3, row);
        grid.Controls.Add(shadowControl, 4, row);
        grid.Controls.Add(outlineThicknessControl, 5, row);
    }
}
