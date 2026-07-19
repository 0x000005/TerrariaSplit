using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal delegate DialogResult SettingsMessageBoxPresenter(
    IWin32Window owner,
    string text,
    string caption,
    MessageBoxButtons buttons,
    MessageBoxIcon icon);

internal sealed class SettingsDialogService
{
    private readonly IWin32Window owner;
    private readonly Func<string, string> localize;
    private readonly SettingsMessageBoxPresenter messageBoxPresenter;

    public SettingsDialogService(
        IWin32Window owner,
        Func<string, string> localize,
        SettingsMessageBoxPresenter? messageBoxPresenter = null)
    {
        this.owner = owner;
        this.localize = localize;
        this.messageBoxPresenter = messageBoxPresenter ?? ShowThemedMessage;
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
            Title = localize("Choose icon")
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

    public void ShowWarning(string message, string title)
    {
        messageBoxPresenter(
            owner,
            message,
            title,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    public bool Confirm(string message, string title)
    {
        return messageBoxPresenter(
            owner,
            message,
            title,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question) == DialogResult.Yes;
    }

    private DialogResult ShowThemedMessage(
        IWin32Window dialogOwner,
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon)
    {
        return SettingsMessageDialog.ShowThemed(
            dialogOwner,
            caption,
            text,
            buttons,
            icon,
            localize);
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
            StaticAppLogger.Instance.Error(ex, "Failed to open TerrariaSplit deleted backup folder.");
            ShowWarning(
                localize("Could not open backup folder."),
                localizeTitle("Create World"));
        }
    }

    public void OpenTerrariaSaveFolder(Func<string, string> localizeTitle)
    {
        try
        {
            string saveRoot = TerrariaSavePaths.SaveRoot();
            Directory.CreateDirectory(saveRoot);
            Process.Start(new ProcessStartInfo
            {
                FileName = saveRoot,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StaticAppLogger.Instance.Error(ex, "Failed to open Terraria save folder.");
            ShowWarning(
                localize("Could not open save folder."),
                localizeTitle("Create World"));
        }
    }
}
