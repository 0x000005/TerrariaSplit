using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed class MainFormContextMenuBuilder
{
    internal const string CheatsToggleItemName = "CheatsToggle";
    internal const string SettingsItemName = "Settings";
    internal const string SettingsFileMenuItemName = "SettingsFileMenu";
    private readonly ISettingsRepository settingsRepository;

    public MainFormContextMenuBuilder()
        : this(new AppSettingsRepository())
    {
    }

    public MainFormContextMenuBuilder(ISettingsRepository settingsRepository)
    {
        this.settingsRepository = settingsRepository;
    }

    public void Rebuild(
        ContextMenuStrip menu,
        AppSettings settings,
        bool canSwitchSettingsFile,
        Action openStatistics,
        Action openRacePanel,
        Action openSettings,
        Action toggleCheats,
        Action<string> switchSettingsFile,
        Action exit)
    {
        menu.Items.Clear();
        menu.Items.Add(Localizer.Get("Statistics...", settings), null, (_, _) => openStatistics());
        menu.Items.Add(Localizer.Get("Race...", settings), null, (_, _) => openRacePanel());
        var settingsItem = new ToolStripMenuItem(Localizer.Get("Settings...", settings))
        {
            Name = SettingsItemName,
            Enabled = canSwitchSettingsFile
        };
        settingsItem.Click += (_, _) => openSettings();
        menu.Items.Add(settingsItem);
        menu.Items.Add(CreateCheatsToggle(settings, toggleCheats));
        menu.Items.Add(CreateSettingsFileMenu(settings, canSwitchSettingsFile, switchSettingsFile));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Localizer.Get("Exit", settings), null, (_, _) => exit());
    }

    private static ToolStripMenuItem CreateCheatsToggle(
        AppSettings settings,
        Action toggleCheats)
    {
        var item = new ToolStripMenuItem(Localizer.Get("Cheats", settings))
        {
            Name = CheatsToggleItemName,
            Checked = settings.Automation.AutoCreate.EnableCheats
        };
        item.Click += (_, _) => toggleCheats();
        return item;
    }

    private ToolStripMenuItem CreateSettingsFileMenu(
        AppSettings settings,
        bool canSwitchSettingsFile,
        Action<string> switchSettingsFile)
    {
        var menu = new ToolStripMenuItem(Localizer.Get("Switch config", settings))
        {
            Name = SettingsFileMenuItemName,
            Enabled = canSwitchSettingsFile
        };
        menu.DropDownOpening += (_, _) => PopulateSettingsFileMenu(menu, settings, switchSettingsFile);
        return menu;
    }

    private void PopulateSettingsFileMenu(
        ToolStripMenuItem menu,
        AppSettings settings,
        Action<string> switchSettingsFile)
    {
        menu.DropDownItems.Clear();
        IReadOnlyList<string> files = settingsRepository.GetSettingsFiles();
        if (files.Count == 0)
        {
            ToolStripMenuItem empty = new(Localizer.Get("No config files", settings))
            {
                Enabled = false
            };
            menu.DropDownItems.Add(empty);
            return;
        }

        string activePath = Path.GetFullPath(settingsRepository.SettingsPath);
        foreach (string file in files)
        {
            string filePath = Path.GetFullPath(file);
            string fileName = Path.GetFileName(file);
            var item = new ToolStripMenuItem(fileName)
            {
                Checked = string.Equals(filePath, activePath, StringComparison.OrdinalIgnoreCase)
            };
            item.Click += (_, _) => switchSettingsFile(filePath);
            menu.DropDownItems.Add(item);
        }
    }
}
