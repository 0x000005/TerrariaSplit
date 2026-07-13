using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class UiSettingsPage : SettingsPageBase
{
    private const float NumericColumnWidth = 118f;
    private const int NumericInputWidth = 86;
    private const float AlignmentColumnWidth = 148f;
    private const int AlignmentInputWidth = 132;

    private readonly Dictionary<string, ColumnControls> columnControls = new();
    private readonly Dictionary<string, FontControls> fontControls = new();
    private readonly TextBox iconNameGapBox = new();
    private readonly TextBox nameTimeGapBox = new();
    private readonly TextBox timeDeltaGapBox = new();
    private readonly TextBox timerOffsetXBox = new();
    private readonly TextBox timerOffsetYBox = new();
    private readonly CheckBox visibleGroupCountLimitEnabledBox = new();
    private readonly TextBox visibleGroupCountLimitBox = new();
    private readonly TextBox currentGroupPositionBox = new();
    private readonly CheckBox showFinalGroupBox = new();
    private readonly CheckBox showAllVisibleGroupsAfterFinalGroupBox = new();
    private readonly CheckBox expandSplitDetailsBox = new();
    private readonly CheckBox collapseSplitDetailsOnCompletionBox = new();
    private readonly CheckBox showAllMultiConditionMainGroupsAfterFinalGroupBox = new();
    private readonly CheckBox autoHideAttachedGroupsBox = new();
    private readonly CheckBox showAllAttachedGroupsAfterFinalGroupBox = new();
    private readonly CheckBox showEarlyDeltaTimeBox = new();
    private readonly TextBox earlyDeltaTimeSecondsBox = new();
    private readonly CheckBox enableDynamicDeltaTimeUnitsBox = new();
    private readonly TextBox iconOpacityBox = new();
    private readonly TextBox iconShadowBox = new();
    private readonly TextBox iconOutlineThicknessBox = new();
    private readonly TextBox nameOpacityBox = new();
    private readonly TextBox nameShadowBox = new();
    private readonly TextBox nameOutlineThicknessBox = new();
    private readonly TextBox timeOpacityBox = new();
    private readonly TextBox timeShadowBox = new();
    private readonly TextBox timeOutlineThicknessBox = new();
    private readonly TextBox deltaOpacityBox = new();
    private readonly TextBox deltaShadowBox = new();
    private readonly TextBox deltaOutlineThicknessBox = new();
    private readonly TextBox attachedIconOpacityBox = new();
    private readonly TextBox attachedIconShadowBox = new();
    private readonly TextBox attachedIconOutlineThicknessBox = new();
    private readonly TextBox attachedNameOpacityBox = new();
    private readonly TextBox attachedNameShadowBox = new();
    private readonly TextBox attachedNameOutlineThicknessBox = new();
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

    internal TextBox IconNameGapBoxForTests => iconNameGapBox;

    internal TextBox NameTimeGapBoxForTests => nameTimeGapBox;

    internal TextBox TimeDeltaGapBoxForTests => timeDeltaGapBox;

    internal CheckBox ShowFinalGroupBoxForTests => showFinalGroupBox;

    internal CheckBox ShowAllVisibleGroupsAfterFinalGroupBoxForTests => showAllVisibleGroupsAfterFinalGroupBox;

    internal CheckBox ExpandSplitDetailsBoxForTests => expandSplitDetailsBox;

    internal CheckBox CollapseSplitDetailsOnCompletionBoxForTests => collapseSplitDetailsOnCompletionBox;

    internal CheckBox ShowAllMultiConditionMainGroupsAfterFinalGroupBoxForTests => showAllMultiConditionMainGroupsAfterFinalGroupBox;

    internal CheckBox AutoHideAttachedGroupsBoxForTests => autoHideAttachedGroupsBox;

    internal CheckBox ShowAllAttachedGroupsAfterFinalGroupBoxForTests => showAllAttachedGroupsAfterFinalGroupBox;

    internal CheckBox ShowEarlyDeltaTimeBoxForTests => showEarlyDeltaTimeBox;

    internal TextBox EarlyDeltaTimeSecondsBoxForTests => earlyDeltaTimeSecondsBox;

    internal CheckBox EnableDynamicDeltaTimeUnitsBoxForTests => enableDynamicDeltaTimeUnitsBox;

    internal TextBox IconOpacityBox => iconOpacityBox;

    internal TextBox IconShadowBox => iconShadowBox;

    internal TextBox IconOutlineThicknessBox => iconOutlineThicknessBox;

    internal TextBox TimeOpacityBox => timeOpacityBox;

    internal TextBox TimeShadowBox => timeShadowBox;

    internal TextBox TimeOutlineThicknessBox => timeOutlineThicknessBox;

    internal TextBox DeltaOpacityBox => deltaOpacityBox;

    internal TextBox DeltaShadowBox => deltaShadowBox;

    internal TextBox DeltaOutlineThicknessBox => deltaOutlineThicknessBox;

    internal TextBox AttachedIconOpacityBox => attachedIconOpacityBox;

    internal TextBox AttachedIconShadowBox => attachedIconShadowBox;

    internal TextBox AttachedIconOutlineThicknessBox => attachedIconOutlineThicknessBox;

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

    internal ThemedDropDownList GetColumnAlignmentBoxForTests(string key)
    {
        return GetColumnControlsForTests(key).Alignment;
    }

    internal CheckBox GetColumnShowBoxForTests(string key)
    {
        return GetColumnControlsForTests(key).Show;
    }

    internal TextBox GetColumnFontSizeBoxForTests(string key)
    {
        return GetColumnControlsForTests(key).FontSize;
    }

    internal CheckBox? GetColumnBoldBoxForTests(string key)
    {
        return GetColumnControlsForTests(key).Bold;
    }

    internal CheckBox? GetItalicBoxForTests(string key)
    {
        if (columnControls.TryGetValue(key, out ColumnControls? columnControlsValue))
        {
            return columnControlsValue.Italic;
        }

        return fontControls.TryGetValue(key, out FontControls? fontControlsValue)
            ? fontControlsValue.Italic
            : null;
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
        foreach (UiColumnDescriptor descriptor in UiColumnDescriptors.SplitDisplay)
        {
            ApplyColumnSettings(descriptor, EnsureColumn(settings.Overlay.Columns, descriptor));
        }
        UiColumnDescriptors.SynchronizeSharedWidths(settings.Overlay.Columns);
        foreach ((UiColumnDescriptor primary, _) in UiColumnDescriptors.SharedWidthPairs)
        {
            UiColumnDescriptors.SetSharedAlignment(
                settings.Overlay.Columns,
                primary,
                GetSelectedColumnAlignment(columnControls[primary.Key].Alignment));
        }

        settings.Overlay.Columns.IconNameGap = SettingsValueParser.ParseIntBox(iconNameGapBox, 5, 0, 1000);
        settings.Overlay.Columns.NameTimeGap = SettingsValueParser.ParseIntBox(nameTimeGapBox, 5, 0, 1000);
        settings.Overlay.Columns.TimeDeltaGap = SettingsValueParser.ParseIntBox(timeDeltaGapBox, 5, 0, 1000);

        foreach (UiColumnDescriptor descriptor in UiColumnDescriptors.TimerDisplay)
        {
            ApplyFontSettings(descriptor, EnsureColumn(settings.Overlay.Columns, descriptor));
        }

        if (ApplyGroupSettings(settings))
        {
            Context.NotifyModelChanged(SettingsModelChange.RouteChanged);
        }
        ApplyDeltaTimeSettings(settings);

        settings.Overlay.Columns.TimerOffsetX = SettingsValueParser.ParseIntBox(timerOffsetXBox, 0, -2000, 2000);
        settings.Overlay.Columns.TimerOffsetY = SettingsValueParser.ParseIntBox(timerOffsetYBox, 0, -2000, 2000);
        settings.Overlay.TextEffects ??= new UiTextEffectSettings();
        ApplyTextEffectSettings(settings.Overlay.TextEffects);
    }

    private void AddColumnSettingsSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = Factory.CreateSection("Split display");
        TableLayoutPanel grid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(92f),
            SettingsUiFactory.ColumnStyleAbsolute(NumericColumnWidth),
            SettingsUiFactory.ColumnStyleAbsolute(AlignmentColumnWidth),
            SettingsUiFactory.ColumnStyleAbsolute(220f),
            SettingsUiFactory.ColumnStyleAbsolute(NumericColumnWidth),
            SettingsUiFactory.ColumnStyleAbsolute(92f),
            SettingsUiFactory.ColumnStyleAbsolute(92f),
            SettingsUiFactory.ColumnStyleAbsolute(NumericColumnWidth),
            SettingsUiFactory.ColumnStyleAbsolute(NumericColumnWidth),
            SettingsUiFactory.ColumnStyleAbsolute(NumericColumnWidth));

        Factory.AddHeaderRow(grid, ContentAlignment.MiddleLeft, "Column", "Show", "Width", "Alignment", "Font family", "Size", "Bold", "Italic", "Opacity %", "Shadow %", "Outline %");
        AddColumnSettingsRow(
            grid,
            UiColumnDescriptors.Icon,
            EnsureColumn(Draft.Overlay.Columns, UiColumnDescriptors.Icon));
        AddColumnSettingsRow(
            grid,
            UiColumnDescriptors.AttachedIcon,
            EnsureColumn(Draft.Overlay.Columns, UiColumnDescriptors.AttachedIcon));
        AddColumnSpacingRow(grid, iconNameGapBox, Draft.Overlay.Columns.IconNameGap);
        AddColumnSettingsRow(
            grid,
            UiColumnDescriptors.Name,
            EnsureColumn(Draft.Overlay.Columns, UiColumnDescriptors.Name));
        AddColumnSettingsRow(
            grid,
            UiColumnDescriptors.AttachedName,
            EnsureColumn(Draft.Overlay.Columns, UiColumnDescriptors.AttachedName));
        AddColumnSpacingRow(grid, nameTimeGapBox, Draft.Overlay.Columns.NameTimeGap);
        AddColumnSettingsRow(
            grid,
            UiColumnDescriptors.Time,
            EnsureColumn(Draft.Overlay.Columns, UiColumnDescriptors.Time));
        AddColumnSettingsRow(
            grid,
            UiColumnDescriptors.AttachedTime,
            EnsureColumn(Draft.Overlay.Columns, UiColumnDescriptors.AttachedTime));
        AddColumnSpacingRow(grid, timeDeltaGapBox, Draft.Overlay.Columns.TimeDeltaGap);
        AddColumnSettingsRow(
            grid,
            UiColumnDescriptors.Delta,
            EnsureColumn(Draft.Overlay.Columns, UiColumnDescriptors.Delta));
        AddColumnSettingsRow(
            grid,
            UiColumnDescriptors.AttachedDelta,
            EnsureColumn(Draft.Overlay.Columns, UiColumnDescriptors.AttachedDelta));

        SettingsUiFactory.AddSectionControl(section, grid);

        foreach (UiColumnDescriptor descriptor in UiColumnDescriptors.SplitDisplay)
        {
            columnControls[descriptor.Key].Show.CheckedChanged += (_, _) => UpdateColumnSettingsAvailability();
        }

        UpdateColumnSettingsAvailability();
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
        ConfigureCheckBox(showAllVisibleGroupsAfterFinalGroupBox, Draft.Route.ShowAllVisibleGroupsAfterFinalGroup);
        Factory.AddSettingRow(visibleGrid, "Enabled", visibleGroupCountLimitEnabledBox);
        Factory.AddSettingRow(visibleGrid, "Visible group count", visibleGroupCountLimitBox);
        Factory.AddSettingRow(visibleGrid, "Current group position", currentGroupPositionBox);
        Factory.AddSettingRow(visibleGrid, "Always show final group", showFinalGroupBox);
        Factory.AddSettingRow(visibleGrid, "Remove limit after final group completion", showAllVisibleGroupsAfterFinalGroupBox);
        SettingsUiFactory.AddSectionControl(section, visibleGrid);

        SettingsUiFactory.AddSectionControl(section, Factory.CreateSubsectionLabel("Main groups"));
        TableLayoutPanel mainGrid = Factory.CreateTwoColumnGrid(280f);
        ConfigureCheckBox(expandSplitDetailsBox, Draft.Route.ExpandSplitDetails);
        expandSplitDetailsBox.CheckedChanged += (_, _) => UpdateMainGroupVisibilityOptionAvailability();
        ConfigureCheckBox(collapseSplitDetailsOnCompletionBox, Draft.Route.CollapseSplitDetailsOnCompletion);
        collapseSplitDetailsOnCompletionBox.CheckedChanged += (_, _) => UpdateMainGroupVisibilityOptionAvailability();
        ConfigureCheckBox(showAllMultiConditionMainGroupsAfterFinalGroupBox, Draft.Route.ShowAllMultiConditionMainGroupsAfterFinalGroup);
        Factory.AddSettingRow(mainGrid, "Auto expand multi-condition main groups", expandSplitDetailsBox);
        Factory.AddSettingRow(mainGrid, "Collapse after completion", collapseSplitDetailsOnCompletionBox);
        Factory.AddSettingRow(mainGrid, "Uncollapse multi-condition main groups after final group completion", showAllMultiConditionMainGroupsAfterFinalGroupBox);
        SettingsUiFactory.AddSectionControl(section, mainGrid);

        SettingsUiFactory.AddSectionControl(section, Factory.CreateSubsectionLabel("Attached groups"));
        TableLayoutPanel attachedGrid = Factory.CreateTwoColumnGrid(280f);
        ConfigureCheckBox(autoHideAttachedGroupsBox, Draft.Route.AutoHideAttachedGroups);
        autoHideAttachedGroupsBox.CheckedChanged += (_, _) => UpdateAttachedGroupVisibilityOptionAvailability();
        ConfigureCheckBox(showAllAttachedGroupsAfterFinalGroupBox, Draft.Route.ShowAllAttachedGroupsAfterFinalGroup);
        Factory.AddSettingRow(attachedGrid, "Auto hide attached groups", autoHideAttachedGroupsBox);
        Factory.AddSettingRow(attachedGrid, "Uncollapse attached groups after final group completion", showAllAttachedGroupsAfterFinalGroupBox);
        SettingsUiFactory.AddSectionControl(section, attachedGrid);

        SettingsUiFactory.AddSection(parent, section);
        UpdateVisibleGroupCountLimitAvailability();
        UpdateMainGroupVisibilityOptionAvailability();
        UpdateAttachedGroupVisibilityOptionAvailability();
    }

    private void AddColumnSettingsRow(
        TableLayoutPanel grid,
        UiColumnDescriptor descriptor,
        UiColumnSettings value)
    {
        var showBox = CreateCenteredCheckBox(value.Show);
        UiColumnDescriptor widthOwner = UiColumnDescriptors.GetWidthOwner(descriptor);
        bool ownsWidth = string.Equals(widthOwner.Key, descriptor.Key, StringComparison.Ordinal);
        TextBox widthBox;
        Control? widthControl = null;
        ThemedDropDownList alignmentBox;
        Control? alignmentControl = null;
        if (ownsWidth)
        {
            widthBox = Factory.CreateNumberBox(value.Width, 1, 1000);
            widthControl = Factory.CreateCenteredCell(widthBox, NumericInputWidth);
            alignmentBox = CreateColumnAlignmentBox(
                UiColumnDescriptors.GetSharedAlignment(Draft.Overlay.Columns, descriptor));
            alignmentControl = Factory.CreateCenteredCell(alignmentBox, AlignmentInputWidth);
        }
        else if (columnControls.TryGetValue(widthOwner.Key, out ColumnControls? ownerControls))
        {
            widthBox = ownerControls.Width;
            alignmentBox = ownerControls.Alignment;
        }
        else
        {
            throw new InvalidOperationException($"Shared width owner controls not found for {descriptor.Key}.");
        }

        FontFamilySelector? fontFamilyBox = descriptor.ShowFontFamily ? CreateFontFamilyBox(value.FontFamily) : null;
        TextBox fontBox = Factory.CreateDecimalBox(value.FontSize, 6, 96);
        TextEffectBoxes effectBoxes = GetTextEffectBoxes(descriptor.TextEffect);
        UiTextEffectSettings textEffects = Draft.Overlay.TextEffects;
        Control opacityControl = CreateEffectCell(effectBoxes.Opacity, descriptor.TextEffect.GetOpacity(textEffects), 100);
        Control shadowControl = CreateEffectCell(effectBoxes.Shadow, descriptor.TextEffect.GetShadow?.Invoke(textEffects) ?? 0, 100);
        Control outlineThicknessControl = CreateEffectCell(effectBoxes.Outline, descriptor.TextEffect.GetOutline?.Invoke(textEffects) ?? 0, 100);
        Control fontFamilyControl = fontFamilyBox is null
            ? CreateEmptySettingsCell()
            : Factory.CreateCenteredCell(fontFamilyBox, 210);

        CheckBox? boldBox = null;
        Control boldControl = CreateEmptySettingsCell();
        if (descriptor.ShowBold)
        {
            boldBox = CreateCenteredCheckBox(value.Bold);
            boldControl = Factory.CreateCenteredCell(boldBox, 28);
        }

        CheckBox? italicBox = null;
        Control italicControl = CreateEmptySettingsCell();
        if (descriptor.ShowItalic)
        {
            italicBox = CreateCenteredCheckBox(value.Italic);
            italicControl = Factory.CreateCenteredCell(italicBox, 28);
        }

        columnControls[descriptor.Key] = new ColumnControls(showBox, widthBox, alignmentBox, fontFamilyBox, fontBox, boldBox, italicBox);

        int row = Factory.AddGridRow(grid);
        grid.Controls.Add(Factory.CreateRowLabel(descriptor.Label), 0, row);
        grid.Controls.Add(Factory.CreateCenteredCell(showBox, 28), 1, row);
        if (widthControl is not null)
        {
            grid.Controls.Add(widthControl, 2, row);
            grid.SetRowSpan(widthControl, 2);
        }
        if (alignmentControl is not null)
        {
            grid.Controls.Add(alignmentControl, 3, row);
            grid.SetRowSpan(alignmentControl, 2);
        }
        grid.Controls.Add(fontFamilyControl, 4, row);
        grid.Controls.Add(Factory.CreateCenteredCell(fontBox, NumericInputWidth), 5, row);
        grid.Controls.Add(boldControl, 6, row);
        grid.Controls.Add(italicControl, 7, row);
        grid.Controls.Add(opacityControl, 8, row);
        grid.Controls.Add(shadowControl, 9, row);
        grid.Controls.Add(outlineThicknessControl, 10, row);
    }

    private void AddColumnSpacingRow(TableLayoutPanel grid, TextBox spacingBox, int value)
    {
        ConfigureNumberBox(spacingBox, value, 0, 1000);
        int row = Factory.AddGridRow(grid);
        grid.Controls.Add(Factory.CreateRowLabel("Spacing"), 0, row);
        grid.Controls.Add(Factory.CreateCenteredCell(spacingBox, NumericInputWidth), 2, row);
    }

    private ThemedDropDownList CreateColumnAlignmentBox(string selectedAlignment)
    {
        ThemedDropDownList box = Factory.CreateDropDownList();
        string normalized = UiColumnAlignment.Normalize(selectedAlignment, UiColumnAlignment.Left);
        foreach (string alignment in UiColumnAlignment.All)
        {
            box.Items.Add(new ColumnAlignmentOption(
                alignment,
                Context.Localize(UiColumnAlignment.GetDisplayName(alignment))));
        }

        for (int i = 0; i < box.Items.Count; i++)
        {
            if (box.Items[i] is ColumnAlignmentOption option &&
                string.Equals(option.Value, normalized, StringComparison.Ordinal))
            {
                box.SelectedIndex = i;
                break;
            }
        }

        if (box.SelectedIndex < 0 && box.Items.Count > 0)
        {
            box.SelectedIndex = 0;
        }

        return box;
    }

    private static string GetSelectedColumnAlignment(ThemedDropDownList box)
    {
        return box.SelectedItem is ColumnAlignmentOption option
            ? option.Value
            : UiColumnAlignment.Left;
    }

    private void AddTimerSettingsSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = Factory.CreateSection("Main timer");
        TableLayoutPanel grid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(92f),
            SettingsUiFactory.ColumnStyleAbsolute(220f),
            SettingsUiFactory.ColumnStyleAbsolute(NumericColumnWidth),
            SettingsUiFactory.ColumnStyleAbsolute(92f),
            SettingsUiFactory.ColumnStyleAbsolute(92f),
            SettingsUiFactory.ColumnStyleAbsolute(NumericColumnWidth),
            SettingsUiFactory.ColumnStyleAbsolute(NumericColumnWidth),
            SettingsUiFactory.ColumnStyleAbsolute(NumericColumnWidth));

        Factory.AddHeaderRow(grid, ContentAlignment.MiddleLeft, "Section", "Show", "Font family", "Size", "Bold", "Italic", "Opacity %", "Shadow %", "Outline %");
        AddFontSettingsRow(
            grid,
            UiColumnDescriptors.Timer,
            EnsureColumn(Draft.Overlay.Columns, UiColumnDescriptors.Timer));
        AddFontSettingsRow(
            grid,
            UiColumnDescriptors.TimerMilliseconds,
            EnsureColumn(Draft.Overlay.Columns, UiColumnDescriptors.TimerMilliseconds));

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
        UiColumnDescriptor descriptor,
        UiColumnSettings value)
    {
        var showBox = CreateCenteredCheckBox(value.Show);
        FontFamilySelector fontFamilyBox = CreateFontFamilyBox(value.FontFamily);
        TextBox fontBox = Factory.CreateDecimalBox(value.FontSize, 6, 96);
        TextEffectBoxes effectBoxes = GetTextEffectBoxes(descriptor.TextEffect);
        UiTextEffectSettings textEffects = Draft.Overlay.TextEffects;
        Control opacityControl = CreateEffectCell(effectBoxes.Opacity, descriptor.TextEffect.GetOpacity(textEffects), 100);
        Control shadowControl = CreateEffectCell(effectBoxes.Shadow, descriptor.TextEffect.GetShadow?.Invoke(textEffects) ?? 0, 100);
        Control outlineThicknessControl = CreateEffectCell(effectBoxes.Outline, descriptor.TextEffect.GetOutline?.Invoke(textEffects) ?? 0, 100);
        var boldBox = CreateCenteredCheckBox(value.Bold);
        var italicBox = CreateCenteredCheckBox(value.Italic);

        fontControls[descriptor.Key] = new FontControls(showBox, fontFamilyBox, fontBox, boldBox, italicBox);
        int row = Factory.AddGridRow(grid);
        grid.Controls.Add(Factory.CreateRowLabel(descriptor.Label), 0, row);
        grid.Controls.Add(Factory.CreateCenteredCell(showBox, 28), 1, row);
        grid.Controls.Add(Factory.CreateCenteredCell(fontFamilyBox, 210), 2, row);
        grid.Controls.Add(Factory.CreateCenteredCell(fontBox, NumericInputWidth), 3, row);
        grid.Controls.Add(Factory.CreateCenteredCell(boldBox, 28), 4, row);
        grid.Controls.Add(Factory.CreateCenteredCell(italicBox, 28), 5, row);
        grid.Controls.Add(opacityControl, 6, row);
        grid.Controls.Add(shadowControl, 7, row);
        grid.Controls.Add(outlineThicknessControl, 8, row);
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
        return Factory.CreateCenteredCell(textBox, NumericInputWidth);
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
        bool showAllVisibleGroupsAfterFinalGroup = showAllVisibleGroupsAfterFinalGroupBox.Checked;
        bool showAllAttachedGroupsAfterFinalGroup = showAllAttachedGroupsAfterFinalGroupBox.Checked;
        bool expand = expandSplitDetailsBox.Checked;
        bool collapse = collapseSplitDetailsOnCompletionBox.Checked;
        bool showAllMultiConditionMainGroupsAfterFinalGroup = showAllMultiConditionMainGroupsAfterFinalGroupBox.Checked;
        bool autoHideAttachedGroups = autoHideAttachedGroupsBox.Checked;
        bool changed =
            settings.Route.EnableVisibleGroupCountLimit != enableVisibleGroupCountLimit ||
            settings.Route.VisibleGroupCountLimit != visibleGroupCountLimit ||
            settings.Route.CurrentGroupPosition != currentGroupPosition ||
            settings.Route.ShowFinalGroup != showFinalGroup ||
            settings.Route.ShowAllVisibleGroupsAfterFinalGroup != showAllVisibleGroupsAfterFinalGroup ||
            settings.Route.ShowAllAttachedGroupsAfterFinalGroup != showAllAttachedGroupsAfterFinalGroup ||
            settings.Route.ExpandSplitDetails != expand ||
            settings.Route.CollapseSplitDetailsOnCompletion != collapse ||
            settings.Route.ShowAllMultiConditionMainGroupsAfterFinalGroup != showAllMultiConditionMainGroupsAfterFinalGroup ||
            settings.Route.AutoHideAttachedGroups != autoHideAttachedGroups;

        settings.Route.EnableVisibleGroupCountLimit = enableVisibleGroupCountLimit;
        settings.Route.VisibleGroupCountLimit = visibleGroupCountLimit;
        settings.Route.CurrentGroupPosition = currentGroupPosition;
        settings.Route.ShowFinalGroup = showFinalGroup;
        settings.Route.ShowAllVisibleGroupsAfterFinalGroup = showAllVisibleGroupsAfterFinalGroup;
        settings.Route.ShowAllAttachedGroupsAfterFinalGroup = showAllAttachedGroupsAfterFinalGroup;
        settings.Route.ExpandSplitDetails = expand;
        settings.Route.CollapseSplitDetailsOnCompletion = collapse;
        settings.Route.ShowAllMultiConditionMainGroupsAfterFinalGroup = showAllMultiConditionMainGroupsAfterFinalGroup;
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

    private void ApplyTextEffectSettings(UiTextEffectSettings textEffects)
    {
        foreach (UiTextEffectDescriptor descriptor in UiTextEffectDescriptors.All)
        {
            TextEffectBoxes boxes = GetTextEffectBoxes(descriptor);
            descriptor.SetOpacity(
                textEffects,
                SettingsValueParser.ParseIntBox(boxes.Opacity, descriptor.GetOpacity(textEffects), 0, 100));

            if (descriptor.GetShadow is not null &&
                descriptor.SetShadow is not null &&
                boxes.Shadow is not null)
            {
                descriptor.SetShadow(
                    textEffects,
                    SettingsValueParser.ParseIntBox(boxes.Shadow, descriptor.GetShadow(textEffects), 0, 100));
            }

            if (descriptor.GetOutline is not null &&
                descriptor.SetOutline is not null &&
                boxes.Outline is not null)
            {
                descriptor.SetOutline(
                    textEffects,
                    SettingsValueParser.ParseIntBox(boxes.Outline, descriptor.GetOutline(textEffects), 0, 100));
            }
        }
    }

    private void UpdateMainGroupVisibilityOptionAvailability()
    {
        collapseSplitDetailsOnCompletionBox.Enabled = expandSplitDetailsBox.Checked;
        showAllMultiConditionMainGroupsAfterFinalGroupBox.Enabled =
            expandSplitDetailsBox.Checked && collapseSplitDetailsOnCompletionBox.Checked;
    }

    private void UpdateColumnSettingsAvailability()
    {
        foreach (UiColumnDescriptor descriptor in UiColumnDescriptors.SplitDisplay)
        {
            ColumnControls controls = columnControls[descriptor.Key];
            bool visible = controls.Show.Checked;
            if (controls.FontFamily is not null)
            {
                controls.FontFamily.Enabled = visible;
            }

            controls.FontSize.Enabled = visible;
            if (controls.Bold is not null)
            {
                controls.Bold.Enabled = visible;
            }

            if (controls.Italic is not null)
            {
                controls.Italic.Enabled = visible;
            }

            TextEffectBoxes effects = GetTextEffectBoxes(descriptor.TextEffect);
            effects.Opacity.Enabled = visible;
            if (effects.Shadow is not null)
            {
                effects.Shadow.Enabled = visible;
            }

            if (effects.Outline is not null)
            {
                effects.Outline.Enabled = visible;
            }
        }

        foreach ((UiColumnDescriptor primary, UiColumnDescriptor attached) in UiColumnDescriptors.SharedWidthPairs)
        {
            ColumnControls primaryControls = columnControls[primary.Key];
            ColumnControls attachedControls = columnControls[attached.Key];
            bool groupVisible = primaryControls.Show.Checked || attachedControls.Show.Checked;
            primaryControls.Width.Enabled = groupVisible;
            primaryControls.Alignment.Enabled = groupVisible;
        }

        iconNameGapBox.Enabled = IsColumnGroupVisible(UiColumnDescriptors.Icon, UiColumnDescriptors.AttachedIcon);
        nameTimeGapBox.Enabled = IsColumnGroupVisible(UiColumnDescriptors.Name, UiColumnDescriptors.AttachedName);
        timeDeltaGapBox.Enabled = IsColumnGroupVisible(UiColumnDescriptors.Time, UiColumnDescriptors.AttachedTime);
    }

    private bool IsColumnGroupVisible(UiColumnDescriptor primary, UiColumnDescriptor attached)
    {
        return columnControls[primary.Key].Show.Checked || columnControls[attached.Key].Show.Checked;
    }

    private void UpdateAttachedGroupVisibilityOptionAvailability()
    {
        showAllAttachedGroupsAfterFinalGroupBox.Enabled = autoHideAttachedGroupsBox.Checked;
    }

    private void UpdateVisibleGroupCountLimitAvailability()
    {
        bool enabled = visibleGroupCountLimitEnabledBox.Checked;
        visibleGroupCountLimitBox.Enabled = enabled;
        currentGroupPositionBox.Enabled = enabled;
        showFinalGroupBox.Enabled = enabled;
        showAllVisibleGroupsAfterFinalGroupBox.Enabled = enabled;
    }

    private void UpdateEarlyDeltaAvailability()
    {
        earlyDeltaTimeSecondsBox.Enabled = showEarlyDeltaTimeBox.Checked;
    }

    private void ApplyColumnSettings(UiColumnDescriptor descriptor, UiColumnSettings target)
    {
        if (!columnControls.TryGetValue(descriptor.Key, out ColumnControls? controls))
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

        if (controls.Italic is not null)
        {
            target.Italic = controls.Italic.Checked;
        }
    }

    private void ApplyFontSettings(UiColumnDescriptor descriptor, UiColumnSettings target)
    {
        if (!fontControls.TryGetValue(descriptor.Key, out FontControls? controls))
        {
            return;
        }

        target.Show = controls.Show.Checked;
        target.FontFamily = GetSelectedFontFamily(controls.FontFamily, target.FontFamily);
        target.FontSize = SettingsValueParser.ParseFloatBox(controls.FontSize, target.FontSize, 6f, 96f);
        target.Bold = controls.Bold.Checked;
        target.Italic = controls.Italic.Checked;
    }

    private static string GetSelectedFontFamily(FontFamilySelector selector, string fallback)
    {
        string selected = selector.SelectedFontFamily;
        return UiFontFactory.Default.NormalizeFamilyName(string.IsNullOrWhiteSpace(selected) ? fallback : selected);
    }

    private UiColumnSettings EnsureColumn(UiColumnLayoutSettings columns, UiColumnDescriptor descriptor)
    {
        UiColumnSettings? column = descriptor.GetValue(columns);
        if (column is not null)
        {
            return column;
        }

        column = new UiColumnSettings();
        descriptor.SetValue(columns, column);
        return column;
    }

    private TextEffectBoxes GetTextEffectBoxes(UiTextEffectDescriptor descriptor)
    {
        return descriptor.Key switch
        {
            nameof(UiTextEffectSettings.IconOpacityPercent) => new TextEffectBoxes(iconOpacityBox, iconShadowBox, iconOutlineThicknessBox),
            nameof(UiTextEffectSettings.NameOpacityPercent) => new TextEffectBoxes(nameOpacityBox, nameShadowBox, nameOutlineThicknessBox),
            nameof(UiTextEffectSettings.TimeOpacityPercent) => new TextEffectBoxes(timeOpacityBox, timeShadowBox, timeOutlineThicknessBox),
            nameof(UiTextEffectSettings.DeltaOpacityPercent) => new TextEffectBoxes(deltaOpacityBox, deltaShadowBox, deltaOutlineThicknessBox),
            nameof(UiTextEffectSettings.AttachedIconOpacityPercent) => new TextEffectBoxes(attachedIconOpacityBox, attachedIconShadowBox, attachedIconOutlineThicknessBox),
            nameof(UiTextEffectSettings.AttachedNameOpacityPercent) => new TextEffectBoxes(attachedNameOpacityBox, attachedNameShadowBox, attachedNameOutlineThicknessBox),
            nameof(UiTextEffectSettings.AttachedTimeOpacityPercent) => new TextEffectBoxes(attachedTimeOpacityBox, attachedTimeShadowBox, attachedTimeOutlineThicknessBox),
            nameof(UiTextEffectSettings.AttachedDeltaOpacityPercent) => new TextEffectBoxes(attachedDeltaOpacityBox, attachedDeltaShadowBox, attachedDeltaOutlineThicknessBox),
            nameof(UiTextEffectSettings.TimerOpacityPercent) => new TextEffectBoxes(timerOpacityBox, timerShadowBox, timerOutlineThicknessBox),
            nameof(UiTextEffectSettings.TimerMillisecondsOpacityPercent) => new TextEffectBoxes(timerMillisecondsOpacityBox, timerMillisecondsShadowBox, timerMillisecondsOutlineThicknessBox),
            _ => throw new NotSupportedException($"Unsupported text effect descriptor: {descriptor.Key}.")
        };
    }

    private sealed record ColumnControls(
        CheckBox Show,
        TextBox Width,
        ThemedDropDownList Alignment,
        FontFamilySelector? FontFamily,
        TextBox FontSize,
        CheckBox? Bold,
        CheckBox? Italic);

    private sealed record FontControls(CheckBox Show, FontFamilySelector FontFamily, TextBox FontSize, CheckBox Bold, CheckBox Italic);

    private sealed record ColumnAlignmentOption(string Value, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private readonly record struct TextEffectBoxes(TextBox Opacity, TextBox? Shadow, TextBox? Outline);
}
