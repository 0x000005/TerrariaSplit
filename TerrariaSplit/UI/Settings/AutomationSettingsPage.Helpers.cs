using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed partial class AutomationSettingsPage : SettingsPageBase
{
    private TableLayoutPanel CreateSpecialSeedSelector()
    {
        var selectedSeeds = AutoCreateSpecialWorldSeed.ParseList(Draft.Automation.AutoCreate.SpecialSeeds)
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

    private TableLayoutPanel CreatePyramidItemSelector()
    {
        var selectedItems = AutoCreatePyramidFilterItem.FromMask(Draft.Automation.AutoCreate.PyramidFilterItemMask)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        autoCreatePyramidItemBoxes.Clear();

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

        for (int index = 0; index < AutoCreatePyramidFilterItem.All.Length; index++)
        {
            string item = AutoCreatePyramidFilterItem.All[index];
            CheckBox button = CreatePyramidItemButton(item, selectedItems.Contains(item));
            int column = index % columnCount;
            int row = index / columnCount;
            if (column == 0)
            {
                panel.RowCount++;
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
            }

            button.Margin = new Padding(0, 0, column == columnCount - 1 ? 0 : 8, 10);
            autoCreatePyramidItemBoxes[item] = button;
            panel.Controls.Add(button, column, row);
        }

        UpdatePyramidItemAvailability();
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

        string selectedStopStage = AutoCreateZenithStarCatchStage.Normalize(Draft.Automation.AutoCreate.ZenithStarCatchStopStage);
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
        CheckBox button = CreateSelectorButton(seed, selected);
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
        return button;
    }

    private CheckBox CreatePyramidItemButton(string item, bool selected)
    {
        CheckBox button = CreateSelectorButton(item, selected);
        button.CheckedChanged += (_, _) => UpdateSpecialSeedButtonState(button);
        return button;
    }

    private CheckBox CreateSelectorButton(string textKey, bool selected)
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
            Text = Context.Localize(textKey),
            TextAlign = ContentAlignment.MiddleCenter,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.CheckedBackColor = UiTheme.Selection;
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

    private void UpdatePyramidItemAvailability()
    {
        autoCreateReturnToMainMenuOnFilterFailureBox.Enabled = autoCreatePyramidFilterBox.Checked;
        autoCreateReturnToMainMenuOnFilterFailureBox.ForeColor = autoCreateReturnToMainMenuOnFilterFailureBox.Enabled
            ? UiTheme.Text
            : UiTheme.MutedText;

        foreach (CheckBox button in autoCreatePyramidItemBoxes.Values)
        {
            button.Enabled = autoCreatePyramidFilterBox.Checked;
            UpdateSpecialSeedButtonState(button);
        }
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

    private void UpdateWorldPoolAvailability()
    {
        autoCreateWorldPoolBox.Enabled = true;
        autoCreateWorldPoolTargetBox.Enabled = autoCreateWorldPoolBox.Checked;
        autoCreateWorldPoolBox.ForeColor = autoCreateWorldPoolBox.Enabled ? UiTheme.Text : UiTheme.MutedText;
        autoCreateWorldPoolTargetBox.ForeColor = autoCreateWorldPoolTargetBox.Enabled ? UiTheme.Text : UiTheme.MutedText;
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
        Factory.AddSettingRow(timingGrid, "Initial wait ms", autoCreateWindowActivationDelayBox);
        Factory.AddSettingRow(timingGrid, "Pre-click wait ms", autoCreateClickFocusDelayBox);
        Factory.AddSettingRow(timingGrid, "Mouse / key duration ms", autoCreateInputPressDurationBox);
        Factory.AddSettingRow(timingGrid, "Adjacent operation delay ms", autoCreateShortActionDelayBox);
        Factory.AddSettingRow(timingGrid, "Cross-menu operation delay ms", autoCreateMenuActionDelayBox);
        Factory.AddSettingRow(timingGrid, "Pyramid filter post wait ms", autoCreatePyramidFilterPostDelayBox);
        SettingsUiFactory.AddSectionControl(timingSection, timingGrid);
        SettingsUiFactory.AddSection(parent, timingSection);
    }

    private void ConfigureOptionBox(ThemedDropDownList comboBox, IEnumerable<string> options, string selected)
    {
        comboBox.Dock = DockStyle.Fill;
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

    private static string GetSelectedOption(ThemedDropDownList comboBox, string fallback)
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
