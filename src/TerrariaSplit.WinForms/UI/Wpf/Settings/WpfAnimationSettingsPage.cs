using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfColor = System.Windows.Media.Color;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfImage = System.Windows.Controls.Image;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfPoint = System.Windows.Point;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace TerrariaSplit.UI.Wpf.Settings;

internal sealed class WpfAnimationSettingsPage : WpfSettingsPageBase
{
    private const float TextEffectPreviewFontSize = 16f;
    private const double DefaultPreviewWidth = 680;
    private const double TextPreviewHeight = 58;
    internal static readonly System.Drawing.Color PreviewBackgroundColor = System.Drawing.Color.FromArgb(28, 28, 28);

    private WpfCheckBox enableDefeatedBossIconLightingBox = null!;
    private WpfTextBox undefeatedIconGrayscaleBox = null!;
    private WpfTextBox undefeatedIconBrightnessBox = null!;
    private WpfTextBox currentBossIconGrayscaleWeakenBox = null!;
    private WpfTextBox currentBossIconBrightnessBoostBox = null!;

    private WpfCheckBox showCurrentSplitHighlightBox = null!;
    private WpfTextBox currentSplitHighlightScaleBox = null!;
    private WpfTextBox currentSplitDepthStrengthBox = null!;

    private WpfCheckBox enableDeltaGradientColorBox = null!;
    private WpfCheckBox enableCurrentDeltaGradientColorBox = null!;
    private WpfCheckBox enableTimerGradientColorBox = null!;
    private WpfTextBox deltaGradientThresholdBox = null!;
    private WpfComboBox deltaGradientCurveBox = null!;

    private WpfCheckBox showSplitCompletionAnimationBox = null!;
    private WpfTextBox splitCompletionAnimationDurationBox = null!;
    private WpfCheckBox showSegmentBestDeltaHighlightBox = null!;

    private readonly Dictionary<string, CompletionTextControls> completionTextControls = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AnimationOutlineControls> animationOutlineControls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SegmentBestDeltaHighlightControls> segmentBestDeltaHighlightControls = new(StringComparer.OrdinalIgnoreCase);

    private readonly WpfImage outlineStylePreviewImage = new() { Stretch = Stretch.Fill };
    private readonly WpfImage segmentBestDeltaHighlightPreviewImage = new() { Stretch = Stretch.Fill };
    private Border deltaGradientPreviewHost = null!;
    private Border deltaGradientBar = null!;
    private Border timerGradientBar = null!;
    private Border outlineStylePreviewHost = null!;
    private Border segmentBestDeltaHighlightPreviewHost = null!;
    private StackPanel completionTextPanel = null!;
    private StackPanel animationComparisonPanel = null!;
    private StackPanel animationOutlinePanel = null!;
    private StackPanel segmentBestDeltaHighlightPanel = null!;

    private DispatcherTimer? previewTimer;
    private string? animationGridSignature;
    private string previewOutlineStyle = SplitCompletionOutlineStyles.Rainbow;
    private string previewSegmentBestDeltaHighlightStyle = SegmentBestDeltaHighlightStyles.Aurora;

    public override SettingsPageId Id => SettingsPageId.Effects;

    internal WpfCheckBox EnableCurrentDeltaGradientColorBox => enableCurrentDeltaGradientColorBox;

    internal IReadOnlyDictionary<string, CompletionTextControls> CompletionTextOptions => completionTextControls;

    internal IReadOnlyCollection<string> AnimationOutlineKeysForTests => animationOutlineControls.Keys.ToList();

    internal IReadOnlyCollection<string> SegmentBestDeltaHighlightKeysForTests => segmentBestDeltaHighlightControls.Keys.ToList();

    protected override FrameworkElement BuildPage(WpfSettingsPageContext context)
    {
        return WpfSettingsUi.CreateScrollPage(BuildSections);
    }

    public override void OnSelected()
    {
        RenderAllPreviews();
        EnsurePreviewTimer();
        previewTimer?.Start();
    }

    public override void OnDeselected()
    {
        previewTimer?.Stop();
    }

    public override void OnDisposing()
    {
        if (previewTimer is not null)
        {
            previewTimer.Stop();
            previewTimer = null;
        }
    }

    public override void Apply(AppSettings settings)
    {
        settings.Overlay.EnableDefeatedBossIconLighting = enableDefeatedBossIconLightingBox.IsChecked == true;
        settings.Overlay.UndefeatedIconGrayscalePercent = WpfSettingsValueParser.ParseIntBox(undefeatedIconGrayscaleBox, 80, 0, 100);
        settings.Overlay.UndefeatedIconBrightnessPercent = WpfSettingsValueParser.ParseIntBox(undefeatedIconBrightnessBox, 40, 0, 100);
        settings.Overlay.CurrentBossIconGrayscaleWeakenPercent = WpfSettingsValueParser.ParseIntBox(currentBossIconGrayscaleWeakenBox, 40, 0, 100);
        settings.Overlay.CurrentBossIconBrightnessBoostPercent = WpfSettingsValueParser.ParseIntBox(currentBossIconBrightnessBoostBox, 35, 0, 100);
        settings.Overlay.ShowSplitCompletionAnimation = showSplitCompletionAnimationBox.IsChecked == true;
        settings.Overlay.ShowCurrentSplitHighlight = showCurrentSplitHighlightBox.IsChecked == true;
        settings.Overlay.CurrentSplitHighlightScalePercent = WpfSettingsValueParser.ParseIntBox(currentSplitHighlightScaleBox, 112, 100, 140);
        settings.Overlay.CurrentSplitDepthStrengthPercent = WpfSettingsValueParser.ParseIntBox(currentSplitDepthStrengthBox, 45, 0, 100);
        settings.Overlay.EnableDeltaGradientColor = enableDeltaGradientColorBox.IsChecked == true;
        settings.Overlay.EnableCurrentDeltaGradientColor = enableCurrentDeltaGradientColorBox.IsChecked == true;
        settings.Overlay.EnableTimerGradientColor = enableTimerGradientColorBox.IsChecked == true;
        settings.Overlay.DeltaGradientThresholdSeconds = WpfSettingsValueParser.ParseTimeBox(deltaGradientThresholdBox, 120, 1, 3600);
        settings.Overlay.DeltaGradientCurve = GetSelectedDeltaGradientCurve(deltaGradientCurveBox);
        settings.Overlay.ShowSegmentBestDeltaHighlight = showSegmentBestDeltaHighlightBox.IsChecked == true;
        settings.Overlay.SplitCompletionAnimationDurationSeconds = WpfSettingsValueParser.ParseFloatBox(splitCompletionAnimationDurationBox, 4.2f, 2f, 20f);
        ApplyCompletionText(settings.Overlay.SplitCompletionText);
        SaveAnimationOutlineControls();
    }

    public override void OnModelChanged(SettingsModelChange change)
    {
        if (change == SettingsModelChange.TextAppearanceChanged)
        {
            RenderAnimatedPreviews();
            return;
        }

        if (change != SettingsModelChange.RouteChanged)
        {
            return;
        }

        SaveAnimationOutlineControls();
        PopulateAnimationOutlineGrid();
        PopulateSegmentBestDeltaHighlightGrid();
    }

    private void BuildSections(StackPanel parent)
    {
        // Icon lighting
        enableDefeatedBossIconLightingBox = WpfSettingsUi.CreateToggleSwitch(Draft.Overlay.EnableDefeatedBossIconLighting);
        enableDefeatedBossIconLightingBox.Checked += (_, _) => UpdateEffectAvailability();
        enableDefeatedBossIconLightingBox.Unchecked += (_, _) => UpdateEffectAvailability();
        undefeatedIconGrayscaleBox = WpfSettingsUi.CreateNumberBox(Draft.Overlay.UndefeatedIconGrayscalePercent, 0, 100);
        undefeatedIconBrightnessBox = WpfSettingsUi.CreateNumberBox(Draft.Overlay.UndefeatedIconBrightnessPercent, 0, 100);
        currentBossIconGrayscaleWeakenBox = WpfSettingsUi.CreateNumberBox(Draft.Overlay.CurrentBossIconGrayscaleWeakenPercent, 0, 100);
        currentBossIconBrightnessBoostBox = WpfSettingsUi.CreateNumberBox(Draft.Overlay.CurrentBossIconBrightnessBoostPercent, 0, 100);

        var iconCard = WpfSettingsUi.CreateCard(Localize("Light icons when current stage completed"), out StackPanel iconBody);
        WpfSettingsUi.AddSettingRow(iconBody, Localize("Enabled"), enableDefeatedBossIconLightingBox);
        WpfSettingsUi.AddSettingRow(iconBody, Localize("Unlit grayscale %"), undefeatedIconGrayscaleBox);
        WpfSettingsUi.AddSettingRow(iconBody, Localize("Unlit brightness %"), undefeatedIconBrightnessBox);
        WpfSettingsUi.AddSettingRow(iconBody, Localize("Current stage icon grayscale weaken %"), currentBossIconGrayscaleWeakenBox);
        WpfSettingsUi.AddSettingRow(iconBody, Localize("Current stage icon brightness boost %"), currentBossIconBrightnessBoostBox, isLast: true);
        parent.Children.Add(iconCard);

        // Current split highlight
        showCurrentSplitHighlightBox = WpfSettingsUi.CreateToggleSwitch(Draft.Overlay.ShowCurrentSplitHighlight);
        showCurrentSplitHighlightBox.Checked += (_, _) => UpdateEffectAvailability();
        showCurrentSplitHighlightBox.Unchecked += (_, _) => UpdateEffectAvailability();
        currentSplitHighlightScaleBox = WpfSettingsUi.CreateNumberBox(Draft.Overlay.CurrentSplitHighlightScalePercent, 100, 140);
        currentSplitDepthStrengthBox = WpfSettingsUi.CreateNumberBox(Draft.Overlay.CurrentSplitDepthStrengthPercent, 0, 100);

        var currentCard = WpfSettingsUi.CreateCard(Localize("Highlight current stage"), out StackPanel currentBody);
        WpfSettingsUi.AddSettingRow(currentBody, Localize("Enabled"), showCurrentSplitHighlightBox);
        WpfSettingsUi.AddSettingRow(currentBody, Localize("Scale %"), currentSplitHighlightScaleBox);
        WpfSettingsUi.AddSettingRow(currentBody, Localize("Depth strength %"), currentSplitDepthStrengthBox, isLast: true);
        parent.Children.Add(currentCard);

        // Delta gradient
        enableDeltaGradientColorBox = WpfSettingsUi.CreateToggleSwitch(Draft.Overlay.EnableDeltaGradientColor);
        enableCurrentDeltaGradientColorBox = WpfSettingsUi.CreateToggleSwitch(Draft.Overlay.EnableCurrentDeltaGradientColor);
        enableTimerGradientColorBox = WpfSettingsUi.CreateToggleSwitch(Draft.Overlay.EnableTimerGradientColor);
        deltaGradientThresholdBox = WpfSettingsUi.CreateTimeBox(
            TimeText.FormatSplit(TimeSpan.FromSeconds(Math.Clamp(Draft.Overlay.DeltaGradientThresholdSeconds, 1, 3600))),
            placeholder: "m:ss or h:mm:ss");
        deltaGradientCurveBox = CreateDeltaGradientCurveBox();

        enableDeltaGradientColorBox.Checked += (_, _) => UpdateDeltaGradientState();
        enableDeltaGradientColorBox.Unchecked += (_, _) => UpdateDeltaGradientState();
        enableCurrentDeltaGradientColorBox.Checked += (_, _) => UpdateDeltaGradientState();
        enableCurrentDeltaGradientColorBox.Unchecked += (_, _) => UpdateDeltaGradientState();
        enableTimerGradientColorBox.Checked += (_, _) => UpdateDeltaGradientState();
        enableTimerGradientColorBox.Unchecked += (_, _) => UpdateDeltaGradientState();
        deltaGradientThresholdBox.TextChanged += (_, _) => RenderDeltaGradientPreview();

        deltaGradientPreviewHost = CreateDeltaGradientPreviewHost();

        var deltaCard = WpfSettingsUi.CreateCard(Localize("Delta time gradient"), out StackPanel deltaBody);
        WpfSettingsUi.AddSettingRow(deltaBody, Localize("Enabled (Historical delta)"), enableDeltaGradientColorBox);
        WpfSettingsUi.AddSettingRow(deltaBody, Localize("Enabled (Current delta)"), enableCurrentDeltaGradientColorBox);
        WpfSettingsUi.AddSettingRow(deltaBody, Localize("Enabled (Main timer)"), enableTimerGradientColorBox);
        WpfSettingsUi.AddSettingRow(deltaBody, Localize("Threshold time"), deltaGradientThresholdBox);
        WpfSettingsUi.AddSettingRow(deltaBody, Localize("Gradient mode"), deltaGradientCurveBox, isLast: true);
        deltaBody.Children.Add(deltaGradientPreviewHost);
        parent.Children.Add(deltaCard);

        // Main stage completion animation
        showSplitCompletionAnimationBox = WpfSettingsUi.CreateToggleSwitch(Draft.Overlay.ShowSplitCompletionAnimation);
        showSplitCompletionAnimationBox.Checked += (_, _) => UpdateEffectAvailability();
        showSplitCompletionAnimationBox.Unchecked += (_, _) => UpdateEffectAvailability();
        splitCompletionAnimationDurationBox = WpfSettingsUi.CreateDecimalBox(Draft.Overlay.SplitCompletionAnimationDurationSeconds, 2m, 20m);

        var completionCard = WpfSettingsUi.CreateCard(Localize("Main stage completion animation"), out StackPanel completionBody);
        WpfSettingsUi.AddSettingRow(completionBody, Localize("Enabled"), showSplitCompletionAnimationBox);
        WpfSettingsUi.AddSettingRow(completionBody, Localize("Animation duration seconds"), splitCompletionAnimationDurationBox);

        completionTextPanel = BuildCompletionTextPanel();
        completionBody.Children.Add(completionTextPanel);

        completionBody.Children.Add(WpfSettingsUi.CreateSubHeader(Localize("Show comparison with reference time")));
        animationComparisonPanel = new StackPanel { Orientation = WpfOrientation.Vertical };
        completionBody.Children.Add(animationComparisonPanel);

        completionBody.Children.Add(WpfSettingsUi.CreateSubHeader(Localize("Outline when faster than reference")));
        animationOutlinePanel = new StackPanel { Orientation = WpfOrientation.Vertical };
        completionBody.Children.Add(animationOutlinePanel);

        outlineStylePreviewHost = CreatePreviewHost(outlineStylePreviewImage, TextPreviewHeight);
        outlineStylePreviewHost.Loaded += (_, _) => RenderOutlinePreview();
        outlineStylePreviewHost.SizeChanged += (_, _) => RenderOutlinePreview();
        completionBody.Children.Add(outlineStylePreviewHost);
        PopulateAnimationOutlineGrid();
        parent.Children.Add(completionCard);

        // Highlight best segment
        showSegmentBestDeltaHighlightBox = WpfSettingsUi.CreateToggleSwitch(Draft.Overlay.ShowSegmentBestDeltaHighlight);
        showSegmentBestDeltaHighlightBox.Checked += (_, _) => UpdateEffectAvailability();
        showSegmentBestDeltaHighlightBox.Unchecked += (_, _) => UpdateEffectAvailability();

        var segCard = WpfSettingsUi.CreateCard(Localize("Highlight best segment"), out StackPanel segBody);
        WpfSettingsUi.AddSettingRow(segBody, Localize("Enabled"), showSegmentBestDeltaHighlightBox);

        segmentBestDeltaHighlightPanel = new StackPanel { Orientation = WpfOrientation.Vertical };
        segBody.Children.Add(segmentBestDeltaHighlightPanel);

        segmentBestDeltaHighlightPreviewHost = CreatePreviewHost(segmentBestDeltaHighlightPreviewImage, TextPreviewHeight);
        segmentBestDeltaHighlightPreviewHost.Loaded += (_, _) => RenderSegmentHighlightPreview();
        segmentBestDeltaHighlightPreviewHost.SizeChanged += (_, _) => RenderSegmentHighlightPreview();
        PopulateSegmentBestDeltaHighlightGrid();
        segBody.Children.Add(segmentBestDeltaHighlightPreviewHost);
        parent.Children.Add(segCard);

        UpdateEffectAvailability();
        RenderAllPreviews();
    }

    private StackPanel BuildCompletionTextPanel()
    {
        var panel = new StackPanel { Orientation = WpfOrientation.Vertical, Margin = new Thickness(0, 6, 0, 2) };

        Grid headerGrid = CreateCompletionTextGrid();
        string[] headers =
        [
            Localize("Section"), Localize("Font family"), Localize("Bold"), Localize("Italic"),
            Localize("Opacity %"), Localize("Shadow %"), Localize("Outline %")
        ];
        TextAlignment[] aligns =
        [
            TextAlignment.Left, TextAlignment.Center, TextAlignment.Center, TextAlignment.Center,
            TextAlignment.Center, TextAlignment.Center, TextAlignment.Center
        ];
        for (int i = 0; i < headers.Length; i++)
        {
            TextBlock h = WpfSettingsUi.CreateTableHeaderText(headers[i], aligns[i]);
            h.Margin = i == 0 ? new Thickness(0) : new Thickness(3, 0, 3, 0);
            Grid.SetColumn(h, i);
            headerGrid.Children.Add(h);
        }

        panel.Children.Add(WpfSettingsUi.CreateTableHeaderContainer(headerGrid));

        (string Key, string Label, SplitCompletionTextStyle Style)[] parts = TextParts(Draft.Overlay.SplitCompletionText).ToArray();
        for (int i = 0; i < parts.Length; i++)
        {
            (string key, string labelKey, SplitCompletionTextStyle style) = parts[i];
            var font = new WpfFontFamilySelector { Width = 148, Margin = new Thickness(3, 0, 3, 0) };
            font.SetSelectedFontFamily(style.FontFamily);
            WpfCheckBox bold = WpfSettingsUi.CreateCheckBox(style.Bold, centered: true);
            WpfCheckBox italic = WpfSettingsUi.CreateCheckBox(style.Italic, centered: true);
            WpfTextBox opacity = WpfSettingsUi.CreateNumberBox(style.OpacityPercent, 0, 100, width: 60);
            WpfTextBox shadow = WpfSettingsUi.CreateNumberBox(style.ShadowPercent, 0, 100, width: 60);
            WpfTextBox outline = WpfSettingsUi.CreateNumberBox(style.OutlineThicknessPercent, 0, 100, width: 60);
            opacity.HorizontalAlignment = WpfHorizontalAlignment.Center;
            shadow.HorizontalAlignment = WpfHorizontalAlignment.Center;
            outline.HorizontalAlignment = WpfHorizontalAlignment.Center;

            completionTextControls[key] = new CompletionTextControls(font, bold, italic, opacity, shadow, outline);
            font.SelectionChanged += (_, _) => RenderOutlinePreview();
            bold.Checked += (_, _) => RenderOutlinePreview();
            bold.Unchecked += (_, _) => RenderOutlinePreview();
            italic.Checked += (_, _) => RenderOutlinePreview();
            italic.Unchecked += (_, _) => RenderOutlinePreview();
            opacity.TextChanged += (_, _) => RenderOutlinePreview();
            shadow.TextChanged += (_, _) => RenderOutlinePreview();
            outline.TextChanged += (_, _) => RenderOutlinePreview();

            Grid row = CreateCompletionTextGrid();
            TextBlock label = WpfSettingsUi.CreateRowLabel(Localize(labelKey));
            Grid.SetColumn(label, 0);
            Grid.SetColumn(font, 1);
            Grid.SetColumn(bold, 2);
            Grid.SetColumn(italic, 3);
            Grid.SetColumn(opacity, 4);
            Grid.SetColumn(shadow, 5);
            Grid.SetColumn(outline, 6);
            row.Children.Add(label);
            row.Children.Add(font);
            row.Children.Add(bold);
            row.Children.Add(italic);
            row.Children.Add(opacity);
            row.Children.Add(shadow);
            row.Children.Add(outline);

            panel.Children.Add(WpfSettingsUi.CreateTableRowContainer(row, showBottomBorder: i < parts.Length - 1));
        }

        return panel;
    }

    private static Grid CreateCompletionTextGrid()
    {
        return WpfSettingsUi.CreateColumnGrid(
            new GridLength(1, GridUnitType.Star),
            new GridLength(156),
            new GridLength(46),
            new GridLength(46),
            new GridLength(68),
            new GridLength(68),
            new GridLength(68));
    }

    private void PopulateAnimationOutlineGrid()
    {
        List<RouteGroup> groups = SplitRouteGroups.Build(Draft).ToList();
        string signature = string.Join('\u001F', groups.Select(group => $"{group.Key}\u001E{SplitRouteGroups.GetGroupDisplayName(group, Draft)}"));
        if (animationGridSignature == signature && animationOutlineControls.Count > 0)
        {
            foreach (RouteGroup group in groups)
            {
                if (!animationOutlineControls.TryGetValue(group.Key, out AnimationOutlineControls? controls))
                {
                    continue;
                }

                controls.SplitComparison.IsChecked = GetAnimationOutlineSetting(Draft.Overlay.SplitCompletionSplitComparisons, group.Key);
                controls.SegmentComparison.IsChecked = GetAnimationOutlineSetting(Draft.Overlay.SplitCompletionSegmentComparisons, group.Key);
                SetOutlineStyle(controls.SplitTime, GetAnimationOutlineStyle(Draft.Overlay.SplitCompletionOutlineSplitStyles, group.Key));
                SetOutlineStyle(controls.SegmentTime, GetAnimationOutlineStyle(Draft.Overlay.SplitCompletionOutlineSegmentStyles, group.Key));
            }

            return;
        }

        animationComparisonPanel.Children.Clear();
        animationOutlinePanel.Children.Clear();
        animationOutlineControls.Clear();

        animationComparisonPanel.Children.Add(CreateThreeColumnHeader(Localize("Group"), Localize("Cumulative time"), Localize("Segment time"), controlLeftMargin: 0));
        animationOutlinePanel.Children.Add(CreateThreeColumnHeader(Localize("Group"), Localize("Cumulative time"), Localize("Segment time"), controlLeftMargin: 6));

        for (int i = 0; i < groups.Count; i++)
        {
            RouteGroup group = groups[i];
            bool isLast = i == groups.Count - 1;
            string groupName = SplitRouteGroups.GetGroupDisplayName(group, Draft);

            WpfCheckBox splitComp = WpfSettingsUi.CreateCheckBox(
                GetAnimationOutlineSetting(Draft.Overlay.SplitCompletionSplitComparisons, group.Key),
                centered: true);
            WpfCheckBox segComp = WpfSettingsUi.CreateCheckBox(
                GetAnimationOutlineSetting(Draft.Overlay.SplitCompletionSegmentComparisons, group.Key),
                centered: true);
            WpfComboBox splitStyle = CreateOutlineStyleBox(GetAnimationOutlineStyle(Draft.Overlay.SplitCompletionOutlineSplitStyles, group.Key));
            WpfComboBox segStyle = CreateOutlineStyleBox(GetAnimationOutlineStyle(Draft.Overlay.SplitCompletionOutlineSegmentStyles, group.Key));

            animationOutlineControls[group.Key] = new AnimationOutlineControls(splitComp, segComp, splitStyle, segStyle);

            Grid compRow = WpfSettingsUi.CreateColumnGrid(new GridLength(1, GridUnitType.Star), new GridLength(140), new GridLength(140));
            TextBlock compLabel = WpfSettingsUi.CreateRowLabel(groupName);
            Grid.SetColumn(compLabel, 0);
            Grid.SetColumn(splitComp, 1);
            Grid.SetColumn(segComp, 2);
            compRow.Children.Add(compLabel);
            compRow.Children.Add(splitComp);
            compRow.Children.Add(segComp);
            animationComparisonPanel.Children.Add(WpfSettingsUi.CreateTableRowContainer(compRow, showBottomBorder: !isLast));

            Grid outlineRow = WpfSettingsUi.CreateColumnGrid(new GridLength(1, GridUnitType.Star), new GridLength(140), new GridLength(140));
            TextBlock outlineLabel = WpfSettingsUi.CreateRowLabel(groupName);
            splitStyle.Margin = new Thickness(6, 0, 0, 0);
            segStyle.Margin = new Thickness(6, 0, 0, 0);
            Grid.SetColumn(outlineLabel, 0);
            Grid.SetColumn(splitStyle, 1);
            Grid.SetColumn(segStyle, 2);
            outlineRow.Children.Add(outlineLabel);
            outlineRow.Children.Add(splitStyle);
            outlineRow.Children.Add(segStyle);
            animationOutlinePanel.Children.Add(WpfSettingsUi.CreateTableRowContainer(outlineRow, showBottomBorder: !isLast));
        }

        animationGridSignature = signature;
        UpdateSplitCompletionAvailability();
    }

    private void PopulateSegmentBestDeltaHighlightGrid()
    {
        List<RouteGroup> groups = SplitRouteGroups.Build(Draft).ToList();
        segmentBestDeltaHighlightPanel.Children.Clear();
        segmentBestDeltaHighlightControls.Clear();

        Grid headerGrid = WpfSettingsUi.CreateColumnGrid(new GridLength(1, GridUnitType.Star), new GridLength(160));
        TextBlock h0 = WpfSettingsUi.CreateTableHeaderText(Localize("Group"));
        TextBlock h1 = WpfSettingsUi.CreateTableHeaderText(Localize("Effect"), TextAlignment.Center);
        h1.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(h0, 0);
        Grid.SetColumn(h1, 1);
        headerGrid.Children.Add(h0);
        headerGrid.Children.Add(h1);
        segmentBestDeltaHighlightPanel.Children.Add(WpfSettingsUi.CreateTableHeaderContainer(headerGrid));

        for (int i = 0; i < groups.Count; i++)
        {
            RouteGroup group = groups[i];
            WpfComboBox styleBox = CreateSegmentBestDeltaHighlightStyleBox(GetSegmentBestDeltaHighlightStyle(group.Key));
            styleBox.Margin = new Thickness(6, 0, 0, 0);
            segmentBestDeltaHighlightControls[group.Key] = new SegmentBestDeltaHighlightControls(styleBox);

            Grid row = WpfSettingsUi.CreateColumnGrid(new GridLength(1, GridUnitType.Star), new GridLength(160));
            TextBlock label = WpfSettingsUi.CreateRowLabel(SplitRouteGroups.GetGroupDisplayName(group, Draft));
            Grid.SetColumn(label, 0);
            Grid.SetColumn(styleBox, 1);
            row.Children.Add(label);
            row.Children.Add(styleBox);
            segmentBestDeltaHighlightPanel.Children.Add(WpfSettingsUi.CreateTableRowContainer(row, showBottomBorder: i < groups.Count - 1));
        }

        UpdateSegmentBestDeltaHighlightAvailability();
    }

    private static Border CreateThreeColumnHeader(string c0, string c1, string c2, double controlLeftMargin)
    {
        Grid grid = WpfSettingsUi.CreateColumnGrid(new GridLength(1, GridUnitType.Star), new GridLength(140), new GridLength(140));
        TextBlock h0 = WpfSettingsUi.CreateTableHeaderText(c0);
        TextBlock h1 = WpfSettingsUi.CreateTableHeaderText(c1, TextAlignment.Center);
        TextBlock h2 = WpfSettingsUi.CreateTableHeaderText(c2, TextAlignment.Center);
        if (controlLeftMargin > 0)
        {
            h1.Margin = new Thickness(controlLeftMargin, 0, 0, 0);
            h2.Margin = new Thickness(controlLeftMargin, 0, 0, 0);
        }

        Grid.SetColumn(h0, 0);
        Grid.SetColumn(h1, 1);
        Grid.SetColumn(h2, 2);
        grid.Children.Add(h0);
        grid.Children.Add(h1);
        grid.Children.Add(h2);
        return WpfSettingsUi.CreateTableHeaderContainer(grid);
    }

    private Border CreateDeltaGradientPreviewHost()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        TextBlock deltaLabel = CreateGradientPreviewLabel(Localize("Delta"));
        TextBlock timerLabel = CreateGradientPreviewLabel(Localize("Main timer"));
        deltaGradientBar = CreateGradientBar();
        timerGradientBar = CreateGradientBar();

        Grid.SetRow(deltaLabel, 0);
        Grid.SetColumn(deltaLabel, 0);
        Grid.SetRow(deltaGradientBar, 0);
        Grid.SetColumn(deltaGradientBar, 1);

        Grid.SetRow(timerLabel, 2);
        Grid.SetColumn(timerLabel, 0);
        Grid.SetRow(timerGradientBar, 2);
        Grid.SetColumn(timerGradientBar, 1);

        grid.Children.Add(deltaLabel);
        grid.Children.Add(deltaGradientBar);
        grid.Children.Add(timerLabel);
        grid.Children.Add(timerGradientBar);

        var border = new Border
        {
            HorizontalAlignment = WpfHorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(5),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 8, 0, 4),
            Child = grid
        };
        border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        border.SetResourceReference(Border.BackgroundProperty, "FieldBgBrush");
        return border;
    }

    private static TextBlock CreateGradientPreviewLabel(string text)
    {
        var label = new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        label.SetResourceReference(TextBlock.FontFamilyProperty, "PrimaryFontFamily");
        label.SetResourceReference(TextBlock.FontSizeProperty, "FontSizeSub");
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        return label;
    }

    private static Border CreateGradientBar()
    {
        var bar = new Border
        {
            Height = 13,
            CornerRadius = new CornerRadius(3.5),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            SnapsToDevicePixels = true
        };
        bar.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        return bar;
    }

    private static Border CreatePreviewHost(WpfImage image, double height)
    {
        var border = new Border
        {
            Height = height,
            HorizontalAlignment = WpfHorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(5),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 8, 0, 4),
            ClipToBounds = true,
            Child = image
        };
        border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        border.Background = WpfSettingsUi.Freeze(WpfColor.FromRgb(
            PreviewBackgroundColor.R,
            PreviewBackgroundColor.G,
            PreviewBackgroundColor.B));
        return border;
    }

    private void EnsurePreviewTimer()
    {
        if (previewTimer is not null)
        {
            return;
        }

        previewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        previewTimer.Tick += (_, _) => RenderAnimatedPreviews();
    }

    private void RenderAllPreviews()
    {
        RenderDeltaGradientPreview();
        RenderAnimatedPreviews();
    }

    private void RenderAnimatedPreviews()
    {
        RenderOutlinePreview();
        RenderSegmentHighlightPreview();
    }

    private void RenderDeltaGradientPreview()
    {
        if (deltaGradientBar is null || timerGradientBar is null)
        {
            return;
        }

        deltaGradientBar.Background = BuildDeltaGradientBrush(GetPreviewDeltaGradientPalette());
        timerGradientBar.Background = BuildDeltaGradientBrush(GetPreviewTimerGradientPalette());
    }

    private void RenderOutlinePreview()
    {
        RenderBitmapPreview(outlineStylePreviewHost, outlineStylePreviewImage, (int)DefaultPreviewWidth, (int)TextPreviewHeight, PaintOutlineStylePreview);
    }

    private void RenderSegmentHighlightPreview()
    {
        RenderBitmapPreview(segmentBestDeltaHighlightPreviewHost, segmentBestDeltaHighlightPreviewImage, (int)DefaultPreviewWidth, (int)TextPreviewHeight, PaintSegmentBestDeltaHighlightPreview);
    }

    private static void RenderBitmapPreview(
        Border? host,
        WpfImage target,
        int defaultWidth,
        int defaultHeight,
        Action<Graphics, Rectangle> paint)
    {
        int logicalWidth = Math.Clamp(host is not null && host.ActualWidth > 16 ? (int)Math.Round(host.ActualWidth - 2d) : defaultWidth, 64, 1600);
        int logicalHeight = Math.Clamp(host is not null && host.ActualHeight > 16 ? (int)Math.Round(host.ActualHeight - 2d) : defaultHeight, 24, 300);

        double scaleX = 1d;
        double scaleY = 1d;
        if (host is not null)
        {
            DpiScale dpi = VisualTreeHelper.GetDpi(host);
            if (dpi.DpiScaleX > 0)
            {
                scaleX = dpi.DpiScaleX;
            }

            if (dpi.DpiScaleY > 0)
            {
                scaleY = dpi.DpiScaleY;
            }
        }

        int pixelWidth = Math.Max(1, (int)Math.Round(logicalWidth * scaleX));
        int pixelHeight = Math.Max(1, (int)Math.Round(logicalHeight * scaleY));
        using var bitmap = new Bitmap(pixelWidth, pixelHeight, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        bitmap.SetResolution(96f, 96f);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.ScaleTransform((float)scaleX, (float)scaleY);
        paint(graphics, new Rectangle(0, 0, logicalWidth, logicalHeight));
        target.Source = WpfBitmapBridge.FromGdiBitmap(bitmap, 96d * scaleX, 96d * scaleY);
    }

    internal void PaintOutlineStylePreview(Graphics graphics, Rectangle bounds)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var bg = new SolidBrush(PreviewBackgroundColor);
        graphics.FillRectangle(bg, bounds);

        SplitCompletionTextStyle appearance = Draft.Overlay.SplitCompletionText.Time with { };
        ReadCompletionTextControls(nameof(SplitCompletionTextSettings.Time), appearance);
        using var font = UiFontFactory.Default.CreateFont(
            appearance.FontFamily,
            TextEffectPreviewFontSize,
            (appearance.Bold ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular) |
            (appearance.Italic ? System.Drawing.FontStyle.Italic : System.Drawing.FontStyle.Regular));

        const string text = "0:01:23.45";
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Near
        };
        UiPalette palette = UiPalette.From(Draft.Overlay.Colors);
        var style = new TextRenderStyle(
            palette.SplitCompletionTimeText,
            palette.SplitCompletionTimeTextOutline,
            palette.SplitCompletionTimeTextShadow,
            appearance.ShadowPercent,
            appearance.OutlineThicknessPercent,
            LinearEffects: true);
        SizeF textSize = graphics.MeasureString(text, font, System.Drawing.Size.Empty, format);
        TextEffectRenderer.DrawOutlinedString(
            graphics,
            text,
            font,
            style,
            bounds.Left + (bounds.Width - textSize.Width) / 2f,
            bounds.Top + (bounds.Height - textSize.Height) / 2f,
            format,
            TimeSpan.FromMilliseconds(Environment.TickCount64),
            previewOutlineStyle,
            appearance.OpacityPercent / 100f);
    }

    internal void PaintSegmentBestDeltaHighlightPreview(Graphics graphics, Rectangle bounds)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var bg = new SolidBrush(PreviewBackgroundColor);
        graphics.FillRectangle(bg, bounds);

        UiColumnSettings appearance = Draft.Overlay.Columns.Delta;
        using var font = UiFontFactory.Default.CreateFont(
            appearance.FontFamily,
            TextEffectPreviewFontSize,
            (appearance.Bold ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular) |
            (appearance.Italic ? System.Drawing.FontStyle.Italic : System.Drawing.FontStyle.Regular));
        UiPalette palette = UiPalette.From(Draft.Overlay.Colors);
        float opacity = OverlayTextStyles.GetDeltaTextOpacity(Draft);

        double seconds = Environment.TickCount64 / 1000.0;
        string[] texts = ["-0:01.23", "+0:01.23"];
        for (int i = 0; i < texts.Length; i++)
        {
            var rect = new Rectangle(bounds.Left + i * bounds.Width / texts.Length, bounds.Top, bounds.Width / texts.Length, bounds.Height);
            var comparison = new SplitComparison(TimeSpan.FromSeconds(i == 0 ? -1.23 : 1.23), ShowDelta: true);
            TextRenderStyle style = OverlayTextStyles.GetDeltaTextStyle(Draft, comparison, palette);
            style = style with { Fill = SegmentBestDeltaHighlightColorMath.Apply(style.Fill, previewSegmentBestDeltaHighlightStyle, seconds) };
            TextEffectRenderer.DrawStyledText(graphics, texts[i], font, style, rect, ContentAlignment.MiddleCenter, opacity, supersampleEffects: false);
        }
    }

    private System.Windows.Media.LinearGradientBrush BuildDeltaGradientBrush(
        (System.Drawing.Color Ahead, System.Drawing.Color Base, System.Drawing.Color Behind, bool Enabled) palette)
    {
        string curve = GetSelectedDeltaGradientCurve(deltaGradientCurveBox);
        const int steps = 32;
        var stops = new GradientStopCollection(steps + 1);
        for (int i = 0; i <= steps; i++)
        {
            double offset = i / (double)steps;
            float signed = (float)(offset * 2d - 1d);
            float amount = DeltaGradientCurveMath.Evaluate(curve, Math.Abs(signed));
            System.Drawing.Color color = signed < 0f
                ? BlendPreviewColor(palette.Base, palette.Ahead, amount)
                : BlendPreviewColor(palette.Base, palette.Behind, amount);
            if (!palette.Enabled)
            {
                color = BlendPreviewColor(color, PreviewBackgroundColor, 0.55f);
            }

            stops.Add(new GradientStop(WpfColor.FromRgb(color.R, color.G, color.B), offset));
        }

        var brush = new System.Windows.Media.LinearGradientBrush(stops, new WpfPoint(0, 0.5), new WpfPoint(1, 0.5));
        brush.Freeze();
        return brush;
    }

    private (System.Drawing.Color Ahead, System.Drawing.Color Base, System.Drawing.Color Behind, bool Enabled) GetPreviewDeltaGradientPalette() =>
    (
        ColorText.Parse(Draft.Overlay.Colors.DeltaAheadText, System.Drawing.Color.FromArgb(114, 213, 114)),
        ColorText.Parse(Draft.Overlay.Colors.DeltaEqualText, System.Drawing.Color.White),
        ColorText.Parse(Draft.Overlay.Colors.DeltaBehindText, System.Drawing.Color.FromArgb(240, 112, 112)),
        enableDeltaGradientColorBox.IsChecked == true || enableCurrentDeltaGradientColorBox.IsChecked == true
    );

    private (System.Drawing.Color Ahead, System.Drawing.Color Base, System.Drawing.Color Behind, bool Enabled) GetPreviewTimerGradientPalette() =>
    (
        ColorText.Parse(Draft.Overlay.Colors.TimerAheadText, System.Drawing.Color.LightGreen),
        ColorText.Parse(Draft.Overlay.Colors.TimerEqualText, System.Drawing.Color.White),
        ColorText.Parse(Draft.Overlay.Colors.TimerBehindText, System.Drawing.Color.LightCoral),
        enableTimerGradientColorBox.IsChecked == true
    );

    private static System.Drawing.Color BlendPreviewColor(System.Drawing.Color from, System.Drawing.Color to, float amount)
    {
        float t = Math.Clamp(amount, 0f, 1f);
        return System.Drawing.Color.FromArgb(
            (int)MathF.Round(from.R + (to.R - from.R) * t),
            (int)MathF.Round(from.G + (to.G - from.G) * t),
            (int)MathF.Round(from.B + (to.B - from.B) * t));
    }

    private void UpdateDeltaGradientState()
    {
        UpdateEffectAvailability();
        RenderDeltaGradientPreview();
    }

    private void UpdateEffectAvailability()
    {
        bool iconLit = enableDefeatedBossIconLightingBox.IsChecked == true;
        undefeatedIconGrayscaleBox.IsEnabled = iconLit;
        undefeatedIconBrightnessBox.IsEnabled = iconLit;
        currentBossIconGrayscaleWeakenBox.IsEnabled = iconLit;
        currentBossIconBrightnessBoostBox.IsEnabled = iconLit;

        bool currentHi = showCurrentSplitHighlightBox.IsChecked == true;
        currentSplitHighlightScaleBox.IsEnabled = currentHi;
        currentSplitDepthStrengthBox.IsEnabled = currentHi;

        bool deltaGrad = enableDeltaGradientColorBox.IsChecked == true ||
            enableCurrentDeltaGradientColorBox.IsChecked == true ||
            enableTimerGradientColorBox.IsChecked == true;
        deltaGradientThresholdBox.IsEnabled = deltaGrad;
        deltaGradientCurveBox.IsEnabled = deltaGrad;
        if (deltaGradientPreviewHost is not null)
        {
            deltaGradientPreviewHost.Opacity = deltaGrad ? 1d : 0.45d;
        }

        UpdateSplitCompletionAvailability();
        UpdateSegmentBestDeltaHighlightAvailability();
    }

    private void UpdateSplitCompletionAvailability()
    {
        bool enabled = showSplitCompletionAnimationBox.IsChecked == true;
        completionTextPanel.IsEnabled = enabled;
        splitCompletionAnimationDurationBox.IsEnabled = enabled;
        if (outlineStylePreviewHost is not null)
        {
            outlineStylePreviewHost.Opacity = enabled ? 1d : 0.45d;
        }

        foreach (AnimationOutlineControls controls in animationOutlineControls.Values)
        {
            controls.SplitComparison.IsEnabled = enabled;
            controls.SegmentComparison.IsEnabled = enabled;
            controls.SplitTime.IsEnabled = enabled;
            controls.SegmentTime.IsEnabled = enabled;
        }
    }

    private void UpdateSegmentBestDeltaHighlightAvailability()
    {
        bool enabled = showSegmentBestDeltaHighlightBox.IsChecked == true;
        if (segmentBestDeltaHighlightPreviewHost is not null)
        {
            segmentBestDeltaHighlightPreviewHost.Opacity = enabled ? 1d : 0.45d;
        }

        foreach (SegmentBestDeltaHighlightControls controls in segmentBestDeltaHighlightControls.Values)
        {
            controls.Style.IsEnabled = enabled;
        }
    }

    private WpfComboBox CreateOutlineStyleBox(string selectedStyle)
    {
        WpfComboBox comboBox = WpfSettingsUi.CreateComboBox();
        foreach (string style in SplitCompletionOutlineStyles.Ids)
        {
            comboBox.Items.Add(new StyleOption(style, Localize(SplitCompletionOutlineStyles.GetDisplayName(style))));
        }

        SetOutlineStyle(comboBox, selectedStyle);
        comboBox.SelectionChanged += (_, _) =>
        {
            previewOutlineStyle = GetSelectedOutlineStyle(comboBox);
            RenderOutlinePreview();
        };
        return comboBox;
    }

    private WpfComboBox CreateSegmentBestDeltaHighlightStyleBox(string selectedStyle)
    {
        WpfComboBox comboBox = WpfSettingsUi.CreateComboBox();
        foreach (string style in SegmentBestDeltaHighlightStyles.Ids)
        {
            comboBox.Items.Add(new StyleOption(style, Localize(SegmentBestDeltaHighlightStyles.GetDisplayName(style))));
        }

        SetEffectStyle(comboBox, selectedStyle);
        comboBox.SelectionChanged += (_, _) =>
        {
            previewSegmentBestDeltaHighlightStyle = GetSelectedEffectStyle(comboBox);
            RenderSegmentHighlightPreview();
        };
        return comboBox;
    }

    private WpfComboBox CreateDeltaGradientCurveBox()
    {
        WpfComboBox comboBox = WpfSettingsUi.CreateComboBox(180);
        foreach (string curve in DeltaGradientCurves.Ids)
        {
            comboBox.Items.Add(new StyleOption(curve, Localize(DeltaGradientCurves.GetDisplayName(curve))));
        }

        SetStyle(comboBox, DeltaGradientCurves.Normalize(Draft.Overlay.DeltaGradientCurve));
        comboBox.SelectionChanged += (_, _) => RenderDeltaGradientPreview();
        return comboBox;
    }

    private static bool GetAnimationOutlineSetting(Dictionary<string, bool> values, string key)
    {
        return !values.TryGetValue(key, out bool enabled) || enabled;
    }

    private static string GetAnimationOutlineStyle(Dictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out string? style)
            ? SplitCompletionOutlineStyles.Normalize(style)
            : SplitCompletionOutlineStyles.Rainbow;
    }

    private string GetSegmentBestDeltaHighlightStyle(string key)
    {
        return Draft.Overlay.SegmentBestDeltaHighlightStyles.TryGetValue(key, out string? style)
            ? SegmentBestDeltaHighlightStyles.Normalize(style)
            : SegmentBestDeltaHighlightStyles.Aurora;
    }

    private static string GetSelectedOutlineStyle(WpfComboBox comboBox) =>
        comboBox.SelectedItem is StyleOption option ? option.Id : SplitCompletionOutlineStyles.None;

    private static string GetSelectedEffectStyle(WpfComboBox comboBox) =>
        comboBox.SelectedItem is StyleOption option ? option.Id : SegmentBestDeltaHighlightStyles.None;

    private static string GetSelectedDeltaGradientCurve(WpfComboBox comboBox) =>
        comboBox.SelectedItem is StyleOption option ? option.Id : DeltaGradientCurves.SoftStep;

    private static void SetOutlineStyle(WpfComboBox comboBox, string style) =>
        SetStyle(comboBox, SplitCompletionOutlineStyles.Normalize(style));

    private static void SetEffectStyle(WpfComboBox comboBox, string style) =>
        SetStyle(comboBox, SegmentBestDeltaHighlightStyles.Normalize(style));

    private static void SetStyle(WpfComboBox comboBox, string normalizedId)
    {
        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is StyleOption option &&
                string.Equals(option.Id, normalizedId, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }

        if (comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }
    }

    private void SaveAnimationOutlineControls()
    {
        foreach ((string key, AnimationOutlineControls controls) in animationOutlineControls)
        {
            Draft.Overlay.SplitCompletionSplitComparisons[key] = controls.SplitComparison.IsChecked == true;
            Draft.Overlay.SplitCompletionSegmentComparisons[key] = controls.SegmentComparison.IsChecked == true;
            Draft.Overlay.SplitCompletionOutlineSplitStyles[key] = GetSelectedOutlineStyle(controls.SplitTime);
            Draft.Overlay.SplitCompletionOutlineSegmentStyles[key] = GetSelectedOutlineStyle(controls.SegmentTime);
        }

        foreach ((string key, SegmentBestDeltaHighlightControls controls) in segmentBestDeltaHighlightControls)
        {
            Draft.Overlay.SegmentBestDeltaHighlightStyles[key] = GetSelectedEffectStyle(controls.Style);
        }
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
        style.Bold = controls.Bold.IsChecked == true;
        style.Italic = controls.Italic.IsChecked == true;
        style.OpacityPercent = WpfSettingsValueParser.ParseIntBox(controls.Opacity, style.OpacityPercent, 0, 100);
        style.ShadowPercent = WpfSettingsValueParser.ParseIntBox(controls.Shadow, style.ShadowPercent, 0, 100);
        style.OutlineThicknessPercent = WpfSettingsValueParser.ParseIntBox(controls.Outline, style.OutlineThicknessPercent, 0, 100);
    }

    private static IEnumerable<(string Key, string Label, SplitCompletionTextStyle Style)> TextParts(SplitCompletionTextSettings settings)
    {
        yield return (nameof(settings.Time), "Time", settings.Time);
        yield return (nameof(settings.Hint), "Hint text", settings.Hint);
        yield return (nameof(settings.Delta), "Delta time", settings.Delta);
    }

    internal sealed record CompletionTextControls(
        WpfFontFamilySelector Font,
        WpfCheckBox Bold,
        WpfCheckBox Italic,
        WpfTextBox Opacity,
        WpfTextBox Shadow,
        WpfTextBox Outline);

    private sealed record StyleOption(string Id, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private sealed record AnimationOutlineControls(
        WpfCheckBox SplitComparison,
        WpfCheckBox SegmentComparison,
        WpfComboBox SplitTime,
        WpfComboBox SegmentTime);

    private sealed record SegmentBestDeltaHighlightControls(WpfComboBox Style);
}
