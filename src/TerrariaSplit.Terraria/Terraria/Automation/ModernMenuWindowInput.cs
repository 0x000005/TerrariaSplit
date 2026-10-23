using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class ModernMenuWindowInput(TerrariaWindowController window) : IModernMenuInput
{
    public bool IsForeground => window.IsInputForeground;
    public bool Position(TerrariaMenuGeometry geometry, Func<TerrariaMenuGeometry, Point> target, out Point point, out string detail) =>
        window.TryPositionClient(size => target(TerrariaMenuGeometry.From(size, geometry.Profile)), out point, out _, out detail);
    public bool Mouse(bool down) => window.SetLeftMouse(down);
    public void Key(Keys key, bool down) => window.SetKey(key, down);
    public void UnicodeText(string text)
    {
        if (!window.TrySendUnicodeText(text, out string detail)) throw new InvalidOperationException(detail);
    }
}
