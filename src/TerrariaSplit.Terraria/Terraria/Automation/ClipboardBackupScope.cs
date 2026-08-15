using System.Windows.Forms;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class ClipboardBackupScope : IDisposable
{
    private readonly string? previousText;
    private readonly bool hadPreviousText;
    private bool disposed;

    private ClipboardBackupScope(string? previousText, bool hadPreviousText)
    {
        this.previousText = previousText;
        this.hadPreviousText = hadPreviousText;
    }

    public static AutomationResult TrySetText(string text, out ClipboardBackupScope? scope)
    {
        scope = null;
        try
        {
            bool hadText = Clipboard.ContainsText();
            string? previous = hadText ? Clipboard.GetText() : null;
            Clipboard.SetText(text);
            scope = new ClipboardBackupScope(previous, hadText);
            return AutomationResult.Success("Clipboard text was set for automation.");
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, "Automation failed to set clipboard text.");
            return AutomationResult.Failure(
                "Could not write to the Windows clipboard.",
                "Automation failed to set clipboard text.",
                ex);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        try
        {
            if (hadPreviousText && previousText is not null)
            {
                Clipboard.SetText(previousText);
            }
            else
            {
                Clipboard.Clear();
            }
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, "Automation failed to restore clipboard text.");
        }
    }
}
