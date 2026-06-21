using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class UiSettingsPage : SettingsPageBase
{
    private readonly Dictionary<string, ColumnControls> columnControls = new();
    private readonly Dictionary<string, FontControls> fontControls = new();
    private readonly TextBox timerOffsetXBox = new();
    private readonly TextBox timerOffsetYBox = new();
    private readonly CheckBox visibleGroupCountLimitEnabledBox = new();
    private readonly TextBox visibleGroupCountLimitBox = new();
    private readonly TextBox currentGroupPositionBox = new();
    private readonly CheckBox showFinalGroupBox = new();
    private readonly CheckBox expandSplitDetailsBox = new();
    private readonly CheckBox collapseSplitDetailsOnCompletionBox = new();
    private readonly CheckBox autoHideAttachedGroupsBox = new();
    private readonly CheckBox showEarlyDeltaTimeBox = new();
    private readonly TextBox earlyDeltaTimeSecondsBox = new();
    private readonly CheckBox enableDynamicDeltaTimeUnitsBox = new();
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

    internal CheckBox VisibleGroupCountLimitEnabledBoxForTests => visibleGroupCountLimitEnabledBox;

    internal TextBox VisibleGroupCountLimitBoxForTests => visibleGroupCountLimitBox;

    internal TextBox CurrentGroupPositionBoxForTests => currentGroupPositionBox;

    internal CheckBox ShowFinalGroupBoxForTests => showFinalGroupBox;

    internal CheckBox ExpandSplitDetailsBoxForTests => expandSplitDetailsBox;

    internal CheckBox CollapseSplitDetailsOnCompletionBoxForTests => collapseSplitDetailsOnCompletionBox;

    internal CheckBox AutoHideAttachedGroupsBoxForTests => autoHideAttachedGroupsBox;

    internal CheckBox ShowEarlyDeltaTimeBoxForTests => showEarlyDeltaTimeBox;

    internal TextBox EarlyDeltaTimeSecondsBoxForTests => earlyDeltaTimeSecondsBox;

    internal CheckBox EnableDynamicDeltaTimeUnitsBoxForTests => enableDynamicDeltaTimeUnitsBox;

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
            AddGroupSettingsSection(content);
            AddDeltaTimeSettingsSection(content);
        });
    }

    public override void Apply(AppSettings settings)
    {
        ApplyColumnSettings("Icon", settings.Overlay.Columns.Icon);
        ApplyColumnSettings("Time", settings.Overlay.Columns.Time);
        ApplyColumnSettings("Delta", settings.Overlay.Columns.Delta);
        ApplyColumnSettings("AttachedIcon", settings.Overlay.Columns.AttachedIcon);
        ApplyColumnSettings("AttachedTime", settings.Overlay.Columns.AttachedTime);
        ApplyColumnSettings("AttachedDelta", settings.Overlay.Columns.AttachedDelta);
        ApplyFontSettings("Timer", settings.Overlay.Columns.Timer);
        ApplyFontSettings("TimerMilliseconds", settings.Overlay.Columns.TimerMilliseconds);
        if (ApplyGroupSettings(settings))
        {
            Context.NotifyModelChanged(SettingsModelChange.RouteChanged);
        }
        ApplyDeltaTimeSettings(settings);

        settings.Overlay.Columns.TimerOffsetX = SettingsValueParser.ParseIntBox(timerOffsetXBox, 0, -2000, 2000);
        settings.Overlay.Columns.TimerOffsetY = SettingsValueParser.ParseIntBox(timerOffsetYBox, 0, -2000, 2000);
        settings.Overlay.TextEffects ??= new UiTextEffectSettings();
        settings.Overlay.TextEffects.IconOpacityPercent = SettingsValueParser.ParseIntBox(iconOpacityBox, 100, 0, 100);
        settings.Overlay.TextEffects.TimeOpacityPercent = SettingsValueParser.ParseIntBox(timeOpacityBox, 100, 0, 100);
        settings.Overlay.TextEffects.TimeShadowPercent = SettingsValueParser.ParseIntBox(timeShadowBox, 0, 0, 100);
        settings.Overlay.TextEffects.TimeOutlineThicknessPercent = SettingsValueParser.ParseIntBox(timeOutlineThicknessBox, 0, 0, 200);
        settings.Overlay.TextEffects.DeltaOpacityPercent = SettingsValueParser.ParseIntBox(deltaOpacityBox, 100, 0, 100);
        settings.Overlay.TextEffects.DeltaShadowPercent = SettingsValueParser.ParseIntBox(deltaShadowBox, 0, 0, 100);
        settings.Overlay.TextEffects.DeltaOutlineThicknessPercent = SettingsValueParser.ParseIntBox(deltaOutlineThicknessBox, 0, 0, 200);
        settings.Overlay.TextEffects.AttachedIconOpacityPercent = SettingsValueParser.ParseIntBox(attachedIconOpacityBox, 100, 0, 100);
        settings.Overlay.TextEffects.AttachedTimeOpacityPercent = SettingsValueParser.ParseIntBox(attachedTimeOpacityBox, 100, 0, 100);
        settings.Overlay.TextEffects.AttachedTimeShadowPercent = SettingsValueParser.ParseIntBox(attachedTimeShadowBox, 0, 0, 100);
        settings.Overlay.TextEffects.AttachedTimeOutlineThicknessPercent = SettingsValueParser.ParseIntBox(attachedTimeOutlineThicknessBox, 0, 0, 200);
        settings.Overlay.TextEffects.AttachedDeltaOpacityPercent = SettingsValueParser.ParseIntBox(attachedDeltaOpacityBox, 100, 0, 100);
        settings.Overlay.TextEffects.AttachedDeltaShadowPercent = SettingsValueParser.ParseIntBox(attachedDeltaShadowBox, 0, 0, 100);
        settings.Overlay.TextEffects.AttachedDeltaOutlineThicknessPercent = SettingsValueParser.ParseIntBox(attachedDeltaOutlineThicknessBox, 0, 0, 200);
        settings.Overlay.TextEffects.TimerOpacityPercent = SettingsValueParser.ParseIntBox(timerOpacityBox, 100, 0, 100);
        settings.Overlay.TextEffects.TimerShadowPercent = SettingsValueParser.ParseIntBox(timerShadowBox, 0, 0, 100);
        settings.Overlay.TextEffects.TimerOutlineThicknessPercent = SettingsValueParser.ParseIntBox(timerOutlineThicknessBox, 0, 0, 200);
        settings.Overlay.TextEffects.TimerMillisecondsOpacityPercent = SettingsValueParser.ParseIntBox(timerMillisecondsOpacityBox, 100, 0, 100);
        settings.Overlay.TextEffects.TimerMillisecondsShadowPercent = SettingsValueParser.ParseIntBox(timerMillisecondsShadowBox, 0, 0, 100);
        settings.Overlay.TextEffects.TimerMillisecondsOutlineThicknessPercent = SettingsValueParser.ParseIntBox(timerMillisecondsOutlineThicknessBox, 0, 0, 200);
    }

    private void AddColumnSettingsSection(TableLayoutPanel parent)
    {
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
            Draft.Overlay.Columns.Icon,
            opacityBox: iconOpacityBox,
            opacityPercent: Draft.Overlay.TextEffects.IconOpacityPercent,
            showFontFamily: false,
            showBold: false);
        AddColumnSettingsRow(
            grid,
            "Time",
            "Time",
            Draft.Overlay.Columns.Time,
            timeOpacityBox,
            Draft.Overlay.TextEffects.TimeOpacityPercent,
            timeShadowBox,
            Draft.Overlay.TextEffects.TimeShadowPercent,
            timeOutlineThicknessBox,
            Draft.Overlay.TextEffects.TimeOutlineThicknessPercent);
        AddColumnSettingsRow(
            grid,
            "Delta",
            "Delta",
            Draft.Overlay.Columns.Delta,
            deltaOpacityBox,
            Draft.Overlay.TextEffects.DeltaOpacityPercent,
            deltaShadowBox,
            Draft.Overlay.TextEffects.DeltaShadowPercent,
            deltaOutlineThicknessBox,
            Draft.Overlay.TextEffects.DeltaOutlineThicknessPercent);
        AddColumnSettingsRow(
            grid,
            "Icon (attached)",
            "AttachedIcon",
            Draft.Overlay.Columns.AttachedIcon,
            opacityBox: attachedIconOpacityBox,
            opacityPercent: Draft.Overlay.TextEffects.AttachedIconOpacityPercent,
            showFontFamily: false,
            showBold: false);
        AddColumnSettingsRow(
            grid,
            "Time (attached)",
            "AttachedTime",
            Draft.Overlay.Columns.AttachedTime,
            attachedTimeOpacityBox,
            Draft.Overlay.TextEffects.AttachedTimeOpacityPercent,
            attachedTimeShadowBox,
            Draft.Overlay.TextEffects.AttachedTimeShadowPercent,
            attachedTimeOutlineThicknessBox,
            Draft.Overlay.TextEffects.AttachedTimeOutlineThicknessPercent);
        AddColumnSettingsRow(
            grid,
            "Delta (attached)",
            "AttachedDelta",
            Draft.Overlay.Columns.AttachedDelta,
            attachedDeltaOpacityBox,
            Draft.Overlay.TextEffects.AttachedDeltaOpacityPercent,
            attachedDeltaShadowBox,
            Draft.Overlay.TextEffects.AttachedDeltaShadowPercent,
            attachedDeltaOutlineThicknessBox,
            Draft.Overlay.TextEffects.AttachedDeltaOutlineThicknessPercent);

        SettingsUiFactory.AddSectionControl(section, grid);
        SettingsUiFactory.AddSection(parent, section);
    }

    private void AddGroupSettingsSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = Factory.CreateSection("Group");

        SettingsUiFactory.AddSectionControl(section, Factory.CreateSubsectionLabel("Group count limit"));
        TableLayoutPanel visibleGrid = Factory.CreateTwoColumnGrid(280f);
        int visibleGroupCountLimit = Math.Clamp(Draft.Route.VisibleGroupCountLimit, 1, 100);
        ConfigureCheckBox(visibleGroupCountLimitEnabledBox, Draft.Route.EnableVisibleGroupCountLimit);
        visibleGroupCountLimitEnabledBox.CheckedChanged += (_, _) => UpdateVisibleGroupCountLimitAvailability();
        ConfigureNumberBox(visibleGroupCountLimitBox, visibleGroupCountLimit, 1, 100);
        ConfigureNumberBox(currentGroupPositionBox, Draft.Route.CurrentGroupPosition, 1, visibleGroupCountLimit);
        ConfigureCheckBox(showFinalGroupBox, Draft.Route.ShowFinalGroup);
        Factory.AddSettingRow(visibleGrid, "Enabled", visibleGroupCountLimitEnabledBox);
        Factory.AddSettingRow(visibleGrid, "Visible group count", visibleGroupCountLimitBox);
        Factory.AddSettingRow(visibleGrid, "Current group position", currentGroupPositionBox);
        Factory.AddSettingRow(visibleGrid, "Show final group", showFinalGroupBox);
        SettingsUiFactory.AddSectionControl(section, visibleGrid);

        SettingsUiFactory.AddSectionControl(section, Factory.CreateSubsectionLabel("Main groups"));
        TableLayoutPanel mainGrid = Factory.CreateTwoColumnGrid(280f);
        ConfigureCheckBox(expandSplitDetailsBox, Draft.Route.ExpandSplitDetails);
        expandSplitDetailsBox.CheckedChanged += (_, _) => UpdateCollapseSplitDetailsAvailability();
        ConfigureCheckBox(collapseSplitDetailsOnCompletionBox, Draft.Route.CollapseSplitDetailsOnCompletion);
        Factory.AddSettingRow(mainGrid, "Auto expand multi-condition main groups", expandSplitDetailsBox);
        Factory.AddSettingRow(mainGrid, "Collapse after completion", collapseSplitDetailsOnCompletionBox);
        SettingsUiFactory.AddSectionControl(section, mainGrid);

        SettingsUiFactory.AddSectionControl(section, Factory.CreateSubsectionLabel("Attached groups"));
        TableLayoutPanel attachedGrid = Factory.CreateTwoColumnGrid(280f);
        ConfigureCheckBox(autoHideAttachedGroupsBox, Draft.Route.AutoHideAttachedGroups);
        Factory.AddSettingRow(attachedGrid, "Auto hide attached groups", autoHideAttachedGroupsBox);
        SettingsUiFactory.AddSectionControl(section, attachedGrid);

        SettingsUiFactory.AddSection(parent, section);
        UpdateVisibleGroupCountLimitAvailability();
        UpdateCollapseSplitDetailsAvailability();
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
            Draft.Overlay.Columns.Timer,
            timerOpacityBox,
            Draft.Overlay.TextEffects.TimerOpacityPercent,
            timerShadowBox,
            Draft.Overlay.TextEffects.TimerShadowPercent,
            timerOutlineThicknessBox,
            Draft.Overlay.TextEffects.TimerOutlineThicknessPercent);
        AddFontSettingsRow(
            grid,
            "After decimal",
            "TimerMilliseconds",
            Draft.Overlay.Columns.TimerMilliseconds,
            timerMillisecondsOpacityBox,
            Draft.Overlay.TextEffects.TimerMillisecondsOpacityPercent,
            timerMillisecondsShadowBox,
            Draft.Overlay.TextEffects.TimerMillisecondsShadowPercent,
            timerMillisecondsOutlineThicknessBox,
            Draft.Overlay.TextEffects.TimerMillisecondsOutlineThicknessPercent);

        ConfigureNumberBox(timerOffsetXBox, Draft.Overlay.Columns.TimerOffsetX, -2000, 2000);
        ConfigureNumberBox(timerOffsetYBox, Draft.Overlay.Columns.TimerOffsetY, -2000, 2000);
        TableLayoutPanel offsetGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(offsetGrid, "Offset X", timerOffsetXBox);
        Factory.AddSettingRow(offsetGrid, "Offset Y", timerOffsetYBox);

        SettingsUiFactory.AddSectionControl(section, grid);
        SettingsUiFactory.AddSectionControl(section, offsetGrid);
        SettingsUiFactory.AddSection(parent, section);
    }

    private void AddDeltaTimeSettingsSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = Factory.CreateSection("Delta time");

        SettingsUiFactory.AddSectionControl(section, Factory.CreateSubsectionLabel("Early delta time"));
        TableLayoutPanel earlyDeltaGrid = Factory.CreateTwoColumnGrid(280f);
        ConfigureCheckBox(showEarlyDeltaTimeBox, Draft.Overlay.ShowEarlyDeltaTime);
        showEarlyDeltaTimeBox.CheckedChanged += (_, _) => UpdateEarlyDeltaAvailability();
        ConfigureNumberBox(earlyDeltaTimeSecondsBox, Draft.Overlay.EarlyDeltaTimeSeconds, 0, 3600);
        Factory.AddSettingRow(earlyDeltaGrid, "Enabled", showEarlyDeltaTimeBox);
        Factory.AddSettingRow(earlyDeltaGrid, "Show when within seconds", earlyDeltaTimeSecondsBox);
        SettingsUiFactory.AddSectionControl(section, earlyDeltaGrid);

        SettingsUiFactory.AddSectionControl(section, Factory.CreateSubsectionLabel("Dynamic delta time units"));
        TableLayoutPanel dynamicUnitGrid = Factory.CreateTwoColumnGrid(280f);
        ConfigureCheckBox(enableDynamicDeltaTimeUnitsBox, Draft.Overlay.EnableDynamicDeltaTimeUnits);
        Factory.AddSettingRow(dynamicUnitGrid, "Enabled", enableDynamicDeltaTimeUnitsBox);
        SettingsUiFactory.AddSectionControl(section, dynamicUnitGrid);

        SettingsUiFactory.AddSection(parent, section);
        UpdateEarlyDeltaAvailability();
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

    private bool ApplyGroupSettings(AppSettings settings)
    {
        bool enableVisibleGroupCountLimit = visibleGroupCountLimitEnabledBox.Checked;
        int visibleGroupCountLimit = SettingsValueParser.ParseIntBox(
            visibleGroupCountLimitBox,
            settings.Route.VisibleGroupCountLimit,
            1,
            100);
        int currentGroupPosition = SettingsValueParser.ParseIntBox(
            currentGroupPositionBox,
            settings.Route.CurrentGroupPosition,
            1,
            visibleGroupCountLimit);

        bool showFinalGroup = showFinalGroupBox.Checked;
        bool expand = expandSplitDetailsBox.Checked;
        bool collapse = collapseSplitDetailsOnCompletionBox.Checked;
        bool autoHideAttachedGroups = autoHideAttachedGroupsBox.Checked;
        bool changed =
            settings.Route.EnableVisibleGroupCountLimit != enableVisibleGroupCountLimit ||
            settings.Route.VisibleGroupCountLimit != visibleGroupCountLimit ||
            settings.Route.CurrentGroupPosition != currentGroupPosition ||
            settings.Route.ShowFinalGroup != showFinalGroup ||
            settings.Route.ExpandSplitDetails != expand ||
            settings.Route.CollapseSplitDetailsOnCompletion != collapse ||
            settings.Route.AutoHideAttachedGroups != autoHideAttachedGroups;

        settings.Route.EnableVisibleGroupCountLimit = enableVisibleGroupCountLimit;
        settings.Route.VisibleGroupCountLimit = visibleGroupCountLimit;
        settings.Route.CurrentGroupPosition = currentGroupPosition;
        settings.Route.ShowFinalGroup = showFinalGroup;
        settings.Route.ExpandSplitDetails = expand;
        settings.Route.CollapseSplitDetailsOnCompletion = collapse;
        settings.Route.AutoHideAttachedGroups = autoHideAttachedGroups;
        return changed;
    }

    private void ApplyDeltaTimeSettings(AppSettings settings)
    {
        settings.Overlay.ShowEarlyDeltaTime = showEarlyDeltaTimeBox.Checked;
        settings.Overlay.EarlyDeltaTimeSeconds = SettingsValueParser.ParseIntBox(
            earlyDeltaTimeSecondsBox,
            settings.Overlay.EarlyDeltaTimeSeconds,
            0,
            3600);
        settings.Overlay.EnableDynamicDeltaTimeUnits = enableDynamicDeltaTimeUnitsBox.Checked;
    }

    private void UpdateCollapseSplitDetailsAvailability()
    {
        collapseSplitDetailsOnCompletionBox.Enabled = expandSplitDetailsBox.Checked;
    }

    private void UpdateVisibleGroupCountLimitAvailability()
    {
        bool enabled = visibleGroupCountLimitEnabledBox.Checked;
        visibleGroupCountLimitBox.Enabled = enabled;
        currentGroupPositionBox.Enabled = enabled;
        showFinalGroupBox.Enabled = enabled;
    }

    private void UpdateEarlyDeltaAvailability()
    {
        earlyDeltaTimeSecondsBox.Enabled = showEarlyDeltaTimeBox.Checked;
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
        return UiFontFactory.Default.NormalizeFamilyName(string.IsNullOrWhiteSpace(selected) ? fallback : selected);
    }

    private sealed record ColumnControls(CheckBox Show, TextBox Width, FontFamilySelector? FontFamily, TextBox FontSize, CheckBox? Bold);

    private sealed record FontControls(CheckBox Show, FontFamilySelector FontFamily, TextBox FontSize, CheckBox Bold);
}
