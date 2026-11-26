using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace TerrariaSplit.UI.Wpf.Settings;

internal sealed class WpfUiSettingsPage : WpfSettingsPageBase
{
    private const double ShowColWidth = 44;
    private const double NumColWidth = 68;
    private const double AlignColWidth = 98;
    private const double FontColWidth = 148;
    private const double FlagColWidth = 44;

    private readonly Dictionary<string, ColumnControls> columnControls = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FontControls> fontControls = new(StringComparer.Ordinal);

    private WpfTextBox iconNameGapBox = null!;
    private WpfTextBox nameTimeGapBox = null!;
    private WpfTextBox timeDeltaGapBox = null!;
    private WpfTextBox timerOffsetXBox = null!;
    private WpfTextBox timerOffsetYBox = null!;

    private WpfCheckBox visibleGroupCountLimitEnabledBox = null!;
    private WpfTextBox visibleGroupCountLimitBox = null!;
    private WpfTextBox currentGroupPositionBox = null!;
    private WpfCheckBox showFinalGroupBox = null!;
    private WpfCheckBox showAllVisibleGroupsAfterFinalGroupBox = null!;
    private WpfCheckBox expandSplitDetailsBox = null!;
    private WpfCheckBox collapseSplitDetailsOnCompletionBox = null!;
    private WpfCheckBox showAllMultiConditionMainGroupsAfterFinalGroupBox = null!;
    private WpfCheckBox autoHideAttachedGroupsBox = null!;
    private WpfCheckBox showAllAttachedGroupsAfterFinalGroupBox = null!;

    private WpfCheckBox showEarlyDeltaTimeBox = null!;
    private WpfTextBox earlyDeltaTimeSecondsBox = null!;
    private WpfCheckBox enableDynamicDeltaTimeUnitsBox = null!;

    private readonly WpfTextBox iconOpacityBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox iconShadowBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox iconOutlineThicknessBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox nameOpacityBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox nameShadowBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox nameOutlineThicknessBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox timeOpacityBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox timeShadowBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox timeOutlineThicknessBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox deltaOpacityBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox deltaShadowBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox deltaOutlineThicknessBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox attachedIconOpacityBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox attachedIconShadowBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox attachedIconOutlineThicknessBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox attachedNameOpacityBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox attachedNameShadowBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox attachedNameOutlineThicknessBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox attachedTimeOpacityBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox attachedTimeShadowBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox attachedTimeOutlineThicknessBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox attachedDeltaOpacityBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox attachedDeltaShadowBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox attachedDeltaOutlineThicknessBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox timerOpacityBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox timerShadowBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox timerOutlineThicknessBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox timerMillisecondsOpacityBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox timerMillisecondsShadowBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);
    private readonly WpfTextBox timerMillisecondsOutlineThicknessBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true, centerAlign: true);

    public override SettingsPageId Id => SettingsPageId.Ui;

    internal WpfTextBox IconNameGapBoxForTests => iconNameGapBox;
    internal WpfTextBox NameTimeGapBoxForTests => nameTimeGapBox;
    internal WpfTextBox TimeDeltaGapBoxForTests => timeDeltaGapBox;
    internal WpfTextBox DeltaOpacityBox => deltaOpacityBox;
    internal WpfTextBox DeltaShadowBox => deltaShadowBox;
    internal WpfTextBox DeltaOutlineThicknessBox => deltaOutlineThicknessBox;

    internal WpfFontFamilySelector GetFontFamilySelectorForTests(string key)
    {
        if (columnControls.TryGetValue(key, out ColumnControls? col) && col.FontFamily is not null)
        {
            return col.FontFamily;
        }

        if (fontControls.TryGetValue(key, out FontControls? font))
        {
            return font.FontFamily;
        }

        throw new InvalidOperationException($"Font family control not found for {key}.");
    }

    internal WpfTextBox GetColumnWidthBoxForTests(string key) => GetColumnControls(key).Width;

    internal WpfComboBox GetColumnAlignmentBoxForTests(string key) => GetColumnControls(key).Alignment;

    internal WpfCheckBox GetColumnShowBoxForTests(string key) => GetColumnControls(key).Show;

    internal WpfTextBox GetColumnFontSizeBoxForTests(string key) => GetColumnControls(key).FontSize;

    internal WpfCheckBox? GetItalicBoxForTests(string key)
    {
        if (columnControls.TryGetValue(key, out ColumnControls? col))
        {
            return col.Italic;
        }

        return fontControls.TryGetValue(key, out FontControls? font) ? font.Italic : null;
    }

    protected override FrameworkElement BuildPage(WpfSettingsPageContext context)
    {
        return WpfSettingsUi.CreateScrollPage(content =>
        {
            AddSplitDisplaySection(content);
            AddMainTimerSection(content);
            AddGroupSection(content);
            AddDeltaTimeSection(content);
        });
    }

    public override void OnDeselected()
    {
        ApplyColumnSettings(UiColumnDescriptors.Delta, Draft.Overlay.Columns.Delta);
        ApplyTextEffectSettings(Draft.Overlay.TextEffects);
        Context.NotifyModelChanged(SettingsModelChange.TextAppearanceChanged);
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

        settings.Overlay.Columns.IconNameGap = WpfSettingsValueParser.ParseIntBox(iconNameGapBox, 5, 0, 1000);
        settings.Overlay.Columns.NameTimeGap = WpfSettingsValueParser.ParseIntBox(nameTimeGapBox, 5, 0, 1000);
        settings.Overlay.Columns.TimeDeltaGap = WpfSettingsValueParser.ParseIntBox(timeDeltaGapBox, 5, 0, 1000);

        foreach (UiColumnDescriptor descriptor in UiColumnDescriptors.TimerDisplay)
        {
            ApplyFontSettings(descriptor, EnsureColumn(settings.Overlay.Columns, descriptor));
        }

        if (ApplyGroupSettings(settings))
        {
            Context.NotifyModelChanged(SettingsModelChange.RouteChanged);
        }

        ApplyDeltaTimeSettings(settings);

        settings.Overlay.Columns.TimerOffsetX = WpfSettingsValueParser.ParseIntBox(timerOffsetXBox, 0, -2000, 2000);
        settings.Overlay.Columns.TimerOffsetY = WpfSettingsValueParser.ParseIntBox(timerOffsetYBox, 0, -2000, 2000);
        settings.Overlay.TextEffects ??= new UiTextEffectSettings();
        ApplyTextEffectSettings(settings.Overlay.TextEffects);
    }

    private void AddSplitDisplaySection(StackPanel parent)
    {
        var card = WpfSettingsUi.CreateCard(Localize("Split display"), out StackPanel body);

        iconNameGapBox = WpfSettingsUi.CreateNumberBox(Draft.Overlay.Columns.IconNameGap, 0, 1000, width: NumColWidth - 8);
        nameTimeGapBox = WpfSettingsUi.CreateNumberBox(Draft.Overlay.Columns.NameTimeGap, 0, 1000, width: NumColWidth - 8);
        timeDeltaGapBox = WpfSettingsUi.CreateNumberBox(Draft.Overlay.Columns.TimeDeltaGap, 0, 1000, width: NumColWidth - 8);

        body.Children.Add(CreateSplitDisplayHeader());
        body.Children.Add(CreateSplitPairBlock(UiColumnDescriptors.Icon, UiColumnDescriptors.AttachedIcon));
        body.Children.Add(CreateSpacingRow(iconNameGapBox));
        body.Children.Add(CreateSplitPairBlock(UiColumnDescriptors.Name, UiColumnDescriptors.AttachedName));
        body.Children.Add(CreateSpacingRow(nameTimeGapBox));
        body.Children.Add(CreateSplitPairBlock(UiColumnDescriptors.Time, UiColumnDescriptors.AttachedTime));
        body.Children.Add(CreateSpacingRow(timeDeltaGapBox));
        body.Children.Add(CreateSplitPairBlock(UiColumnDescriptors.Delta, UiColumnDescriptors.AttachedDelta, isLast: true));

        foreach (UiColumnDescriptor descriptor in UiColumnDescriptors.SplitDisplay)
        {
            WpfCheckBox show = columnControls[descriptor.Key].Show;
            show.Checked += (_, _) => UpdateColumnSettingsAvailability();
            show.Unchecked += (_, _) => UpdateColumnSettingsAvailability();
        }

        UpdateColumnSettingsAvailability();
        parent.Children.Add(card);
    }

    private Grid CreateSplitDisplayColumnGrid()
    {
        return WpfSettingsUi.CreateColumnGrid(
            new GridLength(1, GridUnitType.Star),
            new GridLength(ShowColWidth),
            new GridLength(NumColWidth),
            new GridLength(AlignColWidth),
            new GridLength(FontColWidth),
            new GridLength(NumColWidth),
            new GridLength(FlagColWidth),
            new GridLength(FlagColWidth),
            new GridLength(NumColWidth),
            new GridLength(NumColWidth),
            new GridLength(NumColWidth));
    }

    private Border CreateSplitDisplayHeader()
    {
        Grid grid = CreateSplitDisplayColumnGrid();
        string[] labels =
        [
            Localize("Column"), Localize("Show"), Localize("Width"), Localize("Alignment"),
            Localize("Font family"), Localize("Size"), Localize("Bold"), Localize("Italic"),
            Localize("Opacity %"), Localize("Shadow %"), Localize("Outline %")
        ];
        TextAlignment[] alignments =
        [
            TextAlignment.Left, TextAlignment.Center, TextAlignment.Center, TextAlignment.Center,
            TextAlignment.Center, TextAlignment.Center, TextAlignment.Center, TextAlignment.Center,
            TextAlignment.Center, TextAlignment.Center, TextAlignment.Center
        ];

        for (int i = 0; i < labels.Length; i++)
        {
            TextBlock header = WpfSettingsUi.CreateTableHeaderText(labels[i], alignments[i]);
            header.Margin = i == 0 ? new Thickness(0) : new Thickness(3, 0, 3, 0);
            Grid.SetColumn(header, i);
            grid.Children.Add(header);
        }

        return WpfSettingsUi.CreateTableHeaderContainer(grid);
    }

    private Border CreateSplitPairBlock(UiColumnDescriptor primary, UiColumnDescriptor attached, bool isLast = false)
    {
        Grid grid = CreateSplitDisplayColumnGrid();
        var primaryRow = new RowDefinition { Height = GridLength.Auto };
        var attachedRow = new RowDefinition { Height = GridLength.Auto };
        primaryRow.SetResourceReference(RowDefinition.MinHeightProperty, "TableRowHeight");
        attachedRow.SetResourceReference(RowDefinition.MinHeightProperty, "TableRowHeight");
        grid.RowDefinitions.Add(primaryRow);
        grid.RowDefinitions.Add(attachedRow);

        UiColumnSettings primaryVal = EnsureColumn(Draft.Overlay.Columns, primary);
        UiColumnSettings attachedVal = EnsureColumn(Draft.Overlay.Columns, attached);

        WpfTextBox sharedWidthBox = WpfSettingsUi.CreateNumberBox(primaryVal.Width, 1, 1000, width: NumColWidth - 8);
        WpfComboBox sharedAlignmentBox = CreateColumnAlignmentBox(UiColumnDescriptors.GetSharedAlignment(Draft.Overlay.Columns, primary));
        sharedAlignmentBox.Width = AlignColWidth - 6;

        PopulateSplitRow(grid, 0, primary, primaryVal, sharedWidthBox, sharedAlignmentBox);
        PopulateSplitRow(grid, 1, attached, attachedVal, sharedWidthBox, sharedAlignmentBox);

        Grid.SetRow(sharedWidthBox, 0);
        Grid.SetRowSpan(sharedWidthBox, 2);
        Grid.SetColumn(sharedWidthBox, 2);
        sharedWidthBox.HorizontalAlignment = WpfHorizontalAlignment.Center;
        sharedWidthBox.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(sharedWidthBox);

        Grid.SetRow(sharedAlignmentBox, 0);
        Grid.SetRowSpan(sharedAlignmentBox, 2);
        Grid.SetColumn(sharedAlignmentBox, 3);
        sharedAlignmentBox.HorizontalAlignment = WpfHorizontalAlignment.Center;
        sharedAlignmentBox.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(sharedAlignmentBox);

        return WpfSettingsUi.CreateRowDividerContainer(grid, showBottomBorder: !isLast, padding: new Thickness(0, 1, 0, 1), minHeightResourceKey: null);
    }

    private void PopulateSplitRow(
        Grid grid,
        int row,
        UiColumnDescriptor descriptor,
        UiColumnSettings value,
        WpfTextBox sharedWidthBox,
        WpfComboBox sharedAlignmentBox)
    {
        var cellPad = new Thickness(3, 0, 3, 0);

        TextBlock label = WpfSettingsUi.CreateRowLabel(Localize(descriptor.Label));
        label.Margin = new Thickness(0, cellPad.Top, 6, cellPad.Bottom);
        AddCell(grid, label, row, 0);

        WpfCheckBox showBox = WpfSettingsUi.CreateCheckBox(value.Show, centered: true);
        showBox.Margin = cellPad;
        AddCell(grid, showBox, row, 1);

        WpfFontFamilySelector? fontFamilyBox = null;
        if (descriptor.ShowFontFamily)
        {
            fontFamilyBox = new WpfFontFamilySelector { Width = FontColWidth - 6, Margin = cellPad };
            fontFamilyBox.SetSelectedFontFamily(value.FontFamily);
            AddCell(grid, fontFamilyBox, row, 4);
        }

        WpfTextBox fontSizeBox = WpfSettingsUi.CreateDecimalBox(value.FontSize, 6, 96, width: NumColWidth - 8);
        fontSizeBox.Margin = cellPad;
        fontSizeBox.HorizontalAlignment = WpfHorizontalAlignment.Center;
        AddCell(grid, fontSizeBox, row, 5);

        WpfCheckBox? boldBox = null;
        if (descriptor.ShowBold)
        {
            boldBox = WpfSettingsUi.CreateCheckBox(value.Bold, centered: true);
            boldBox.Margin = cellPad;
            AddCell(grid, boldBox, row, 6);
        }

        WpfCheckBox? italicBox = null;
        if (descriptor.ShowItalic)
        {
            italicBox = WpfSettingsUi.CreateCheckBox(value.Italic, centered: true);
            italicBox.Margin = cellPad;
            AddCell(grid, italicBox, row, 7);
        }

        TextEffectBoxes effects = GetTextEffectBoxes(descriptor.TextEffect);
        UiTextEffectSettings textEffects = Draft.Overlay.TextEffects;
        ConfigureEffectBox(effects.Opacity, descriptor.TextEffect.GetOpacity(textEffects), cellPad);
        AddCell(grid, effects.Opacity, row, 8);

        if (effects.Shadow is not null)
        {
            ConfigureEffectBox(effects.Shadow, descriptor.TextEffect.GetShadow?.Invoke(textEffects) ?? 0, cellPad);
            AddCell(grid, effects.Shadow, row, 9);
        }

        if (effects.Outline is not null)
        {
            ConfigureEffectBox(effects.Outline, descriptor.TextEffect.GetOutline?.Invoke(textEffects) ?? 0, cellPad);
            AddCell(grid, effects.Outline, row, 10);
        }

        columnControls[descriptor.Key] = new ColumnControls(
            showBox,
            sharedWidthBox,
            sharedAlignmentBox,
            fontFamilyBox,
            fontSizeBox,
            boldBox,
            italicBox);
    }

    private Border CreateSpacingRow(WpfTextBox spacingBox)
    {
        Grid grid = CreateSplitDisplayColumnGrid();
        TextBlock label = WpfSettingsUi.CreateTableHeaderText(Localize("Spacing"));
        label.Margin = new Thickness(6, 0, 6, 0);
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        spacingBox.HorizontalAlignment = WpfHorizontalAlignment.Center;
        Grid.SetColumn(spacingBox, 2);
        grid.Children.Add(spacingBox);

        return WpfSettingsUi.CreateTableRowContainer(grid, showBottomBorder: true);
    }

    private void AddMainTimerSection(StackPanel parent)
    {
        var card = WpfSettingsUi.CreateCard(Localize("Main timer"), out StackPanel body);

        body.Children.Add(CreateTimerHeader());
        body.Children.Add(CreateTimerRow(UiColumnDescriptors.Timer, EnsureColumn(Draft.Overlay.Columns, UiColumnDescriptors.Timer), isLast: false));
        body.Children.Add(CreateTimerRow(UiColumnDescriptors.TimerMilliseconds, EnsureColumn(Draft.Overlay.Columns, UiColumnDescriptors.TimerMilliseconds), isLast: false));

        timerOffsetXBox = WpfSettingsUi.CreateNumberBox(Draft.Overlay.Columns.TimerOffsetX, -2000, 2000);
        timerOffsetYBox = WpfSettingsUi.CreateNumberBox(Draft.Overlay.Columns.TimerOffsetY, -2000, 2000);
        WpfSettingsUi.AddSettingRow(body, Localize("Offset X"), timerOffsetXBox);
        WpfSettingsUi.AddSettingRow(body, Localize("Offset Y"), timerOffsetYBox, isLast: true);

        parent.Children.Add(card);
    }

    private Grid CreateTimerColumnGrid()
    {
        return WpfSettingsUi.CreateColumnGrid(
            new GridLength(1, GridUnitType.Star),
            new GridLength(ShowColWidth),
            new GridLength(FontColWidth),
            new GridLength(NumColWidth),
            new GridLength(FlagColWidth),
            new GridLength(FlagColWidth),
            new GridLength(NumColWidth),
            new GridLength(NumColWidth),
            new GridLength(NumColWidth));
    }

    private Border CreateTimerHeader()
    {
        Grid grid = CreateTimerColumnGrid();
        string[] labels =
        [
            Localize("Section"), Localize("Show"), Localize("Font family"), Localize("Size"),
            Localize("Bold"), Localize("Italic"), Localize("Opacity %"), Localize("Shadow %"), Localize("Outline %")
        ];
        TextAlignment[] alignments =
        [
            TextAlignment.Left, TextAlignment.Center, TextAlignment.Center, TextAlignment.Center,
            TextAlignment.Center, TextAlignment.Center, TextAlignment.Center, TextAlignment.Center, TextAlignment.Center
        ];

        for (int i = 0; i < labels.Length; i++)
        {
            TextBlock header = WpfSettingsUi.CreateTableHeaderText(labels[i], alignments[i]);
            header.Margin = i == 0 ? new Thickness(0) : new Thickness(3, 0, 3, 0);
            Grid.SetColumn(header, i);
            grid.Children.Add(header);
        }

        return WpfSettingsUi.CreateTableHeaderContainer(grid);
    }

    private Border CreateTimerRow(UiColumnDescriptor descriptor, UiColumnSettings value, bool isLast)
    {
        Grid grid = CreateTimerColumnGrid();
        var cellPad = new Thickness(3, 0, 3, 0);

        TextBlock label = WpfSettingsUi.CreateRowLabel(Localize(descriptor.Label));
        AddCell(grid, label, 0, 0);

        WpfCheckBox showBox = WpfSettingsUi.CreateCheckBox(value.Show, centered: true);
        AddCell(grid, showBox, 0, 1);

        var fontFamilyBox = new WpfFontFamilySelector { Width = FontColWidth - 6, Margin = cellPad };
        fontFamilyBox.SetSelectedFontFamily(value.FontFamily);
        AddCell(grid, fontFamilyBox, 0, 2);

        WpfTextBox fontSizeBox = WpfSettingsUi.CreateDecimalBox(value.FontSize, 6, 96, width: NumColWidth - 8);
        fontSizeBox.HorizontalAlignment = WpfHorizontalAlignment.Center;
        AddCell(grid, fontSizeBox, 0, 3);

        WpfCheckBox boldBox = WpfSettingsUi.CreateCheckBox(value.Bold, centered: true);
        AddCell(grid, boldBox, 0, 4);

        WpfCheckBox italicBox = WpfSettingsUi.CreateCheckBox(value.Italic, centered: true);
        AddCell(grid, italicBox, 0, 5);

        TextEffectBoxes effects = GetTextEffectBoxes(descriptor.TextEffect);
        UiTextEffectSettings textEffects = Draft.Overlay.TextEffects;
        ConfigureEffectBox(effects.Opacity, descriptor.TextEffect.GetOpacity(textEffects), cellPad);
        AddCell(grid, effects.Opacity, 0, 6);

        if (effects.Shadow is not null)
        {
            ConfigureEffectBox(effects.Shadow, descriptor.TextEffect.GetShadow?.Invoke(textEffects) ?? 0, cellPad);
            AddCell(grid, effects.Shadow, 0, 7);
        }

        if (effects.Outline is not null)
        {
            ConfigureEffectBox(effects.Outline, descriptor.TextEffect.GetOutline?.Invoke(textEffects) ?? 0, cellPad);
            AddCell(grid, effects.Outline, 0, 8);
        }

        fontControls[descriptor.Key] = new FontControls(showBox, fontFamilyBox, fontSizeBox, boldBox, italicBox);
        return WpfSettingsUi.CreateTableRowContainer(grid, showBottomBorder: !isLast);
    }

    private void AddGroupSection(StackPanel parent)
    {
        var card = WpfSettingsUi.CreateCard(Localize("Group"), out StackPanel body);

        int limit = Math.Clamp(Draft.Route.VisibleGroupCountLimit, 1, 100);
        visibleGroupCountLimitEnabledBox = WpfSettingsUi.CreateToggleSwitch(Draft.Route.EnableVisibleGroupCountLimit);
        visibleGroupCountLimitEnabledBox.Checked += (_, _) => UpdateVisibleGroupCountLimitAvailability();
        visibleGroupCountLimitEnabledBox.Unchecked += (_, _) => UpdateVisibleGroupCountLimitAvailability();
        visibleGroupCountLimitBox = WpfSettingsUi.CreateNumberBox(limit, 1, 100);
        currentGroupPositionBox = WpfSettingsUi.CreateNumberBox(Draft.Route.CurrentGroupPosition, 1, limit);
        showFinalGroupBox = WpfSettingsUi.CreateToggleSwitch(Draft.Route.ShowFinalGroup);
        showAllVisibleGroupsAfterFinalGroupBox = WpfSettingsUi.CreateToggleSwitch(Draft.Route.ShowAllVisibleGroupsAfterFinalGroup);

        body.Children.Add(WpfSettingsUi.CreateSubHeader(Localize("Group count limit"), new Thickness(0, 6, 0, 2)));
        WpfSettingsUi.AddSettingRow(body, Localize("Enabled"), visibleGroupCountLimitEnabledBox);
        WpfSettingsUi.AddSettingRow(body, Localize("Visible group count"), visibleGroupCountLimitBox);
        WpfSettingsUi.AddSettingRow(body, Localize("Current group position"), currentGroupPositionBox);
        WpfSettingsUi.AddSettingRow(body, Localize("Always show final group"), showFinalGroupBox);
        WpfSettingsUi.AddSettingRow(body, Localize("Remove limit after final group completion"), showAllVisibleGroupsAfterFinalGroupBox);

        expandSplitDetailsBox = WpfSettingsUi.CreateToggleSwitch(Draft.Route.ExpandSplitDetails);
        expandSplitDetailsBox.Checked += (_, _) => UpdateMainGroupVisibilityOptionAvailability();
        expandSplitDetailsBox.Unchecked += (_, _) => UpdateMainGroupVisibilityOptionAvailability();
        collapseSplitDetailsOnCompletionBox = WpfSettingsUi.CreateToggleSwitch(Draft.Route.CollapseSplitDetailsOnCompletion);
        collapseSplitDetailsOnCompletionBox.Checked += (_, _) => UpdateMainGroupVisibilityOptionAvailability();
        collapseSplitDetailsOnCompletionBox.Unchecked += (_, _) => UpdateMainGroupVisibilityOptionAvailability();
        showAllMultiConditionMainGroupsAfterFinalGroupBox = WpfSettingsUi.CreateToggleSwitch(Draft.Route.ShowAllMultiConditionMainGroupsAfterFinalGroup);

        body.Children.Add(WpfSettingsUi.CreateSubHeader(Localize("Main groups"), new Thickness(0, 10, 0, 2)));
        WpfSettingsUi.AddSettingRow(body, Localize("Auto expand multi-condition main groups"), expandSplitDetailsBox);
        WpfSettingsUi.AddSettingRow(body, Localize("Collapse after completion"), collapseSplitDetailsOnCompletionBox);
        WpfSettingsUi.AddSettingRow(body, Localize("Uncollapse multi-condition main groups after final group completion"), showAllMultiConditionMainGroupsAfterFinalGroupBox);

        autoHideAttachedGroupsBox = WpfSettingsUi.CreateToggleSwitch(Draft.Route.AutoHideAttachedGroups);
        autoHideAttachedGroupsBox.Checked += (_, _) => UpdateAttachedGroupVisibilityOptionAvailability();
        autoHideAttachedGroupsBox.Unchecked += (_, _) => UpdateAttachedGroupVisibilityOptionAvailability();
        showAllAttachedGroupsAfterFinalGroupBox = WpfSettingsUi.CreateToggleSwitch(Draft.Route.ShowAllAttachedGroupsAfterFinalGroup);

        body.Children.Add(WpfSettingsUi.CreateSubHeader(Localize("Attached groups"), new Thickness(0, 10, 0, 2)));
        WpfSettingsUi.AddSettingRow(body, Localize("Auto hide attached groups"), autoHideAttachedGroupsBox);
        WpfSettingsUi.AddSettingRow(body, Localize("Uncollapse attached groups after final group completion"), showAllAttachedGroupsAfterFinalGroupBox, isLast: true);

        UpdateVisibleGroupCountLimitAvailability();
        UpdateMainGroupVisibilityOptionAvailability();
        UpdateAttachedGroupVisibilityOptionAvailability();
        parent.Children.Add(card);
    }

    private void AddDeltaTimeSection(StackPanel parent)
    {
        var card = WpfSettingsUi.CreateCard(Localize("Delta time"), out StackPanel body);

        showEarlyDeltaTimeBox = WpfSettingsUi.CreateToggleSwitch(Draft.Overlay.ShowEarlyDeltaTime);
        showEarlyDeltaTimeBox.Checked += (_, _) => UpdateEarlyDeltaAvailability();
        showEarlyDeltaTimeBox.Unchecked += (_, _) => UpdateEarlyDeltaAvailability();
        earlyDeltaTimeSecondsBox = WpfSettingsUi.CreateNumberBox(Draft.Overlay.EarlyDeltaTimeSeconds, 0, 3600);
        enableDynamicDeltaTimeUnitsBox = WpfSettingsUi.CreateToggleSwitch(Draft.Overlay.EnableDynamicDeltaTimeUnits);

        WpfSettingsUi.AddSettingRow(body, Localize("Early delta time"), showEarlyDeltaTimeBox);
        WpfSettingsUi.AddSettingRow(body, Localize("Show when within seconds"), earlyDeltaTimeSecondsBox);
        WpfSettingsUi.AddSettingRow(body, Localize("Dynamic delta time units"), enableDynamicDeltaTimeUnitsBox, isLast: true);

        UpdateEarlyDeltaAvailability();
        parent.Children.Add(card);
    }

    private WpfComboBox CreateColumnAlignmentBox(string selectedAlignment)
    {
        WpfComboBox box = WpfSettingsUi.CreateComboBox();
        string normalized = UiColumnAlignment.Normalize(selectedAlignment, UiColumnAlignment.Left);
        foreach (string alignment in UiColumnAlignment.All)
        {
            box.Items.Add(new ColumnAlignmentOption(alignment, Localize(UiColumnAlignment.GetDisplayName(alignment))));
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

    private static string GetSelectedColumnAlignment(WpfComboBox box)
    {
        return box.SelectedItem is ColumnAlignmentOption option
            ? option.Value
            : UiColumnAlignment.Left;
    }

    private static void ConfigureEffectBox(WpfTextBox box, int value, Thickness margin)
    {
        box.Width = NumColWidth - 8;
        box.Margin = margin;
        box.Padding = new Thickness(5, 0, 5, 0);
        box.TextAlignment = TextAlignment.Center;
        box.HorizontalContentAlignment = WpfHorizontalAlignment.Center;
        box.HorizontalAlignment = WpfHorizontalAlignment.Center;
        box.Text = Math.Clamp(value, 0, 100).ToString(CultureInfo.InvariantCulture);
    }

    private static void AddCell(Grid grid, FrameworkElement element, int row, int column)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    private void UpdateColumnSettingsAvailability()
    {
        foreach (UiColumnDescriptor descriptor in UiColumnDescriptors.SplitDisplay)
        {
            ColumnControls controls = columnControls[descriptor.Key];
            bool visible = controls.Show.IsChecked == true;
            if (controls.FontFamily is not null)
            {
                controls.FontFamily.IsEnabled = visible;
            }

            controls.FontSize.IsEnabled = visible;
            if (controls.Bold is not null)
            {
                controls.Bold.IsEnabled = visible;
            }

            if (controls.Italic is not null)
            {
                controls.Italic.IsEnabled = visible;
            }

            TextEffectBoxes effects = GetTextEffectBoxes(descriptor.TextEffect);
            effects.Opacity.IsEnabled = visible;
            if (effects.Shadow is not null)
            {
                effects.Shadow.IsEnabled = visible;
            }

            if (effects.Outline is not null)
            {
                effects.Outline.IsEnabled = visible;
            }
        }

        foreach ((UiColumnDescriptor primary, UiColumnDescriptor attached) in UiColumnDescriptors.SharedWidthPairs)
        {
            ColumnControls primaryControls = columnControls[primary.Key];
            ColumnControls attachedControls = columnControls[attached.Key];
            bool groupVisible = primaryControls.Show.IsChecked == true || attachedControls.Show.IsChecked == true;
            primaryControls.Width.IsEnabled = groupVisible;
            primaryControls.Alignment.IsEnabled = groupVisible;
        }

        iconNameGapBox.IsEnabled = IsColumnGroupVisible(UiColumnDescriptors.Icon, UiColumnDescriptors.AttachedIcon);
        nameTimeGapBox.IsEnabled = IsColumnGroupVisible(UiColumnDescriptors.Name, UiColumnDescriptors.AttachedName);
        timeDeltaGapBox.IsEnabled = IsColumnGroupVisible(UiColumnDescriptors.Time, UiColumnDescriptors.AttachedTime);
    }

    private bool IsColumnGroupVisible(UiColumnDescriptor primary, UiColumnDescriptor attached)
    {
        return columnControls[primary.Key].Show.IsChecked == true || columnControls[attached.Key].Show.IsChecked == true;
    }

    private void UpdateMainGroupVisibilityOptionAvailability()
    {
        bool expand = expandSplitDetailsBox.IsChecked == true;
        collapseSplitDetailsOnCompletionBox.IsEnabled = expand;
        showAllMultiConditionMainGroupsAfterFinalGroupBox.IsEnabled =
            expand && collapseSplitDetailsOnCompletionBox.IsChecked == true;
    }

    private void UpdateAttachedGroupVisibilityOptionAvailability()
    {
        showAllAttachedGroupsAfterFinalGroupBox.IsEnabled = autoHideAttachedGroupsBox.IsChecked == true;
    }

    private void UpdateVisibleGroupCountLimitAvailability()
    {
        bool enabled = visibleGroupCountLimitEnabledBox.IsChecked == true;
        visibleGroupCountLimitBox.IsEnabled = enabled;
        currentGroupPositionBox.IsEnabled = enabled;
        showFinalGroupBox.IsEnabled = enabled;
        showAllVisibleGroupsAfterFinalGroupBox.IsEnabled = enabled;
    }

    private void UpdateEarlyDeltaAvailability()
    {
        earlyDeltaTimeSecondsBox.IsEnabled = showEarlyDeltaTimeBox.IsChecked == true;
    }

    private bool ApplyGroupSettings(AppSettings settings)
    {
        bool enableLimit = visibleGroupCountLimitEnabledBox.IsChecked == true;
        int limit = WpfSettingsValueParser.ParseIntBox(visibleGroupCountLimitBox, settings.Route.VisibleGroupCountLimit, 1, 100);
        int position = WpfSettingsValueParser.ParseIntBox(currentGroupPositionBox, settings.Route.CurrentGroupPosition, 1, limit);
        bool showFinal = showFinalGroupBox.IsChecked == true;
        bool showAllVisible = showAllVisibleGroupsAfterFinalGroupBox.IsChecked == true;
        bool showAllAttached = showAllAttachedGroupsAfterFinalGroupBox.IsChecked == true;
        bool expand = expandSplitDetailsBox.IsChecked == true;
        bool collapse = collapseSplitDetailsOnCompletionBox.IsChecked == true;
        bool showAllMulti = showAllMultiConditionMainGroupsAfterFinalGroupBox.IsChecked == true;
        bool autoHideAttached = autoHideAttachedGroupsBox.IsChecked == true;

        bool changed =
            settings.Route.EnableVisibleGroupCountLimit != enableLimit ||
            settings.Route.VisibleGroupCountLimit != limit ||
            settings.Route.CurrentGroupPosition != position ||
            settings.Route.ShowFinalGroup != showFinal ||
            settings.Route.ShowAllVisibleGroupsAfterFinalGroup != showAllVisible ||
            settings.Route.ShowAllAttachedGroupsAfterFinalGroup != showAllAttached ||
            settings.Route.ExpandSplitDetails != expand ||
            settings.Route.CollapseSplitDetailsOnCompletion != collapse ||
            settings.Route.ShowAllMultiConditionMainGroupsAfterFinalGroup != showAllMulti ||
            settings.Route.AutoHideAttachedGroups != autoHideAttached;

        settings.Route.EnableVisibleGroupCountLimit = enableLimit;
        settings.Route.VisibleGroupCountLimit = limit;
        settings.Route.CurrentGroupPosition = position;
        settings.Route.ShowFinalGroup = showFinal;
        settings.Route.ShowAllVisibleGroupsAfterFinalGroup = showAllVisible;
        settings.Route.ShowAllAttachedGroupsAfterFinalGroup = showAllAttached;
        settings.Route.ExpandSplitDetails = expand;
        settings.Route.CollapseSplitDetailsOnCompletion = collapse;
        settings.Route.ShowAllMultiConditionMainGroupsAfterFinalGroup = showAllMulti;
        settings.Route.AutoHideAttachedGroups = autoHideAttached;
        return changed;
    }

    private void ApplyDeltaTimeSettings(AppSettings settings)
    {
        settings.Overlay.ShowEarlyDeltaTime = showEarlyDeltaTimeBox.IsChecked == true;
        settings.Overlay.EarlyDeltaTimeSeconds = WpfSettingsValueParser.ParseIntBox(
            earlyDeltaTimeSecondsBox,
            settings.Overlay.EarlyDeltaTimeSeconds,
            0,
            3600);
        settings.Overlay.EnableDynamicDeltaTimeUnits = enableDynamicDeltaTimeUnitsBox.IsChecked == true;
    }

    private void ApplyTextEffectSettings(UiTextEffectSettings textEffects)
    {
        foreach (UiTextEffectDescriptor descriptor in UiTextEffectDescriptors.All)
        {
            TextEffectBoxes boxes = GetTextEffectBoxes(descriptor);
            descriptor.SetOpacity(
                textEffects,
                WpfSettingsValueParser.ParseIntBox(boxes.Opacity, descriptor.GetOpacity(textEffects), 0, 100));

            if (descriptor.GetShadow is not null &&
                descriptor.SetShadow is not null &&
                boxes.Shadow is not null)
            {
                descriptor.SetShadow(
                    textEffects,
                    WpfSettingsValueParser.ParseIntBox(boxes.Shadow, descriptor.GetShadow(textEffects), 0, 100));
            }

            if (descriptor.GetOutline is not null &&
                descriptor.SetOutline is not null &&
                boxes.Outline is not null)
            {
                descriptor.SetOutline(
                    textEffects,
                    WpfSettingsValueParser.ParseIntBox(boxes.Outline, descriptor.GetOutline(textEffects), 0, 100));
            }
        }
    }

    private void ApplyColumnSettings(UiColumnDescriptor descriptor, UiColumnSettings target)
    {
        if (!columnControls.TryGetValue(descriptor.Key, out ColumnControls? controls))
        {
            return;
        }

        target.Show = controls.Show.IsChecked == true;
        target.Width = WpfSettingsValueParser.ParseIntBox(controls.Width, target.Width, 1, 1000);
        if (controls.FontFamily is not null)
        {
            target.FontFamily = GetSelectedFontFamily(controls.FontFamily, target.FontFamily);
        }

        target.FontSize = WpfSettingsValueParser.ParseFloatBox(controls.FontSize, target.FontSize, 6f, 96f);
        if (controls.Bold is not null)
        {
            target.Bold = controls.Bold.IsChecked == true;
        }

        if (controls.Italic is not null)
        {
            target.Italic = controls.Italic.IsChecked == true;
        }
    }

    private void ApplyFontSettings(UiColumnDescriptor descriptor, UiColumnSettings target)
    {
        if (!fontControls.TryGetValue(descriptor.Key, out FontControls? controls))
        {
            return;
        }

        target.Show = controls.Show.IsChecked == true;
        target.FontFamily = GetSelectedFontFamily(controls.FontFamily, target.FontFamily);
        target.FontSize = WpfSettingsValueParser.ParseFloatBox(controls.FontSize, target.FontSize, 6f, 96f);
        target.Bold = controls.Bold.IsChecked == true;
        target.Italic = controls.Italic.IsChecked == true;
    }

    private static string GetSelectedFontFamily(WpfFontFamilySelector selector, string fallback)
    {
        string selected = selector.SelectedFontFamily;
        return UiFontFactory.Default.NormalizeFamilyName(string.IsNullOrWhiteSpace(selected) ? fallback : selected);
    }

    private static UiColumnSettings EnsureColumn(UiColumnLayoutSettings columns, UiColumnDescriptor descriptor)
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

    private ColumnControls GetColumnControls(string key)
    {
        return columnControls.TryGetValue(key, out ColumnControls? controls)
            ? controls
            : throw new InvalidOperationException($"Column controls not found for {key}.");
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
        WpfCheckBox Show,
        WpfTextBox Width,
        WpfComboBox Alignment,
        WpfFontFamilySelector? FontFamily,
        WpfTextBox FontSize,
        WpfCheckBox? Bold,
        WpfCheckBox? Italic);

    private sealed record FontControls(
        WpfCheckBox Show,
        WpfFontFamilySelector FontFamily,
        WpfTextBox FontSize,
        WpfCheckBox Bold,
        WpfCheckBox Italic);

    private sealed record ColumnAlignmentOption(string Value, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private readonly record struct TextEffectBoxes(WpfTextBox Opacity, WpfTextBox? Shadow, WpfTextBox? Outline);
}
