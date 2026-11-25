using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal static class DiagnosticExportDialog
{
    public static async Task ExportAsync(IWin32Window owner, AppFault? fault = null)
    {
        using var picker = new SaveFileDialog
        {
            Title = "Export diagnostics", Filter = "Diagnostic archive|*.zip",
            FileName = "TerrariaSplit-diagnostics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip",
            DefaultExt = "zip", AddExtension = true, OverwritePrompt = true
        };
        if (picker.ShowDialog(owner) != DialogResult.OK) return;
        try
        {
            await AppLog.Provider.ExportAsync(picker.FileName, fault);
        }
        catch (Exception exception)
        {
            AppFault failure = AppLog.Failure("Diagnostics", "diagnostics.export.failed",
                "Could not export diagnostics.", "Destination: " + picker.FileName, exception);
            SettingsMessageDialog.ShowThemed(owner, "Export diagnostics", failure.ToReport(),
                MessageBoxButtons.OK, MessageBoxIcon.Error, text => text, selectableMessage: true);
        }
    }
}
