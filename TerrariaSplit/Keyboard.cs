using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TerrariaSplit;

internal static class Keyboard
{
    private static readonly Dictionary<Keys, bool> KeyStates = new();

    public static bool PollPressed(Keys key)
    {
        bool isDown = (GetAsyncKeyState((int)key) & 0x8000) != 0;
        KeyStates.TryGetValue(key, out bool wasDown);
        bool pressed = isDown && !wasDown;
        KeyStates[key] = isDown;
        return pressed;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
