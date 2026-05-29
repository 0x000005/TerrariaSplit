using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class AutomationSettingsPage : SettingsPageBase
{
    private static readonly Color SpecialSeedButtonHover = Color.FromArgb(40, 48, 53);
    private static readonly Color SpecialSeedButtonSelectedHover = Color.FromArgb(58, 93, 88);
    private static readonly Color SpecialSeedButtonDown = Color.FromArgb(34, 41, 46);
    private static readonly Color SpecialSeedButtonSelectedDown = Color.FromArgb(46, 76, 71);

    private readonly TextBox autoCreatePlayerNameBox = new();
    private readonly TextBox autoCreatePlayerTemplateCodeBox = new();
    private readonly ComboBox autoCreatePlayerDifficultyBox = new();
    private readonly ComboBox autoCreateWorldSizeBox = new();
    private readonly ComboBox autoCreateWorldDifficultyBox = new();
    private readonly ComboBox autoCreateWorldEvilBox = new();
    private readonly TextBox autoCreateSecretSeedsBox = new();
    private readonly CheckBox autoCreateZenithStarCatchBox = new();
    private readonly ThemedSlider autoCreateZenithStarCatchSpeedBar = new();
    private readonly Label autoCreateZenithStarCatchSpeedValueLabel = new();
    private readonly CheckBox autoCreatePyramidFilterBox = new();
    private readonly CheckBox autoCreateSeedPoolBox = new();
    private readonly TextBox autoCreateSeedPoolTargetBox = new();
    private readonly TextBox autoCreateShortActionDelayBox = new();
    private readonly TextBox autoCreateMenuActionDelayBox = new();
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
    internal CheckBox AutoCreateZenithStarCatchBox => autoCreateZenithStarCatchBox;
    internal IReadOnlyDictionary<string, CheckBox> AutoCreateZenithStarCatchStageBoxes => autoCreateZenithStarCatchStageBoxes;
    internal ThemedSlider AutoCreateZenithStarCatchSpeedBar => autoCreateZenithStarCatchSpeedBar;
    internal CheckBox AutoCreatePyramidFilterBox => autoCreatePyramidFilterBox;
    internal CheckBox AutoCreateSeedPoolBox => autoCreateSeedPoolBox;
    internal TextBox AutoCreateSeedPoolTargetBox => autoCreateSeedPoolTargetBox;
    internal TextBox AutoCreateSecretSeedsBox => autoCreateSecretSeedsBox;

    protected override Control BuildPage(SettingsPageContext context)
    {
        return context.BuildScrollPage(BuildSections);
    }

    public override void Apply(AppSettings settings)
    {
        settings.AutoCreate.PlayerName = autoCreatePlayerNameBox.Text.Trim();
        settings.AutoCreate.PlayerTemplateCode = autoCreatePlayerTemplateCodeBox.Text.Trim();
        settings.AutoCreate.PlayerDifficulty = AutoCreatePlayerDifficulty.Normalize(
            GetSelectedOption(autoCreatePlayerDifficultyBox, AutoCreatePlayerDifficulty.Softcore));
        settings.AutoCreate.WorldSize = AutoCreateWorldSize.Normalize(
            GetSelectedOption(autoCreateWorldSizeBox, AutoCreateWorldSize.Medium));
        settings.AutoCreate.WorldDifficulty = AutoCreateWorldDifficulty.Normalize(
            GetSelectedOption(autoCreateWorldDifficultyBox, AutoCreateWorldDifficulty.Classic));
        settings.AutoCreate.WorldEvil = AutoCreateWorldEvil.Normalize(
            GetSelectedOption(autoCreateWorldEvilBox, AutoCreateWorldEvil.Random));
        string selectedSpecialSeeds = string.Join(
            "|",
            AutoCreateSpecialWorldSeed.All.Where(seed =>
                autoCreateSpecialSeedBoxes.TryGetValue(seed, out CheckBox? box) && box.Checked));
        settings.AutoCreate.SpecialSeeds = string.Join("|", AutoCreateSpecialWorldSeed.ParseList(selectedSpecialSeeds));
        settings.AutoCreate.SecretSeeds = autoCreateSecretSeedsBox.Text.Trim();
        settings.AutoCreate.EnableZenithStarCatch = autoCreateZenithStarCatchBox.Checked;
        settings.AutoCreate.ZenithStarCatchStopStage = GetSelectedZenithStarCatchStopStage();
        settings.AutoCreate.ZenithStarCatchSpeedSliderValue = AutoCreateZenithStarCatchSpeed.NormalizeSliderValue(autoCreateZenithStarCatchSpeedBar.Value);
        settings.AutoCreate.EnablePyramidFilter = autoCreatePyramidFilterBox.Checked;
        settings.AutoCreate.EnableSeedPool = autoCreateSeedPoolBox.Checked;
        settings.AutoCreate.SeedPoolTargetCount = SettingsValueParser.ParseIntBox(
            autoCreateSeedPoolTargetBox,
            AppSettingsDefaults.AutoCreate.SeedPoolTargetCount,
            1,
            50);
        settings.AutoCreate.ShortActionDelayMilliseconds = SettingsValueParser.ParseIntBox(
            autoCreateShortActionDelayBox,
            AppSettingsDefaults.AutoCreate.ShortActionDelayMilliseconds,
            0,
            5000);
        settings.AutoCreate.MenuActionDelayMilliseconds = SettingsValueParser.ParseIntBox(
            autoCreateMenuActionDelayBox,
            AppSettingsDefaults.AutoCreate.MenuActionDelayMilliseconds,
            0,
            5000);
        settings.AutoCreate.WindowActivationDelayMilliseconds = SettingsValueParser.ParseIntBox(
            autoCreateWindowActivationDelayBox,
            AppSettingsDefaults.AutoCreate.WindowActivationDelayMilliseconds,
            0,
            5000);
        settings.AutoCreate.ClickFocusDelayMilliseconds = SettingsValueParser.ParseIntBox(
            autoCreateClickFocusDelayBox,
            AppSettingsDefaults.AutoCreate.ClickFocusDelayMilliseconds,
            0,
            5000);
        settings.AutoCreate.InputPressDurationMilliseconds = SettingsValueParser.ParseIntBox(
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
        autoCreatePlayerNameBox.Text = Draft.AutoCreate.PlayerName;
        autoCreatePlayerNameBox.PlaceholderText = Context.Localize("Empty = 1");

        UiTheme.StyleTextBox(autoCreatePlayerTemplateCodeBox);
        autoCreatePlayerTemplateCodeBox.Dock = DockStyle.Fill;
        autoCreatePlayerTemplateCodeBox.Multiline = true;
        autoCreatePlayerTemplateCodeBox.AcceptsReturn = true;
        autoCreatePlayerTemplateCodeBox.ScrollBars = ScrollBars.Vertical;
        autoCreatePlayerTemplateCodeBox.Height = autoCreatePlayerTemplateCodeBox.Font.Height * 10 + 14;
        autoCreatePlayerTemplateCodeBox.Text = Draft.AutoCreate.PlayerTemplateCode;
        autoCreatePlayerTemplateCodeBox.PlaceholderText = Context.Localize("Empty = default character");

        ConfigureOptionBox(autoCreatePlayerDifficultyBox, AutoCreatePlayerDifficulty.All, Draft.AutoCreate.PlayerDifficulty);
        ConfigureOptionBox(autoCreateWorldSizeBox, AutoCreateWorldSize.All, Draft.AutoCreate.WorldSize);
        ConfigureOptionBox(autoCreateWorldDifficultyBox, AutoCreateWorldDifficulty.All, Draft.AutoCreate.WorldDifficulty);
        ConfigureOptionBox(autoCreateWorldEvilBox, AutoCreateWorldEvil.All, Draft.AutoCreate.WorldEvil);
        ConfigureSeedListBox(autoCreateSecretSeedsBox, Draft.AutoCreate.SecretSeeds);
        ConfigureCheckBox(autoCreateZenithStarCatchBox, Draft.AutoCreate.EnableZenithStarCatch);
        autoCreateZenithStarCatchBox.CheckedChanged += (_, _) => UpdateZenithStarCatchAvailability();
        ConfigureZenithStarCatchSpeedBar(autoCreateZenithStarCatchSpeedBar, Draft.AutoCreate.ZenithStarCatchSpeedSliderValue);
        autoCreateZenithStarCatchSpeedBar.ValueChanged += (_, _) => UpdateZenithStarCatchSpeedLabel();
        ConfigureCheckBox(autoCreatePyramidFilterBox, Draft.AutoCreate.EnablePyramidFilter);
        autoCreatePyramidFilterBox.CheckedChanged += (_, _) => UpdateSeedPoolAvailability();
        ConfigureCheckBox(autoCreateSeedPoolBox, Draft.AutoCreate.EnableSeedPool);
        autoCreateSeedPoolBox.CheckedChanged += (_, _) => UpdateSeedPoolAvailability();
        ConfigureNumberBox(autoCreateSeedPoolTargetBox, Draft.AutoCreate.SeedPoolTargetCount, 1, 50);
        ConfigureNumberBox(autoCreateShortActionDelayBox, Draft.AutoCreate.ShortActionDelayMilliseconds, 0, 5000);
        ConfigureNumberBox(autoCreateMenuActionDelayBox, Draft.AutoCreate.MenuActionDelayMilliseconds, 0, 5000);
        ConfigureNumberBox(autoCreateWindowActivationDelayBox, Draft.AutoCreate.WindowActivationDelayMilliseconds, 0, 5000);
        ConfigureNumberBox(autoCreateClickFocusDelayBox, Draft.AutoCreate.ClickFocusDelayMilliseconds, 0, 5000);
        ConfigureNumberBox(autoCreateInputPressDurationBox, Draft.AutoCreate.InputPressDurationMilliseconds, 1, 5000);
        UpdateSeedPoolAvailability();

        AddCreateWorldSection(parent);
        AddEnterWorldSection(parent);
        AddDelaySection(parent);
    }

    private Control CreateBackupNoticeRow(Color warningColor)
    {
        var row = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.Surface,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 8, 0, 8),
            Padding = Padding.Empty
        };
        UiTheme.EnableDoubleBuffering(row);
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        Label label = Factory.CreateWrappedFieldLabel(
            "The most recent 50 deletions are kept in the backup folder.",
            warningColor);
        label.Dock = DockStyle.Fill;
        label.Margin = new Padding(0, 0, 12, 0);
        label.TextAlign = ContentAlignment.MiddleLeft;

        Button openButton = Factory.CreateSmallButton("Open folder");
        openButton.Width = 252;
        openButton.MinimumSize = new Size(252, 36);
        openButton.Margin = Padding.Empty;
        openButton.Click += (_, _) => Dialogs.OpenAutoCreateBackupFolder(Context.Localize);

        row.Controls.Add(label, 0, 0);
        row.Controls.Add(openButton, 1, 0);
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
            Factory.CreateWrappedFieldLabel(
                "Create World deletes all non-favorite players and worlds.",
                warningColor));
        SettingsUiFactory.AddSectionControl(createSection, CreateBackupNoticeRow(warningColor));

        TableLayoutPanel createGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(360f));
        Factory.AddSettingRow(createGrid, "Player name", autoCreatePlayerNameBox);
        Factory.AddSettingRow(createGrid, "Player difficulty", autoCreatePlayerDifficultyBox);
        SettingsUiFactory.AddSectionControl(createSection, createGrid);
        SettingsUiFactory.AddSectionControl(createSection, Factory.CreateFieldLabel("Player code"));
        SettingsUiFactory.AddSectionControl(createSection, autoCreatePlayerTemplateCodeBox);

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
        Factory.AddSettingRow(seedGrid, "Secret seeds", autoCreateSecretSeedsBox);
        SettingsUiFactory.AddSectionControl(createSection, seedGrid);

        TableLayoutPanel zenithStarCatchGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(360f));
        Factory.AddSettingRow(zenithStarCatchGrid, "Enable auto star catch (Zenith worlds only)", autoCreateZenithStarCatchBox);
        SettingsUiFactory.AddSectionControl(createSection, zenithStarCatchGrid);
        SettingsUiFactory.AddSectionControl(createSection, Factory.CreateFieldLabel("Stop after stage"));
        SettingsUiFactory.AddSectionControl(createSection, CreateZenithStarCatchStageSelector());
        TableLayoutPanel zenithStarCatchSpeedGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(360f));
        Factory.AddSettingRow(zenithStarCatchSpeedGrid, "Catch speed", CreateZenithStarCatchSpeedControl());
        SettingsUiFactory.AddSectionControl(createSection, zenithStarCatchSpeedGrid);
        TableLayoutPanel pyramidFilterGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(360f));
        Factory.AddSettingRow(pyramidFilterGrid, "Auto filter pyramid", autoCreatePyramidFilterBox);
        SettingsUiFactory.AddSectionControl(createSection, pyramidFilterGrid);

        TableLayoutPanel seedPoolGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(360f));
        Factory.AddSettingRow(seedPoolGrid, "Background seed pool", autoCreateSeedPoolBox);
        Factory.AddSettingRow(seedPoolGrid, "Seed pool size", autoCreateSeedPoolTargetBox);
        SettingsUiFactory.AddSectionControl(createSection, seedPoolGrid);

        SettingsUiFactory.AddSection(parent, createSection);
    }

    private TableLayoutPanel CreateSpecialSeedSelector()
    {
        var selectedSeeds = AutoCreateSpecialWorldSeed.ParseList(Draft.AutoCreate.SpecialSeeds)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        autoCreateSpecialSeedBoxes.Clear();

        const int columnCount = 3;
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.Surface,
            ColumnCount = columnCount,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 8),
            Padding = Padding.Empty
        };
        UiTheme.EnableDoubleBuffering(panel);
        for (int i = 0; i < columnCount; i++)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columnCount));
        }

        for (int index = 0; index < AutoCreateSpecialWorldSeed.All.Length; index++)
        {
            string seed = AutoCreateSpecialWorldSeed.All[index];
            CheckBox button = CreateSpecialSeedButton(seed, selectedSeeds.Contains(seed));
            int column = index % columnCount;
            int row = index / columnCount;
            if (column == 0)
            {
                panel.RowCount++;
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
            }

            button.Margin = new Padding(0, 0, column == columnCount - 1 ? 0 : 8, 10);
            autoCreateSpecialSeedBoxes[seed] = button;
            panel.Controls.Add(button, column, row);
        }

        UpdateSpecialSeedAvailability();
        return panel;
    }

    private TableLayoutPanel CreateZenithStarCatchStageSelector()
    {
        autoCreateZenithStarCatchStageBoxes.Clear();

        const int columnCount = 3;
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.Surface,
            ColumnCount = columnCount,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 8),
            Padding = Padding.Empty
        };
        UiTheme.EnableDoubleBuffering(panel);
        for (int i = 0; i < columnCount; i++)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columnCount));
        }

        string selectedStopStage = AutoCreateZenithStarCatchStage.Normalize(Draft.AutoCreate.ZenithStarCatchStopStage);
        for (int index = 0; index < AutoCreateZenithStarCatchStage.All.Length; index++)
        {
            string stage = AutoCreateZenithStarCatchStage.All[index];
            CheckBox button = CreateZenithStarCatchStageButton(
                stage,
                AutoCreateZenithStarCatchStage.Includes(selectedStopStage, stage));
            int column = index % columnCount;
            int row = index / columnCount;
            if (column == 0)
            {
                panel.RowCount++;
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
            }

            button.Margin = new Padding(0, 0, column == columnCount - 1 ? 0 : 8, 10);
            autoCreateZenithStarCatchStageBoxes[stage] = button;
            panel.Controls.Add(button, column, row);
        }

        ApplyZenithStarCatchStageSelection(selectedStopStage);
        UpdateZenithStarCatchAvailability();
        return panel;
    }

    private CheckBox CreateSpecialSeedButton(string seed, bool selected)
    {
        var button = new CheckBox
        {
            Appearance = Appearance.Button,
            AutoEllipsis = true,
            BackColor = selected ? UiTheme.Selection : UiTheme.SurfaceRaised,
            Checked = selected,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            Font = UiTheme.FormFont(9f),
            ForeColor = UiTheme.Text,
            Height = 44,
            MinimumSize = new Size(0, 44),
            Padding = new Padding(8, 0, 8, 2),
            Text = Context.Localize(seed),
            TextAlign = ContentAlignment.MiddleCenter,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.CheckedBackColor = UiTheme.Selection;
        button.CheckedChanged += (_, _) =>
        {
            if (string.Equals(seed, AutoCreateSpecialWorldSeed.Zenith, StringComparison.OrdinalIgnoreCase))
            {
                UpdateSpecialSeedAvailability();
            }
            else
            {
                UpdateSpecialSeedButtonState(button);
            }
        };
        button.EnabledChanged += (_, _) => UpdateSpecialSeedButtonState(button);
        UpdateSpecialSeedButtonState(button);
        return button;
    }

    private CheckBox CreateZenithStarCatchStageButton(string stage, bool selected)
    {
        var button = new CheckBox
        {
            Appearance = Appearance.Button,
            AutoEllipsis = true,
            BackColor = selected ? UiTheme.Selection : UiTheme.SurfaceRaised,
            Checked = selected,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            Font = UiTheme.FormFont(9f),
            ForeColor = UiTheme.Text,
            Height = 44,
            MinimumSize = new Size(0, 44),
            Padding = new Padding(8, 0, 8, 2),
            Text = Context.Localize(stage),
            TextAlign = ContentAlignment.MiddleCenter,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.CheckedBackColor = UiTheme.Selection;
        button.CheckedChanged += (_, _) => SelectZenithStarCatchStage(stage);
        button.EnabledChanged += (_, _) => UpdateSpecialSeedButtonState(button);
        UpdateSpecialSeedButtonState(button);
        return button;
    }

    private Control CreateZenithStarCatchSpeedControl()
    {
        autoCreateZenithStarCatchSpeedValueLabel.AutoEllipsis = false;
        autoCreateZenithStarCatchSpeedValueLabel.Dock = DockStyle.Fill;
        autoCreateZenithStarCatchSpeedValueLabel.ForeColor = UiTheme.Text;
        autoCreateZenithStarCatchSpeedValueLabel.Font = UiTheme.FormFont(9.5f, FontStyle.Bold);
        autoCreateZenithStarCatchSpeedValueLabel.Margin = new Padding(10, 8, 0, 8);
        autoCreateZenithStarCatchSpeedValueLabel.TextAlign = ContentAlignment.MiddleRight;
        UpdateZenithStarCatchSpeedLabel();

        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.Surface,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        UiTheme.EnableDoubleBuffering(panel);
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82f));
        panel.Controls.Add(autoCreateZenithStarCatchSpeedBar, 0, 0);
        panel.Controls.Add(autoCreateZenithStarCatchSpeedValueLabel, 1, 0);
        return panel;
    }

    private static void UpdateSpecialSeedButtonState(CheckBox button)
    {
        button.BackColor = button.Checked ? UiTheme.Selection : UiTheme.SurfaceRaised;
        button.FlatAppearance.BorderColor = button.Checked ? UiTheme.Accent : UiTheme.Border;
        button.FlatAppearance.CheckedBackColor = UiTheme.Selection;
        button.FlatAppearance.MouseOverBackColor = button.Checked
            ? SpecialSeedButtonSelectedHover
            : SpecialSeedButtonHover;
        button.FlatAppearance.MouseDownBackColor = button.Checked
            ? SpecialSeedButtonSelectedDown
            : SpecialSeedButtonDown;
        button.ForeColor = button.Enabled ? UiTheme.Text : UiTheme.MutedText;
        button.Invalidate();
    }

    private void UpdateSpecialSeedAvailability()
    {
        bool zenithSelected = autoCreateSpecialSeedBoxes.TryGetValue(AutoCreateSpecialWorldSeed.Zenith, out CheckBox? zenithBox) &&
            zenithBox.Checked;

        foreach ((string seed, CheckBox button) in autoCreateSpecialSeedBoxes)
        {
            if (AutoCreateSpecialWorldSeed.IsZenithDependency(seed))
            {
                if (zenithSelected)
                {
                    button.Checked = false;
                }

                button.Enabled = !zenithSelected;
            }

            UpdateSpecialSeedButtonState(button);
        }

        UpdateZenithStarCatchAvailability();
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
            foreach ((string stage, CheckBox button) in autoCreateZenithStarCatchStageBoxes)
            {
                button.Checked = AutoCreateZenithStarCatchStage.Includes(selectedStopStage, stage);
                UpdateSpecialSeedButtonState(button);
            }
        }
        finally
        {
            updatingZenithStarCatchStageSelection = false;
        }
    }

    private string GetSelectedZenithStarCatchStopStage()
    {
        for (int index = AutoCreateZenithStarCatchStage.All.Length - 1; index >= 0; index--)
        {
            string stage = AutoCreateZenithStarCatchStage.All[index];
            if (autoCreateZenithStarCatchStageBoxes.TryGetValue(stage, out CheckBox? button) && button.Checked)
            {
                return stage;
            }
        }

        return AutoCreateZenithStarCatchStage.Default;
    }

    private void UpdateZenithStarCatchAvailability()
    {
        bool zenithSelected = autoCreateSpecialSeedBoxes.TryGetValue(AutoCreateSpecialWorldSeed.Zenith, out CheckBox? zenithBox) &&
            zenithBox.Checked;

        autoCreateZenithStarCatchBox.Enabled = zenithSelected;
        bool starCatchControlsEnabled = zenithSelected && autoCreateZenithStarCatchBox.Checked;
        foreach (CheckBox button in autoCreateZenithStarCatchStageBoxes.Values)
        {
            button.Enabled = starCatchControlsEnabled;
            UpdateSpecialSeedButtonState(button);
        }

        autoCreateZenithStarCatchSpeedBar.Enabled = starCatchControlsEnabled;
        autoCreateZenithStarCatchSpeedValueLabel.Enabled = starCatchControlsEnabled;
        autoCreateZenithStarCatchSpeedValueLabel.ForeColor = autoCreateZenithStarCatchSpeedValueLabel.Enabled
            ? UiTheme.Text
            : UiTheme.MutedText;
    }

    private void UpdateSeedPoolAvailability()
    {
        bool pyramidFilterEnabled = autoCreatePyramidFilterBox.Checked;
        autoCreateSeedPoolBox.Enabled = pyramidFilterEnabled;
        autoCreateSeedPoolTargetBox.Enabled = pyramidFilterEnabled && autoCreateSeedPoolBox.Checked;
        autoCreateSeedPoolBox.ForeColor = autoCreateSeedPoolBox.Enabled ? UiTheme.Text : UiTheme.MutedText;
        autoCreateSeedPoolTargetBox.ForeColor = autoCreateSeedPoolTargetBox.Enabled ? UiTheme.Text : UiTheme.MutedText;
    }

    private void AddEnterWorldSection(TableLayoutPanel parent)
    {
        practiceSlotControls.Clear();

        TableLayoutPanel slotsSection = Factory.CreateSection("Load World");
        SettingsUiFactory.AddSectionControl(
            slotsSection,
            Factory.CreateWrappedFieldLabel(
                "Load World copies the selected player and/or world files to Terraria's save folder, then opens Single Player.",
                UiTheme.Text));
        SettingsUiFactory.AddSectionControl(
            slotsSection,
            Factory.CreateWrappedFieldLabel(
                "Do not choose players or worlds in the default save location.",
                Color.FromArgb(255, 210, 120)));

        TableLayoutPanel slotsGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStyleAbsolute(48f),
            SettingsUiFactory.ColumnStyleAbsolute(180f),
            SettingsUiFactory.ColumnStylePercent(50f),
            SettingsUiFactory.ColumnStyleAbsolute(152f),
            SettingsUiFactory.ColumnStylePercent(50f),
            SettingsUiFactory.ColumnStyleAbsolute(152f));
        Factory.AddHeaderRow(slotsGrid, string.Empty, "Name", "Player file", string.Empty, "World file", string.Empty);

        IReadOnlyList<PracticeWorldSlot> slots = Draft.PracticeWorlds.Slots;
        for (int index = 0; index < PracticeWorldSettings.SlotCount; index++)
        {
            PracticeWorldSlot slot = index < slots.Count ? slots[index] : new PracticeWorldSlot();
            AddPracticeWorldSlotRow(slotsGrid, index, slot);
        }

        SettingsUiFactory.AddSectionControl(slotsSection, slotsGrid);
        SettingsUiFactory.AddSection(parent, slotsSection);
    }

    private void AddPracticeWorldSlotRow(TableLayoutPanel grid, int index, PracticeWorldSlot slot)
    {
        TextBox nameBox = Factory.CreateTextBox(slot.Name);
        TextBox playerPathBox = Factory.CreateTextBox(slot.PlayerFilePath);
        TextBox worldPathBox = Factory.CreateTextBox(slot.WorldFilePath);

        Button playerBrowseButton = CreatePracticeBrowseButton(
            "Choose player file",
            "Terraria player|*.plr|All files|*.*",
            playerPathBox);
        Button worldBrowseButton = CreatePracticeBrowseButton(
            "Choose world file",
            "Terraria world|*.wld|All files|*.*",
            worldPathBox);

        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 64f));
        grid.Controls.Add(CreatePracticeSlotKeyLabel(index), 0, row);
        grid.Controls.Add(nameBox, 1, row);
        grid.Controls.Add(playerPathBox, 2, row);
        grid.Controls.Add(playerBrowseButton, 3, row);
        grid.Controls.Add(worldPathBox, 4, row);
        grid.Controls.Add(worldBrowseButton, 5, row);

        practiceSlotControls.Add(new PracticeSlotControls(nameBox, playerPathBox, worldPathBox));
    }

    private Button CreatePracticeBrowseButton(string title, string filter, TextBox target)
    {
        Button button = Factory.CreateSmallButton("Browse");
        button.Click += (_, _) => Dialogs.PickFile(target, title, filter);
        return button;
    }

    private Label CreatePracticeSlotKeyLabel(int index)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.Text,
            Font = UiTheme.FormFont(10f, FontStyle.Bold),
            Margin = new Padding(0, 0, 10, 0),
            Text = index == 9 ? "0" : (index + 1).ToString(CultureInfo.InvariantCulture),
            TextAlign = ContentAlignment.MiddleCenter
        };
    }

    private void AddDelaySection(TableLayoutPanel parent)
    {
        TableLayoutPanel timingSection = Factory.CreateSection("Delay");
        TableLayoutPanel timingGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(180f));
        Factory.AddSettingRow(timingGrid, "Mouse / key press ms", autoCreateInputPressDurationBox);
        Factory.AddSettingRow(timingGrid, "Window activation wait ms", autoCreateWindowActivationDelayBox);
        Factory.AddSettingRow(timingGrid, "Click focus wait ms", autoCreateClickFocusDelayBox);
        Factory.AddSettingRow(timingGrid, "Short action delay ms", autoCreateShortActionDelayBox);
        Factory.AddSettingRow(timingGrid, "Menu action delay ms", autoCreateMenuActionDelayBox);
        SettingsUiFactory.AddSectionControl(timingSection, timingGrid);
        SettingsUiFactory.AddSection(parent, timingSection);
    }

    private void ConfigureOptionBox(ComboBox comboBox, IEnumerable<string> options, string selected)
    {
        comboBox.Dock = DockStyle.Fill;
        UiTheme.StyleComboBox(comboBox);
        comboBox.Items.Clear();

        foreach (string option in options)
        {
            comboBox.Items.Add(new LocalizedOption(option, Context.Localize(option)));
        }

        comboBox.SelectedItem = comboBox.Items
            .Cast<LocalizedOption>()
            .FirstOrDefault(option => string.Equals(option.Value, selected, StringComparison.OrdinalIgnoreCase));
        if (comboBox.SelectedIndex < 0 && comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }
    }

    private static string GetSelectedOption(ComboBox comboBox, string fallback)
    {
        return comboBox.SelectedItem switch
        {
            LocalizedOption option => option.Value,
            string value => value,
            _ => fallback
        };
    }

    private static void ConfigureCheckBox(CheckBox checkBox, bool selected)
    {
        checkBox.Checked = selected;
        checkBox.Dock = DockStyle.Fill;
        UiTheme.StyleCheckBox(checkBox);
    }

    private static void ConfigureZenithStarCatchSpeedBar(ThemedSlider bar, int selected)
    {
        bar.BackColor = UiTheme.Surface;
        bar.Dock = DockStyle.Fill;
        bar.Height = 36;
        bar.Margin = new Padding(0, 2, 0, 2);
        bar.Minimum = AutoCreateZenithStarCatchSpeed.MinimumSliderValue;
        bar.Maximum = AutoCreateZenithStarCatchSpeed.MaximumSliderValue;
        bar.Value = AutoCreateZenithStarCatchSpeed.NormalizeSliderValue(selected);
    }

    private void UpdateZenithStarCatchSpeedLabel()
    {
        autoCreateZenithStarCatchSpeedValueLabel.Text = AutoCreateZenithStarCatchSpeed.FormatMultiplier(autoCreateZenithStarCatchSpeedBar.Value);
    }

    private static void ConfigureNumberBox(TextBox textBox, int selected, int minimum, int maximum)
    {
        UiTheme.StyleTextBox(textBox);
        textBox.Dock = DockStyle.Fill;
        textBox.Text = Math.Clamp(selected, minimum, maximum).ToString(CultureInfo.InvariantCulture);
    }

    private void ConfigureSeedListBox(TextBox textBox, string selected)
    {
        UiTheme.StyleTextBox(textBox);
        textBox.Dock = DockStyle.Fill;
        textBox.Text = selected;
        textBox.PlaceholderText = Context.Localize("Empty = none");
    }

    internal sealed record PracticeSlotControls(
        TextBox NameBox,
        TextBox PlayerFilePathBox,
        TextBox WorldFilePathBox);

    private sealed record LocalizedOption(string Value, string DisplayName)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }
}
