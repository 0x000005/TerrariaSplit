using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class AdvancedSettingsPage : SettingsPageBase
{
    private readonly CheckBox enableTerrariaUiScalePatchBox = new();
    private readonly ComboBox readyWatcherPollHzBox = new();
    private readonly ComboBox readyUiControlHzBox = new();
    private readonly ComboBox runningStatusPaintHzBox = new();
    private readonly ComboBox timerOverlayRefreshModeBox = new();
    private readonly ComboBox timerOverlayRefreshHzBox = new();

    public override SettingsPageId Id => SettingsPageId.Advanced;

    internal CheckBox EnableTerrariaUiScalePatchBox => enableTerrariaUiScalePatchBox;
    internal ComboBox ReadyWatcherPollHzBox => readyWatcherPollHzBox;
    internal ComboBox ReadyUiControlHzBox => readyUiControlHzBox;
    internal ComboBox RunningStatusPaintHzBox => runningStatusPaintHzBox;
    internal ComboBox TimerOverlayRefreshModeBox => timerOverlayRefreshModeBox;
    internal ComboBox TimerOverlayRefreshHzBox => timerOverlayRefreshHzBox;

    protected override Control BuildPage(SettingsPageContext context)
    {
        return context.BuildScrollPage(BuildSections);
    }

    public override void Apply(AppSettings settings)
    {
        settings.Advanced ??= new AdvancedSettings();
        settings.Advanced.EnableTerrariaUiScalePatch = enableTerrariaUiScalePatchBox.Checked;
        settings.Advanced.ReadyWatcherPollHz = GetSelectedFrequency(
            readyWatcherPollHzBox,
            AdvancedSettings.DefaultReadyWatcherPollHz);
        settings.Advanced.ReadyUiControlHz = GetSelectedFrequency(
            readyUiControlHzBox,
            AdvancedSettings.DefaultReadyUiControlHz);
        settings.Advanced.RunningStatusPaintHz = GetSelectedFrequency(
            runningStatusPaintHzBox,
            AdvancedSettings.DefaultRunningStatusPaintHz);
        settings.Advanced.TimerOverlayRefreshMode = GetSelectedRefreshMode();
        settings.Advanced.TimerOverlayRefreshHz = GetSelectedFrequency(
            timerOverlayRefreshHzBox,
            AdvancedSettings.DefaultTimerOverlayRefreshHz);
    }

    private void BuildSections(TableLayoutPanel parent)
    {
        ConfigureCheckBox(enableTerrariaUiScalePatchBox, Draft.Advanced?.EnableTerrariaUiScalePatch == true);
        ConfigureFrequencyBox(
            readyWatcherPollHzBox,
            RefreshRateSettings.ReadyWatcherPollHzOptions,
            RefreshRateSettings.NormalizeReadyWatcherPollHz(
                Draft.Advanced?.ReadyWatcherPollHz ?? AdvancedSettings.DefaultReadyWatcherPollHz));
        ConfigureFrequencyBox(
            readyUiControlHzBox,
            RefreshRateSettings.StandardRefreshHzOptions,
            RefreshRateSettings.NormalizeReadyUiControlHz(
                Draft.Advanced?.ReadyUiControlHz ?? AdvancedSettings.DefaultReadyUiControlHz));
        ConfigureFrequencyBox(
            runningStatusPaintHzBox,
            RefreshRateSettings.StandardRefreshHzOptions,
            RefreshRateSettings.NormalizeRunningStatusPaintHz(
                Draft.Advanced?.RunningStatusPaintHz ?? AdvancedSettings.DefaultRunningStatusPaintHz));
        ConfigureRefreshModeBox(timerOverlayRefreshModeBox, Draft.Advanced?.TimerOverlayRefreshMode);
        ConfigureFrequencyBox(
            timerOverlayRefreshHzBox,
            RefreshRateSettings.StandardRefreshHzOptions,
            RefreshRateSettings.NormalizeTimerOverlayRefreshHz(
                Draft.Advanced?.TimerOverlayRefreshHz ?? AdvancedSettings.DefaultTimerOverlayRefreshHz));
        timerOverlayRefreshModeBox.SelectedIndexChanged += (_, _) => UpdateRefreshRateAvailability();
        UpdateRefreshRateAvailability();

        TableLayoutPanel uiScaleSection = Factory.CreateSection("Terraria UI scale enhancement");
        TableLayoutPanel uiScaleGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(uiScaleGrid, "Enabled", enableTerrariaUiScalePatchBox);
        SettingsUiFactory.AddSectionControl(uiScaleSection, uiScaleGrid);
        SettingsUiFactory.AddSectionControl(
            uiScaleSection,
            Factory.CreateWrappedFieldLabel(
                "Raises Terraria's in-game UI scale slider limit from 200% to 300%.",
                UiTheme.MutedText));
        SettingsUiFactory.AddSectionControl(
            uiScaleSection,
            Factory.CreateWrappedFieldLabel(
                "If Terraria's options menu was already opened before enabling, restart Terraria for the change to take effect.",
                Color.FromArgb(255, 210, 120)));
        SettingsUiFactory.AddSectionControl(
            uiScaleSection,
            Factory.CreateWrappedFieldLabel(
                "This changes the running Terraria process memory; enable with caution.",
                Color.FromArgb(255, 210, 120)));
        SettingsUiFactory.AddSection(parent, uiScaleSection);

        TableLayoutPanel readyRefreshSection = Factory.CreateSection("Ready refresh");
        TableLayoutPanel readyRefreshGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(readyRefreshGrid, "Timer sampling Hz", readyWatcherPollHzBox);
        Factory.AddSettingRow(readyRefreshGrid, "UI control Hz", readyUiControlHzBox);
        SettingsUiFactory.AddSectionControl(readyRefreshSection, readyRefreshGrid);
        SettingsUiFactory.AddSectionControl(
            readyRefreshSection,
            Factory.CreateWrappedFieldLabel(
                "Used after Terraria is attached and memory is ready.",
                UiTheme.MutedText));
        SettingsUiFactory.AddSection(parent, readyRefreshSection);

        TableLayoutPanel runningRefreshSection = Factory.CreateSection("Running overlay refresh");
        TableLayoutPanel runningRefreshGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(runningRefreshGrid, "Status paint Hz", runningStatusPaintHzBox);
        Factory.AddSettingRow(runningRefreshGrid, "Timer paint mode", timerOverlayRefreshModeBox);
        Factory.AddSettingRow(runningRefreshGrid, "Timer paint Hz", timerOverlayRefreshHzBox);
        SettingsUiFactory.AddSectionControl(runningRefreshSection, runningRefreshGrid);
        SettingsUiFactory.AddSectionControl(
            runningRefreshSection,
            Factory.CreateWrappedFieldLabel(
                "These values are used while the timer is running in a world. Auto timer paint follows the current display refresh rate.",
                UiTheme.MutedText));
        SettingsUiFactory.AddSection(parent, runningRefreshSection);
    }

    private void UpdateRefreshRateAvailability()
    {
        bool fixedMode = string.Equals(GetSelectedRefreshMode(), TimerOverlayRefreshModes.Fixed, StringComparison.OrdinalIgnoreCase);
        timerOverlayRefreshHzBox.Enabled = fixedMode;
        timerOverlayRefreshHzBox.ForeColor = fixedMode ? UiTheme.Text : UiTheme.MutedText;
    }

    private static void ConfigureCheckBox(CheckBox checkBox, bool selected)
    {
        checkBox.Checked = selected;
        checkBox.Dock = DockStyle.Fill;
        UiTheme.StyleCheckBox(checkBox);
    }

    private void ConfigureRefreshModeBox(ComboBox comboBox, string? selected)
    {
        comboBox.Items.Clear();
        foreach (string option in TimerOverlayRefreshModes.All)
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
        comboBox.Dock = DockStyle.Fill;
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        UiTheme.StyleComboBox(comboBox);
    }

    private string GetSelectedRefreshMode()
    {
        return timerOverlayRefreshModeBox.SelectedItem switch
        {
            LocalizedOption option => TimerOverlayRefreshModes.Normalize(option.Value),
            string value => TimerOverlayRefreshModes.Normalize(value),
            _ => TimerOverlayRefreshModes.Auto
        };
    }

    private static void ConfigureFrequencyBox(ComboBox comboBox, IReadOnlyList<int> options, int selected)
    {
        comboBox.Items.Clear();
        foreach (int hz in options)
        {
            comboBox.Items.Add(new FrequencyOption(hz));
        }

        comboBox.SelectedItem = comboBox.Items
            .Cast<FrequencyOption>()
            .FirstOrDefault(option => option.Hz == selected);
        if (comboBox.SelectedIndex < 0 && comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }

        comboBox.Dock = DockStyle.Fill;
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        UiTheme.StyleComboBox(comboBox);
    }

    private static int GetSelectedFrequency(ComboBox comboBox, int fallback)
    {
        return comboBox.SelectedItem is FrequencyOption option ? option.Hz : fallback;
    }

    private sealed record LocalizedOption(string Value, string DisplayName)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }

    private sealed record FrequencyOption(int Hz)
    {
        public override string ToString()
        {
            return Hz.ToString(System.Globalization.CultureInfo.InvariantCulture) + " Hz";
        }
    }
}
