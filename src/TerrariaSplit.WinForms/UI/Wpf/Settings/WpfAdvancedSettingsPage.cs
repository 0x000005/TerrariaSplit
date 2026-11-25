using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TerrariaSplit.UI.Wpf.Controls;
using TerrariaSplit.UI.Wpf.Interop;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace TerrariaSplit.UI.Wpf.Settings;

internal sealed class WpfAdvancedSettingsPage : WpfSettingsPageBase
{
    private WpfCheckBox enableManualSplitBox = null!;
    private HotkeyCaptureBox manualSplitKeyBox = null!;
    private WpfCheckBox enableTerrariaUiScalePatchBox = null!;
    private WpfCheckBox enableRtssOverlayBox = null!;
    private WpfTextBox rtssExecutablePathBox = null!;
    private WpfTextBox rtssOverlayXBox = null!;
    private WpfTextBox rtssOverlayYBox = null!;
    private WpfTextBox rtssOverlayZoomBox = null!;
    private WpfButton rtssExecutableBrowseButton = null!;
    private WpfComboBox readyWatcherPollHzBox = null!;
    private WpfComboBox readyUiControlHzBox = null!;
    private WpfComboBox runningStatusPaintHzBox = null!;
    private WpfComboBox timerOverlayRefreshHzBox = null!;
    private WpfComboBox minimumLogLevelBox = null!;

    public override SettingsPageId Id => SettingsPageId.Advanced;

    internal WpfCheckBox EnableTerrariaUiScalePatchBox => enableTerrariaUiScalePatchBox;

    internal WpfCheckBox EnableManualSplitBox => enableManualSplitBox;

    internal HotkeyCaptureBox ManualSplitKeyBox => manualSplitKeyBox;

    internal WpfCheckBox EnableRtssOverlayBox => enableRtssOverlayBox;

    internal WpfTextBox RtssExecutablePathBox => rtssExecutablePathBox;

    internal WpfTextBox RtssOverlayXBox => rtssOverlayXBox;

    internal WpfTextBox RtssOverlayYBox => rtssOverlayYBox;

    internal WpfTextBox RtssOverlayZoomBox => rtssOverlayZoomBox;

    internal WpfButton RtssExecutableBrowseButton => rtssExecutableBrowseButton;

    internal WpfComboBox ReadyWatcherPollHzBox => readyWatcherPollHzBox;

    internal WpfComboBox ReadyUiControlHzBox => readyUiControlHzBox;

    internal WpfComboBox RunningStatusPaintHzBox => runningStatusPaintHzBox;

    internal WpfComboBox TimerOverlayRefreshHzBox => timerOverlayRefreshHzBox;

    internal WpfComboBox MinimumLogLevelBox => minimumLogLevelBox;

    protected override FrameworkElement BuildPage(WpfSettingsPageContext context)
    {
        return WpfSettingsUi.CreateScrollPage(BuildSections);
    }

    public override void Apply(AppSettings settings)
    {
        bool enableRtssOverlay = enableRtssOverlayBox.IsChecked == true;
        string rtssExecutablePath = rtssExecutablePathBox.Text.Trim();
        if (enableRtssOverlay && string.IsNullOrWhiteSpace(rtssExecutablePath))
        {
            rtssExecutablePathBox.Focus();
            throw new SettingsApplyFailedException(
                Localize("RTSS executable is required when RTSS fullscreen projection is enabled."));
        }

        int rtssOverlayZoom;
        if (enableRtssOverlay)
        {
            if (!TryParseRtssOverlayZoom(rtssOverlayZoomBox.Text, out rtssOverlayZoom))
            {
                rtssOverlayZoomBox.Focus();
                throw new SettingsApplyFailedException(
                    Localize("RTSS zoom must be an integer from 1 to 8."));
            }
        }
        else
        {
            rtssOverlayZoom = WpfSettingsValueParser.ParseIntBox(rtssOverlayZoomBox, 1, 1, 8);
        }

        settings.Advanced ??= new AdvancedSettings();
        settings.Advanced.MinimumLogLevel = DiagnosticLogLevels.All.ElementAtOrDefault(minimumLogLevelBox.SelectedIndex)
            ?? DiagnosticLogLevels.Default;
        settings.Advanced.EnableManualSplit = enableManualSplitBox.IsChecked == true;
        settings.Hotkeys.ManualSplitKey = manualSplitKeyBox.Hotkey.ToString();
        settings.Advanced.EnableTerrariaUiScalePatch = enableTerrariaUiScalePatchBox.IsChecked == true;
        settings.Advanced.EnableRtssOverlay = enableRtssOverlay;
        settings.Advanced.RtssExecutablePath = rtssExecutablePath;
        settings.Advanced.RtssOverlayX = WpfSettingsValueParser.ParseIntBox(rtssOverlayXBox, 10, -10000, 10000);
        settings.Advanced.RtssOverlayY = WpfSettingsValueParser.ParseIntBox(rtssOverlayYBox, 10, -10000, 10000);
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

    private void BuildSections(StackPanel parent)
    {
        // Manual split
        enableManualSplitBox = WpfSettingsUi.CreateToggleSwitch(Draft.Advanced?.EnableManualSplit == true);
        manualSplitKeyBox = WpfSettingsUi.CreateHotkeyBox(Draft.GetManualSplitKeys());
        enableManualSplitBox.Checked += (_, _) => UpdateManualSplitControlsEnabled();
        enableManualSplitBox.Unchecked += (_, _) => UpdateManualSplitControlsEnabled();

        var manualCard = WpfSettingsUi.CreateCard(Localize("Manual split"), out StackPanel manualBody);
        WpfSettingsUi.AddSettingRow(manualBody, Localize("Enabled"), enableManualSplitBox);
        WpfSettingsUi.AddSettingRow(manualBody, Localize("Hotkey"), manualSplitKeyBox, isLast: true);
        parent.Children.Add(manualCard);
        UpdateManualSplitControlsEnabled();

        // Terraria UI scale enhancement
        enableTerrariaUiScalePatchBox = WpfSettingsUi.CreateToggleSwitch(Draft.Advanced?.EnableTerrariaUiScalePatch == true);
        var uiScaleCard = WpfSettingsUi.CreateCard(
            Localize("Terraria UI scale enhancement"),
            out StackPanel uiScaleBody,
            subtitle: Localize("Raises Terraria's in-game UI scale slider limit from 200% to 300%."));
        uiScaleBody.Children.Add(WpfSettingsUi.CreateNoticeBanner(
            Localize("If Terraria's options menu was already opened before enabling, restart Terraria for the change to take effect.") +
            Environment.NewLine +
            Localize("This changes the running Terraria process memory; enable with caution."),
            isWarning: true));
        WpfSettingsUi.AddSettingRow(
            uiScaleBody,
            Localize("Enabled"),
            enableTerrariaUiScalePatchBox,
            isLast: true);
        parent.Children.Add(uiScaleCard);

        // RTSS fullscreen projection
        enableRtssOverlayBox = WpfSettingsUi.CreateToggleSwitch(Draft.Advanced?.EnableRtssOverlay == true);
        rtssExecutablePathBox = WpfSettingsUi.CreateTextBox(Draft.Advanced?.RtssExecutablePath ?? string.Empty);
        rtssOverlayXBox = WpfSettingsUi.CreateNumberBox(Draft.Advanced?.RtssOverlayX ?? 10, -10000, 10000);
        rtssOverlayYBox = WpfSettingsUi.CreateNumberBox(Draft.Advanced?.RtssOverlayY ?? 10, -10000, 10000);
        rtssOverlayZoomBox = WpfSettingsUi.CreateNumberBox(Draft.Advanced?.RtssOverlayZoom ?? 1, 1, 8);
        enableRtssOverlayBox.Checked += (_, _) => UpdateRtssOverlayControlsEnabled();
        enableRtssOverlayBox.Unchecked += (_, _) => UpdateRtssOverlayControlsEnabled();

        var rtssCard = WpfSettingsUi.CreateCard(
            Localize("RTSS fullscreen projection"),
            out StackPanel rtssBody,
            subtitle: Localize("Writes the timer to RivaTuner Statistics Server so RTSS can draw it over exclusive fullscreen Terraria."));
        rtssBody.Children.Add(WpfSettingsUi.CreateNoticeBanner(
            Localize("If RTSS is running as administrator, TerrariaSplit also needs administrator privileges."),
            isWarning: true));
        WpfSettingsUi.AddSettingRow(rtssBody, Localize("Enabled"), enableRtssOverlayBox);
        WpfSettingsUi.AddSettingRow(rtssBody, Localize("RTSS executable"), CreateRtssExecutablePicker());
        WpfSettingsUi.AddSettingRow(rtssBody, Localize("X position"), rtssOverlayXBox);
        WpfSettingsUi.AddSettingRow(rtssBody, Localize("Y position"), rtssOverlayYBox);
        WpfSettingsUi.AddSettingRow(rtssBody, Localize("Zoom"), rtssOverlayZoomBox, isLast: true);
        parent.Children.Add(rtssCard);
        UpdateRtssOverlayControlsEnabled();

        // Performance
        readyWatcherPollHzBox = CreateFrequencyBox(
            RefreshRateSettings.ReadyWatcherPollHzOptions,
            RefreshRateSettings.NormalizeReadyWatcherPollHz(
                Draft.Advanced?.ReadyWatcherPollHz ?? AppSettingsDefaults.Advanced.ReadyWatcherPollHz));
        readyUiControlHzBox = CreateFrequencyBox(
            RefreshRateSettings.StandardRefreshHzOptions,
            RefreshRateSettings.NormalizeReadyUiControlHz(
                Draft.Advanced?.ReadyUiControlHz ?? AppSettingsDefaults.Advanced.ReadyUiControlHz));
        runningStatusPaintHzBox = CreateFrequencyBox(
            RefreshRateSettings.PaintRefreshHzOptions,
            RefreshRateSettings.NormalizeRunningStatusPaintHz(
                Draft.Advanced?.RunningStatusPaintHz ?? AppSettingsDefaults.Advanced.RunningStatusPaintHz));
        timerOverlayRefreshHzBox = CreateFrequencyBox(
            RefreshRateSettings.PaintRefreshHzOptions,
            RefreshRateSettings.NormalizeTimerOverlayRefreshHz(
                Draft.Advanced?.TimerOverlayRefreshHz ?? AppSettingsDefaults.Advanced.TimerOverlayRefreshHz));

        var perfCard = WpfSettingsUi.CreateCard(Localize("Performance"), out StackPanel perfBody);
        WpfSettingsUi.AddSettingRow(perfBody, Localize("Sampling frequency"), readyWatcherPollHzBox);
        WpfSettingsUi.AddSettingRow(perfBody, Localize("Control frequency"), readyUiControlHzBox);
        WpfSettingsUi.AddSettingRow(perfBody, Localize("Split timer refresh rate"), runningStatusPaintHzBox);
        WpfSettingsUi.AddSettingRow(perfBody, Localize("Main timer refresh rate"), timerOverlayRefreshHzBox, isLast: true);
        parent.Children.Add(perfCard);

        // Logging
        minimumLogLevelBox = WpfSettingsUi.CreateComboBox(WpfSettingsUi.StandardFieldWidth);
        foreach (string level in DiagnosticLogLevels.All)
        {
            minimumLogLevelBox.Items.Add(level == DiagnosticLogLevels.None ? Localize("No logging") : level);
        }

        minimumLogLevelBox.SelectedIndex = DiagnosticLogLevels.All.ToList().IndexOf(
            DiagnosticLogLevels.Normalize(Draft.Advanced?.MinimumLogLevel));

        WpfButton exportDiagnostics = WpfSettingsUi.CreateButton(Localize("Export diagnostics"), minWidth: 112);
        exportDiagnostics.HorizontalAlignment = WpfHorizontalAlignment.Left;
        exportDiagnostics.Margin = new Thickness(0, 8, 0, 4);
        exportDiagnostics.Click += async (_, _) =>
            await DiagnosticExportDialog.ExportAsync(WpfWindowInterop.AsWin32Window(Owner.Window));

        var loggingCard = WpfSettingsUi.CreateCard(Localize("Logging"), out StackPanel loggingBody);
        WpfSettingsUi.AddSettingRow(loggingBody, Localize("Minimum log level"), minimumLogLevelBox);
        loggingBody.Children.Add(exportDiagnostics);
        parent.Children.Add(loggingCard);
    }

    private FrameworkElement CreateRtssExecutablePicker()
    {
        Grid grid = WpfSettingsUi.CreateColumnGrid(new GridLength(280), GridLength.Auto);
        rtssExecutablePathBox.Margin = new Thickness(0, 0, 6, 0);

        rtssExecutableBrowseButton = WpfSettingsUi.CreateButton(Localize("Browse"), minWidth: 68);
        rtssExecutableBrowseButton.Click += (_, _) => Dialogs.PickFile(
            rtssExecutablePathBox,
            "Choose RTSS.exe",
            "RTSS executable|RTSS.exe|Applications|*.exe|All files|*.*");

        Grid.SetColumn(rtssExecutablePathBox, 0);
        Grid.SetColumn(rtssExecutableBrowseButton, 1);
        grid.Children.Add(rtssExecutablePathBox);
        grid.Children.Add(rtssExecutableBrowseButton);
        return grid;
    }

    private void UpdateManualSplitControlsEnabled()
    {
        manualSplitKeyBox.IsEnabled = enableManualSplitBox.IsChecked == true;
    }

    private void UpdateRtssOverlayControlsEnabled()
    {
        bool enabled = enableRtssOverlayBox.IsChecked == true;
        rtssExecutablePathBox.IsEnabled = enabled;
        rtssExecutableBrowseButton.IsEnabled = enabled;
        rtssOverlayXBox.IsEnabled = enabled;
        rtssOverlayYBox.IsEnabled = enabled;
        rtssOverlayZoomBox.IsEnabled = enabled;
    }

    private static WpfComboBox CreateFrequencyBox(IReadOnlyList<int> options, int selected)
    {
        WpfComboBox comboBox = WpfSettingsUi.CreateComboBox(140);
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

        return comboBox;
    }

    private static int GetSelectedFrequency(WpfComboBox comboBox, int fallback)
    {
        return comboBox.SelectedItem is FrequencyOption option ? option.Hz : fallback;
    }

    private static bool TryParseRtssOverlayZoom(string text, out int value)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
            value >= 1 &&
            value <= 8;
    }

    private sealed record FrequencyOption(int Hz)
    {
        public override string ToString()
        {
            return Hz.ToString(CultureInfo.InvariantCulture) + " Hz";
        }
    }
}
