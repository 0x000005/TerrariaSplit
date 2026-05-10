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
            ColumnStyleAbsolute(92f));

        AddHeaderRow(grid, ContentAlignment.MiddleLeft, "Column", "Show", "Width", "Font", "Bold");
        AddColumnSettingsRow(grid, "Icon", "Icon", settings.Columns.Icon);
        AddColumnSettingsRow(grid, "Time", "Time", settings.Columns.Time);
        AddColumnSettingsRow(grid, "Delta", "Delta", settings.Columns.Delta);

        TableLayoutPanel optionsGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(optionsGrid, "Dynamic delta time units", enableDynamicDeltaTimeUnitsBox);

        AddSectionControl(section, grid);
        AddSectionControl(section, optionsGrid);
        AddSection(parent, section);
    }


    private void AddColumnSettingsRow(TableLayoutPanel grid, string label, string key, UiColumnSettings value)
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

        var boldBox = new CheckBox
        {
            Checked = value.Bold,
            Dock = DockStyle.Fill,
            ForeColor = TextColor,
            TextAlign = ContentAlignment.MiddleCenter
        };
        UiTheme.StyleCheckBox(boldBox);

        columnControls[key] = new ColumnControls(showBox, widthBox, fontBox, boldBox);

        int row = AddGridRow(grid);
        grid.Controls.Add(CreateRowLabel(label), 0, row);
        grid.Controls.Add(CreateCenteredCell(showBox, 28), 1, row);
        grid.Controls.Add(CreateCenteredCell(widthBox, 86), 2, row);
        grid.Controls.Add(CreateCenteredCell(fontBox, 92), 3, row);
        grid.Controls.Add(CreateCenteredCell(boldBox, 28), 4, row);
    }


    internal void AddTimerSettingsSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = CreateSection("Main timer");
        TableLayoutPanel grid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(92f),
            ColumnStyleAbsolute(132f),
            ColumnStyleAbsolute(92f));

        AddHeaderRow(grid, ContentAlignment.MiddleLeft, "Section", "Show", "Font", "Bold");
        AddFontSettingsRow(grid, "Before decimal", "Timer", settings.Columns.Timer);
        AddFontSettingsRow(grid, "After decimal", "TimerMilliseconds", settings.Columns.TimerMilliseconds);

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


    private void AddFontSettingsRow(TableLayoutPanel grid, string label, string key, UiColumnSettings value)
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
    }
}
