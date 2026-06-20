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
    private readonly TextBox iconOpacityBox = new();
    private readonly TextBox timeOpacityBox = new();
    private readonly TextBox timeShadowBox = new();
    private readonly TextBox timeOutlineThicknessBox = new();
    private readonly TextBox deltaOpacityBox = new();
    private readonly TextBox deltaShadowBox = new();
    private readonly TextBox deltaOutlineThicknessBox = new();
    private readonly TextBox attachedIconOpacityBox = new();
    private readonly TextBox attachedTimeOpacityBox = new();
    private readonly TextBox attachedTimeShadowBox = new();
    private readonly TextBox attachedTimeOutlineThicknessBox = new();
    private readonly TextBox attachedDeltaOpacityBox = new();
    private readonly TextBox attachedDeltaShadowBox = new();
    private readonly TextBox attachedDeltaOutlineThicknessBox = new();
    private readonly TextBox timerOpacityBox = new();
    private readonly TextBox timerShadowBox = new();
    private readonly TextBox timerOutlineThicknessBox = new();
    private readonly TextBox timerMillisecondsOpacityBox = new();
    private readonly TextBox timerMillisecondsShadowBox = new();
    private readonly TextBox timerMillisecondsOutlineThicknessBox = new();

    public override SettingsPageId Id => SettingsPageId.Ui;

    internal CheckBox EnableDynamicDeltaTimeUnitsBox => enableDynamicDeltaTimeUnitsBox;

    internal TextBox IconOpacityBox => iconOpacityBox;

    internal TextBox TimeOpacityBox => timeOpacityBox;

    internal TextBox TimeShadowBox => timeShadowBox;

    internal TextBox TimeOutlineThicknessBox => timeOutlineThicknessBox;

    internal TextBox DeltaOpacityBox => deltaOpacityBox;

    internal TextBox DeltaShadowBox => deltaShadowBox;

    internal TextBox DeltaOutlineThicknessBox => deltaOutlineThicknessBox;

    internal TextBox AttachedIconOpacityBox => attachedIconOpacityBox;

    internal TextBox AttachedTimeOpacityBox => attachedTimeOpacityBox;

    internal TextBox AttachedTimeShadowBox => attachedTimeShadowBox;

    internal TextBox AttachedTimeOutlineThicknessBox => attachedTimeOutlineThicknessBox;

    internal TextBox AttachedDeltaOpacityBox => attachedDeltaOpacityBox;

    internal TextBox AttachedDeltaShadowBox => attachedDeltaShadowBox;

    internal TextBox AttachedDeltaOutlineThicknessBox => attachedDeltaOutlineThicknessBox;

    internal TextBox TimerOpacityBox => timerOpacityBox;

    internal TextBox TimerShadowBox => timerShadowBox;

    internal TextBox TimerOutlineThicknessBox => timerOutlineThicknessBox;

    internal TextBox TimerMillisecondsOpacityBox => timerMillisecondsOpacityBox;

    internal TextBox TimerMillisecondsShadowBox => timerMillisecondsShadowBox;

    internal TextBox TimerMillisecondsOutlineThicknessBox => timerMillisecondsOutlineThicknessBox;

    internal FontFamilySelector GetFontFamilySelectorForTests(string key)
    {
        if (columnControls.TryGetValue(key, out ColumnControls? columnControlsValue) &&
            columnControlsValue.FontFamily is not null)
        {
            return columnControlsValue.FontFamily;
        }

        if (fontControls.TryGetValue(key, out FontControls? fontControlsValue))
        {
            return fontControlsValue.FontFamily;
        }

        throw new InvalidOperationException($"Font family control not found for {key}.");
    }

    internal TextBox GetColumnWidthBoxForTests(string key)
    {
        return GetColumnControlsForTests(key).Width;
    }

    internal TextBox GetColumnFontSizeBoxForTests(string key)
    {
        return GetColumnControlsForTests(key).FontSize;
    }

    internal CheckBox? GetColumnBoldBoxForTests(string key)
    {
        return GetColumnControlsForTests(key).Bold;
    }

    private ColumnControls GetColumnControlsForTests(string key)
    {
        return columnControls.TryGetValue(key, out ColumnControls? controls)
            ? controls
            : throw new InvalidOperationException($"Column controls not found for {key}.");
    }

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
        ApplyColumnSettings("AttachedIcon", settings.Columns.AttachedIcon);
        ApplyColumnSettings("AttachedTime", settings.Columns.AttachedTime);
        ApplyColumnSettings("AttachedDelta", settings.Columns.AttachedDelta);
        ApplyFontSettings("Timer", settings.Columns.Timer);
        ApplyFontSettings("TimerMilliseconds", settings.Columns.TimerMilliseconds);

        settings.EnableDynamicDeltaTimeUnits = enableDynamicDeltaTimeUnitsBox.Checked;
        settings.Columns.TimerOffsetX = SettingsValueParser.ParseIntBox(timerOffsetXBox, 0, -2000, 2000);
        settings.Columns.TimerOffsetY = SettingsValueParser.ParseIntBox(timerOffsetYBox, 0, -2000, 2000);
        settings.TextEffects ??= new UiTextEffectSettings();
        settings.TextEffects.IconOpacityPercent = SettingsValueParser.ParseIntBox(iconOpacityBox, 100, 0, 100);
        settings.TextEffects.TimeOpacityPercent = SettingsValueParser.ParseIntBox(timeOpacityBox, 100, 0, 100);
        settings.TextEffects.TimeShadowPercent = SettingsValueParser.ParseIntBox(timeShadowBox, 0, 0, 100);
        settings.TextEffects.TimeOutlineThicknessPercent = SettingsValueParser.ParseIntBox(timeOutlineThicknessBox, 0, 0, 200);
        settings.TextEffects.DeltaOpacityPercent = SettingsValueParser.ParseIntBox(deltaOpacityBox, 100, 0, 100);
        settings.TextEffects.DeltaShadowPercent = SettingsValueParser.ParseIntBox(deltaShadowBox, 0, 0, 100);
        settings.TextEffects.DeltaOutlineThicknessPercent = SettingsValueParser.ParseIntBox(deltaOutlineThicknessBox, 0, 0, 200);
        settings.TextEffects.AttachedIconOpacityPercent = SettingsValueParser.ParseIntBox(attachedIconOpacityBox, 100, 0, 100);
        settings.TextEffects.AttachedTimeOpacityPercent = SettingsValueParser.ParseIntBox(attachedTimeOpacityBox, 100, 0, 100);
        settings.TextEffects.AttachedTimeShadowPercent = SettingsValueParser.ParseIntBox(attachedTimeShadowBox, 0, 0, 100);
        settings.TextEffects.AttachedTimeOutlineThicknessPercent = SettingsValueParser.ParseIntBox(attachedTimeOutlineThicknessBox, 0, 0, 200);
        settings.TextEffects.AttachedDeltaOpacityPercent = SettingsValueParser.ParseIntBox(attachedDeltaOpacityBox, 100, 0, 100);
        settings.TextEffects.AttachedDeltaShadowPercent = SettingsValueParser.ParseIntBox(attachedDeltaShadowBox, 0, 0, 100);
        settings.TextEffects.AttachedDeltaOutlineThicknessPercent = SettingsValueParser.ParseIntBox(attachedDeltaOutlineThicknessBox, 0, 0, 200);
        settings.TextEffects.TimerOpacityPercent = SettingsValueParser.ParseIntBox(timerOpacityBox, 100, 0, 100);
        settings.TextEffects.TimerShadowPercent = SettingsValueParser.ParseIntBox(timerShadowBox, 0, 0, 100);
        settings.TextEffects.TimerOutlineThicknessPercent = SettingsValueParser.ParseIntBox(timerOutlineThicknessBox, 0, 0, 200);
        settings.TextEffects.TimerMillisecondsOpacityPercent = SettingsValueParser.ParseIntBox(timerMillisecondsOpacityBox, 100, 0, 100);
        settings.TextEffects.TimerMillisecondsShadowPercent = SettingsValueParser.ParseIntBox(timerMillisecondsShadowBox, 0, 0, 100);
        settings.TextEffects.TimerMillisecondsOutlineThicknessPercent = SettingsValueParser.ParseIntBox(timerMillisecondsOutlineThicknessBox, 0, 0, 200);
    }

    private void AddColumnSettingsSection(TableLayoutPanel parent)
    {
        ConfigureCheckBox(enableDynamicDeltaTimeUnitsBox, Draft.EnableDynamicDeltaTimeUnits);

        TableLayoutPanel section = Factory.CreateSection("Split display");
        TableLayoutPanel grid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(92f),
            SettingsUiFactory.ColumnStyleAbsolute(118f),
            SettingsUiFactory.ColumnStyleAbsolute(220f),
            SettingsUiFactory.ColumnStyleAbsolute(132f),
            SettingsUiFactory.ColumnStyleAbsolute(92f),
            SettingsUiFactory.ColumnStyleAbsolute(152f),
            SettingsUiFactory.ColumnStyleAbsolute(152f),
            SettingsUiFactory.ColumnStyleAbsolute(172f));

        Factory.AddHeaderRow(grid, ContentAlignment.MiddleLeft, "Column", "Show", "Width", "Font family", "Size", "Bold", "Opacity %", "Shadow %", "Outline %");
        AddColumnSettingsRow(
            grid,
            "Icon",
            "Icon",
            Draft.Columns.Icon,
            opacityBox: iconOpacityBox,
            opacityPercent: Draft.TextEffects.IconOpacityPercent,
            showFontFamily: false,
            showBold: false);
        AddColumnSettingsRow(
            grid,
            "Time",
            "Time",
            Draft.Columns.Time,
            timeOpacityBox,
            Draft.TextEffects.TimeOpacityPercent,
            timeShadowBox,
            Draft.TextEffects.TimeShadowPercent,
            timeOutlineThicknessBox,
            Draft.TextEffects.TimeOutlineThicknessPercent);
        AddColumnSettingsRow(
            grid,
            "Delta",
            "Delta",
            Draft.Columns.Delta,
            deltaOpacityBox,
            Draft.TextEffects.DeltaOpacityPercent,
            deltaShadowBox,
            Draft.TextEffects.DeltaShadowPercent,
            deltaOutlineThicknessBox,
            Draft.TextEffects.DeltaOutlineThicknessPercent);
        AddColumnSettingsRow(
            grid,
            "Icon (attached)",
            "AttachedIcon",
            Draft.Columns.AttachedIcon,
            opacityBox: attachedIconOpacityBox,
            opacityPercent: Draft.TextEffects.AttachedIconOpacityPercent,
            showFontFamily: false,
            showBold: false);
        AddColumnSettingsRow(
            grid,
            "Time (attached)",
            "AttachedTime",
            Draft.Columns.AttachedTime,
            attachedTimeOpacityBox,
            Draft.TextEffects.AttachedTimeOpacityPercent,
            attachedTimeShadowBox,
            Draft.TextEffects.AttachedTimeShadowPercent,
            attachedTimeOutlineThicknessBox,
            Draft.TextEffects.AttachedTimeOutlineThicknessPercent);
        AddColumnSettingsRow(
            grid,
            "Delta (attached)",
            "AttachedDelta",
            Draft.Columns.AttachedDelta,
            attachedDeltaOpacityBox,
            Draft.TextEffects.AttachedDeltaOpacityPercent,
            attachedDeltaShadowBox,
            Draft.TextEffects.AttachedDeltaShadowPercent,
            attachedDeltaOutlineThicknessBox,
            Draft.TextEffects.AttachedDeltaOutlineThicknessPercent);

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
        TextBox? opacityBox = null,
        int opacityPercent = 100,
        TextBox? shadowBox = null,
        int shadowPercent = 0,
        TextBox? outlineThicknessBox = null,
        int outlineThicknessPercent = 0,
        bool showFontFamily = true,
        bool showBold = true)
    {
        var showBox = CreateCenteredCheckBox(value.Show);
        TextBox widthBox = Factory.CreateNumberBox(value.Width, 1, 1000);
        FontFamilySelector? fontFamilyBox = showFontFamily ? CreateFontFamilyBox(value.FontFamily) : null;
        TextBox fontBox = Factory.CreateDecimalBox(value.FontSize, 6, 96);
        Control opacityControl = CreateEffectCell(opacityBox, opacityPercent, 100);
        Control shadowControl = CreateEffectCell(shadowBox, shadowPercent, 100);
        Control outlineThicknessControl = CreateEffectCell(outlineThicknessBox, outlineThicknessPercent, 200);
        Control fontFamilyControl = fontFamilyBox is null
            ? CreateEmptySettingsCell()
            : Factory.CreateCenteredCell(fontFamilyBox, 210);

        CheckBox? boldBox = null;
        Control boldControl = CreateEmptySettingsCell();
        if (showBold)
        {
            boldBox = CreateCenteredCheckBox(value.Bold);
            boldControl = Factory.CreateCenteredCell(boldBox, 28);
        }

        columnControls[key] = new ColumnControls(showBox, widthBox, fontFamilyBox, fontBox, boldBox);

        int row = Factory.AddGridRow(grid);
        grid.Controls.Add(Factory.CreateRowLabel(label), 0, row);
        grid.Controls.Add(Factory.CreateCenteredCell(showBox, 28), 1, row);
        grid.Controls.Add(Factory.CreateCenteredCell(widthBox, 86), 2, row);
        grid.Controls.Add(fontFamilyControl, 3, row);
        grid.Controls.Add(Factory.CreateCenteredCell(fontBox, 92), 4, row);
        grid.Controls.Add(boldControl, 5, row);
        grid.Controls.Add(opacityControl, 6, row);
        grid.Controls.Add(shadowControl, 7, row);
        grid.Controls.Add(outlineThicknessControl, 8, row);
    }

    private void AddTimerSettingsSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = Factory.CreateSection("Main timer");
        TableLayoutPanel grid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(92f),
            SettingsUiFactory.ColumnStyleAbsolute(220f),
            SettingsUiFactory.ColumnStyleAbsolute(132f),
            SettingsUiFactory.ColumnStyleAbsolute(92f),
            SettingsUiFactory.ColumnStyleAbsolute(152f),
            SettingsUiFactory.ColumnStyleAbsolute(152f),
            SettingsUiFactory.ColumnStyleAbsolute(172f));

        Factory.AddHeaderRow(grid, ContentAlignment.MiddleLeft, "Section", "Show", "Font family", "Size", "Bold", "Opacity %", "Shadow %", "Outline %");
        AddFontSettingsRow(
            grid,
            "Before decimal",
            "Timer",
            Draft.Columns.Timer,
            timerOpacityBox,
            Draft.TextEffects.TimerOpacityPercent,
            timerShadowBox,
            Draft.TextEffects.TimerShadowPercent,
            timerOutlineThicknessBox,
            Draft.TextEffects.TimerOutlineThicknessPercent);
        AddFontSettingsRow(
            grid,
            "After decimal",
            "TimerMilliseconds",
            Draft.Columns.TimerMilliseconds,
            timerMillisecondsOpacityBox,
            Draft.TextEffects.TimerMillisecondsOpacityPercent,
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
        TextBox opacityBox,
        int opacityPercent,
        TextBox shadowBox,
        int shadowPercent,
        TextBox outlineThicknessBox,
        int outlineThicknessPercent)
    {
        var showBox = CreateCenteredCheckBox(value.Show);
        FontFamilySelector fontFamilyBox = CreateFontFamilyBox(value.FontFamily);
        TextBox fontBox = Factory.CreateDecimalBox(value.FontSize, 6, 96);
        Control opacityControl = CreateEffectCell(opacityBox, opacityPercent, 100);
        Control shadowControl = CreateEffectCell(shadowBox, shadowPercent, 100);
        Control outlineThicknessControl = CreateEffectCell(outlineThicknessBox, outlineThicknessPercent, 200);
        var boldBox = CreateCenteredCheckBox(value.Bold);

        fontControls[key] = new FontControls(showBox, fontFamilyBox, fontBox, boldBox);
        int row = Factory.AddGridRow(grid);
        grid.Controls.Add(Factory.CreateRowLabel(label), 0, row);
        grid.Controls.Add(Factory.CreateCenteredCell(showBox, 28), 1, row);
        grid.Controls.Add(Factory.CreateCenteredCell(fontFamilyBox, 210), 2, row);
        grid.Controls.Add(Factory.CreateCenteredCell(fontBox, 92), 3, row);
        grid.Controls.Add(Factory.CreateCenteredCell(boldBox, 28), 4, row);
        grid.Controls.Add(opacityControl, 5, row);
        grid.Controls.Add(shadowControl, 6, row);
        grid.Controls.Add(outlineThicknessControl, 7, row);
    }

    private FontFamilySelector CreateFontFamilyBox(string familyName)
    {
        var selector = new FontFamilySelector();
        selector.SetSelectedFontFamily(familyName);
        return selector;
    }

    private Control CreateEffectCell(TextBox? textBox, int value, int maximum)
    {
        if (textBox is null)
        {
            return CreateEmptySettingsCell();
        }

        ConfigureNumberBox(textBox, value, 0, maximum);
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
        if (controls.FontFamily is not null)
        {
            target.FontFamily = GetSelectedFontFamily(controls.FontFamily, target.FontFamily);
        }

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
        target.FontFamily = GetSelectedFontFamily(controls.FontFamily, target.FontFamily);
        target.FontSize = SettingsValueParser.ParseFloatBox(controls.FontSize, target.FontSize, 6f, 96f);
        target.Bold = controls.Bold.Checked;
    }

    private static string GetSelectedFontFamily(FontFamilySelector selector, string fallback)
    {
        string selected = selector.SelectedFontFamily;
        return UiFontSettings.NormalizeFamilyName(string.IsNullOrWhiteSpace(selected) ? fallback : selected);
    }

    private sealed record ColumnControls(CheckBox Show, TextBox Width, FontFamilySelector? FontFamily, TextBox FontSize, CheckBox? Bold);

    private sealed record FontControls(CheckBox Show, FontFamilySelector FontFamily, TextBox FontSize, CheckBox Bold);
}
