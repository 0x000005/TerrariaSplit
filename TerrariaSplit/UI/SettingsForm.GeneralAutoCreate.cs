using System.Drawing;
using System.Diagnostics;
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
        ConfigureKeyBox(practiceWorldKeyBox, settings.PracticeWorldKeys);
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
        AddSettingRow(hotkeysGrid, "Quick enter world (Disabled in world)", practiceWorldKeyBox);
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

        AddAutoCreateNoticeSection(parent);

        TableLayoutPanel createSection = CreateSection("Create World");
        TableLayoutPanel createGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(360f));
        AddSettingRow(createGrid, "Player name", autoCreatePlayerNameBox);
        AddSettingRow(createGrid, "Player difficulty", autoCreatePlayerDifficultyBox);
        AddSectionControl(createSection, createGrid);
        AddSectionControl(createSection, CreateFieldLabel("Player code"));
        AddSectionControl(createSection, autoCreatePlayerTemplateCodeBox);

        TableLayoutPanel worldGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(360f));
        AddSettingRow(worldGrid, "World size", autoCreateWorldSizeBox);
        AddSettingRow(worldGrid, "World difficulty", autoCreateWorldDifficultyBox);
        AddSettingRow(worldGrid, "World evil", autoCreateWorldEvilBox);
        AddSectionControl(createSection, worldGrid);
        AddSection(parent, createSection);

        AddPracticeWorldSection(parent);

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

    private void AddAutoCreateNoticeSection(TableLayoutPanel parent)
    {
        var noticeSection = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SectionColor,
            ColumnCount = 1,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 18),
            Padding = new Padding(18, 14, 18, 14)
        };
        UiTheme.EnableDoubleBuffering(noticeSection);
        noticeSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        Color warningColor = Color.FromArgb(255, 210, 120);
        AddSectionControl(
            noticeSection,
            CreateWrappedFieldLabel(
                "Automatically creates or enters a world by simulating mouse and keyboard input.",
                TextColor));
        AddSectionControl(
            noticeSection,
            CreateWrappedFieldLabel(
                "Deletes all non-favorite players and worlds.",
                warningColor));
        AddSectionControl(noticeSection, CreateAutoCreateBackupNoticeRow(warningColor));
        AddSection(parent, noticeSection);
    }

    private Control CreateAutoCreateBackupNoticeRow(Color warningColor)
    {
        var row = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SectionColor,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 8, 0, 8),
            Padding = Padding.Empty
        };
        UiTheme.EnableDoubleBuffering(row);
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        Label label = CreateWrappedFieldLabel(
            "The most recent 50 deletions are kept in the backup folder.",
            warningColor);
        label.Dock = DockStyle.Fill;
        label.Margin = new Padding(0, 0, 12, 0);
        label.TextAlign = ContentAlignment.MiddleLeft;

        Button openButton = CreateSmallButton("Open folder");
        openButton.Width = 252;
        openButton.MinimumSize = new Size(252, 36);
        openButton.Margin = Padding.Empty;
        openButton.Click += (_, _) => OpenAutoCreateBackupFolder();

        row.Controls.Add(label, 0, 0);
        row.Controls.Add(openButton, 1, 0);
        return row;
    }

    private void OpenAutoCreateBackupFolder()
    {
        try
        {
            string backupRoot = TerrariaSavePaths.DeletedSavesRoot();
            Directory.CreateDirectory(backupRoot);
            Process.Start(new ProcessStartInfo
            {
                FileName = backupRoot,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Failed to open TerrariaSplit deleted backup folder.");
            MessageBox.Show(
                this,
                Localizer.Get("Could not open backup folder.", settings),
                Localizer.Get("Create World", settings),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
