using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class GeneralSettingsPage : SettingsPageBase
{
    private readonly SettingsHotkeyTextBox pauseKeyBox = new();
    private readonly SettingsHotkeyTextBox resetKeyBox = new();
    private readonly SettingsHotkeyTextBox mouseClickThroughKeyBox = new();
    private readonly SettingsHotkeyTextBox createWorldKeyBox = new();
    private readonly SettingsHotkeyTextBox practiceWorldKeyBox = new();
    private readonly CheckBox showMouseClickThroughIndicatorBox = new();
    private readonly ComboBox languageBox = new();
    private readonly CheckBox alwaysOnTopBox = new();
    private readonly CheckBox practiceModeBox = new();
    private readonly TextBox globalScaleBox = new();

    public override SettingsPageId Id => SettingsPageId.General;

    internal TextBox GlobalScaleBox => globalScaleBox;

    internal SettingsHotkeyTextBox PracticeWorldKeyBox => practiceWorldKeyBox;

    protected override Control BuildPage(SettingsPageContext context)
    {
        return context.BuildScrollPage(BuildSections);
    }

    public override void Apply(AppSettings settings)
    {
        settings.Language = languageBox.SelectedItem as string ?? LanguageNames.English;
        settings.PauseResumeKey = pauseKeyBox.Hotkey.ToString();
        settings.ResetKey = resetKeyBox.Hotkey.ToString();
        settings.MouseClickThroughKey = mouseClickThroughKeyBox.Hotkey.ToString();
        settings.CreateWorldKey = createWorldKeyBox.Hotkey.ToString();
        settings.PracticeWorldKey = practiceWorldKeyBox.Hotkey.ToString();
        settings.ShowMouseClickThroughIndicator = showMouseClickThroughIndicatorBox.Checked;
        settings.Columns.ScalePercent = SettingsValueParser.ParseIntBox(globalScaleBox, 100, 25, 300);
        settings.AlwaysOnTop = alwaysOnTopBox.Checked;
        settings.PracticeMode = practiceModeBox.Checked;
    }

    private void BuildSections(TableLayoutPanel parent)
    {
        ConfigureHotkeyBox(pauseKeyBox, Draft.PauseResumeKeys);
        ConfigureHotkeyBox(resetKeyBox, Draft.ResetKeys);
        ConfigureHotkeyBox(mouseClickThroughKeyBox, Draft.MouseClickThroughKeys);
        ConfigureHotkeyBox(createWorldKeyBox, Draft.CreateWorldKeys);
        ConfigureHotkeyBox(practiceWorldKeyBox, Draft.PracticeWorldKeys);

        ConfigureCheckBox(showMouseClickThroughIndicatorBox, Draft.ShowMouseClickThroughIndicator);
        ConfigureCheckBox(alwaysOnTopBox, Draft.AlwaysOnTop);
        ConfigureCheckBox(practiceModeBox, Draft.PracticeMode);

        UiTheme.StyleComboBox(languageBox);
        languageBox.Dock = DockStyle.Fill;
        languageBox.Items.Add(LanguageNames.English);
        languageBox.Items.Add(LanguageNames.Chinese);
        languageBox.SelectedItem = LanguageNames.Normalize(Draft.Language);

        UiTheme.StyleTextBox(globalScaleBox);
        globalScaleBox.Dock = DockStyle.Fill;
        globalScaleBox.Text = Math.Clamp(Draft.Columns.ScalePercent, 25, 300).ToString(System.Globalization.CultureInfo.InvariantCulture);

        TableLayoutPanel commonSection = Factory.CreateSection("Common");
        TableLayoutPanel commonGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(commonGrid, "Language", languageBox);
        Factory.AddSettingRow(commonGrid, "Global scale %", globalScaleBox);
        Factory.AddSettingRow(commonGrid, "Always on top", alwaysOnTopBox);
        SettingsUiFactory.AddSectionControl(commonSection, commonGrid);
        SettingsUiFactory.AddSection(parent, commonSection);

        TableLayoutPanel hotkeysSection = Factory.CreateSection("Hotkeys");
        TableLayoutPanel hotkeysGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(hotkeysGrid, "Pause / Resume", pauseKeyBox);
        Factory.AddSettingRow(hotkeysGrid, "Reset (Disabled in world)", resetKeyBox);
        Factory.AddSettingRow(hotkeysGrid, "Mouse passthrough", mouseClickThroughKeyBox);
        Factory.AddSettingRow(hotkeysGrid, "Create world (Disabled in world)", createWorldKeyBox);
        Factory.AddSettingRow(hotkeysGrid, "Quick enter world (Disabled in world)", practiceWorldKeyBox);
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

    private static void ConfigureCheckBox(CheckBox checkBox, bool selected)
    {
        checkBox.Checked = selected;
        checkBox.Dock = DockStyle.Fill;
        UiTheme.StyleCheckBox(checkBox);
    }
}
