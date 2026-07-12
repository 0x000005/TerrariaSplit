using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed partial class AutomationSettingsPage : SettingsPageBase
{
    private static readonly Color SpecialSeedButtonHover = Color.FromArgb(40, 48, 53);
    private static readonly Color SpecialSeedButtonSelectedHover = Color.FromArgb(58, 93, 88);
    private static readonly Color SpecialSeedButtonDown = Color.FromArgb(34, 41, 46);
    private static readonly Color SpecialSeedButtonSelectedDown = Color.FromArgb(46, 76, 71);

    private readonly TextBox autoCreatePlayerNameBox = new();
    private readonly TextBox autoCreatePlayerTemplateCodeBox = new();
    private readonly ThemedDropDownList autoCreatePlayerDifficultyBox = new();
    private readonly CheckBox autoCreatePreserveExistingSavesBox = new();
    private readonly ThemedDropDownList autoCreateWorldSizeBox = new();
    private readonly ThemedDropDownList autoCreateWorldDifficultyBox = new();
    private readonly ThemedDropDownList autoCreateWorldEvilBox = new();
    private readonly TextBox autoCreateSecretSeedsBox = new();
    private readonly CheckBox autoCreateZenithStarCatchBox = new();
    private readonly ThemedSlider autoCreateZenithStarCatchSpeedBar = new();
    private readonly Label autoCreateZenithStarCatchSpeedValueLabel = new();
    private readonly CheckBox autoCreatePyramidFilterBox = new();
    private readonly CheckBox autoCreateCrimsonBetweenDungeonAndSpawnBox = new();
    private readonly CheckBox autoCreateReturnToMainMenuOnFilterFailureBox = new();
    private readonly Dictionary<string, CheckBox> autoCreatePyramidItemBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly CheckBox autoCreateWorldPoolBox = new();
    private readonly TextBox autoCreateWorldPoolTargetBox = new();
    private readonly TextBox autoCreateShortActionDelayBox = new();
    private readonly TextBox autoCreateMenuActionDelayBox = new();
    private readonly TextBox autoCreatePyramidFilterPostDelayBox = new();
    private readonly TextBox autoCreateWindowActivationDelayBox = new();
    private readonly TextBox autoCreateClickFocusDelayBox = new();
    private readonly TextBox autoCreateInputPressDurationBox = new();
    private readonly Dictionary<string, CheckBox> autoCreateSpecialSeedBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CheckBox> autoCreateZenithStarCatchStageBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PracticeSlotControls> practiceSlotControls = new();
    private bool updatingZenithStarCatchStageSelection;

    public override SettingsPageId Id => SettingsPageId.Automation;

    internal IReadOnlyList<PracticeSlotControls> PracticeSlots => practiceSlotControls;
    internal IReadOnlyDictionary<string, CheckBox> AutoCreateSpecialSeedBoxes => autoCreateSpecialSeedBoxes;
    internal CheckBox AutoCreatePreserveExistingSavesBox => autoCreatePreserveExistingSavesBox;
    internal CheckBox AutoCreateZenithStarCatchBox => autoCreateZenithStarCatchBox;
    internal IReadOnlyDictionary<string, CheckBox> AutoCreateZenithStarCatchStageBoxes => autoCreateZenithStarCatchStageBoxes;
    internal ThemedSlider AutoCreateZenithStarCatchSpeedBar => autoCreateZenithStarCatchSpeedBar;
    internal CheckBox AutoCreatePyramidFilterBox => autoCreatePyramidFilterBox;
    internal CheckBox AutoCreateCrimsonBetweenDungeonAndSpawnBox => autoCreateCrimsonBetweenDungeonAndSpawnBox;
    internal CheckBox AutoCreateReturnToMainMenuOnFilterFailureBox => autoCreateReturnToMainMenuOnFilterFailureBox;
    internal IReadOnlyDictionary<string, CheckBox> AutoCreatePyramidItemBoxes => autoCreatePyramidItemBoxes;
    internal CheckBox AutoCreateWorldPoolBox => autoCreateWorldPoolBox;
    internal TextBox AutoCreateWorldPoolTargetBox => autoCreateWorldPoolTargetBox;
    internal TextBox AutoCreateSecretSeedsBox => autoCreateSecretSeedsBox;

    protected override Control BuildPage(SettingsPageContext context)
    {
        return context.BuildScrollPage(BuildSections);
    }

    public override void Apply(AppSettings settings)
    {
        settings.Automation.AutoCreate.PlayerName = autoCreatePlayerNameBox.Text.Trim();
        settings.Automation.AutoCreate.PlayerTemplateCode = autoCreatePlayerTemplateCodeBox.Text.Trim();
        settings.Automation.AutoCreate.PlayerDifficulty = AutoCreatePlayerDifficulty.Normalize(
            GetSelectedOption(autoCreatePlayerDifficultyBox, AutoCreatePlayerDifficulty.Softcore));
        settings.Automation.AutoCreate.PreserveExistingSaves = autoCreatePreserveExistingSavesBox.Checked;
        settings.Automation.AutoCreate.WorldSize = AutoCreateWorldSize.Normalize(
            GetSelectedOption(autoCreateWorldSizeBox, AutoCreateWorldSize.Medium));
        settings.Automation.AutoCreate.WorldDifficulty = AutoCreateWorldDifficulty.Normalize(
            GetSelectedOption(autoCreateWorldDifficultyBox, AutoCreateWorldDifficulty.Classic));
        settings.Automation.AutoCreate.WorldEvil = AutoCreateWorldEvil.Normalize(
            GetSelectedOption(autoCreateWorldEvilBox, AutoCreateWorldEvil.Random));
        string selectedSpecialSeeds = string.Join(
            "|",
            AutoCreateSpecialWorldSeed.All.Where(seed =>
                autoCreateSpecialSeedBoxes.TryGetValue(seed, out CheckBox? box) && box.Checked));
        settings.Automation.AutoCreate.SpecialSeeds = string.Join("|", AutoCreateSpecialWorldSeed.ParseList(selectedSpecialSeeds));
        settings.Automation.AutoCreate.SecretSeeds = autoCreateSecretSeedsBox.Text.Trim();
        settings.Automation.AutoCreate.EnableZenithStarCatch = autoCreateZenithStarCatchBox.Checked;
        settings.Automation.AutoCreate.ZenithStarCatchStopStage = GetSelectedZenithStarCatchStopStage();
        settings.Automation.AutoCreate.ZenithStarCatchSpeedSliderValue = AutoCreateZenithStarCatchSpeed.NormalizeSliderValue(autoCreateZenithStarCatchSpeedBar.Value);
        settings.Automation.AutoCreate.EnablePyramidFilter = autoCreatePyramidFilterBox.Checked;
        settings.Automation.AutoCreate.RequireCrimsonBetweenDungeonAndSpawn = autoCreateCrimsonBetweenDungeonAndSpawnBox.Checked;
        settings.Automation.AutoCreate.ReturnToMainMenuOnFilterFailure = autoCreateReturnToMainMenuOnFilterFailureBox.Checked;
        settings.Automation.AutoCreate.PyramidFilterItemMask = AutoCreatePyramidFilterItem.ToMask(
            AutoCreatePyramidFilterItem.All.Where(item =>
                autoCreatePyramidItemBoxes.TryGetValue(item, out CheckBox? box) && box.Checked));
        settings.Automation.AutoCreate.EnableWorldPool = autoCreateWorldPoolBox.Checked;
        settings.Automation.AutoCreate.WorldPoolTargetCount = SettingsValueParser.ParseIntBox(
            autoCreateWorldPoolTargetBox,
            AppSettingsDefaults.AutoCreate.WorldPoolTargetCount,
            1,
            50);
        settings.Automation.AutoCreate.ShortActionDelayMilliseconds = SettingsValueParser.ParseIntBox(
            autoCreateShortActionDelayBox,
            AppSettingsDefaults.AutoCreate.ShortActionDelayMilliseconds,
            0,
            5000);
        settings.Automation.AutoCreate.MenuActionDelayMilliseconds = SettingsValueParser.ParseIntBox(
            autoCreateMenuActionDelayBox,
            AppSettingsDefaults.AutoCreate.MenuActionDelayMilliseconds,
            0,
            5000);
        settings.Automation.AutoCreate.PyramidFilterPostDelayMilliseconds = SettingsValueParser.ParseIntBox(
            autoCreatePyramidFilterPostDelayBox,
            AppSettingsDefaults.AutoCreate.PyramidFilterPostDelayMilliseconds,
            0,
            5000);
        settings.Automation.AutoCreate.WindowActivationDelayMilliseconds = SettingsValueParser.ParseIntBox(
            autoCreateWindowActivationDelayBox,
            AppSettingsDefaults.AutoCreate.WindowActivationDelayMilliseconds,
            0,
            5000);
        settings.Automation.AutoCreate.ClickFocusDelayMilliseconds = SettingsValueParser.ParseIntBox(
            autoCreateClickFocusDelayBox,
            AppSettingsDefaults.AutoCreate.ClickFocusDelayMilliseconds,
            0,
            5000);
        settings.Automation.AutoCreate.InputPressDurationMilliseconds = SettingsValueParser.ParseIntBox(
            autoCreateInputPressDurationBox,
            AppSettingsDefaults.AutoCreate.InputPressDurationMilliseconds,
            1,
            5000);

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

    private void BuildSections(TableLayoutPanel parent)
    {
        UiTheme.StyleTextBox(autoCreatePlayerNameBox);
        autoCreatePlayerNameBox.Dock = DockStyle.Fill;
        autoCreatePlayerNameBox.Text = Draft.Automation.AutoCreate.PlayerName;
        autoCreatePlayerNameBox.PlaceholderText = Context.Localize("Empty = 1");

        UiTheme.StyleTextBox(autoCreatePlayerTemplateCodeBox);
        autoCreatePlayerTemplateCodeBox.Dock = DockStyle.Fill;
        autoCreatePlayerTemplateCodeBox.Multiline = true;
        autoCreatePlayerTemplateCodeBox.AcceptsReturn = true;
        autoCreatePlayerTemplateCodeBox.ScrollBars = ScrollBars.None;
        autoCreatePlayerTemplateCodeBox.Height = autoCreatePlayerTemplateCodeBox.Font.Height * 10 + 14;
        autoCreatePlayerTemplateCodeBox.Text = Draft.Automation.AutoCreate.PlayerTemplateCode;
        autoCreatePlayerTemplateCodeBox.PlaceholderText = Context.Localize("Empty = default character");

        ConfigureOptionBox(autoCreatePlayerDifficultyBox, AutoCreatePlayerDifficulty.All, Draft.Automation.AutoCreate.PlayerDifficulty);
        ConfigureCheckBox(autoCreatePreserveExistingSavesBox, Draft.Automation.AutoCreate.PreserveExistingSaves);
        ConfigureOptionBox(autoCreateWorldSizeBox, AutoCreateWorldSize.All, Draft.Automation.AutoCreate.WorldSize);
        autoCreateWorldSizeBox.SelectedIndexChanged += (_, _) => UpdatePostGenerationFilterAvailability();
        ConfigureOptionBox(autoCreateWorldDifficultyBox, AutoCreateWorldDifficulty.All, Draft.Automation.AutoCreate.WorldDifficulty);
        ConfigureOptionBox(autoCreateWorldEvilBox, AutoCreateWorldEvil.All, Draft.Automation.AutoCreate.WorldEvil);
        autoCreateWorldEvilBox.SelectedIndexChanged += (_, _) => UpdatePostGenerationFilterAvailability();
        ConfigureSeedListBox(autoCreateSecretSeedsBox, Draft.Automation.AutoCreate.SecretSeeds);
        ConfigureCheckBox(autoCreateZenithStarCatchBox, Draft.Automation.AutoCreate.EnableZenithStarCatch);
        autoCreateZenithStarCatchBox.CheckedChanged += (_, _) => UpdateZenithStarCatchAvailability();
        ConfigureZenithStarCatchSpeedBar(autoCreateZenithStarCatchSpeedBar, Draft.Automation.AutoCreate.ZenithStarCatchSpeedSliderValue);
        autoCreateZenithStarCatchSpeedBar.ValueChanged += (_, _) => UpdateZenithStarCatchSpeedLabel();
        ConfigureCheckBox(autoCreatePyramidFilterBox, Draft.Automation.AutoCreate.EnablePyramidFilter);
        autoCreatePyramidFilterBox.CheckedChanged += (_, _) => UpdatePyramidItemAvailability();
        ConfigureCheckBox(
            autoCreateCrimsonBetweenDungeonAndSpawnBox,
            Draft.Automation.AutoCreate.RequireCrimsonBetweenDungeonAndSpawn);
        autoCreateCrimsonBetweenDungeonAndSpawnBox.CheckedChanged += (_, _) => UpdatePostGenerationFilterAvailability();
        ConfigureCheckBox(
            autoCreateReturnToMainMenuOnFilterFailureBox,
            Draft.Automation.AutoCreate.ReturnToMainMenuOnFilterFailure);
        ConfigureCheckBox(autoCreateWorldPoolBox, Draft.Automation.AutoCreate.EnableWorldPool);
        autoCreateWorldPoolBox.CheckedChanged += (_, _) => UpdateWorldPoolAvailability();
        ConfigureNumberBox(autoCreateWorldPoolTargetBox, Draft.Automation.AutoCreate.WorldPoolTargetCount, 1, 50);
        ConfigureNumberBox(autoCreateShortActionDelayBox, Draft.Automation.AutoCreate.ShortActionDelayMilliseconds, 0, 5000);
        ConfigureNumberBox(autoCreateMenuActionDelayBox, Draft.Automation.AutoCreate.MenuActionDelayMilliseconds, 0, 5000);
        ConfigureNumberBox(autoCreatePyramidFilterPostDelayBox, Draft.Automation.AutoCreate.PyramidFilterPostDelayMilliseconds, 0, 5000);
        ConfigureNumberBox(autoCreateWindowActivationDelayBox, Draft.Automation.AutoCreate.WindowActivationDelayMilliseconds, 0, 5000);
        ConfigureNumberBox(autoCreateClickFocusDelayBox, Draft.Automation.AutoCreate.ClickFocusDelayMilliseconds, 0, 5000);
        ConfigureNumberBox(autoCreateInputPressDurationBox, Draft.Automation.AutoCreate.InputPressDurationMilliseconds, 1, 5000);
        UpdateWorldPoolAvailability();
        UpdatePyramidItemAvailability();
        UpdatePostGenerationFilterAvailability();

        AddCreateWorldSection(parent);
        AddEnterWorldSection(parent);
        AddDelaySection(parent);
    }

    private Control CreateFolderNoticeRow(string text, string buttonText, EventHandler openFolder, Color warningColor)
    {
        var row = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.Surface,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 8, 0, 8),
            Padding = Padding.Empty,
            RowCount = 1
        };
        UiTheme.EnableDoubleBuffering(row);
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Label label = Factory.CreateWrappedFieldLabel(
            text,
            warningColor);
        label.Dock = DockStyle.Fill;
        label.Margin = new Padding(0, 0, 12, 0);
        label.MinimumSize = new Size(0, 36);
        label.TextAlign = ContentAlignment.MiddleLeft;

        Button openButton = Factory.CreateSmallButton(buttonText);
        openButton.Width = 252;
        openButton.MinimumSize = new Size(252, 36);
        openButton.Margin = Padding.Empty;
        openButton.Click += openFolder;

        row.Controls.Add(label, 0, 0);
        row.Controls.Add(openButton, 1, 0);
        row.Layout += (_, _) => SettingsUiFactory.UpdateWrappedLabelHeight(label);
        return row;
    }

    private void AddCreateWorldSection(TableLayoutPanel parent)
    {
        Color warningColor = Color.FromArgb(255, 210, 120);
        TableLayoutPanel createSection = Factory.CreateSection("Create World");
        SettingsUiFactory.AddSectionControl(
            createSection,
            Factory.CreateWrappedFieldLabel(
                "Create World creates a world automatically by simulating mouse and keyboard input.",
                UiTheme.Text));
        SettingsUiFactory.AddSectionControl(
            createSection,
            CreateFolderNoticeRow(
                "By default, Create World moves non-favorite players and worlds to the backup folder before creating.",
                "Open save folder",
                (_, _) => Dialogs.OpenTerrariaSaveFolder(Context.Localize),
                warningColor));
        SettingsUiFactory.AddSectionControl(
            createSection,
            CreateFolderNoticeRow(
                "When existing saves are not preserved, the most recent 50 cleanup batches are kept in the backup folder.",
                "Open backup folder",
                (_, _) => Dialogs.OpenAutoCreateBackupFolder(Context.Localize),
                warningColor));
        SettingsUiFactory.AddSectionControl(
            createSection,
            Factory.CreateWrappedFieldLabel(
                "If clicks are too fast for your computer to respond, adjust the delay settings at the bottom of this page.",
                UiTheme.Text));

        SettingsUiFactory.AddSectionControl(createSection, Factory.CreateSubsectionLabel("Player options"));
        TableLayoutPanel createGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(360f));
        Factory.AddSettingRow(createGrid, "Player name", autoCreatePlayerNameBox);
        Factory.AddSettingRow(createGrid, "Player difficulty", autoCreatePlayerDifficultyBox);
        SettingsUiFactory.AddSectionControl(createSection, createGrid);
        SettingsUiFactory.AddSectionControl(createSection, Factory.CreateFieldLabel("Player code"));
        SettingsUiFactory.AddSectionControl(createSection, autoCreatePlayerTemplateCodeBox);

        SettingsUiFactory.AddSectionControl(createSection, Factory.CreateSubsectionLabel("World options"));
        TableLayoutPanel worldGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(360f));
        Factory.AddSettingRow(worldGrid, "World size", autoCreateWorldSizeBox);
        Factory.AddSettingRow(worldGrid, "World difficulty", autoCreateWorldDifficultyBox);
        Factory.AddSettingRow(worldGrid, "World evil", autoCreateWorldEvilBox);
        SettingsUiFactory.AddSectionControl(createSection, worldGrid);

        SettingsUiFactory.AddSectionControl(createSection, Factory.CreateFieldLabel("Special seeds"));
        SettingsUiFactory.AddSectionControl(createSection, CreateSpecialSeedSelector());

        TableLayoutPanel seedGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(360f));
        Factory.AddSettingRow(seedGrid, "Secret seed / fixed seed", autoCreateSecretSeedsBox);
        SettingsUiFactory.AddSectionControl(createSection, seedGrid);

        SettingsUiFactory.AddSectionControl(createSection, Factory.CreateSubsectionLabel("Zenith star catch"));
        TableLayoutPanel zenithStarCatchGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(360f));
        Factory.AddSettingRow(zenithStarCatchGrid, "Enabled", autoCreateZenithStarCatchBox);
        SettingsUiFactory.AddSectionControl(createSection, zenithStarCatchGrid);
        SettingsUiFactory.AddSectionControl(createSection, Factory.CreateFieldLabel("Stop after stage"));
        SettingsUiFactory.AddSectionControl(createSection, CreateZenithStarCatchStageSelector());
        TableLayoutPanel zenithStarCatchSpeedGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(360f));
        Factory.AddSettingRow(zenithStarCatchSpeedGrid, "Catch speed", CreateZenithStarCatchSpeedControl());
        SettingsUiFactory.AddSectionControl(createSection, zenithStarCatchSpeedGrid);

        SettingsUiFactory.AddSectionControl(createSection, Factory.CreateSubsectionLabel("Pyramid filter"));
        TableLayoutPanel pyramidFilterGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(360f));
        Factory.AddSettingRow(pyramidFilterGrid, "Enabled", autoCreatePyramidFilterBox);
        SettingsUiFactory.AddSectionControl(createSection, pyramidFilterGrid);
        SettingsUiFactory.AddSectionControl(createSection, Factory.CreateFieldLabel("Required pyramid items"));
        SettingsUiFactory.AddSectionControl(createSection, CreatePyramidItemSelector());

        SettingsUiFactory.AddSectionControl(createSection, Factory.CreateSubsectionLabel("World post-generation filters"));
        SettingsUiFactory.AddSectionControl(
            createSection,
            Factory.CreateWrappedFieldLabel(
                "Available for Crimson worlds of any size. The generated world file is checked before the world is kept.",
                UiTheme.Text));
        TableLayoutPanel postGenerationFilterGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(360f));
        Factory.AddSettingRow(
            postGenerationFilterGrid,
            "Require Crimson between dungeon and spawn",
            autoCreateCrimsonBetweenDungeonAndSpawnBox);
        Factory.AddSettingRow(
            postGenerationFilterGrid,
            "Return to main menu on filter failure",
            autoCreateReturnToMainMenuOnFilterFailureBox);
        SettingsUiFactory.AddSectionControl(createSection, postGenerationFilterGrid);

        SettingsUiFactory.AddSectionControl(createSection, Factory.CreateSubsectionLabel("Background world generation"));
        TableLayoutPanel worldPoolGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(360f));
        Factory.AddSettingRow(worldPoolGrid, "Background world pool", autoCreateWorldPoolBox);
        Factory.AddSettingRow(worldPoolGrid, "World pool size", autoCreateWorldPoolTargetBox);
        SettingsUiFactory.AddSectionControl(createSection, worldPoolGrid);

        SettingsUiFactory.AddSectionControl(createSection, Factory.CreateSubsectionLabel("Force keep all files"));
        SettingsUiFactory.AddSectionControl(
            createSection,
            Factory.CreateWrappedFieldLabel(
                "When enabled, world creation will not delete any files. This can leave many worlds and players to clean up manually.",
                UiTheme.Text));
        TableLayoutPanel existingSavesGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(360f));
        Factory.AddSettingRow(existingSavesGrid, "Enabled", autoCreatePreserveExistingSavesBox);
        SettingsUiFactory.AddSectionControl(createSection, existingSavesGrid);

        SettingsUiFactory.AddSection(parent, createSection);
    }

}
