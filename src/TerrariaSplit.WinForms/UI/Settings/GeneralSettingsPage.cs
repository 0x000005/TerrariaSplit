using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class GeneralSettingsPage : SettingsPageBase
{
    private readonly SettingsHotkeyTextBox pauseKeyBox = new();
    private readonly SettingsHotkeyTextBox resetKeyBox = new();
    private readonly SettingsHotkeyTextBox mouseClickThroughKeyBox = new();
    private readonly SettingsHotkeyTextBox createWorldKeyBox = new();
    private readonly SettingsHotkeyTextBox practiceWorldKeyBox = new();
    private readonly CheckBox showMouseClickThroughIndicatorBox = new();
    private readonly ThemedDropDownList languageBox = new();
    private readonly CheckBox alwaysOnTopBox = new();
    private readonly CheckBox practiceModeBox = new();
    private readonly TextBox globalScaleBox = new();

    public override SettingsPageId Id => SettingsPageId.General;

    internal TextBox GlobalScaleBox => globalScaleBox;

    internal SettingsHotkeyTextBox CreateWorldKeyBox => createWorldKeyBox;

    internal SettingsHotkeyTextBox PracticeWorldKeyBox => practiceWorldKeyBox;

    protected override Control BuildPage(SettingsPageContext context)
    {
        return context.BuildScrollPage(BuildSections);
    }

    public override void Apply(AppSettings settings)
    {
        settings.General.Language = languageBox.SelectedItem as string ?? LanguageNames.English;
        settings.Hotkeys.PauseResumeKey = pauseKeyBox.Hotkey.ToString();
        settings.Hotkeys.ResetKey = resetKeyBox.Hotkey.ToString();
        settings.Hotkeys.MouseClickThroughKey = mouseClickThroughKeyBox.Hotkey.ToString();
        settings.Hotkeys.CreateWorldKey = createWorldKeyBox.Hotkey.ToString();
        settings.Hotkeys.PracticeWorldKey = practiceWorldKeyBox.Hotkey.ToString();
        settings.General.ShowMouseClickThroughIndicator = showMouseClickThroughIndicatorBox.Checked;
        settings.Overlay.Columns.ScalePercent = SettingsValueParser.ParseIntBox(globalScaleBox, 100, 25, 300);
        settings.General.AlwaysOnTop = alwaysOnTopBox.Checked;
        settings.General.PracticeMode = practiceModeBox.Checked;
    }

    private void BuildSections(TableLayoutPanel parent)
    {
        ConfigureHotkeyBox(pauseKeyBox, Draft.GetPauseResumeKeys());
        ConfigureHotkeyBox(resetKeyBox, Draft.GetResetKeys());
        ConfigureHotkeyBox(mouseClickThroughKeyBox, Draft.GetMouseClickThroughKeys());
        ConfigureHotkeyBox(createWorldKeyBox, Draft.GetCreateWorldKeys());
        ConfigureHotkeyBox(practiceWorldKeyBox, Draft.GetPracticeWorldKeys());
        AttachAutomationHotkeyWarnings();

        ConfigureCheckBox(showMouseClickThroughIndicatorBox, Draft.General.ShowMouseClickThroughIndicator);
        ConfigureCheckBox(alwaysOnTopBox, Draft.General.AlwaysOnTop);
        ConfigureCheckBox(practiceModeBox, Draft.General.PracticeMode);

        languageBox.Dock = DockStyle.Fill;
        languageBox.Items.Add(LanguageNames.English);
        languageBox.Items.Add(LanguageNames.Chinese);
        languageBox.SelectedItem = LanguageNames.Normalize(Draft.General.Language);

        UiTheme.StyleTextBox(globalScaleBox);
        globalScaleBox.Dock = DockStyle.Fill;
        globalScaleBox.Text = Math.Clamp(Draft.Overlay.Columns.ScalePercent, 25, 300).ToString(System.Globalization.CultureInfo.InvariantCulture);

        TableLayoutPanel commonSection = Factory.CreateSection("Common");
        TableLayoutPanel commonGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(commonGrid, "Language", languageBox);
        Factory.AddSettingRow(commonGrid, "Global scale %", globalScaleBox);
        Factory.AddSettingRow(commonGrid, "Always on top", alwaysOnTopBox);
        SettingsUiFactory.AddSectionControl(commonSection, commonGrid);
        SettingsUiFactory.AddSection(parent, commonSection);

        TableLayoutPanel hotkeysSection = Factory.CreateSection("Hotkeys");
        SettingsUiFactory.AddSectionControl(
            hotkeysSection,
            Factory.CreateMutedLabel("Hotkeys support a single key, or a Ctrl / Alt / Shift chord. Press Esc in a hotkey box to disable that shortcut."));
        TableLayoutPanel hotkeysGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(hotkeysGrid, "Pause / Resume", pauseKeyBox);
        Factory.AddSettingRow(hotkeysGrid, "Reset (Disabled in world)", resetKeyBox);
        Factory.AddSettingRow(hotkeysGrid, "Mouse passthrough", mouseClickThroughKeyBox);
        Factory.AddSettingRow(hotkeysGrid, "Create world (Disabled in world)", createWorldKeyBox);
        Factory.AddSettingRow(hotkeysGrid, "Load world (Disabled in world)", practiceWorldKeyBox);
        SettingsUiFactory.AddSectionControl(hotkeysSection, hotkeysGrid);
        SettingsUiFactory.AddSection(parent, hotkeysSection);

        TableLayoutPanel specialSection = Factory.CreateSection("Special Options");
        TableLayoutPanel specialGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(specialGrid, "Allow manual time editing", practiceModeBox);
        Factory.AddSettingRow(specialGrid, "Mouse passthrough indicator", showMouseClickThroughIndicatorBox);
        SettingsUiFactory.AddSectionControl(specialSection, specialGrid);
        SettingsUiFactory.AddSection(parent, specialSection);
    }

    private static void ConfigureHotkeyBox(SettingsHotkeyTextBox textBox, Keys selected)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.ReadOnly = true;
        UiTheme.StyleTextBox(textBox);
        textBox.SetHotkey(selected);
    }

    private void AttachAutomationHotkeyWarnings()
    {
        Keys previousCreateWorldHotkey = createWorldKeyBox.Hotkey;
        createWorldKeyBox.HotkeyCaptured += (_, _) =>
        {
            Keys currentCreateWorldHotkey = createWorldKeyBox.Hotkey;
            if (previousCreateWorldHotkey == Keys.None && currentCreateWorldHotkey != Keys.None)
            {
                ShowAutomationHotkeyWarning(
                    createWorldKeyBox,
                    "Create World hotkey {0} is now active. Please read the Create World notes in the Automation settings tab first. Enabling this blindly may delete your save files by mistake.");
            }

            previousCreateWorldHotkey = currentCreateWorldHotkey;
        };
    }

    private void ShowAutomationHotkeyWarning(SettingsHotkeyTextBox textBox, string messageKey)
    {
        if (textBox.Hotkey == Keys.None)
        {
            return;
        }

        Dialogs.ShowWarning(
            string.Format(
                CultureInfo.CurrentCulture,
                Context.Localize(messageKey),
                HotkeyKeyValidator.Format(textBox.Hotkey)),
            Context.Localize("Hotkey warning"));
    }

    private static void ConfigureCheckBox(CheckBox checkBox, bool selected)
    {
        checkBox.Checked = selected;
        checkBox.Dock = DockStyle.Fill;
        UiTheme.StyleCheckBox(checkBox);
    }
}
