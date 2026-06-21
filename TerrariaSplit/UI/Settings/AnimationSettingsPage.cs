using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed partial class AnimationSettingsPage : SettingsPageBase
{
    private readonly CheckBox showSplitCompletionAnimationBox = new();
    private readonly CheckBox showCurrentSplitHighlightBox = new();
    private readonly TextBox currentSplitHighlightScaleBox = new();
    private readonly TextBox currentSplitDepthStrengthBox = new();
    private readonly CheckBox showEarlyDeltaTimeBox = new();
    private readonly TextBox earlyDeltaTimeSecondsBox = new();
    private readonly CheckBox enableDynamicDeltaTimeUnitsBox = new();
    private readonly CheckBox enableDeltaGradientColorBox = new();
    private readonly CheckBox enableCurrentDeltaGradientColorBox = new();
    private readonly CheckBox enableTimerGradientColorBox = new();
    private readonly TextBox deltaGradientThresholdBox = new();
    private readonly ThemedDropDownList deltaGradientCurveBox = new();
    private readonly CheckBox showSegmentBestDeltaHighlightBox = new();
    private readonly CheckBox enableDefeatedBossIconLightingBox = new();
    private readonly TextBox splitCompletionAnimationDurationBox = new();
    private readonly TextBox splitCompletionOutlineThicknessBox = new();
    private readonly TextBox undefeatedIconGrayscaleBox = new();
    private readonly TextBox undefeatedIconBrightnessBox = new();
    private readonly TextBox currentBossIconGrayscaleWeakenBox = new();
    private readonly TextBox currentBossIconBrightnessBoostBox = new();
    private readonly Dictionary<string, AnimationOutlineControls> animationOutlineControls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SegmentBestDeltaHighlightControls> segmentBestDeltaHighlightControls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Panel outlineStylePreview = new();
    private readonly Panel segmentBestDeltaHighlightPreview = new();
    private readonly Panel deltaGradientPreview = new();
    private readonly System.Windows.Forms.Timer outlineStylePreviewTimer = new();

    private TableLayoutPanel? animationComparisonGrid;
    private TableLayoutPanel? animationOutlineGrid;
    private TableLayoutPanel? segmentBestDeltaHighlightGrid;
    private string? animationGridSignature;
    private string previewOutlineStyle = SplitCompletionOutlineStyles.Rainbow;
    private string previewSegmentBestDeltaHighlightStyle = SegmentBestDeltaHighlightStyles.Aurora;

    public override SettingsPageId Id => SettingsPageId.Effects;

    internal CheckBox EnableCurrentDeltaGradientColorBox => enableCurrentDeltaGradientColorBox;

    internal IReadOnlyCollection<string> AnimationOutlineKeysForTests => animationOutlineControls.Keys.ToList();

    internal IReadOnlyCollection<string> SegmentBestDeltaHighlightKeysForTests => segmentBestDeltaHighlightControls.Keys.ToList();

    protected override Control BuildPage(SettingsPageContext context)
    {
        return context.BuildScrollPage(BuildSections);
    }

    public override void Apply(AppSettings settings)
    {
        settings.Overlay.EnableDefeatedBossIconLighting = enableDefeatedBossIconLightingBox.Checked;
        settings.Overlay.UndefeatedIconGrayscalePercent = SettingsValueParser.ParseIntBox(undefeatedIconGrayscaleBox, 80, 0, 100);
        settings.Overlay.UndefeatedIconBrightnessPercent = SettingsValueParser.ParseIntBox(undefeatedIconBrightnessBox, 40, 0, 100);
        settings.Overlay.CurrentBossIconGrayscaleWeakenPercent = SettingsValueParser.ParseIntBox(currentBossIconGrayscaleWeakenBox, 40, 0, 100);
        settings.Overlay.CurrentBossIconBrightnessBoostPercent = SettingsValueParser.ParseIntBox(currentBossIconBrightnessBoostBox, 35, 0, 100);
        settings.Overlay.ShowSplitCompletionAnimation = showSplitCompletionAnimationBox.Checked;
        settings.Overlay.ShowCurrentSplitHighlight = showCurrentSplitHighlightBox.Checked;
        settings.Overlay.CurrentSplitHighlightScalePercent = SettingsValueParser.ParseIntBox(currentSplitHighlightScaleBox, 112, 100, 140);
        settings.Overlay.CurrentSplitDepthStrengthPercent = SettingsValueParser.ParseIntBox(currentSplitDepthStrengthBox, 45, 0, 100);
        settings.Overlay.ShowEarlyDeltaTime = showEarlyDeltaTimeBox.Checked;
        settings.Overlay.EarlyDeltaTimeSeconds = SettingsValueParser.ParseIntBox(earlyDeltaTimeSecondsBox, 60, 0, 3600);
        settings.Overlay.EnableDeltaGradientColor = enableDeltaGradientColorBox.Checked;
        settings.Overlay.EnableCurrentDeltaGradientColor = enableCurrentDeltaGradientColorBox.Checked;
        settings.Overlay.EnableTimerGradientColor = enableTimerGradientColorBox.Checked;
        settings.Overlay.DeltaGradientThresholdSeconds = SettingsValueParser.ParseTimeBox(deltaGradientThresholdBox, 120, 1, 3600);
        settings.Overlay.DeltaGradientCurve = GetSelectedDeltaGradientCurve(deltaGradientCurveBox);
        settings.Overlay.ShowSegmentBestDeltaHighlight = showSegmentBestDeltaHighlightBox.Checked;
        settings.Overlay.SplitCompletionAnimationDurationSeconds = SettingsValueParser.ParseFloatBox(splitCompletionAnimationDurationBox, 4.2f, 2f, 20f);
        settings.Overlay.SplitCompletionOutlineThicknessPercent = SettingsValueParser.ParseIntBox(splitCompletionOutlineThicknessBox, 30, 0, 100);
        SaveAnimationOutlineControls();
    }

    public override void OnModelChanged(SettingsModelChange change)
    {
        if (change != SettingsModelChange.RouteChanged)
        {
            return;
        }

        SaveAnimationOutlineControls();
        PopulateAnimationOutlineGrid();
        PopulateSegmentBestDeltaHighlightGrid();
    }

    private void BuildSections(TableLayoutPanel parent)
    {
        ConfigureCheckBox(enableDefeatedBossIconLightingBox, Draft.Overlay.EnableDefeatedBossIconLighting);
        enableDefeatedBossIconLightingBox.CheckedChanged += (_, _) => UpdateEffectAvailability();
        ConfigureNumberBox(undefeatedIconGrayscaleBox, Draft.Overlay.UndefeatedIconGrayscalePercent, 0, 100);
        ConfigureNumberBox(undefeatedIconBrightnessBox, Draft.Overlay.UndefeatedIconBrightnessPercent, 0, 100);
        ConfigureNumberBox(currentBossIconGrayscaleWeakenBox, Draft.Overlay.CurrentBossIconGrayscaleWeakenPercent, 0, 100);
        ConfigureNumberBox(currentBossIconBrightnessBoostBox, Draft.Overlay.CurrentBossIconBrightnessBoostPercent, 0, 100);
        ConfigureCheckBox(showCurrentSplitHighlightBox, Draft.Overlay.ShowCurrentSplitHighlight);
        showCurrentSplitHighlightBox.CheckedChanged += (_, _) => UpdateEffectAvailability();
        ConfigureNumberBox(currentSplitHighlightScaleBox, Draft.Overlay.CurrentSplitHighlightScalePercent, 100, 140);
        ConfigureNumberBox(currentSplitDepthStrengthBox, Draft.Overlay.CurrentSplitDepthStrengthPercent, 0, 100);
        ConfigureCheckBox(showEarlyDeltaTimeBox, Draft.Overlay.ShowEarlyDeltaTime);
        showEarlyDeltaTimeBox.CheckedChanged += (_, _) => UpdateEffectAvailability();
        ConfigureNumberBox(earlyDeltaTimeSecondsBox, Draft.Overlay.EarlyDeltaTimeSeconds, 0, 3600);
        ConfigureCheckBox(enableDeltaGradientColorBox, Draft.Overlay.EnableDeltaGradientColor);
        ConfigureCheckBox(enableCurrentDeltaGradientColorBox, Draft.Overlay.EnableCurrentDeltaGradientColor);
        ConfigureCheckBox(enableTimerGradientColorBox, Draft.Overlay.EnableTimerGradientColor);
        ConfigureTimeBox(deltaGradientThresholdBox, Draft.Overlay.DeltaGradientThresholdSeconds, 1, 3600);
        ConfigureDeltaGradientCurveBox();
        enableDeltaGradientColorBox.CheckedChanged += (_, _) => UpdateDeltaGradientState();
        enableCurrentDeltaGradientColorBox.CheckedChanged += (_, _) => UpdateDeltaGradientState();
        enableTimerGradientColorBox.CheckedChanged += (_, _) => UpdateDeltaGradientState();
        deltaGradientThresholdBox.TextChanged += (_, _) => InvalidateDeltaGradientPreview();
        ConfigureCheckBox(showSplitCompletionAnimationBox, Draft.Overlay.ShowSplitCompletionAnimation);
        showSplitCompletionAnimationBox.CheckedChanged += (_, _) => UpdateEffectAvailability();
        ConfigureDecimalBox(splitCompletionAnimationDurationBox, Draft.Overlay.SplitCompletionAnimationDurationSeconds, 2m, 20m);
        ConfigureNumberBox(splitCompletionOutlineThicknessBox, Draft.Overlay.SplitCompletionOutlineThicknessPercent, 0, 100);
        splitCompletionOutlineThicknessBox.TextChanged += (_, _) => outlineStylePreview.Invalidate();
        ConfigureCheckBox(showSegmentBestDeltaHighlightBox, Draft.Overlay.ShowSegmentBestDeltaHighlight);
        showSegmentBestDeltaHighlightBox.CheckedChanged += (_, _) => UpdateEffectAvailability();

        AddIconLightingSection(parent);
        AddCurrentSplitSection(parent);
        AddEarlyDeltaSection(parent);
        AddDeltaGradientSection(parent);
        AddSplitCompletionSection(parent);
        AddSegmentBestDeltaHighlightSection(parent);
        UpdateEffectAvailability();
    }

    private void AddIconLightingSection(TableLayoutPanel parent)
    {
        TableLayoutPanel iconSection = Factory.CreateSection("Light icons when current stage completed");
        TableLayoutPanel iconGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(iconGrid, "Enabled", enableDefeatedBossIconLightingBox);
        Factory.AddSettingRow(iconGrid, "Unlit grayscale %", undefeatedIconGrayscaleBox);
        Factory.AddSettingRow(iconGrid, "Unlit brightness %", undefeatedIconBrightnessBox);
        Factory.AddSettingRow(iconGrid, "Current stage icon grayscale weaken %", currentBossIconGrayscaleWeakenBox);
        Factory.AddSettingRow(iconGrid, "Current stage icon brightness boost %", currentBossIconBrightnessBoostBox);
        SettingsUiFactory.AddSectionControl(iconSection, iconGrid);
        SettingsUiFactory.AddSection(parent, iconSection);
    }

    private void AddCurrentSplitSection(TableLayoutPanel parent)
    {
        TableLayoutPanel currentSection = Factory.CreateSection("Highlight current stage");
        TableLayoutPanel currentGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(currentGrid, "Enabled", showCurrentSplitHighlightBox);
        Factory.AddSettingRow(currentGrid, "Scale %", currentSplitHighlightScaleBox);
        Factory.AddSettingRow(currentGrid, "Depth strength %", currentSplitDepthStrengthBox);
        SettingsUiFactory.AddSectionControl(currentSection, currentGrid);
        SettingsUiFactory.AddSection(parent, currentSection);
    }

    private void AddEarlyDeltaSection(TableLayoutPanel parent)
    {
        TableLayoutPanel earlyDeltaSection = Factory.CreateSection("Early delta time");
        TableLayoutPanel earlyDeltaGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(earlyDeltaGrid, "Enabled", showEarlyDeltaTimeBox);
        Factory.AddSettingRow(earlyDeltaGrid, "Show when within seconds", earlyDeltaTimeSecondsBox);
        SettingsUiFactory.AddSectionControl(earlyDeltaSection, earlyDeltaGrid);
        SettingsUiFactory.AddSection(parent, earlyDeltaSection);
    }

    private void AddDeltaGradientSection(TableLayoutPanel parent)
    {
        TableLayoutPanel deltaGradientSection = Factory.CreateSection("Delta time gradient");
        TableLayoutPanel deltaGradientGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(deltaGradientGrid, "Enabled (Historical delta)", enableDeltaGradientColorBox);
        Factory.AddSettingRow(deltaGradientGrid, "Enabled (Current delta)", enableCurrentDeltaGradientColorBox);
        Factory.AddSettingRow(deltaGradientGrid, "Enabled (Main timer)", enableTimerGradientColorBox);
        Factory.AddSettingRow(deltaGradientGrid, "Threshold time", deltaGradientThresholdBox);
        Factory.AddSettingRow(deltaGradientGrid, "Gradient mode", deltaGradientCurveBox);
        SettingsUiFactory.AddSectionControl(deltaGradientSection, deltaGradientGrid);
        SettingsUiFactory.AddSectionControl(deltaGradientSection, CreateDeltaGradientPreview());
        SettingsUiFactory.AddSection(parent, deltaGradientSection);
    }

    private void AddSplitCompletionSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = Factory.CreateSection("Main stage completion animation");
        TableLayoutPanel optionGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(optionGrid, "Enabled", showSplitCompletionAnimationBox);
        Factory.AddSettingRow(optionGrid, "Animation duration seconds", splitCompletionAnimationDurationBox);
        SettingsUiFactory.AddSectionControl(section, optionGrid);

        SettingsUiFactory.AddSectionControl(section, Factory.CreateSubsectionLabel("Show comparison with reference time"));
        animationComparisonGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(180f),
            SettingsUiFactory.ColumnStyleAbsolute(180f));
        SettingsUiFactory.AddSectionControl(section, animationComparisonGrid);

        SettingsUiFactory.AddSectionControl(section, Factory.CreateSubsectionLabel("Outline when faster than reference"));
        TableLayoutPanel outlineOptionGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(outlineOptionGrid, "Outline %", splitCompletionOutlineThicknessBox);
        SettingsUiFactory.AddSectionControl(section, outlineOptionGrid);

        animationOutlineGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(180f),
            SettingsUiFactory.ColumnStyleAbsolute(180f));
        SettingsUiFactory.AddSectionControl(section, animationOutlineGrid);
        SettingsUiFactory.AddSectionControl(section, CreateOutlineStylePreview());
        PopulateAnimationOutlineGrid();
        SettingsUiFactory.AddSection(parent, section);
    }

    private void AddSegmentBestDeltaHighlightSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = Factory.CreateSection("Highlight best segment");
        TableLayoutPanel optionGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(optionGrid, "Enabled", showSegmentBestDeltaHighlightBox);
        SettingsUiFactory.AddSectionControl(section, optionGrid);

        segmentBestDeltaHighlightGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(220f));
        PopulateSegmentBestDeltaHighlightGrid();
        SettingsUiFactory.AddSectionControl(section, segmentBestDeltaHighlightGrid);
        SettingsUiFactory.AddSectionControl(section, CreateSegmentBestDeltaHighlightPreview());
        SettingsUiFactory.AddSection(parent, section);
    }

}
