using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class MainFormContextMenuBuilder
{
    public void Rebuild(
        ContextMenuStrip menu,
        AppSettings settings,
        Action openStatistics,
        Action openSettings,
        Action<string> switchSettingsFile,
        Action exit)
    {
        menu.Items.Clear();
        menu.Items.Add(Localizer.Get("Statistics...", settings), null, (_, _) => openStatistics());
        menu.Items.Add(Localizer.Get("Settings...", settings), null, (_, _) => openSettings());
        menu.Items.Add(CreateSettingsFileMenu(settings, switchSettingsFile));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Localizer.Get("Exit", settings), null, (_, _) => exit());
    }

    private static ToolStripMenuItem CreateSettingsFileMenu(
        AppSettings settings,
        Action<string> switchSettingsFile)
    {
        var menu = new ToolStripMenuItem(Localizer.Get("Switch config", settings));
        menu.DropDownOpening += (_, _) => PopulateSettingsFileMenu(menu, settings, switchSettingsFile);
        return menu;
    }

    private static void PopulateSettingsFileMenu(
        ToolStripMenuItem menu,
        AppSettings settings,
        Action<string> switchSettingsFile)
    {
        menu.DropDownItems.Clear();
        IReadOnlyList<string> files = AppSettingsStore.GetSettingsFiles();
        if (files.Count == 0)
        {
            ToolStripMenuItem empty = new(Localizer.Get("No config files", settings))
            {
                Enabled = false
            };
            menu.DropDownItems.Add(empty);
            return;
        }

        string activePath = Path.GetFullPath(AppSettingsStore.SettingsPath);
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
