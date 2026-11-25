using TerrariaSplit.UI.Wpf.Dialogs;
using TerrariaSplit.UI.Wpf.Interop;

namespace TerrariaSplit.UI;

internal static class TimeEditDialog
{
    public static bool TryShow(
        IWin32Window owner,
        AppSettings settings,
        string title,
        string value,
        bool allowEmpty,
        Func<System.Windows.Window, IDisposable> registerModalWindow,
        out string editedText)
    {
        editedText = value;
        var dialog = new ModernTimeEditDialog(settings, title, value, allowEmpty);
        WpfWindowInterop.AttachOwner(dialog, owner);
        using IDisposable modalWindow = registerModalWindow(dialog);
        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        editedText = dialog.EditedText;
        return allowEmpty || editedText.Length > 0;
    }
}
