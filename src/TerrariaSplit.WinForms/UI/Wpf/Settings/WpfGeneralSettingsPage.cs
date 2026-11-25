using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TerrariaSplit.UI.Wpf.Controls;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace TerrariaSplit.UI.Wpf.Settings;

internal sealed class WpfGeneralSettingsPage : WpfSettingsPageBase
{
    private HotkeyCaptureBox pauseKeyBox = null!;
    private HotkeyCaptureBox resetKeyBox = null!;
    private HotkeyCaptureBox mouseClickThroughKeyBox = null!;
    private HotkeyCaptureBox createWorldKeyBox = null!;
    private HotkeyCaptureBox practiceWorldKeyBox = null!;
    private WpfCheckBox showMouseClickThroughIndicatorBox = null!;
    private WpfComboBox languageBox = null!;
    private WpfCheckBox alwaysOnTopBox = null!;
    private WpfCheckBox practiceModeBox = null!;
    private WpfTextBox globalScaleBox = null!;

    public override SettingsPageId Id => SettingsPageId.General;

    internal WpfTextBox GlobalScaleBox => globalScaleBox;

    internal HotkeyCaptureBox CreateWorldKeyBox => createWorldKeyBox;

    internal HotkeyCaptureBox PracticeWorldKeyBox => practiceWorldKeyBox;

    protected override FrameworkElement BuildPage(WpfSettingsPageContext context)
    {
        return WpfSettingsUi.CreateScrollPage(BuildSections);
    }

    public override void Apply(AppSettings settings)
    {
        settings.General.Language = languageBox.SelectedItem as string ?? LanguageNames.English;
        settings.Hotkeys.PauseResumeKey = pauseKeyBox.Hotkey.ToString();
        settings.Hotkeys.ResetKey = resetKeyBox.Hotkey.ToString();
        settings.Hotkeys.MouseClickThroughKey = mouseClickThroughKeyBox.Hotkey.ToString();
        settings.Hotkeys.CreateWorldKey = createWorldKeyBox.Hotkey.ToString();
        settings.Hotkeys.PracticeWorldKey = practiceWorldKeyBox.Hotkey.ToString();
        settings.General.ShowMouseClickThroughIndicator = showMouseClickThroughIndicatorBox.IsChecked == true;
        settings.Overlay.Columns.ScalePercent = WpfSettingsValueParser.ParseIntBox(globalScaleBox, 100, 25, 300);
        settings.General.AlwaysOnTop = alwaysOnTopBox.IsChecked == true;
        settings.General.PracticeMode = practiceModeBox.IsChecked == true;
    }

    private void BuildSections(StackPanel parent)
    {
        // Common
        languageBox = WpfSettingsUi.CreateComboBox(WpfSettingsUi.StandardFieldWidth);
        languageBox.Items.Add(LanguageNames.English);
        languageBox.Items.Add(LanguageNames.Chinese);
        languageBox.SelectedItem = LanguageNames.Normalize(Draft.General.Language);

        globalScaleBox = WpfSettingsUi.CreateNumberBox(Draft.Overlay.Columns.ScalePercent, 25, 300);
        alwaysOnTopBox = WpfSettingsUi.CreateToggleSwitch(Draft.General.AlwaysOnTop);

        var commonCard = WpfSettingsUi.CreateCard(Localize("Common"), out StackPanel commonBody);
        WpfSettingsUi.AddSettingRow(commonBody, Localize("Language"), languageBox);
        WpfSettingsUi.AddSettingRow(commonBody, Localize("Global scale %"), globalScaleBox);
        WpfSettingsUi.AddSettingRow(commonBody, Localize("Always on top"), alwaysOnTopBox, isLast: true);
        parent.Children.Add(commonCard);

        // Hotkeys
        pauseKeyBox = WpfSettingsUi.CreateHotkeyBox(Draft.GetPauseResumeKeys());
        resetKeyBox = WpfSettingsUi.CreateHotkeyBox(Draft.GetResetKeys());
        mouseClickThroughKeyBox = WpfSettingsUi.CreateHotkeyBox(Draft.GetMouseClickThroughKeys());
        createWorldKeyBox = WpfSettingsUi.CreateHotkeyBox(Draft.GetCreateWorldKeys());
        practiceWorldKeyBox = WpfSettingsUi.CreateHotkeyBox(Draft.GetPracticeWorldKeys());
        AttachAutomationHotkeyWarnings();

        var hotkeysCard = WpfSettingsUi.CreateCard(
            Localize("Hotkeys"),
            out StackPanel hotkeysBody,
            subtitle: Localize("Hotkeys support a single key, or a Ctrl / Alt / Shift chord. Press Esc in a hotkey box to disable that shortcut."));
        WpfSettingsUi.AddSettingRow(hotkeysBody, Localize("Pause / Resume"), pauseKeyBox);
        WpfSettingsUi.AddSettingRow(hotkeysBody, Localize("Reset (Disabled in world)"), resetKeyBox);
        WpfSettingsUi.AddSettingRow(hotkeysBody, Localize("Mouse passthrough"), mouseClickThroughKeyBox);
        WpfSettingsUi.AddSettingRow(hotkeysBody, Localize("Create world (Disabled in world)"), createWorldKeyBox);
        WpfSettingsUi.AddSettingRow(hotkeysBody, Localize("Load world (Disabled in world)"), practiceWorldKeyBox, isLast: true);
        parent.Children.Add(hotkeysCard);

        // Special Options
        practiceModeBox = WpfSettingsUi.CreateToggleSwitch(Draft.General.PracticeMode);
        showMouseClickThroughIndicatorBox = WpfSettingsUi.CreateToggleSwitch(Draft.General.ShowMouseClickThroughIndicator);

        var specialCard = WpfSettingsUi.CreateCard(Localize("Special Options"), out StackPanel specialBody);
        WpfSettingsUi.AddSettingRow(specialBody, Localize("Allow right-click time editing"), practiceModeBox);
        WpfSettingsUi.AddSettingRow(specialBody, Localize("Mouse passthrough indicator"), showMouseClickThroughIndicatorBox, isLast: true);
        parent.Children.Add(specialCard);
    }

    private void AttachAutomationHotkeyWarnings()
    {
        Keys previousCreateWorldHotkey = createWorldKeyBox.Hotkey;
        createWorldKeyBox.HotkeyCaptured += (_, _) =>
        {
            Keys currentCreateWorldHotkey = createWorldKeyBox.Hotkey;
            if (previousCreateWorldHotkey == Keys.None && currentCreateWorldHotkey != Keys.None)
            {
                Dialogs.ShowWarning(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Localize("Create World hotkey {0} is now active. Please read the Create World notes in the Automation settings tab first. Enabling this blindly may move save files to the backup folder unless existing saves are kept."),
                        HotkeyKeyValidator.Format(currentCreateWorldHotkey)),
                    Localize("Hotkey warning"));
            }

            previousCreateWorldHotkey = currentCreateWorldHotkey;
        };
    }
}
