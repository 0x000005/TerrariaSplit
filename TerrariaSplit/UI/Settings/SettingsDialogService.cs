using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class SettingsDialogService
{
    private readonly IWin32Window owner;
    private readonly Func<string, string> localize;

    public SettingsDialogService(IWin32Window owner, Func<string, string> localize)
    {
        this.owner = owner;
        this.localize = localize;
    }

    public bool PickColor(TextBox target)
    {
        Color currentColor = ColorText.Parse(target.Text, Color.White);
        if (currentColor.A == 0)
        {
            currentColor = Color.White;
        }

        using var dialog = new ColorDialog
        {
            Color = currentColor,
            FullOpen = true
        };

        if (dialog.ShowDialog(owner) != DialogResult.OK)
        {
            return false;
        }

        target.Text = ColorText.Format(dialog.Color);
        return true;
    }

    public bool PickBossIcon(TextBox target)
    {
        using var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*",
            Title = localize("Choose BOSS Icon")
        };

        if (dialog.ShowDialog(owner) != DialogResult.OK)
        {
            return false;
        }

        target.Text = dialog.FileName;
        return true;
    }

    public bool PickSound(TextBox target)
    {
        using var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = "Wave audio|*.wav|All files|*.*",
            Title = localize("Choose sound")
        };

        if (dialog.ShowDialog(owner) != DialogResult.OK)
        {
            return false;
        }

        target.Text = dialog.FileName;
        return true;
    }

    public bool PickFile(TextBox target, string title, string filter)
    {
        using var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = filter,
            Title = localize(title)
        };

        if (dialog.ShowDialog(owner) != DialogResult.OK)
        {
            return false;
        }

        target.Text = dialog.FileName;
        return true;
    }

    public void OpenAutoCreateBackupFolder(Func<string, string> localizeTitle)
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
                owner,
                localize("Could not open backup folder."),
                localizeTitle("Create World"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
