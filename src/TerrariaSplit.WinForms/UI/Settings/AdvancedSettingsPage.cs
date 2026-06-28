using System.Globalization;
using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class AdvancedSettingsPage : SettingsPageBase
{
    private readonly CheckBox enableTerrariaUiScalePatchBox = new();
    private readonly CheckBox enableRtssOverlayBox = new();
    private readonly TextBox rtssExecutablePathBox = new();
    private readonly TextBox rtssOverlayXBox = new();
    private readonly TextBox rtssOverlayYBox = new();
    private readonly TextBox rtssOverlayZoomBox = new();
    private Button? rtssExecutableBrowseButton;
    private readonly ThemedDropDownList readyWatcherPollHzBox = new();
    private readonly ThemedDropDownList readyUiControlHzBox = new();
    private readonly ThemedDropDownList runningStatusPaintHzBox = new();
    private readonly ThemedDropDownList timerOverlayRefreshHzBox = new();

    public override SettingsPageId Id => SettingsPageId.Advanced;

    internal CheckBox EnableTerrariaUiScalePatchBox => enableTerrariaUiScalePatchBox;
    internal CheckBox EnableRtssOverlayBox => enableRtssOverlayBox;
    internal TextBox RtssExecutablePathBox => rtssExecutablePathBox;
    internal TextBox RtssOverlayXBox => rtssOverlayXBox;
    internal TextBox RtssOverlayYBox => rtssOverlayYBox;
    internal TextBox RtssOverlayZoomBox => rtssOverlayZoomBox;
    internal Button? RtssExecutableBrowseButton => rtssExecutableBrowseButton;
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
        bool enableRtssOverlay = enableRtssOverlayBox.Checked;
        string rtssExecutablePath = rtssExecutablePathBox.Text.Trim();
        if (enableRtssOverlay && string.IsNullOrWhiteSpace(rtssExecutablePath))
        {
            rtssExecutablePathBox.Focus();
            throw new SettingsApplyFailedException(
                Context.Localize("RTSS executable is required when RTSS fullscreen projection is enabled."));
        }

        int rtssOverlayZoom;
        if (enableRtssOverlay)
        {
            if (!TryParseRtssOverlayZoom(rtssOverlayZoomBox.Text, out rtssOverlayZoom))
            {
                rtssOverlayZoomBox.Focus();
                throw new SettingsApplyFailedException(
                    Context.Localize("RTSS zoom must be an integer from 1 to 8."));
            }
        }
        else
        {
            rtssOverlayZoom = SettingsValueParser.ParseIntBox(rtssOverlayZoomBox, 1, 1, 8);
        }

        settings.Advanced ??= new AdvancedSettings();
        settings.Advanced.EnableTerrariaUiScalePatch = enableTerrariaUiScalePatchBox.Checked;
        settings.Advanced.EnableRtssOverlay = enableRtssOverlay;
        settings.Advanced.RtssExecutablePath = rtssExecutablePath;
        settings.Advanced.RtssOverlayX = SettingsValueParser.ParseIntBox(rtssOverlayXBox, 10, -10000, 10000);
        settings.Advanced.RtssOverlayY = SettingsValueParser.ParseIntBox(rtssOverlayYBox, 10, -10000, 10000);
        settings.Advanced.RtssOverlayZoom = rtssOverlayZoom;
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
        ConfigureCheckBox(enableRtssOverlayBox, Draft.Advanced?.EnableRtssOverlay == true);
        ConfigurePathBox(rtssExecutablePathBox, Draft.Advanced?.RtssExecutablePath ?? string.Empty);
        ConfigureNumberBox(rtssOverlayXBox, Draft.Advanced?.RtssOverlayX ?? 10);
        ConfigureNumberBox(rtssOverlayYBox, Draft.Advanced?.RtssOverlayY ?? 10);
        ConfigureNumberBox(rtssOverlayZoomBox, Draft.Advanced?.RtssOverlayZoom ?? 1);
        enableRtssOverlayBox.CheckedChanged += (_, _) => UpdateRtssOverlayControlsEnabled();
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

        TableLayoutPanel rtssSection = Factory.CreateSection("RTSS fullscreen projection");
        TableLayoutPanel rtssGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(520f));
        Factory.AddSettingRow(rtssGrid, "Enabled", enableRtssOverlayBox);
        Factory.AddSettingRow(rtssGrid, "RTSS executable", CreateRtssExecutablePicker());
        Factory.AddSettingRow(rtssGrid, "X position", rtssOverlayXBox);
        Factory.AddSettingRow(rtssGrid, "Y position", rtssOverlayYBox);
        Factory.AddSettingRow(rtssGrid, "Zoom", rtssOverlayZoomBox);
        SettingsUiFactory.AddSectionControl(rtssSection, rtssGrid);
        SettingsUiFactory.AddSectionControl(
            rtssSection,
            Factory.CreateWrappedFieldLabel(
                "Writes the timer to RivaTuner Statistics Server so RTSS can draw it over exclusive fullscreen Terraria.",
                UiTheme.MutedText));
        SettingsUiFactory.AddSectionControl(
            rtssSection,
            Factory.CreateWrappedFieldLabel(
                "If RTSS is running as administrator, TerrariaSplit also needs administrator privileges.",
                Color.FromArgb(255, 210, 120)));
        SettingsUiFactory.AddSection(parent, rtssSection);
        UpdateRtssOverlayControlsEnabled();

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

    private static void ConfigurePathBox(TextBox textBox, string value)
    {
        textBox.Text = value;
        textBox.Dock = DockStyle.Fill;
        UiTheme.StyleTextBox(textBox);
    }

    private static void ConfigureNumberBox(TextBox textBox, int value)
    {
        textBox.Text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        textBox.Dock = DockStyle.Fill;
        UiTheme.StyleTextBox(textBox);
    }

    private static bool TryParseRtssOverlayZoom(string text, out int value)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
            value >= 1 &&
            value <= 8;
    }

    private void UpdateRtssOverlayControlsEnabled()
    {
        bool enabled = enableRtssOverlayBox.Checked;
        rtssExecutablePathBox.Enabled = enabled;
        if (rtssExecutableBrowseButton is not null)
        {
            rtssExecutableBrowseButton.Enabled = enabled;
        }

        rtssOverlayXBox.Enabled = enabled;
        rtssOverlayYBox.Enabled = enabled;
        rtssOverlayZoomBox.Enabled = enabled;
    }

    private Control CreateRtssExecutablePicker()
    {
        TableLayoutPanel panel = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(144f));
        panel.Margin = Padding.Empty;
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));
        panel.RowCount = 1;

        Button browseButton = Factory.CreateSmallButton("Browse");
        rtssExecutableBrowseButton = browseButton;
        browseButton.Click += (_, _) => Dialogs.PickFile(
            rtssExecutablePathBox,
            "Choose RTSS.exe",
            "RTSS executable|RTSS.exe|Applications|*.exe|All files|*.*");

        panel.Controls.Add(rtssExecutablePathBox, 0, 0);
        panel.Controls.Add(browseButton, 1, 0);
        return panel;
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
