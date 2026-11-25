using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfSlider = System.Windows.Controls.Slider;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace TerrariaSplit.UI.Wpf.Settings;

internal sealed class WpfAutomationSettingsPage : WpfSettingsPageBase
{
    private WpfTextBox autoCreatePlayerNameBox = null!;
    private WpfTextBox autoCreatePlayerTemplateCodeBox = null!;
    private WpfComboBox autoCreatePlayerDifficultyBox = null!;
    private WpfCheckBox autoCreatePreserveExistingSavesBox = null!;
    private WpfComboBox autoCreateWorldSizeBox = null!;
    private WpfComboBox autoCreateWorldDifficultyBox = null!;
    private WpfComboBox autoCreateWorldEvilBox = null!;
    private WpfTextBox autoCreateSecretSeedsBox = null!;
    private WpfTextBox autoCreateFixedSeedBox = null!;
    private WpfCheckBox autoCreateZenithStarCatchBox = null!;
    private WpfSlider autoCreateZenithStarCatchSpeedBar = null!;
    private TextBlock autoCreateZenithStarCatchSpeedValueLabel = null!;
    private WpfCheckBox autoCreateCheatsBox = null!;

    private ToggleButton autoCreatePyramidFilterBox = null!;
    private ToggleButton autoCreateCrimsonBetweenDungeonAndSpawnBox = null!;
    private ToggleButton autoCreateJungleRouteDepthBox = null!;

    private readonly Dictionary<string, ToggleButton> autoCreateSpecialSeedBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ToggleButton> autoCreateZenithStarCatchStageBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ToggleButton> autoCreatePyramidItemBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, ToggleButton> autoCreatePyramidCoinPileMinimumBoxes = new();
    private readonly Dictionary<int, ToggleButton> autoCreatePyramidDepthBoxes = new();
    private readonly Dictionary<string, ToggleButton> autoCreateCrimsonDistanceBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ToggleButton> autoCreateJungleRouteDepthBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ToggleButton> autoCreateResourceItemBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, ToggleButton> autoCreateStarfuryDistanceBoxes = new();
    private readonly Dictionary<int, ToggleButton> autoCreateFinchStaffDistanceBoxes = new();
    private readonly Dictionary<int, ToggleButton> autoCreateLifeCrystalMinimumBoxes = new();
    private readonly Dictionary<int, ToggleButton> autoCreateSpelunkerMinimumBoxes = new();
    private readonly Dictionary<int, ToggleButton> autoCreateFeatherfallMinimumBoxes = new();

    private WpfCheckBox autoCreateWorldPoolBox = null!;
    private WpfTextBox autoCreateWorldPoolTargetBox = null!;
    private WpfTextBox autoCreateShortActionDelayBox = null!;
    private WpfTextBox autoCreateMenuActionDelayBox = null!;
    private WpfTextBox autoCreateWindowActivationDelayBox = null!;
    private WpfTextBox autoCreateClickFocusDelayBox = null!;
    private WpfTextBox autoCreateInputPressDurationBox = null!;

    private readonly List<PracticeSlotControls> practiceSlotControls = new();

    private bool updatingPyramidDepth;
    private int selectedPyramidDepth;
    private bool updatingZenithStarCatchStageSelection;
    private bool updatingCrimsonDistanceSelection;
    private bool updatingJungleRouteDepthSelection;
    private bool updatingResourceMinimumSelection;
    private bool updatingPostGenerationFilterAvailability;

    public override SettingsPageId Id => SettingsPageId.Automation;

    internal IReadOnlyList<PracticeSlotControls> PracticeSlots => practiceSlotControls;
    internal IReadOnlyDictionary<string, ToggleButton> AutoCreateSpecialSeedBoxes => autoCreateSpecialSeedBoxes;
    internal WpfCheckBox AutoCreatePreserveExistingSavesBox => autoCreatePreserveExistingSavesBox;
    internal WpfCheckBox AutoCreateZenithStarCatchBox => autoCreateZenithStarCatchBox;
    internal IReadOnlyDictionary<string, ToggleButton> AutoCreateZenithStarCatchStageBoxes => autoCreateZenithStarCatchStageBoxes;
    internal WpfSlider AutoCreateZenithStarCatchSpeedBar => autoCreateZenithStarCatchSpeedBar;
    internal WpfCheckBox AutoCreateCheatsBox => autoCreateCheatsBox;
    internal ToggleButton AutoCreatePyramidFilterBox => autoCreatePyramidFilterBox;
    internal IReadOnlyDictionary<int, ToggleButton> AutoCreatePyramidDepthBoxes => autoCreatePyramidDepthBoxes;
    internal IReadOnlyDictionary<int, ToggleButton> AutoCreateStarfuryDistanceBoxes => autoCreateStarfuryDistanceBoxes;
    internal IReadOnlyDictionary<int, ToggleButton> AutoCreateFinchStaffDistanceBoxes => autoCreateFinchStaffDistanceBoxes;
    internal IReadOnlyDictionary<int, ToggleButton> AutoCreatePyramidCoinPileMinimumBoxes => autoCreatePyramidCoinPileMinimumBoxes;
    internal ToggleButton AutoCreateCrimsonBetweenDungeonAndSpawnBox => autoCreateCrimsonBetweenDungeonAndSpawnBox;
    internal IReadOnlyDictionary<string, ToggleButton> AutoCreateCrimsonDistanceBoxes => autoCreateCrimsonDistanceBoxes;
    internal ToggleButton AutoCreateJungleRouteDepthBox => autoCreateJungleRouteDepthBox;
    internal IReadOnlyDictionary<string, ToggleButton> AutoCreateJungleRouteDepthBoxes => autoCreateJungleRouteDepthBoxes;
    internal IReadOnlyDictionary<string, ToggleButton> AutoCreateResourceItemBoxes => autoCreateResourceItemBoxes;
    internal IReadOnlyDictionary<int, ToggleButton> AutoCreateLifeCrystalMinimumBoxes => autoCreateLifeCrystalMinimumBoxes;
    internal IReadOnlyDictionary<int, ToggleButton> AutoCreateSpelunkerMinimumBoxes => autoCreateSpelunkerMinimumBoxes;
    internal IReadOnlyDictionary<int, ToggleButton> AutoCreateFeatherfallMinimumBoxes => autoCreateFeatherfallMinimumBoxes;
    internal IReadOnlyDictionary<string, ToggleButton> AutoCreatePyramidItemBoxes => autoCreatePyramidItemBoxes;
    internal WpfCheckBox AutoCreateWorldPoolBox => autoCreateWorldPoolBox;
    internal WpfTextBox AutoCreateWorldPoolTargetBox => autoCreateWorldPoolTargetBox;
    internal WpfTextBox AutoCreateSecretSeedsBox => autoCreateSecretSeedsBox;
    internal WpfTextBox AutoCreateFixedSeedBox => autoCreateFixedSeedBox;
    internal WpfComboBox AutoCreateWorldSizeBox => autoCreateWorldSizeBox;

    protected override FrameworkElement BuildPage(WpfSettingsPageContext context)
    {
        return WpfSettingsUi.CreateScrollPage(BuildSections);
    }

    public override void Apply(AppSettings settings)
    {
        settings.Automation.AutoCreate.PlayerName = autoCreatePlayerNameBox.Text.Trim();
        settings.Automation.AutoCreate.PlayerTemplateCode = autoCreatePlayerTemplateCodeBox.Text.Trim();
        settings.Automation.AutoCreate.PlayerDifficulty = AutoCreatePlayerDifficulty.Normalize(
            GetSelectedOption(autoCreatePlayerDifficultyBox, AutoCreatePlayerDifficulty.Softcore));
        settings.Automation.AutoCreate.PreserveExistingSaves = autoCreatePreserveExistingSavesBox.IsChecked == true;
        settings.Automation.AutoCreate.WorldSize = AutoCreateWorldSize.Normalize(
            GetSelectedOption(autoCreateWorldSizeBox, AutoCreateWorldSize.Medium));
        settings.Automation.AutoCreate.WorldDifficulty = AutoCreateWorldDifficulty.Normalize(
            GetSelectedOption(autoCreateWorldDifficultyBox, AutoCreateWorldDifficulty.Classic));
        settings.Automation.AutoCreate.WorldEvil = AutoCreateWorldEvil.Normalize(
            GetSelectedOption(autoCreateWorldEvilBox, AutoCreateWorldEvil.Random));

        string selectedSpecialSeeds = string.Join(
            "|",
            AutoCreateSpecialWorldSeed.All.Where(seed =>
                autoCreateSpecialSeedBoxes.TryGetValue(seed, out ToggleButton? box) && box.IsChecked == true));
        settings.Automation.AutoCreate.SpecialSeeds = string.Join("|", AutoCreateSpecialWorldSeed.ParseList(selectedSpecialSeeds));
        settings.Automation.AutoCreate.SecretSeeds = autoCreateSecretSeedsBox.Text.Trim();
        settings.Automation.AutoCreate.FixedSeed = autoCreateFixedSeedBox.Text.Trim();
        settings.Automation.AutoCreate.EnableZenithStarCatch = autoCreateZenithStarCatchBox.IsChecked == true;
        settings.Automation.AutoCreate.ZenithStarCatchStopStage = GetSelectedZenithStarCatchStopStage();
        settings.Automation.AutoCreate.ZenithStarCatchSpeedSliderValue = AutoCreateZenithStarCatchSpeed.NormalizeSliderValue(
            (int)Math.Round(autoCreateZenithStarCatchSpeedBar.Value));
        settings.Automation.AutoCreate.EnableCheats = autoCreateCheatsBox.IsChecked == true;
        settings.Automation.AutoCreate.EnablePyramidFilter = autoCreatePyramidFilterBox.IsChecked == true;
        settings.Automation.AutoCreate.PyramidMaximumDepth = selectedPyramidDepth;
        settings.Automation.AutoCreate.StarfuryMaximumDistance = GetSelectedMaximum(autoCreateStarfuryDistanceBoxes);
        settings.Automation.AutoCreate.FinchStaffMaximumDistance = GetSelectedMaximum(autoCreateFinchStaffDistanceBoxes);
        settings.Automation.AutoCreate.PyramidFilterCoinPileMinimum = GetSelectedMinimum(
            autoCreatePyramidCoinPileMinimumBoxes,
            AutoCreatePyramidCoinPileMinimum.All);
        settings.Automation.AutoCreate.RequireCrimsonBetweenDungeonAndSpawn = autoCreateCrimsonBetweenDungeonAndSpawnBox.IsChecked == true;
        settings.Automation.AutoCreate.CrimsonDistance = GetSelectedCrimsonDistance();
        settings.Automation.AutoCreate.JungleRouteDepth = GetSelectedJungleRouteDepth();
        settings.Automation.AutoCreate.ResourceFilterItemMask = AutoCreateResourceFilterItem.ToMask(
            AutoCreateResourceFilterItem.All.Where(item =>
                autoCreateResourceItemBoxes.TryGetValue(item, out ToggleButton? box) && box.IsChecked == true));
        settings.Automation.AutoCreate.ResourceFilterLifeCrystalMinimum = GetSelectedMinimum(
            autoCreateLifeCrystalMinimumBoxes,
            AutoCreateResourceMinimum.LifeCrystals);
        settings.Automation.AutoCreate.ResourceFilterSpelunkerPotionMinimum = GetSelectedMinimum(
            autoCreateSpelunkerMinimumBoxes,
            AutoCreateResourceMinimum.Potions);
        settings.Automation.AutoCreate.ResourceFilterFeatherfallPotionMinimum = GetSelectedMinimum(
            autoCreateFeatherfallMinimumBoxes,
            AutoCreateResourceMinimum.Potions);
        settings.Automation.AutoCreate.PyramidFilterItemMask = AutoCreatePyramidFilterItem.ToMask(
            AutoCreatePyramidFilterItem.All.Where(item =>
                autoCreatePyramidItemBoxes.TryGetValue(item, out ToggleButton? box) && box.IsChecked == true));
        settings.Automation.AutoCreate.EnableWorldPool = autoCreateWorldPoolBox.IsChecked == true;
        settings.Automation.AutoCreate.WorldPoolTargetCount = WpfSettingsValueParser.ParseIntBox(
            autoCreateWorldPoolTargetBox,
            AppSettingsDefaults.AutoCreate.WorldPoolTargetCount,
            1,
            50);
        settings.Automation.AutoCreate.ShortActionDelayMilliseconds = WpfSettingsValueParser.ParseIntBox(
            autoCreateShortActionDelayBox,
            AppSettingsDefaults.AutoCreate.ShortActionDelayMilliseconds,
            0,
            5000);
        settings.Automation.AutoCreate.MenuActionDelayMilliseconds = WpfSettingsValueParser.ParseIntBox(
            autoCreateMenuActionDelayBox,
            AppSettingsDefaults.AutoCreate.MenuActionDelayMilliseconds,
            0,
            5000);
        settings.Automation.AutoCreate.WindowActivationDelayMilliseconds = WpfSettingsValueParser.ParseIntBox(
            autoCreateWindowActivationDelayBox,
            AppSettingsDefaults.AutoCreate.WindowActivationDelayMilliseconds,
            0,
            5000);
        settings.Automation.AutoCreate.ClickFocusDelayMilliseconds = WpfSettingsValueParser.ParseIntBox(
            autoCreateClickFocusDelayBox,
            AppSettingsDefaults.AutoCreate.ClickFocusDelayMilliseconds,
            0,
            5000);
        settings.Automation.AutoCreate.InputPressDurationMilliseconds = WpfSettingsValueParser.ParseIntBox(
            autoCreateInputPressDurationBox,
            AppSettingsDefaults.AutoCreate.InputPressDurationMilliseconds,
            1,
            5000);
        SettingsSectionNormalizer.NormalizeAutoCreate(settings.Automation.AutoCreate);

        settings.PracticeWorlds.Slots.Clear();
        foreach (PracticeSlotControls controls in practiceSlotControls)
        {
            settings.PracticeWorlds.Slots.Add(new PracticeWorldSlot
            {
                Name = controls.NameBox.Text.Trim(),
                PlayerFilePath = controls.PlayerFilePathBox.Text.Trim(),
                WorldFilePath = controls.WorldFilePathBox.Text.Trim()
            });
        }
    }

    private void BuildSections(StackPanel parent)
    {
        BuildCreateWorldCard(parent);
        BuildLoadWorldCard(parent);
        BuildDelaysCard(parent);
    }

    private void BuildCreateWorldCard(StackPanel parent)
    {
        AutoCreateWorldSettings ac = Draft.Automation.AutoCreate;
        var card = WpfSettingsUi.CreateCard(
            Localize("Create World"),
            out StackPanel body,
            subtitle: Localize("Create World creates a world automatically by simulating mouse and keyboard input."));

        // Info & backup notices
        WpfButton openSaveBtn = WpfSettingsUi.CreateButton(Localize("Open save folder"), minWidth: 118);
        openSaveBtn.Click += (_, _) => Dialogs.OpenTerrariaSaveFolder(Localize);

        WpfButton openBackupBtn = WpfSettingsUi.CreateButton(Localize("Open backup folder"), minWidth: 118);
        openBackupBtn.Click += (_, _) => Dialogs.OpenAutoCreateBackupFolder(Localize);

        body.Children.Add(WpfSettingsUi.CreateNoticeGroup(
            (
                Localize("By default, Create World moves non-favorite players and worlds to the backup folder before creating."),
                true,
                openSaveBtn
            ),
            (
                Localize("When existing saves are not preserved, the most recent 50 cleanup batches are kept in the backup folder."),
                true,
                openBackupBtn
            )));

        // Player options
        autoCreatePlayerNameBox = WpfSettingsUi.CreateTextBox(ac.PlayerName, placeholder: Localize("Empty = 1"), width: WpfSettingsUi.StandardFieldWidth);
        autoCreatePlayerDifficultyBox = CreateOptionBox(AutoCreatePlayerDifficulty.All, ac.PlayerDifficulty, width: WpfSettingsUi.StandardFieldWidth);
        autoCreatePlayerTemplateCodeBox = WpfSettingsUi.CreateMultilineTextBox(
            ac.PlayerTemplateCode,
            placeholder: Localize("Empty = default character"),
            height: 90);

        body.Children.Add(WpfSettingsUi.CreateSubHeader(Localize("Player options")));
        WpfSettingsUi.AddSettingRow(body, Localize("Player name"), autoCreatePlayerNameBox);
        WpfSettingsUi.AddSettingRow(body, Localize("Player difficulty"), autoCreatePlayerDifficultyBox);
        TextBlock playerCodeLabel = WpfSettingsUi.CreateRowLabel(Localize("Player code"));
        playerCodeLabel.Margin = new Thickness(0, 8, 0, 2);
        body.Children.Add(playerCodeLabel);
        body.Children.Add(autoCreatePlayerTemplateCodeBox);

        // World options
        autoCreateWorldSizeBox = CreateOptionBox(AutoCreateWorldSize.All, ac.WorldSize, width: 220);
        autoCreateWorldSizeBox.SelectionChanged += (_, _) => UpdatePostGenerationFilterAvailability();
        autoCreateWorldDifficultyBox = CreateOptionBox(AutoCreateWorldDifficulty.All, ac.WorldDifficulty, width: 220);
        autoCreateWorldEvilBox = CreateOptionBox(AutoCreateWorldEvil.All, ac.WorldEvil, width: 220);
        autoCreateWorldEvilBox.SelectionChanged += (_, _) => UpdatePostGenerationFilterAvailability();
        autoCreateSecretSeedsBox = WpfSettingsUi.CreateTextBox(ac.SecretSeeds, placeholder: Localize("Empty = none"), width: 220);
        autoCreateSecretSeedsBox.TextChanged += (_, _) => UpdatePostGenerationFilterAvailability();
        autoCreateFixedSeedBox = WpfSettingsUi.CreateTextBox(ac.FixedSeed, placeholder: Localize("Empty = random visible seed"), width: 220);
        autoCreateFixedSeedBox.TextChanged += (_, _) => UpdatePostGenerationFilterAvailability();

        body.Children.Add(WpfSettingsUi.CreateSubHeader(Localize("World options")));
        WpfSettingsUi.AddSettingRow(body, Localize("World size"), autoCreateWorldSizeBox);
        WpfSettingsUi.AddSettingRow(body, Localize("World difficulty"), autoCreateWorldDifficultyBox);
        WpfSettingsUi.AddSettingRow(body, Localize("World evil"), autoCreateWorldEvilBox);
        TextBlock specialSeedsLabel = WpfSettingsUi.CreateRowLabel(Localize("Special seeds"));
        specialSeedsLabel.Margin = new Thickness(0, 8, 0, 5);
        body.Children.Add(specialSeedsLabel);
        body.Children.Add(BuildSpecialSeedSelector());
        WpfSettingsUi.AddSettingRow(body, Localize("Secret seeds"), autoCreateSecretSeedsBox);
        WpfSettingsUi.AddSettingRow(body, Localize("Fixed seed"), autoCreateFixedSeedBox);

        // Zenith star catch
        autoCreateZenithStarCatchBox = WpfSettingsUi.CreateToggleSwitch(ac.EnableZenithStarCatch);
        autoCreateZenithStarCatchBox.Checked += (_, _) => UpdateZenithStarCatchAvailability();
        autoCreateZenithStarCatchBox.Unchecked += (_, _) => UpdateZenithStarCatchAvailability();

        autoCreateZenithStarCatchSpeedBar = new WpfSlider
        {
            Minimum = AutoCreateZenithStarCatchSpeed.MinimumSliderValue,
            Maximum = AutoCreateZenithStarCatchSpeed.MaximumSliderValue,
            Value = AutoCreateZenithStarCatchSpeed.NormalizeSliderValue(ac.ZenithStarCatchSpeedSliderValue),
            Width = 164,
            IsSnapToTickEnabled = true,
            TickFrequency = 1,
            VerticalAlignment = VerticalAlignment.Center
        };
        autoCreateZenithStarCatchSpeedValueLabel = new TextBlock
        {
            Width = 48,
            TextAlignment = TextAlignment.Right,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        autoCreateZenithStarCatchSpeedValueLabel.SetResourceReference(TextBlock.FontFamilyProperty, "MonoFontFamily");
        autoCreateZenithStarCatchSpeedValueLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        autoCreateZenithStarCatchSpeedBar.ValueChanged += (_, _) => UpdateZenithStarCatchSpeedLabel();
        UpdateZenithStarCatchSpeedLabel();

        var speedRow = new StackPanel { Orientation = WpfOrientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        speedRow.Children.Add(autoCreateZenithStarCatchSpeedBar);
        speedRow.Children.Add(autoCreateZenithStarCatchSpeedValueLabel);

        body.Children.Add(WpfSettingsUi.CreateSubHeader(Localize("Zenith star catch")));
        WpfSettingsUi.AddSettingRow(body, Localize("Enabled"), autoCreateZenithStarCatchBox);
        TextBlock stopAfterStageLabel = WpfSettingsUi.CreateRowLabel(Localize("Stop after stage"));
        stopAfterStageLabel.Margin = new Thickness(0, 8, 0, 5);
        body.Children.Add(stopAfterStageLabel);
        body.Children.Add(BuildZenithStarCatchStageSelector());
        WpfSettingsUi.AddSettingRow(body, Localize("Catch speed"), speedRow);

        // Seed Filtering
        autoCreateCheatsBox = WpfSettingsUi.CreateToggleSwitch(ac.EnableCheats);
        autoCreateCheatsBox.Checked += (_, _) => UpdatePostGenerationFilterAvailability();
        autoCreateCheatsBox.Unchecked += (_, _) => UpdatePostGenerationFilterAvailability();

        autoCreatePyramidFilterBox = WpfSettingsUi.CreateChipToggle(Localize("Pyramid"), ac.EnablePyramidFilter);
        autoCreatePyramidFilterBox.Checked += (_, _) => UpdatePyramidItemAvailability();
        autoCreatePyramidFilterBox.Unchecked += (_, _) => UpdatePyramidItemAvailability();

        autoCreateCrimsonBetweenDungeonAndSpawnBox = WpfSettingsUi.CreateChipToggle(
            Localize("Dungeon-side Crimson"),
            ac.RequireCrimsonBetweenDungeonAndSpawn);
        autoCreateCrimsonBetweenDungeonAndSpawnBox.Checked += (_, _) => UpdatePostGenerationFilterAvailability();
        autoCreateCrimsonBetweenDungeonAndSpawnBox.Unchecked += (_, _) => UpdatePostGenerationFilterAvailability();

        autoCreateJungleRouteDepthBox = WpfSettingsUi.CreateChipToggle(
            Localize("Jungle main route"),
            AutoCreateJungleRouteDepth.Normalize(ac.JungleRouteDepth) != AutoCreateJungleRouteDepth.None);
        autoCreateJungleRouteDepthBox.Checked += (_, _) => SelectJungleRouteDepth(AutoCreateJungleRouteDepth.None, true);
        autoCreateJungleRouteDepthBox.Unchecked += (_, _) => SelectJungleRouteDepth(AutoCreateJungleRouteDepth.None, false);

        body.Children.Add(WpfSettingsUi.CreateSubHeader(Localize("Seed Filtering")));
        WpfSettingsUi.AddSettingRow(body, Localize("Enabled"), autoCreateCheatsBox);
        body.Children.Add(BuildPyramidItemSelector());
        body.Children.Add(BuildMinimumSelector(
            AutoCreatePyramidCoinPileMinimum.All,
            AutoCreatePyramidCoinPileMinimum.Normalize(ac.PyramidFilterCoinPileMinimum),
            autoCreatePyramidCoinPileMinimumBoxes,
            "Pyramid gold coin piles"));
        body.Children.Add(BuildPyramidDepthSelector());
        body.Children.Add(BuildCrimsonDistanceSelector());
        body.Children.Add(BuildJungleRouteDepthSelector());
        body.Children.Add(BuildResourceItemSelector());
        body.Children.Add(BuildItemDistanceSelector(
            AutoCreateItemDistance.Starfury,
            AutoCreateItemDistance.StarfuryLabel,
            AutoCreateItemDistance.NormalizeStarfury(ac.StarfuryMaximumDistance),
            autoCreateStarfuryDistanceBoxes));
        body.Children.Add(BuildItemDistanceSelector(
            AutoCreateItemDistance.FinchStaff,
            AutoCreateItemDistance.FinchStaffLabel,
            AutoCreateItemDistance.NormalizeFinchStaff(ac.FinchStaffMaximumDistance),
            autoCreateFinchStaffDistanceBoxes));
        body.Children.Add(BuildMinimumSelector(
            AutoCreateResourceMinimum.LifeCrystals,
            AutoCreateResourceMinimum.NormalizeLifeCrystals(ac.ResourceFilterLifeCrystalMinimum),
            autoCreateLifeCrystalMinimumBoxes,
            "Life Crystal"));
        body.Children.Add(BuildMinimumSelector(
            AutoCreateResourceMinimum.Potions,
            AutoCreateResourceMinimum.NormalizePotions(ac.ResourceFilterSpelunkerPotionMinimum),
            autoCreateSpelunkerMinimumBoxes,
            "Spelunker Potion"));
        body.Children.Add(BuildMinimumSelector(
            AutoCreateResourceMinimum.Potions,
            AutoCreateResourceMinimum.NormalizePotions(ac.ResourceFilterFeatherfallPotionMinimum),
            autoCreateFeatherfallMinimumBoxes,
            "Featherfall Potion"));

        // Background world pool
        autoCreateWorldPoolBox = WpfSettingsUi.CreateToggleSwitch(ac.EnableWorldPool);
        autoCreateWorldPoolBox.Checked += (_, _) => UpdateWorldPoolAvailability();
        autoCreateWorldPoolBox.Unchecked += (_, _) => UpdateWorldPoolAvailability();
        autoCreateWorldPoolTargetBox = WpfSettingsUi.CreateNumberBox(ac.WorldPoolTargetCount, 1, 50);

        body.Children.Add(WpfSettingsUi.CreateSubHeader(Localize("Background world generation")));
        WpfSettingsUi.AddSettingRow(body, Localize("Background world pool"), autoCreateWorldPoolBox);
        WpfSettingsUi.AddSettingRow(body, Localize("World pool size"), autoCreateWorldPoolTargetBox);

        // Force keep all files
        autoCreatePreserveExistingSavesBox = WpfSettingsUi.CreateToggleSwitch(ac.PreserveExistingSaves);
        body.Children.Add(WpfSettingsUi.CreateSubHeader(Localize("Force keep all files")));
        WpfSettingsUi.AddSettingRow(
            body,
            Localize("Enabled"),
            autoCreatePreserveExistingSavesBox,
            isLast: true,
            description: Localize("When enabled, world creation will not delete any files. This can leave many worlds and players to clean up manually."));

        UpdateSpecialSeedAvailability();
        UpdateWorldPoolAvailability();
        UpdatePostGenerationFilterAvailability();
        parent.Children.Add(card);
    }

    private FrameworkElement BuildSpecialSeedSelector()
    {
        HashSet<string> selected = AutoCreateSpecialWorldSeed.ParseList(Draft.Automation.AutoCreate.SpecialSeeds)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        autoCreateSpecialSeedBoxes.Clear();

        var grid = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 6) };
        foreach (string seed in AutoCreateSpecialWorldSeed.All)
        {
            ToggleButton chip = WpfSettingsUi.CreateChipToggle(Localize(seed), selected.Contains(seed));
            chip.Margin = new Thickness(0, 0, 5, 5);
            void OnToggle(object s, RoutedEventArgs e)
            {
                if (string.Equals(seed, AutoCreateSpecialWorldSeed.Zenith, StringComparison.OrdinalIgnoreCase))
                {
                    UpdateSpecialSeedAvailability();
                }
                else
                {
                    UpdatePostGenerationFilterAvailability();
                }
            }

            chip.Checked += OnToggle;
            chip.Unchecked += OnToggle;
            autoCreateSpecialSeedBoxes[seed] = chip;
            grid.Children.Add(chip);
        }

        return grid;
    }

    private FrameworkElement BuildZenithStarCatchStageSelector()
    {
        autoCreateZenithStarCatchStageBoxes.Clear();
        string selectedStopStage = AutoCreateZenithStarCatchStage.Normalize(Draft.Automation.AutoCreate.ZenithStarCatchStopStage);
        var grid = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 4) };
        foreach (string stage in AutoCreateZenithStarCatchStage.All)
        {
            ToggleButton chip = WpfSettingsUi.CreateChipToggle(
                Localize(stage),
                AutoCreateZenithStarCatchStage.Includes(selectedStopStage, stage));
            chip.Margin = new Thickness(0, 0, 5, 5);
            chip.Checked += (_, _) => SelectZenithStarCatchStage(stage);
            chip.Unchecked += (_, _) => SelectZenithStarCatchStage(stage);
            autoCreateZenithStarCatchStageBoxes[stage] = chip;
            grid.Children.Add(chip);
        }

        ApplyZenithStarCatchStageSelection(selectedStopStage);
        return grid;
    }

    private FrameworkElement BuildPyramidItemSelector()
    {
        HashSet<string> selected = AutoCreatePyramidFilterItem.FromMask(Draft.Automation.AutoCreate.PyramidFilterItemMask)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        autoCreatePyramidItemBoxes.Clear();

        var options = new List<ToggleButton>();
        foreach (string item in AutoCreatePyramidFilterItem.All)
        {
            ToggleButton chip = WpfSettingsUi.CreateChipToggle(Localize(item), selected.Contains(item));
            autoCreatePyramidItemBoxes[item] = chip;
            options.Add(chip);
        }

        return CreateFilterRow(autoCreatePyramidFilterBox, options);
    }

    private FrameworkElement BuildPyramidDepthSelector()
    {
        selectedPyramidDepth = AutoCreatePyramidFilterDepth.Normalize(Draft.Automation.AutoCreate.PyramidMaximumDepth);
        autoCreatePyramidDepthBoxes.Clear();
        ToggleButton toggle = null!;
        var options = new List<ToggleButton>();

        foreach (int value in AutoCreatePyramidFilterDepth.All)
        {
            ToggleButton chip = WpfSettingsUi.CreateChipToggle(
                Localize(AutoCreatePyramidFilterDepth.Label(value)),
                selectedPyramidDepth > 0 && (value == 0 || value <= selectedPyramidDepth));
            autoCreatePyramidDepthBoxes[value] = chip;

            void OnToggle(object s, RoutedEventArgs e)
            {
                if (updatingPyramidDepth)
                {
                    return;
                }

                selectedPyramidDepth = value == 0
                    ? (chip.IsChecked == true ? 30 : 0)
                    : value;
                updatingPyramidDepth = true;
                try
                {
                    foreach ((int depth, ToggleButton box) in autoCreatePyramidDepthBoxes)
                    {
                        box.IsChecked = selectedPyramidDepth > 0 && (depth == 0 || depth <= selectedPyramidDepth);
                    }
                }
                finally
                {
                    updatingPyramidDepth = false;
                }

                UpdatePostGenerationFilterAvailability();
            }

            chip.Checked += OnToggle;
            chip.Unchecked += OnToggle;

            if (value == 0)
            {
                toggle = chip;
            }
            else
            {
                options.Add(chip);
            }
        }

        return CreateFilterRow(toggle, options);
    }

    private FrameworkElement BuildCrimsonDistanceSelector()
    {
        autoCreateCrimsonDistanceBoxes.Clear();
        string selectedDistance = AutoCreateCrimsonDistance.Normalize(Draft.Automation.AutoCreate.CrimsonDistance);
        var options = new List<ToggleButton>();
        foreach (string distance in AutoCreateCrimsonDistance.All)
        {
            ToggleButton chip = WpfSettingsUi.CreateChipToggle(
                Localize(distance),
                AutoCreateCrimsonDistance.Includes(selectedDistance, distance));
            chip.Checked += (_, _) => SelectCrimsonDistance(distance);
            chip.Unchecked += (_, _) => SelectCrimsonDistance(distance);
            autoCreateCrimsonDistanceBoxes[distance] = chip;
            options.Add(chip);
        }

        ApplyCrimsonDistanceSelection(selectedDistance);
        return CreateFilterRow(autoCreateCrimsonBetweenDungeonAndSpawnBox, options);
    }

    private FrameworkElement BuildJungleRouteDepthSelector()
    {
        autoCreateJungleRouteDepthBoxes.Clear();
        string selectedDepth = AutoCreateJungleRouteDepth.Normalize(Draft.Automation.AutoCreate.JungleRouteDepth);
        var options = new List<ToggleButton>();
        foreach (string depth in AutoCreateJungleRouteDepth.All)
        {
            ToggleButton chip = WpfSettingsUi.CreateChipToggle(
                Localize(depth),
                AutoCreateJungleRouteDepth.Includes(selectedDepth, depth));
            chip.Checked += (_, _) => SelectJungleRouteDepth(depth, true);
            chip.Unchecked += (_, _) => SelectJungleRouteDepth(depth, false);
            autoCreateJungleRouteDepthBoxes[depth] = chip;
            options.Add(chip);
        }

        ApplyJungleRouteDepthSelection(selectedDepth);
        return CreateFilterRow(autoCreateJungleRouteDepthBox, options);
    }

    private FrameworkElement BuildResourceItemSelector()
    {
        int selectedMask = AutoCreateResourceFilterItem.NormalizeMask(Draft.Automation.AutoCreate.ResourceFilterItemMask);
        autoCreateResourceItemBoxes.Clear();

        var panel = new StackPanel { Orientation = WpfOrientation.Vertical };
        foreach (string item in AutoCreateResourceFilterItem.All)
        {
            ToggleButton chip = WpfSettingsUi.CreateChipToggle(
                Localize(item),
                (selectedMask & AutoCreateResourceFilterItem.Mask(item)) != 0);
            autoCreateResourceItemBoxes[item] = chip;
            panel.Children.Add(CreateFilterRow(chip, []));
        }

        return panel;
    }

    private FrameworkElement BuildItemDistanceSelector(
        IReadOnlyList<int> values,
        Func<int, string> label,
        int selected,
        Dictionary<int, ToggleButton> boxes)
    {
        bool updating = false;
        boxes.Clear();
        ToggleButton toggle = null!;
        var options = new List<ToggleButton>();

        foreach (int value in values)
        {
            ToggleButton chip = WpfSettingsUi.CreateChipToggle(
                Localize(label(value)),
                selected > 0 && value <= selected);
            boxes[value] = chip;

            void OnToggle(object s, RoutedEventArgs e)
            {
                if (updating)
                {
                    return;
                }

                selected = value == 0
                    ? (chip.IsChecked == true ? values[^1] : 0)
                    : value;
                updating = true;
                try
                {
                    foreach ((int distance, ToggleButton box) in boxes)
                    {
                        box.IsChecked = selected > 0 && distance <= selected;
                    }
                }
                finally
                {
                    updating = false;
                }

                UpdatePostGenerationFilterAvailability();
            }

            chip.Checked += OnToggle;
            chip.Unchecked += OnToggle;

            if (value == 0)
            {
                toggle = chip;
            }
            else
            {
                options.Add(chip);
            }
        }

        return CreateFilterRow(toggle, options);
    }

    private FrameworkElement BuildMinimumSelector(
        IReadOnlyList<int> values,
        int selectedMinimum,
        Dictionary<int, ToggleButton> boxes,
        string nameKey)
    {
        boxes.Clear();
        ToggleButton toggle = null!;
        var options = new List<ToggleButton>();

        for (int i = 0; i < values.Count; i++)
        {
            int value = values[i];
            string text = value == 0
                ? Localize(nameKey)
                : i == values.Count - 1
                    ? $"{value.ToString(CultureInfo.InvariantCulture)}+"
                    : value.ToString(CultureInfo.InvariantCulture);
            ToggleButton chip = WpfSettingsUi.CreateChipToggle(
                text,
                selectedMinimum > 0 && (value == 0 || value >= selectedMinimum));
            chip.Tag = text;
            chip.Checked += (_, _) => SelectMinimum(value, true, values, boxes);
            chip.Unchecked += (_, _) => SelectMinimum(value, false, values, boxes);
            boxes[value] = chip;

            if (value == 0)
            {
                toggle = chip;
            }
            else
            {
                options.Add(chip);
            }
        }

        ApplyMinimumSelection(selectedMinimum, boxes);
        return CreateFilterRow(toggle, options);
    }

    private static Grid CreateFilterRow(ToggleButton primaryToggle, IReadOnlyList<ToggleButton> optionButtons)
    {
        Grid grid = WpfSettingsUi.CreateColumnGrid(
            new GridLength(162),
            new GridLength(14),
            new GridLength(1, GridUnitType.Star));
        grid.Margin = new Thickness(0, 4, 0, 2);

        Grid.SetColumn(primaryToggle, 0);
        grid.Children.Add(primaryToggle);

        if (optionButtons.Count > 0)
        {
            var optionsGrid = new UniformGrid { Columns = optionButtons.Count };
            for (int i = 0; i < optionButtons.Count; i++)
            {
                ToggleButton btn = optionButtons[i];
                btn.Margin = i == optionButtons.Count - 1 ? new Thickness(0) : new Thickness(0, 0, 5, 0);
                optionsGrid.Children.Add(btn);
            }

            Grid.SetColumn(optionsGrid, 2);
            grid.Children.Add(optionsGrid);
        }

        return grid;
    }

    private void BuildLoadWorldCard(StackPanel parent)
    {
        practiceSlotControls.Clear();
        var card = WpfSettingsUi.CreateCard(
            Localize("Load World"),
            out StackPanel body,
            subtitle: Localize("Load World copies the selected player and/or world files to Terraria's save folder, then opens Single Player."),
            contentPadding: new Thickness(14, 6, 14, 8));

        body.Children.Add(WpfSettingsUi.CreateNoticeBanner(
            Localize("Do not choose players or worlds in the default save location."),
            isWarning: true));

        Grid headerGrid = CreatePracticeSlotGrid();
        TextBlock hName = WpfSettingsUi.CreateTableHeaderText(Localize("Name"), TextAlignment.Center);
        hName.Margin = new Thickness(0, 0, 6, 0);
        TextBlock hPlayer = WpfSettingsUi.CreateTableHeaderText(Localize("Player file"), TextAlignment.Center);
        hPlayer.Margin = new Thickness(0, 0, 8, 0);
        TextBlock hWorld = WpfSettingsUi.CreateTableHeaderText(Localize("World file"), TextAlignment.Center);
        Grid.SetColumn(hName, 1);
        Grid.SetColumn(hPlayer, 2);
        Grid.SetColumnSpan(hPlayer, 2);
        Grid.SetColumn(hWorld, 4);
        Grid.SetColumnSpan(hWorld, 2);
        headerGrid.Children.Add(hName);
        headerGrid.Children.Add(hPlayer);
        headerGrid.Children.Add(hWorld);
        body.Children.Add(WpfSettingsUi.CreateTableHeaderContainer(headerGrid));

        IReadOnlyList<PracticeWorldSlot> slots = Draft.PracticeWorlds.Slots;
        for (int i = 0; i < PracticeWorldSettings.SlotCount; i++)
        {
            PracticeWorldSlot slot = i < slots.Count ? slots[i] : new PracticeWorldSlot();
            body.Children.Add(CreatePracticeSlotRow(i, slot, isLast: i == PracticeWorldSettings.SlotCount - 1));
        }

        parent.Children.Add(card);
    }

    private static Grid CreatePracticeSlotGrid()
    {
        return WpfSettingsUi.CreateColumnGrid(
            new GridLength(30),
            new GridLength(136),
            new GridLength(1, GridUnitType.Star),
            GridLength.Auto,
            new GridLength(1, GridUnitType.Star),
            GridLength.Auto);
    }

    private Border CreatePracticeSlotRow(int index, PracticeWorldSlot slot, bool isLast)
    {
        Grid grid = CreatePracticeSlotGrid();

        var keycap = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = WpfHorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        keycap.SetResourceReference(Border.BackgroundProperty, "SurfaceRaisedBrush");
        keycap.SetResourceReference(Border.BorderBrushProperty, "ControlElevationBorderBrush");
        var keyText = new TextBlock
        {
            Text = index == 9 ? "0" : (index + 1).ToString(CultureInfo.InvariantCulture),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        keyText.SetResourceReference(TextBlock.FontFamilyProperty, "MonoFontFamily");
        keyText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        keycap.Child = keyText;

        WpfTextBox nameBox = WpfSettingsUi.CreateTextBox(slot.Name);
        nameBox.Margin = new Thickness(0, 0, 6, 0);

        WpfTextBox playerBox = WpfSettingsUi.CreateTextBox(slot.PlayerFilePath);
        playerBox.Margin = new Thickness(0, 0, 5, 0);

        WpfButton playerBrowse = WpfSettingsUi.CreateButton(Localize("Browse"), minWidth: 58);
        playerBrowse.Margin = new Thickness(0, 0, 8, 0);
        playerBrowse.Click += (_, _) => Dialogs.PickFile(playerBox, "Choose player file", "Terraria player|*.plr|All files|*.*");

        WpfTextBox worldBox = WpfSettingsUi.CreateTextBox(slot.WorldFilePath);
        worldBox.Margin = new Thickness(0, 0, 5, 0);

        WpfButton worldBrowse = WpfSettingsUi.CreateButton(Localize("Browse"), minWidth: 58);
        worldBrowse.Click += (_, _) => Dialogs.PickFile(worldBox, "Choose world file", "Terraria world|*.wld|All files|*.*");

        Grid.SetColumn(keycap, 0);
        Grid.SetColumn(nameBox, 1);
        Grid.SetColumn(playerBox, 2);
        Grid.SetColumn(playerBrowse, 3);
        Grid.SetColumn(worldBox, 4);
        Grid.SetColumn(worldBrowse, 5);
        grid.Children.Add(keycap);
        grid.Children.Add(nameBox);
        grid.Children.Add(playerBox);
        grid.Children.Add(playerBrowse);
        grid.Children.Add(worldBox);
        grid.Children.Add(worldBrowse);

        practiceSlotControls.Add(new PracticeSlotControls(nameBox, playerBox, worldBox));
        return WpfSettingsUi.CreateTableRowContainer(grid, showBottomBorder: !isLast);
    }

    private void BuildDelaysCard(StackPanel parent)
    {
        AutoCreateWorldSettings ac = Draft.Automation.AutoCreate;
        autoCreateWindowActivationDelayBox = WpfSettingsUi.CreateNumberBox(ac.WindowActivationDelayMilliseconds, 0, 5000);
        autoCreateClickFocusDelayBox = WpfSettingsUi.CreateNumberBox(ac.ClickFocusDelayMilliseconds, 0, 5000);
        autoCreateInputPressDurationBox = WpfSettingsUi.CreateNumberBox(ac.InputPressDurationMilliseconds, 1, 5000);
        autoCreateShortActionDelayBox = WpfSettingsUi.CreateNumberBox(ac.ShortActionDelayMilliseconds, 0, 5000);
        autoCreateMenuActionDelayBox = WpfSettingsUi.CreateNumberBox(ac.MenuActionDelayMilliseconds, 0, 5000);

        var card = WpfSettingsUi.CreateCard(Localize("Delays (for Terraria 1.4.4)"), out StackPanel body);
        WpfSettingsUi.AddSettingRow(body, Localize("Initial wait ms"), autoCreateWindowActivationDelayBox);
        WpfSettingsUi.AddSettingRow(body, Localize("Pre-click wait ms"), autoCreateClickFocusDelayBox);
        WpfSettingsUi.AddSettingRow(body, Localize("Mouse / key duration ms"), autoCreateInputPressDurationBox);
        WpfSettingsUi.AddSettingRow(body, Localize("Adjacent operation delay ms"), autoCreateShortActionDelayBox);
        WpfSettingsUi.AddSettingRow(body, Localize("Cross-menu operation delay ms"), autoCreateMenuActionDelayBox, isLast: true);
        parent.Children.Add(card);
    }

    private void UpdateSpecialSeedAvailability()
    {
        bool zenithSelected = autoCreateSpecialSeedBoxes.TryGetValue(AutoCreateSpecialWorldSeed.Zenith, out ToggleButton? zenithBox) &&
            zenithBox.IsChecked == true;

        foreach ((string seed, ToggleButton button) in autoCreateSpecialSeedBoxes)
        {
            if (AutoCreateSpecialWorldSeed.IsZenithDependency(seed))
            {
                if (zenithSelected)
                {
                    button.IsChecked = false;
                }

                button.IsEnabled = !zenithSelected;
            }
        }

        UpdateZenithStarCatchAvailability();
        UpdatePostGenerationFilterAvailability();
    }

    private void UpdatePyramidItemAvailability()
    {
        bool filtersEnabled = autoCreateCheatsBox.IsChecked == true &&
            string.IsNullOrWhiteSpace(autoCreateFixedSeedBox.Text) &&
            AutoCreateAdvancedFilterEligibility.IsEligible(
                GetSelectedOption(autoCreateWorldSizeBox, AutoCreateWorldSize.Small),
                GetSelectedOption(autoCreateWorldEvilBox, AutoCreateWorldEvil.Crimson),
                null,
                null);

        autoCreatePyramidFilterBox.IsEnabled = filtersEnabled;
        bool pyramidEnabled = filtersEnabled && autoCreatePyramidFilterBox.IsChecked == true;
        foreach (ToggleButton button in autoCreatePyramidItemBoxes.Values)
        {
            button.IsEnabled = pyramidEnabled;
        }

        UpdateMinimumAvailability(autoCreatePyramidCoinPileMinimumBoxes, pyramidEnabled);
        UpdateMinimumAvailability(autoCreatePyramidDepthBoxes, pyramidEnabled);
    }

    private void UpdatePostGenerationFilterAvailability()
    {
        if (updatingPostGenerationFilterAvailability)
        {
            return;
        }

        updatingPostGenerationFilterAvailability = true;
        try
        {
            string selectedWorldSize = GetSelectedOption(autoCreateWorldSizeBox, AutoCreateWorldSize.Small);
            string selectedWorldEvil = GetSelectedOption(autoCreateWorldEvilBox, AutoCreateWorldEvil.Crimson);
            string selectedSpecialSeeds = string.Join(
                "|",
                AutoCreateSpecialWorldSeed.All.Where(seed =>
                    autoCreateSpecialSeedBoxes.TryGetValue(seed, out ToggleButton? button) && button.IsChecked == true));
            bool cheatsEnabled = autoCreateCheatsBox.IsChecked == true;
            bool eligible = string.IsNullOrWhiteSpace(autoCreateFixedSeedBox.Text) &&
                AutoCreateAdvancedFilterEligibility.IsEligible(
                    selectedWorldSize,
                    selectedWorldEvil,
                    selectedSpecialSeeds,
                    autoCreateSecretSeedsBox.Text);
            bool supportsAdvanced = cheatsEnabled && eligible;

            bool supportsCrimson = supportsAdvanced && AutoCreateAdvancedFilterEligibility.IsCrimsonEligible(selectedWorldEvil);
            autoCreateCrimsonBetweenDungeonAndSpawnBox.IsEnabled = supportsCrimson;
            bool crimsonDistEnabled = supportsCrimson && autoCreateCrimsonBetweenDungeonAndSpawnBox.IsChecked == true;
            foreach (ToggleButton button in autoCreateCrimsonDistanceBoxes.Values)
            {
                button.IsEnabled = crimsonDistEnabled;
            }

            autoCreateJungleRouteDepthBox.IsEnabled = supportsAdvanced;
            bool jungleEnabled = supportsAdvanced && autoCreateJungleRouteDepthBox.IsChecked == true;
            foreach (ToggleButton button in autoCreateJungleRouteDepthBoxes.Values)
            {
                button.IsEnabled = jungleEnabled;
            }

            foreach (ToggleButton button in autoCreateResourceItemBoxes.Values)
            {
                button.IsEnabled = supportsAdvanced;
            }

            UpdateMinimumAvailability(autoCreateLifeCrystalMinimumBoxes, supportsAdvanced);
            UpdateMinimumAvailability(autoCreateStarfuryDistanceBoxes, supportsAdvanced);
            UpdateMinimumAvailability(autoCreateFinchStaffDistanceBoxes, supportsAdvanced);
            UpdateMinimumAvailability(autoCreateSpelunkerMinimumBoxes, supportsAdvanced);
            UpdateMinimumAvailability(autoCreateFeatherfallMinimumBoxes, supportsAdvanced);
            UpdatePyramidItemAvailability();
        }
        finally
        {
            updatingPostGenerationFilterAvailability = false;
        }
    }

    private static void UpdateMinimumAvailability(IReadOnlyDictionary<int, ToggleButton> boxes, bool supported)
    {
        bool enabled = supported && boxes.TryGetValue(0, out ToggleButton? toggle) && toggle.IsChecked == true;
        foreach ((int value, ToggleButton button) in boxes)
        {
            button.IsEnabled = supported && (value == 0 || enabled);
        }
    }

    private void SelectMinimum(int selectedMinimum, bool selected, IReadOnlyList<int> values, Dictionary<int, ToggleButton> boxes)
    {
        if (updatingResourceMinimumSelection)
        {
            return;
        }

        int normalized = selectedMinimum == 0
            ? (selected ? values.FirstOrDefault(v => v > 0) : 0)
            : (values.Contains(selectedMinimum) ? selectedMinimum : 0);
        ApplyMinimumSelection(normalized, boxes);
        UpdatePostGenerationFilterAvailability();
    }

    private void ApplyMinimumSelection(int selectedMinimum, Dictionary<int, ToggleButton> boxes)
    {
        updatingResourceMinimumSelection = true;
        try
        {
            bool enabled = selectedMinimum > 0;
            foreach ((int value, ToggleButton button) in boxes)
            {
                button.IsChecked = enabled && (value == 0 || value >= selectedMinimum);
            }
        }
        finally
        {
            updatingResourceMinimumSelection = false;
        }
    }

    private static int GetSelectedMinimum(IReadOnlyDictionary<int, ToggleButton> boxes, IReadOnlyList<int> values)
    {
        if (!boxes.TryGetValue(0, out ToggleButton? toggle) || toggle.IsChecked != true)
        {
            return 0;
        }

        foreach (int value in values.Where(v => v > 0))
        {
            if (boxes.TryGetValue(value, out ToggleButton? button) && button.IsChecked == true)
            {
                return value;
            }
        }

        return 0;
    }

    private static int GetSelectedMaximum(IReadOnlyDictionary<int, ToggleButton> boxes) =>
        boxes.Where(pair => pair.Value.IsChecked == true).Select(pair => pair.Key).DefaultIfEmpty(0).Max();

    private void SelectCrimsonDistance(string selectedDistance)
    {
        if (updatingCrimsonDistanceSelection)
        {
            return;
        }

        ApplyCrimsonDistanceSelection(selectedDistance);
    }

    private void ApplyCrimsonDistanceSelection(string selectedDistance)
    {
        updatingCrimsonDistanceSelection = true;
        try
        {
            foreach ((string distance, ToggleButton button) in autoCreateCrimsonDistanceBoxes)
            {
                button.IsChecked = AutoCreateCrimsonDistance.Includes(selectedDistance, distance);
            }
        }
        finally
        {
            updatingCrimsonDistanceSelection = false;
        }
    }

    private string GetSelectedCrimsonDistance()
    {
        for (int i = AutoCreateCrimsonDistance.All.Length - 1; i >= 0; i--)
        {
            string distance = AutoCreateCrimsonDistance.All[i];
            if (autoCreateCrimsonDistanceBoxes.TryGetValue(distance, out ToggleButton? button) && button.IsChecked == true)
            {
                return distance;
            }
        }

        return AutoCreateCrimsonDistance.Default;
    }

    private void SelectJungleRouteDepth(string selectedDepth, bool selected)
    {
        if (updatingJungleRouteDepthSelection)
        {
            return;
        }

        string normalized = selectedDepth == AutoCreateJungleRouteDepth.None
            ? (selected ? AutoCreateJungleRouteDepth.Medium : AutoCreateJungleRouteDepth.None)
            : AutoCreateJungleRouteDepth.Normalize(selectedDepth);
        ApplyJungleRouteDepthSelection(normalized);
        UpdatePostGenerationFilterAvailability();
    }

    private void ApplyJungleRouteDepthSelection(string selectedDepth)
    {
        updatingJungleRouteDepthSelection = true;
        try
        {
            string normalized = AutoCreateJungleRouteDepth.Normalize(selectedDepth);
            bool enabled = normalized != AutoCreateJungleRouteDepth.None;
            autoCreateJungleRouteDepthBox.IsChecked = enabled;
            foreach ((string depth, ToggleButton button) in autoCreateJungleRouteDepthBoxes)
            {
                button.IsChecked = enabled && AutoCreateJungleRouteDepth.Includes(normalized, depth);
            }
        }
        finally
        {
            updatingJungleRouteDepthSelection = false;
        }
    }

    private string GetSelectedJungleRouteDepth()
    {
        if (autoCreateJungleRouteDepthBox.IsChecked != true)
        {
            return AutoCreateJungleRouteDepth.None;
        }

        foreach (string depth in AutoCreateJungleRouteDepth.All)
        {
            if (autoCreateJungleRouteDepthBoxes.TryGetValue(depth, out ToggleButton? button) && button.IsChecked == true)
            {
                return depth;
            }
        }

        return AutoCreateJungleRouteDepth.None;
    }

    private void SelectZenithStarCatchStage(string selectedStopStage)
    {
        if (updatingZenithStarCatchStageSelection)
        {
            return;
        }

        ApplyZenithStarCatchStageSelection(selectedStopStage);
    }

    private void ApplyZenithStarCatchStageSelection(string selectedStopStage)
    {
        updatingZenithStarCatchStageSelection = true;
        try
        {
            foreach ((string stage, ToggleButton button) in autoCreateZenithStarCatchStageBoxes)
            {
                button.IsChecked = AutoCreateZenithStarCatchStage.Includes(selectedStopStage, stage);
            }
        }
        finally
        {
            updatingZenithStarCatchStageSelection = false;
        }
    }

    private string GetSelectedZenithStarCatchStopStage()
    {
        for (int i = AutoCreateZenithStarCatchStage.All.Length - 1; i >= 0; i--)
        {
            string stage = AutoCreateZenithStarCatchStage.All[i];
            if (autoCreateZenithStarCatchStageBoxes.TryGetValue(stage, out ToggleButton? button) && button.IsChecked == true)
            {
                return stage;
            }
        }

        return AutoCreateZenithStarCatchStage.Default;
    }

    private void UpdateZenithStarCatchAvailability()
    {
        bool zenithSelected = autoCreateSpecialSeedBoxes.TryGetValue(AutoCreateSpecialWorldSeed.Zenith, out ToggleButton? zenithBox) &&
            zenithBox.IsChecked == true;

        autoCreateZenithStarCatchBox.IsEnabled = zenithSelected;
        bool enabled = zenithSelected && autoCreateZenithStarCatchBox.IsChecked == true;
        foreach (ToggleButton button in autoCreateZenithStarCatchStageBoxes.Values)
        {
            button.IsEnabled = enabled;
        }

        autoCreateZenithStarCatchSpeedBar.IsEnabled = enabled;
        autoCreateZenithStarCatchSpeedValueLabel.Opacity = enabled ? 1d : 0.45d;
    }

    private void UpdateWorldPoolAvailability()
    {
        autoCreateWorldPoolTargetBox.IsEnabled = autoCreateWorldPoolBox.IsChecked == true;
    }

    private void UpdateZenithStarCatchSpeedLabel()
    {
        autoCreateZenithStarCatchSpeedValueLabel.Text = AutoCreateZenithStarCatchSpeed.FormatMultiplier(
            (int)Math.Round(autoCreateZenithStarCatchSpeedBar.Value));
    }

    private WpfComboBox CreateOptionBox(IEnumerable<string> options, string selected, double width)
    {
        WpfComboBox comboBox = WpfSettingsUi.CreateComboBox(width);
        foreach (string option in options)
        {
            comboBox.Items.Add(new LocalizedOption(option, Localize(option)));
        }

        comboBox.SelectedItem = comboBox.Items
            .Cast<LocalizedOption>()
            .FirstOrDefault(o => string.Equals(o.Value, selected, StringComparison.OrdinalIgnoreCase));
        if (comboBox.SelectedIndex < 0 && comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }

        return comboBox;
    }

    private static string GetSelectedOption(WpfComboBox comboBox, string fallback)
    {
        return comboBox.SelectedItem switch
        {
            LocalizedOption option => option.Value,
            string value => value,
            _ => fallback
        };
    }

    internal sealed record PracticeSlotControls(
        WpfTextBox NameBox,
        WpfTextBox PlayerFilePathBox,
        WpfTextBox WorldFilePathBox);

    private sealed record LocalizedOption(string Value, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }
}
