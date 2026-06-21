using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

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
    private readonly Action<IntPtr>? modalHandleChanged;

    public SettingsDialogService(
        IWin32Window owner,
        Func<string, string> localize,
        SettingsMessageBoxPresenter? messageBoxPresenter = null,
        Action<IntPtr>? modalHandleChanged = null)
    {
        this.owner = owner;
        this.localize = localize;
        this.messageBoxPresenter = messageBoxPresenter ?? ShowThemedMessage;
        this.modalHandleChanged = modalHandleChanged;
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

    private DialogResult ShowThemedMessage(
        IWin32Window dialogOwner,
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon)
    {
        using var dialog = new SettingsMessageDialog(caption, text, buttons, icon, localize);
        dialog.HandleDestroyed += (_, _) => modalHandleChanged?.Invoke(IntPtr.Zero);
        dialog.Shown += (_, _) =>
        {
            dialog.BringToFront();
            dialog.Activate();
            NativeMethods.SetForegroundWindow(dialog.Handle);
        };

        if (modalHandleChanged is null)
        {
            return dialog.ShowDialog(dialogOwner);
        }

        CenterOverOwner(dialog, dialogOwner);
        try
        {
            _ = dialog.Handle;
            modalHandleChanged(dialog.Handle);
            return dialog.ShowDialog();
        }
        finally
        {
            modalHandleChanged(IntPtr.Zero);
        }
    }

    private static void CenterOverOwner(Form dialog, IWin32Window dialogOwner)
    {
        if (dialogOwner is not Control ownerControl || ownerControl.IsDisposed || !ownerControl.IsHandleCreated)
        {
            return;
        }

        Rectangle ownerBounds = ownerControl.RectangleToScreen(ownerControl.ClientRectangle);
        Rectangle workingArea = Screen.FromControl(ownerControl).WorkingArea;
        int x = ownerBounds.Left + Math.Max(0, (ownerBounds.Width - dialog.Width) / 2);
        int y = ownerBounds.Top + Math.Max(0, (ownerBounds.Height - dialog.Height) / 2);
        x = Math.Clamp(x, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - dialog.Width));
        y = Math.Clamp(y, workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - dialog.Height));
        dialog.StartPosition = FormStartPosition.Manual;
        dialog.Location = new Point(x, y);
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
            ShowWarning(
                localize("Could not open backup folder."),
                localizeTitle("Create World"));
        }
    }
}
