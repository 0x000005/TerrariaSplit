using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class AdvancedSettingsPage : SettingsPageBase
{
    private readonly CheckBox enableTerrariaUiScalePatchBox = new();
    private readonly ThemedDropDownList readyWatcherPollHzBox = new();
    private readonly ThemedDropDownList readyUiControlHzBox = new();
    private readonly ThemedDropDownList runningStatusPaintHzBox = new();
    private readonly ThemedDropDownList timerOverlayRefreshHzBox = new();

    public override SettingsPageId Id => SettingsPageId.Advanced;

    internal CheckBox EnableTerrariaUiScalePatchBox => enableTerrariaUiScalePatchBox;
    internal ThemedDropDownList ReadyWatcherPollHzBox => readyWatcherPollHzBox;
    internal ThemedDropDownList ReadyUiControlHzBox => readyUiControlHzBox;
    internal ThemedDropDownList RunningStatusPaintHzBox => runningStatusPaintHzBox;
    internal ThemedDropDownList TimerOverlayRefreshHzBox => timerOverlayRefreshHzBox;

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
            AppSettingsDefaults.Advanced.ReadyWatcherPollHz);
        settings.Advanced.ReadyUiControlHz = GetSelectedFrequency(
            readyUiControlHzBox,
            AppSettingsDefaults.Advanced.ReadyUiControlHz);
        settings.Advanced.RunningStatusPaintHz = GetSelectedFrequency(
            runningStatusPaintHzBox,
            AppSettingsDefaults.Advanced.RunningStatusPaintHz);
        settings.Advanced.TimerOverlayRefreshHz = GetSelectedFrequency(
            timerOverlayRefreshHzBox,
            AppSettingsDefaults.Advanced.TimerOverlayRefreshHz);
    }

    private void BuildSections(TableLayoutPanel parent)
    {
        ConfigureCheckBox(enableTerrariaUiScalePatchBox, Draft.Advanced?.EnableTerrariaUiScalePatch == true);
        ConfigureFrequencyBox(
            readyWatcherPollHzBox,
            RefreshRateSettings.ReadyWatcherPollHzOptions,
            RefreshRateSettings.NormalizeReadyWatcherPollHz(
                Draft.Advanced?.ReadyWatcherPollHz ?? AppSettingsDefaults.Advanced.ReadyWatcherPollHz));
        ConfigureFrequencyBox(
            readyUiControlHzBox,
            RefreshRateSettings.StandardRefreshHzOptions,
            RefreshRateSettings.NormalizeReadyUiControlHz(
                Draft.Advanced?.ReadyUiControlHz ?? AppSettingsDefaults.Advanced.ReadyUiControlHz));
        ConfigureFrequencyBox(
            runningStatusPaintHzBox,
            RefreshRateSettings.PaintRefreshHzOptions,
            RefreshRateSettings.NormalizeRunningStatusPaintHz(
                Draft.Advanced?.RunningStatusPaintHz ?? AppSettingsDefaults.Advanced.RunningStatusPaintHz));
        ConfigureFrequencyBox(
            timerOverlayRefreshHzBox,
            RefreshRateSettings.PaintRefreshHzOptions,
            RefreshRateSettings.NormalizeTimerOverlayRefreshHz(
                Draft.Advanced?.TimerOverlayRefreshHz ?? AppSettingsDefaults.Advanced.TimerOverlayRefreshHz));

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

        TableLayoutPanel performanceSection = Factory.CreateSection("Performance");
        TableLayoutPanel performanceGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(performanceGrid, "Sampling frequency", readyWatcherPollHzBox);
        Factory.AddSettingRow(performanceGrid, "Control frequency", readyUiControlHzBox);
        Factory.AddSettingRow(performanceGrid, "Split timer refresh rate", runningStatusPaintHzBox);
        Factory.AddSettingRow(performanceGrid, "Main timer refresh rate", timerOverlayRefreshHzBox);
        SettingsUiFactory.AddSectionControl(performanceSection, performanceGrid);
        SettingsUiFactory.AddSection(parent, performanceSection);
    }

    private static void ConfigureCheckBox(CheckBox checkBox, bool selected)
    {
        checkBox.Checked = selected;
        checkBox.Dock = DockStyle.Fill;
        UiTheme.StyleCheckBox(checkBox);
    }

    private static void ConfigureFrequencyBox(ThemedDropDownList comboBox, IReadOnlyList<int> options, int selected)
    {
        FillFrequencyBox(comboBox, options, selected);
        comboBox.Dock = DockStyle.Fill;
    }

    private static void FillFrequencyBox(ThemedDropDownList comboBox, IReadOnlyList<int> options, int selected)
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
    }

    private static int GetSelectedFrequency(ThemedDropDownList comboBox, int fallback)
    {
        return comboBox.SelectedItem is FrequencyOption option ? option.Hz : fallback;
    }

    private sealed record FrequencyOption(int Hz)
    {
        public override string ToString()
        {
            return Hz.ToString(System.Globalization.CultureInfo.InvariantCulture) + " Hz";
        }
    }
}
