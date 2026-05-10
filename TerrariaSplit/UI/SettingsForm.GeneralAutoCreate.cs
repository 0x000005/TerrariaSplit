using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed partial class SettingsForm : Form
{

    internal void AddHotkeySection(TableLayoutPanel parent)
    {
        ConfigureKeyBox(pauseKeyBox, settings.PauseResumeKeys);
        ConfigureKeyBox(resetKeyBox, settings.ResetKeys);
        ConfigureKeyBox(mouseClickThroughKeyBox, settings.MouseClickThroughKeys);
        ConfigureKeyBox(createWorldKeyBox, settings.CreateWorldKeys);
        ConfigureCheckBox(showMouseClickThroughIndicatorBox, settings.ShowMouseClickThroughIndicator);

        UiTheme.StyleComboBox(languageBox);
        languageBox.Dock = DockStyle.Fill;
        languageBox.Items.Add(LanguageNames.English);
        languageBox.Items.Add(LanguageNames.Chinese);
        languageBox.SelectedItem = LanguageNames.Normalize(settings.Language);

        ConfigureNumberBox(globalScaleBox, settings.Columns.ScalePercent, 25, 300);
        ConfigureCheckBox(alwaysOnTopBox, settings.AlwaysOnTop);
        ConfigureCheckBox(practiceModeBox, settings.PracticeMode);

        TableLayoutPanel commonSection = CreateSection("Common");
        TableLayoutPanel commonGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(commonGrid, "Language", languageBox);
        AddSettingRow(commonGrid, "Global scale %", globalScaleBox);
        AddSettingRow(commonGrid, "Always on top", alwaysOnTopBox);
        AddSectionControl(commonSection, commonGrid);
        AddSection(parent, commonSection);

        TableLayoutPanel hotkeysSection = CreateSection("Hotkeys");
        TableLayoutPanel hotkeysGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(hotkeysGrid, "Pause / Resume", pauseKeyBox);
        AddSettingRow(hotkeysGrid, "Reset (Disabled in world)", resetKeyBox);
        AddSettingRow(hotkeysGrid, "Mouse passthrough", mouseClickThroughKeyBox);
        AddSettingRow(hotkeysGrid, "Create world (Disabled in world)", createWorldKeyBox);
        AddSectionControl(hotkeysSection, hotkeysGrid);
        AddSection(parent, hotkeysSection);

        TableLayoutPanel specialSection = CreateSection("Special Options");
        TableLayoutPanel specialGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(specialGrid, "Allow manual time editing", practiceModeBox);
        AddSettingRow(specialGrid, "Mouse passthrough indicator", showMouseClickThroughIndicatorBox);
        AddSectionControl(specialSection, specialGrid);
        AddSection(parent, specialSection);
    }


    internal void AddAutoCreateSection(TableLayoutPanel parent)
    {
        UiTheme.StyleTextBox(autoCreatePlayerNameBox);
        autoCreatePlayerNameBox.Dock = DockStyle.Fill;
        autoCreatePlayerNameBox.Text = settings.AutoCreate.PlayerName;
        autoCreatePlayerNameBox.PlaceholderText = Localizer.Get("Empty = 1", settings);

        UiTheme.StyleTextBox(autoCreatePlayerTemplateCodeBox);
        autoCreatePlayerTemplateCodeBox.Dock = DockStyle.Fill;
        autoCreatePlayerTemplateCodeBox.Multiline = true;
        autoCreatePlayerTemplateCodeBox.AcceptsReturn = true;
        autoCreatePlayerTemplateCodeBox.ScrollBars = ScrollBars.Vertical;
        autoCreatePlayerTemplateCodeBox.Height = autoCreatePlayerTemplateCodeBox.Font.Height * 10 + 14;
        autoCreatePlayerTemplateCodeBox.Text = settings.AutoCreate.PlayerTemplateCode;
        autoCreatePlayerTemplateCodeBox.PlaceholderText = Localizer.Get("Empty = default character", settings);

        ConfigureOptionBox(autoCreatePlayerDifficultyBox, AutoCreatePlayerDifficulty.All, settings.AutoCreate.PlayerDifficulty);
        ConfigureOptionBox(autoCreateWorldSizeBox, AutoCreateWorldSize.All, settings.AutoCreate.WorldSize);
        ConfigureOptionBox(autoCreateWorldDifficultyBox, AutoCreateWorldDifficulty.All, settings.AutoCreate.WorldDifficulty);
        ConfigureOptionBox(autoCreateWorldEvilBox, AutoCreateWorldEvil.All, settings.AutoCreate.WorldEvil);
        ConfigureNumberBox(autoCreateShortActionDelayBox, settings.AutoCreate.ShortActionDelayMilliseconds, 0, 5000);
        ConfigureNumberBox(autoCreateMenuActionDelayBox, settings.AutoCreate.MenuActionDelayMilliseconds, 0, 5000);
        ConfigureNumberBox(autoCreateWindowActivationDelayBox, settings.AutoCreate.WindowActivationDelayMilliseconds, 0, 5000);
        ConfigureNumberBox(autoCreateClickFocusDelayBox, settings.AutoCreate.ClickFocusDelayMilliseconds, 0, 5000);
        ConfigureNumberBox(autoCreateInputPressDurationBox, settings.AutoCreate.InputPressDurationMilliseconds, 1, 5000);

        TableLayoutPanel characterSection = CreateSection("Character");
        TableLayoutPanel characterGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(360f));
        AddSettingRow(characterGrid, "Player name", autoCreatePlayerNameBox);
        AddSettingRow(characterGrid, "Player difficulty", autoCreatePlayerDifficultyBox);
        AddSectionControl(characterSection, characterGrid);
        AddSectionControl(characterSection, CreateFieldLabel("Player code"));
        AddSectionControl(characterSection, autoCreatePlayerTemplateCodeBox);
        AddSection(parent, characterSection);

        TableLayoutPanel worldSection = CreateSection("World");
        TableLayoutPanel worldGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(360f));
        AddSettingRow(worldGrid, "World size", autoCreateWorldSizeBox);
        AddSettingRow(worldGrid, "World difficulty", autoCreateWorldDifficultyBox);
        AddSettingRow(worldGrid, "World evil", autoCreateWorldEvilBox);
        AddSectionControl(worldSection, worldGrid);
        AddSection(parent, worldSection);

        TableLayoutPanel timingSection = CreateSection("Delay");
        TableLayoutPanel timingGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(180f));
        AddSettingRow(timingGrid, "Mouse / key press ms", autoCreateInputPressDurationBox);
        AddSettingRow(timingGrid, "Window activation wait ms", autoCreateWindowActivationDelayBox);
        AddSettingRow(timingGrid, "Click focus wait ms", autoCreateClickFocusDelayBox);
        AddSettingRow(timingGrid, "Short action delay ms", autoCreateShortActionDelayBox);
        AddSettingRow(timingGrid, "Menu action delay ms", autoCreateMenuActionDelayBox);
        AddSectionControl(timingSection, timingGrid);
        AddSection(parent, timingSection);
    }
}
