using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed partial class AutomationSettingsPage : SettingsPageBase
{
    private const float CheatActivationButtonPercent = 20f;
    private const float CheatActivationSpacerPercent = 10f;
    private const float CheatOptionButtonsPercent = 70f;
    private const float CheatSelectorRowHeight = 54f;
    private const int CheatSelectorHorizontalGap = 8;
    private const int CheatSelectorGap = 10;

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

        int columnCount = AutoCreatePyramidFilterItem.All.Length + 1;
        TableLayoutPanel panel = CreateSelectorPanel(columnCount, fixedFirstColumn: true);
        autoCreatePyramidFilterBox.Margin = new Padding(0, 0, 0, CheatSelectorGap);
        panel.Controls.Add(autoCreatePyramidFilterBox, 0, 0);

        for (int index = 0; index < AutoCreatePyramidFilterItem.All.Length; index++)
        {
            string item = AutoCreatePyramidFilterItem.All[index];
            CheckBox button = CreatePyramidItemButton(item, selectedItems.Contains(item));
            int column = index + 2;
            button.Margin = SelectorMargin(index, AutoCreatePyramidFilterItem.All.Length);
            autoCreatePyramidItemBoxes[item] = button;
            panel.Controls.Add(button, column, 0);
        }

        FinishSingleRowSelector(panel);
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

    private TableLayoutPanel CreateResourceItemSelector()
    {
        int selectedMask = AutoCreateResourceFilterItem.NormalizeMask(Draft.Automation.AutoCreate.ResourceFilterItemMask);
        autoCreateResourceItemBoxes.Clear();
        TableLayoutPanel panel = CreateSelectorPanel(1, fixedFirstColumn: true);
        for (int index = 0; index < AutoCreateResourceFilterItem.All.Length; index++)
        {
            string item = AutoCreateResourceFilterItem.All[index];
            CheckBox button = CreateSelectorButton(
                item,
                (selectedMask & AutoCreateResourceFilterItem.Mask(item)) != 0);
            button.Margin = new Padding(0, 0, 0, CheatSelectorGap);
            button.CheckedChanged += (_, _) => UpdateSpecialSeedButtonState(button);
            autoCreateResourceItemBoxes[item] = button;
            panel.RowCount++;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, CheatSelectorRowHeight));
            panel.Controls.Add(button, 0, index);
        }

        return panel;
    }

    private TableLayoutPanel CreateLifeCrystalMinimumSelector() => CreateMinimumSelector(
        AutoCreateResourceMinimum.LifeCrystals,
        AutoCreateResourceMinimum.NormalizeLifeCrystals(Draft.Automation.AutoCreate.ResourceFilterLifeCrystalMinimum),
        autoCreateLifeCrystalMinimumBoxes,
        "Life Crystal");

    private TableLayoutPanel CreateSpelunkerMinimumSelector() => CreateMinimumSelector(
        AutoCreateResourceMinimum.Potions,
        AutoCreateResourceMinimum.NormalizePotions(Draft.Automation.AutoCreate.ResourceFilterSpelunkerPotionMinimum),
        autoCreateSpelunkerMinimumBoxes,
        "Spelunker Potion");

    private TableLayoutPanel CreateFeatherfallMinimumSelector() => CreateMinimumSelector(
        AutoCreateResourceMinimum.Potions,
        AutoCreateResourceMinimum.NormalizePotions(Draft.Automation.AutoCreate.ResourceFilterFeatherfallPotionMinimum),
        autoCreateFeatherfallMinimumBoxes,
        "Featherfall Potion");

    private TableLayoutPanel CreateMinimumSelector(
        IReadOnlyList<int> values,
        int selectedMinimum,
        Dictionary<int, CheckBox> boxes,
        string nameKey)
    {
        boxes.Clear();
        TableLayoutPanel panel = CreateSelectorPanel(values.Count, fixedFirstColumn: true);
        for (int index = 0; index < values.Count; index++)
        {
            int value = values[index];
            string label = value == 0
                ? nameKey
                : index == values.Count - 1
                ? $"{value.ToString(CultureInfo.InvariantCulture)}+"
                : value.ToString(CultureInfo.InvariantCulture);
            CheckBox button = CreateSelectorButton(
                label,
                selectedMinimum > 0 && (value == 0 || value >= selectedMinimum));
            if (value != 0)
            {
                button.AutoEllipsis = false;
                button.Padding = new Padding(0, 0, 0, 2);
            }
            button.Margin = value == 0
                ? new Padding(0, 0, 0, CheatSelectorGap)
                : SelectorMargin(index - 1, values.Count - 1);
            button.CheckedChanged += (_, _) => SelectMinimum(value, button.Checked, values, boxes);
            boxes[value] = button;
            panel.Controls.Add(button, value == 0 ? 0 : index + 1, 0);
        }

        FinishSingleRowSelector(panel);
        ApplyMinimumSelection(selectedMinimum, boxes);
        return panel;
    }

    private TableLayoutPanel CreateHookMinimumSelector()
    {
        autoCreateHookMinimumBoxes.Clear();
        string selectedMinimum = AutoCreateResourceHook.Normalize(Draft.Automation.AutoCreate.ResourceFilterHookMinimum);
        TableLayoutPanel panel = CreateSelectorPanel(AutoCreateResourceHook.All.Length, fixedFirstColumn: true);
        for (int index = 0; index < AutoCreateResourceHook.All.Length; index++)
        {
            string hook = AutoCreateResourceHook.All[index];
            CheckBox button = CreateSelectorButton(
                hook == AutoCreateResourceHook.None ? "Hook" : hook,
                selectedMinimum != AutoCreateResourceHook.None &&
                    (hook == AutoCreateResourceHook.None || AutoCreateResourceHook.Includes(selectedMinimum, hook)));
            button.Margin = hook == AutoCreateResourceHook.None
                ? new Padding(0, 0, 0, CheatSelectorGap)
                : SelectorMargin(index - 1, AutoCreateResourceHook.All.Length - 1);
            button.CheckedChanged += (_, _) => SelectHookMinimum(hook, button.Checked);
            autoCreateHookMinimumBoxes[hook] = button;
            panel.Controls.Add(button, hook == AutoCreateResourceHook.None ? 0 : index + 1, 0);
        }

        FinishSingleRowSelector(panel);
        ApplyHookMinimumSelection(selectedMinimum);
        return panel;
    }

    private static TableLayoutPanel CreateSelectorPanel(int columnCount, bool fixedFirstColumn = false)
    {
        int physicalColumnCount = fixedFirstColumn
            ? columnCount == 1 ? 3 : columnCount + 1
            : columnCount;
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.Surface,
            ColumnCount = physicalColumnCount,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        UiTheme.EnableDoubleBuffering(panel);
        if (fixedFirstColumn)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, CheatActivationButtonPercent));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, CheatActivationSpacerPercent));
            if (columnCount == 1)
            {
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, CheatOptionButtonsPercent));
            }
            else
            {
                for (int index = 1; index < columnCount; index++)
                {
                    panel.ColumnStyles.Add(new ColumnStyle(
                        SizeType.Percent,
                        CheatOptionButtonsPercent / (columnCount - 1)));
                }
            }
        }
        else
        {
            for (int index = 0; index < columnCount; index++)
            {
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columnCount));
            }
        }

        return panel;
    }

    private static Padding SelectorMargin(int index, int count) =>
        new(0, 0, index == count - 1 ? 0 : CheatSelectorHorizontalGap, CheatSelectorGap);

    private static void FinishSingleRowSelector(TableLayoutPanel panel)
    {
        panel.RowCount = 1;
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, CheatSelectorRowHeight));
    }

    private TableLayoutPanel CreateCrimsonDistanceSelector()
    {
        autoCreateCrimsonDistanceBoxes.Clear();

        int columnCount = AutoCreateCrimsonDistance.All.Length + 1;
        TableLayoutPanel panel = CreateSelectorPanel(columnCount, fixedFirstColumn: true);
        autoCreateCrimsonBetweenDungeonAndSpawnBox.Margin = new Padding(0, 0, 0, CheatSelectorGap);
        panel.Controls.Add(autoCreateCrimsonBetweenDungeonAndSpawnBox, 0, 0);

        string selectedDistance = AutoCreateCrimsonDistance.Normalize(Draft.Automation.AutoCreate.CrimsonDistance);
        for (int index = 0; index < AutoCreateCrimsonDistance.All.Length; index++)
        {
            string distance = AutoCreateCrimsonDistance.All[index];
            CheckBox button = CreateSelectorButton(
                distance,
                AutoCreateCrimsonDistance.Includes(selectedDistance, distance));
            int column = index + 2;
            button.Margin = SelectorMargin(index, AutoCreateCrimsonDistance.All.Length);
            button.CheckedChanged += (_, _) => SelectCrimsonDistance(distance);
            autoCreateCrimsonDistanceBoxes[distance] = button;
            panel.Controls.Add(button, column, 0);
        }

        FinishSingleRowSelector(panel);
        ApplyCrimsonDistanceSelection(selectedDistance);
        UpdatePostGenerationFilterAvailability();
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
        var button = new CheckBox();
        ConfigureSelectorButton(button, textKey, selected);
        return button;
    }

    private void ConfigureSelectorButton(CheckBox button, string textKey, bool selected)
    {
        button.Appearance = Appearance.Button;
        button.AutoEllipsis = true;
        button.BackColor = selected ? UiTheme.Selection : UiTheme.SurfaceRaised;
        button.Checked = selected;
        button.Dock = DockStyle.Fill;
        button.FlatStyle = FlatStyle.Flat;
        button.Font = UiTheme.FormFont(9f);
        button.ForeColor = UiTheme.Text;
        button.Height = 44;
        button.MinimumSize = new Size(0, 44);
        button.Padding = new Padding(8, 0, 8, 2);
        button.Text = Context.Localize(textKey);
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.UseVisualStyleBackColor = false;
        button.FlatAppearance.CheckedBackColor = UiTheme.Selection;
        button.CheckedChanged += (_, _) => UpdateSpecialSeedButtonState(button);
        button.EnabledChanged += (_, _) => UpdateSpecialSeedButtonState(button);
        UpdateSpecialSeedButtonState(button);
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
        bool cheatsEnabled = autoCreateCheatsBox.Checked;
        autoCreatePyramidFilterBox.Enabled = cheatsEnabled;
        UpdateSpecialSeedButtonState(autoCreatePyramidFilterBox);
        foreach (CheckBox button in autoCreatePyramidItemBoxes.Values)
        {
            button.Enabled = cheatsEnabled && autoCreatePyramidFilterBox.Checked;
            UpdateSpecialSeedButtonState(button);
        }
    }

    private void UpdatePostGenerationFilterAvailability()
    {
        string selectedWorldSize = GetSelectedOption(autoCreateWorldSizeBox, AutoCreateWorldSize.Small);
        string selectedWorldEvil = GetSelectedOption(autoCreateWorldEvilBox, AutoCreateWorldEvil.Crimson);
        bool cheatsEnabled = autoCreateCheatsBox.Checked;
        bool supportsCrimsonCorridor =
            cheatsEnabled &&
            string.Equals(
                selectedWorldEvil,
                AutoCreateWorldEvil.Crimson,
                StringComparison.Ordinal);
        autoCreateCrimsonBetweenDungeonAndSpawnBox.Enabled = supportsCrimsonCorridor;
        autoCreateCrimsonBetweenDungeonAndSpawnBox.ForeColor = supportsCrimsonCorridor
            ? UiTheme.Text
            : UiTheme.MutedText;
        bool crimsonDistanceEnabled = supportsCrimsonCorridor && autoCreateCrimsonBetweenDungeonAndSpawnBox.Checked;
        foreach (CheckBox button in autoCreateCrimsonDistanceBoxes.Values)
        {
            button.Enabled = crimsonDistanceEnabled;
            UpdateSpecialSeedButtonState(button);
        }

        bool supportsResourceFilter =
            cheatsEnabled &&
            string.Equals(selectedWorldSize, AutoCreateWorldSize.Small, StringComparison.Ordinal) &&
            string.Equals(selectedWorldEvil, AutoCreateWorldEvil.Crimson, StringComparison.Ordinal);
        foreach (CheckBox button in autoCreateResourceItemBoxes.Values)
        {
            button.Enabled = supportsResourceFilter;
            UpdateSpecialSeedButtonState(button);
        }
        UpdateMinimumAvailability(autoCreateLifeCrystalMinimumBoxes, supportsResourceFilter);
        UpdateHookMinimumAvailability(supportsResourceFilter);
        UpdateMinimumAvailability(autoCreateSpelunkerMinimumBoxes, supportsResourceFilter);
        UpdateMinimumAvailability(autoCreateFeatherfallMinimumBoxes, supportsResourceFilter);
        UpdatePyramidItemAvailability();
    }

    private static void UpdateMinimumAvailability(
        IReadOnlyDictionary<int, CheckBox> boxes,
        bool supported)
    {
        bool enabled = supported && boxes.TryGetValue(0, out CheckBox? toggle) && toggle.Checked;
        foreach ((int value, CheckBox button) in boxes)
        {
            button.Enabled = supported && (value == 0 || enabled);
        }
    }

    private void UpdateHookMinimumAvailability(bool supported)
    {
        bool enabled = supported &&
            autoCreateHookMinimumBoxes.TryGetValue(AutoCreateResourceHook.None, out CheckBox? toggle) &&
            toggle.Checked;
        foreach ((string hook, CheckBox button) in autoCreateHookMinimumBoxes)
        {
            button.Enabled = supported && (hook == AutoCreateResourceHook.None || enabled);
        }
    }

    private void SelectMinimum(
        int selectedMinimum,
        bool selected,
        IReadOnlyList<int> values,
        Dictionary<int, CheckBox> boxes)
    {
        if (updatingResourceMinimumSelection)
        {
            return;
        }

        int normalized = selectedMinimum == 0
            ? selected ? values.FirstOrDefault(value => value > 0) : 0
            : values.Contains(selectedMinimum) ? selectedMinimum : 0;
        ApplyMinimumSelection(normalized, boxes);
        UpdatePostGenerationFilterAvailability();
    }

    private void ApplyMinimumSelection(int selectedMinimum, Dictionary<int, CheckBox> boxes)
    {
        updatingResourceMinimumSelection = true;
        try
        {
            bool enabled = selectedMinimum > 0;
            foreach ((int value, CheckBox button) in boxes)
            {
                button.Checked = enabled && (value == 0 || value >= selectedMinimum);
                UpdateSpecialSeedButtonState(button);
            }
        }
        finally
        {
            updatingResourceMinimumSelection = false;
        }
    }

    private static int GetSelectedMinimum(
        IReadOnlyDictionary<int, CheckBox> boxes,
        IReadOnlyList<int> values)
    {
        if (!boxes.TryGetValue(0, out CheckBox? toggle) || !toggle.Checked)
        {
            return 0;
        }

        foreach (int value in values.Where(value => value > 0))
        {
            if (boxes.TryGetValue(value, out CheckBox? button) && button.Checked)
            {
                return value;
            }
        }

        return 0;
    }

    private void SelectHookMinimum(string selectedMinimum, bool selected)
    {
        if (updatingResourceMinimumSelection)
        {
            return;
        }

        string normalized = selectedMinimum == AutoCreateResourceHook.None
            ? selected ? AutoCreateResourceHook.Amethyst : AutoCreateResourceHook.None
            : selectedMinimum;
        ApplyHookMinimumSelection(normalized);
        UpdatePostGenerationFilterAvailability();
    }

    private void ApplyHookMinimumSelection(string selectedMinimum)
    {
        updatingResourceMinimumSelection = true;
        try
        {
            bool enabled = selectedMinimum != AutoCreateResourceHook.None;
            foreach ((string hook, CheckBox button) in autoCreateHookMinimumBoxes)
            {
                button.Checked = enabled &&
                    (hook == AutoCreateResourceHook.None || AutoCreateResourceHook.Includes(selectedMinimum, hook));
                UpdateSpecialSeedButtonState(button);
            }
        }
        finally
        {
            updatingResourceMinimumSelection = false;
        }
    }

    private string GetSelectedHookMinimum()
    {
        if (!autoCreateHookMinimumBoxes.TryGetValue(AutoCreateResourceHook.None, out CheckBox? toggle) ||
            !toggle.Checked)
        {
            return AutoCreateResourceHook.None;
        }

        foreach (string hook in AutoCreateResourceHook.All.Where(hook => hook != AutoCreateResourceHook.None))
        {
            if (autoCreateHookMinimumBoxes.TryGetValue(hook, out CheckBox? button) && button.Checked)
            {
                return hook;
            }
        }

        return AutoCreateResourceHook.None;
    }

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
            foreach ((string distance, CheckBox button) in autoCreateCrimsonDistanceBoxes)
            {
                button.Checked = AutoCreateCrimsonDistance.Includes(selectedDistance, distance);
                UpdateSpecialSeedButtonState(button);
            }
        }
        finally
        {
            updatingCrimsonDistanceSelection = false;
        }
    }

    private string GetSelectedCrimsonDistance()
    {
        for (int index = AutoCreateCrimsonDistance.All.Length - 1; index >= 0; index--)
        {
            string distance = AutoCreateCrimsonDistance.All[index];
            if (autoCreateCrimsonDistanceBoxes.TryGetValue(distance, out CheckBox? button) && button.Checked)
            {
                return distance;
            }
        }

        return AutoCreateCrimsonDistance.Default;
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
