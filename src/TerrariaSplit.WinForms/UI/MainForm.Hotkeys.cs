using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed partial class MainForm : Form
{
    private void ShowHotkeyWarning(string message)
    {
        using var dialog = new HotkeyWarningDialog(
            Localizer.Get("Hotkey warning", settings),
            message);
        modalWindows.ShowDialog(dialog);
    }

    private void ShowSettingsSaveFailure(OperationResult result)
    {
        string message = string.IsNullOrWhiteSpace(result.Message)
            ? Localizer.Get("Failed to save settings.", settings)
            : result.Message;
        using var dialog = new HotkeyWarningDialog(SegmentTimerWindowTitle, message);
        modalWindows.ShowDialog(dialog);
    }

}
