using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class ModernMenuWindowInput(TerrariaWindowController window) : IModernMenuInput
{
    public bool IsForeground => window.IsInputForeground;
    public bool Position(Point point, out string detail)
    {
        // Game button coordinates are already checked against the game screen, not the Windows DPI-logical client.
        bool success = window.TryPositionClient(_ => point, out Point resolved, out Size clientSize, out detail,
            validateLogicalClientBounds: false);
        AppLog.Debug("automation.position", new { point, resolved, clientSize, success,
            detail = success ? window.LastCoordinateDiagnostic : detail });
        return success;
    }
    public bool Mouse(bool down) => window.SetLeftMouse(down);
    public void Key(Keys key, bool down) => window.SetKey(key, down);
    public void UnicodeText(string text)
    {
        if (!window.TrySendUnicodeText(text, out string detail)) throw new InvalidOperationException(detail);
    }
}
