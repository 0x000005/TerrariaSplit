using System.Runtime.InteropServices;

namespace TerrariaSplit;

internal static class Keyboard
{
    private const int RKey = 0x52;
    private const int TKey = 0x54;
    private static bool rWasDown;
    private static bool tWasDown;

    public static bool PollRPressed()
    {
        return PollPressed(RKey, ref rWasDown);
    }

    public static bool PollTPressed()
    {
        return PollPressed(TKey, ref tWasDown);
    }

    private static bool PollPressed(int virtualKey, ref bool wasDown)
    {
        bool isDown = (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        bool pressed = isDown && !wasDown;
        wasDown = isDown;
        return pressed;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
