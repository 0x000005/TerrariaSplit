using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class UiSettingsPage : SettingsPageBase
{
    private readonly CheckBox enableDynamicDeltaTimeUnitsBox = new();
    private readonly Dictionary<string, ColumnControls> columnControls = new();
    private readonly Dictionary<string, FontControls> fontControls = new();
    private readonly TextBox timerOffsetXBox = new();
    private readonly TextBox timerOffsetYBox = new();
    private readonly TextBox timeShadowBox = new();
    private readonly TextBox timeOutlineThicknessBox = new();
    private readonly TextBox deltaShadowBox = new();
    private readonly TextBox deltaOutlineThicknessBox = new();
    private readonly TextBox timerShadowBox = new();
    private readonly TextBox timerOutlineThicknessBox = new();
    private readonly TextBox timerMillisecondsShadowBox = new();
    private readonly TextBox timerMillisecondsOutlineThicknessBox = new();

    public override SettingsPageId Id => SettingsPageId.Ui;

    internal CheckBox EnableDynamicDeltaTimeUnitsBox => enableDynamicDeltaTimeUnitsBox;

    internal TextBox TimeShadowBox => timeShadowBox;

    internal TextBox TimeOutlineThicknessBox => timeOutlineThicknessBox;

    internal TextBox DeltaShadowBox => deltaShadowBox;

    internal TextBox DeltaOutlineThicknessBox => deltaOutlineThicknessBox;

    internal TextBox TimerShadowBox => timerShadowBox;

    internal TextBox TimerOutlineThicknessBox => timerOutlineThicknessBox;

    internal TextBox TimerMillisecondsShadowBox => timerMillisecondsShadowBox;

    internal TextBox TimerMillisecondsOutlineThicknessBox => timerMillisecondsOutlineThicknessBox;

    protected override Control BuildPage(SettingsPageContext context)
    {
        return context.BuildScrollPage(content =>
        {
            AddColumnSettingsSection(content);
            AddTimerSettingsSection(content);
        });
    }

    public override void Apply(AppSettings settings)
    {
        ApplyColumnSettings("Icon", settings.Columns.Icon);
        ApplyColumnSettings("Time", settings.Columns.Time);
        ApplyColumnSettings("Delta", settings.Columns.Delta);
        ApplyFontSettings("Timer", settings.Columns.Timer);
        ApplyFontSettings("TimerMilliseconds", settings.Columns.TimerMilliseconds);

        settings.EnableDynamicDeltaTimeUnits = enableDynamicDeltaTimeUnitsBox.Checked;
        settings.Columns.TimerOffsetX = SettingsValueParser.ParseIntBox(timerOffsetXBox, 0, -2000, 2000);
        settings.Columns.TimerOffsetY = SettingsValueParser.ParseIntBox(timerOffsetYBox, 0, -2000, 2000);
        settings.TextEffects ??= new UiTextEffectSettings();
        settings.TextEffects.TimeShadowPercent = SettingsValueParser.ParseIntBox(timeShadowBox, 0, 0, 100);
        settings.TextEffects.TimeOutlineThicknessPercent = SettingsValueParser.ParseIntBox(timeOutlineThicknessBox, 0, 0, 100);
        settings.TextEffects.DeltaShadowPercent = SettingsValueParser.ParseIntBox(deltaShadowBox, 0, 0, 100);
        settings.TextEffects.DeltaOutlineThicknessPercent = SettingsValueParser.ParseIntBox(deltaOutlineThicknessBox, 0, 0, 100);
        settings.TextEffects.TimerShadowPercent = SettingsValueParser.ParseIntBox(timerShadowBox, 0, 0, 100);
        settings.TextEffects.TimerOutlineThicknessPercent = SettingsValueParser.ParseIntBox(timerOutlineThicknessBox, 0, 0, 100);
        settings.TextEffects.TimerMillisecondsShadowPercent = SettingsValueParser.ParseIntBox(timerMillisecondsShadowBox, 0, 0, 100);
        settings.TextEffects.TimerMillisecondsOutlineThicknessPercent = SettingsValueParser.ParseIntBox(timerMillisecondsOutlineThicknessBox, 0, 0, 100);
    }

    private void AddColumnSettingsSection(TableLayoutPanel parent)
    {
        ConfigureCheckBox(enableDynamicDeltaTimeUnitsBox, Draft.EnableDynamicDeltaTimeUnits);

        TableLayoutPanel section = Factory.CreateSection("Split display");
        TableLayoutPanel grid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(92f),
            SettingsUiFactory.ColumnStyleAbsolute(118f),
            SettingsUiFactory.ColumnStyleAbsolute(132f),
            SettingsUiFactory.ColumnStyleAbsolute(92f),
            SettingsUiFactory.ColumnStyleAbsolute(152f),
            SettingsUiFactory.ColumnStyleAbsolute(172f));

        Factory.AddHeaderRow(grid, ContentAlignment.MiddleLeft, "Column", "Show", "Width", "Font", "Bold", "Shadow %", "Outline %");
        AddColumnSettingsRow(grid, "Icon", "Icon", Draft.Columns.Icon, showBold: false);
        AddColumnSettingsRow(
            grid,
            "Time",
            "Time",
            Draft.Columns.Time,
            timeShadowBox,
            Draft.TextEffects.TimeShadowPercent,
            timeOutlineThicknessBox,
            Draft.TextEffects.TimeOutlineThicknessPercent);
        AddColumnSettingsRow(
            grid,
            "Delta",
            "Delta",
            Draft.Columns.Delta,
            deltaShadowBox,
            Draft.TextEffects.DeltaShadowPercent,
            deltaOutlineThicknessBox,
            Draft.TextEffects.DeltaOutlineThicknessPercent);

        TableLayoutPanel optionsGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(optionsGrid, "Dynamic delta time units", enableDynamicDeltaTimeUnitsBox);

        SettingsUiFactory.AddSectionControl(section, grid);
        SettingsUiFactory.AddSectionControl(section, optionsGrid);
        SettingsUiFactory.AddSection(parent, section);
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
        var showBox = CreateCenteredCheckBox(value.Show);
        TextBox widthBox = Factory.CreateNumberBox(value.Width, 1, 1000);
        TextBox fontBox = Factory.CreateDecimalBox(value.FontSize, 6, 96);
        Control shadowControl = CreateEffectCell(shadowBox, shadowPercent);
        Control outlineThicknessControl = CreateEffectCell(outlineThicknessBox, outlineThicknessPercent);

        CheckBox? boldBox = null;
        Control boldControl = CreateEmptySettingsCell();
        if (showBold)
        {
            boldBox = CreateCenteredCheckBox(value.Bold);
            boldControl = Factory.CreateCenteredCell(boldBox, 28);
        }

        columnControls[key] = new ColumnControls(showBox, widthBox, fontBox, boldBox);

        int row = Factory.AddGridRow(grid);
        grid.Controls.Add(Factory.CreateRowLabel(label), 0, row);
        grid.Controls.Add(Factory.CreateCenteredCell(showBox, 28), 1, row);
        grid.Controls.Add(Factory.CreateCenteredCell(widthBox, 86), 2, row);
        grid.Controls.Add(Factory.CreateCenteredCell(fontBox, 92), 3, row);
        grid.Controls.Add(boldControl, 4, row);
        grid.Controls.Add(shadowControl, 5, row);
        grid.Controls.Add(outlineThicknessControl, 6, row);
    }

    private void AddTimerSettingsSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = Factory.CreateSection("Main timer");
        TableLayoutPanel grid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(92f),
            SettingsUiFactory.ColumnStyleAbsolute(132f),
            SettingsUiFactory.ColumnStyleAbsolute(92f),
            SettingsUiFactory.ColumnStyleAbsolute(152f),
            SettingsUiFactory.ColumnStyleAbsolute(172f));

        Factory.AddHeaderRow(grid, ContentAlignment.MiddleLeft, "Section", "Show", "Font", "Bold", "Shadow %", "Outline %");
        AddFontSettingsRow(
            grid,
            "Before decimal",
            "Timer",
            Draft.Columns.Timer,
            timerShadowBox,
            Draft.TextEffects.TimerShadowPercent,
            timerOutlineThicknessBox,
            Draft.TextEffects.TimerOutlineThicknessPercent);
        AddFontSettingsRow(
            grid,
            "After decimal",
            "TimerMilliseconds",
            Draft.Columns.TimerMilliseconds,
            timerMillisecondsShadowBox,
            Draft.TextEffects.TimerMillisecondsShadowPercent,
            timerMillisecondsOutlineThicknessBox,
            Draft.TextEffects.TimerMillisecondsOutlineThicknessPercent);

        ConfigureNumberBox(timerOffsetXBox, Draft.Columns.TimerOffsetX, -2000, 2000);
        ConfigureNumberBox(timerOffsetYBox, Draft.Columns.TimerOffsetY, -2000, 2000);
        TableLayoutPanel offsetGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(offsetGrid, "Offset X", timerOffsetXBox);
        Factory.AddSettingRow(offsetGrid, "Offset Y", timerOffsetYBox);

        SettingsUiFactory.AddSectionControl(section, grid);
        SettingsUiFactory.AddSectionControl(section, offsetGrid);
        SettingsUiFactory.AddSection(parent, section);
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
        var showBox = CreateCenteredCheckBox(value.Show);
        TextBox fontBox = Factory.CreateDecimalBox(value.FontSize, 6, 96);
        Control shadowControl = CreateEffectCell(shadowBox, shadowPercent);
        Control outlineThicknessControl = CreateEffectCell(outlineThicknessBox, outlineThicknessPercent);
        var boldBox = CreateCenteredCheckBox(value.Bold);

        fontControls[key] = new FontControls(showBox, fontBox, boldBox);
        int row = Factory.AddGridRow(grid);
        grid.Controls.Add(Factory.CreateRowLabel(label), 0, row);
        grid.Controls.Add(Factory.CreateCenteredCell(showBox, 28), 1, row);
        grid.Controls.Add(Factory.CreateCenteredCell(fontBox, 92), 2, row);
        grid.Controls.Add(Factory.CreateCenteredCell(boldBox, 28), 3, row);
        grid.Controls.Add(shadowControl, 4, row);
        grid.Controls.Add(outlineThicknessControl, 5, row);
    }

    private Control CreateEffectCell(TextBox? textBox, int value)
    {
        if (textBox is null)
        {
            return CreateEmptySettingsCell();
        }

        ConfigureNumberBox(textBox, value, 0, 100);
        return Factory.CreateCenteredCell(textBox, 112);
    }

    private static Control CreateEmptySettingsCell()
    {
        return new Panel
        {
            BackColor = UiTheme.Surface,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
    }

    private static CheckBox CreateCenteredCheckBox(bool value)
    {
        var checkBox = new CheckBox
        {
            Checked = value,
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.Text,
            TextAlign = ContentAlignment.MiddleCenter
        };
        UiTheme.StyleCheckBox(checkBox);
        return checkBox;
    }

    private static void ConfigureCheckBox(CheckBox checkBox, bool selected)
    {
        checkBox.Checked = selected;
        checkBox.Dock = DockStyle.Fill;
        UiTheme.StyleCheckBox(checkBox);
    }

    private static void ConfigureNumberBox(TextBox textBox, int selected, int minimum, int maximum)
    {
        UiTheme.StyleTextBox(textBox);
        textBox.Dock = DockStyle.Fill;
        textBox.Text = Math.Clamp(selected, minimum, maximum).ToString(CultureInfo.InvariantCulture);
    }

    private void ApplyColumnSettings(string key, UiColumnSettings target)
    {
        if (!columnControls.TryGetValue(key, out ColumnControls? controls))
        {
            return;
        }

        target.Show = controls.Show.Checked;
        target.Width = SettingsValueParser.ParseIntBox(controls.Width, target.Width, 1, 1000);
        target.FontSize = SettingsValueParser.ParseFloatBox(controls.FontSize, target.FontSize, 6f, 96f);
        if (controls.Bold is not null)
        {
            target.Bold = controls.Bold.Checked;
        }
    }

    private void ApplyFontSettings(string key, UiColumnSettings target)
    {
        if (!fontControls.TryGetValue(key, out FontControls? controls))
        {
            return;
        }

        target.Show = controls.Show.Checked;
        target.FontSize = SettingsValueParser.ParseFloatBox(controls.FontSize, target.FontSize, 6f, 96f);
        target.Bold = controls.Bold.Checked;
    }

    private sealed record ColumnControls(CheckBox Show, TextBox Width, TextBox FontSize, CheckBox? Bold);

    private sealed record FontControls(CheckBox Show, TextBox FontSize, CheckBox Bold);
}
