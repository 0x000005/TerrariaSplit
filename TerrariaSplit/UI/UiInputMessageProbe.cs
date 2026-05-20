using System.Runtime.InteropServices;

namespace TerrariaSplit;

internal static class UiInputMessageProbe
{
    private const uint QsKey = 0x0001;
    private const uint QsMouseMove = 0x0002;
    private const uint QsMouseButton = 0x0004;
    private const uint QsHotkey = 0x0080;
    private const uint QsRawInput = 0x0400;
    private const uint QsTouch = 0x0800;
    private const uint QsPointer = 0x1000;
    private const uint InputMask =
        QsKey |
        QsMouseMove |
        QsMouseButton |
        QsHotkey |
        QsRawInput |
        QsTouch |
        QsPointer;

    public static bool HasPendingInputMessage()
    {
        uint status = GetQueueStatus(InputMask);
        return ((status >> 16) & InputMask) != 0;
    }

    [DllImport("user32.dll")]
    private static extern uint GetQueueStatus(uint flags);
}
