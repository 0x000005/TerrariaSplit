using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TerrariaSplit.Terraria;

public sealed class TerrariaWindowController
{
    private const int SwRestore = 9;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint KeyEventKeyUp = 0x0002;

    public int WindowActivationDelayMilliseconds { get; set; } = AppSettingsDefaults.Automation.AutoCreate.WindowActivationDelayMilliseconds;
    public int ClickFocusDelayMilliseconds { get; set; } = AppSettingsDefaults.Automation.AutoCreate.ClickFocusDelayMilliseconds;
    public int InputPressDurationMilliseconds { get; set; } = AppSettingsDefaults.Automation.AutoCreate.InputPressDurationMilliseconds;

    public bool TryActivate(out Size clientSize)
    {
        return TryActivate(out clientSize, WindowActivationDelayMilliseconds);
    }

    public bool TryActivate(out Size clientSize, int activationDelayMilliseconds)
    {
        clientSize = Size.Empty;
        Process? process = TerrariaProcessFinder.FindNewest();
        if (process is null || process.MainWindowHandle == IntPtr.Zero)
        {
            return false;
        }

        IntPtr handle = process.MainWindowHandle;
        if (IsIconic(handle))
        {
            ShowWindow(handle, SwRestore);
        }

        SetForegroundWindow(handle);
        Sleep(activationDelayMilliseconds);
        if (!GetClientRect(handle, out Rect rect))
        {
            return false;
        }

        clientSize = new Size(rect.Right - rect.Left, rect.Bottom - rect.Top);
        return clientSize.Width > 0 && clientSize.Height > 0;
    }

    public bool TryGetClientScreenBounds(out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        Process? process = TerrariaProcessFinder.FindNewest();
        if (process is null || process.MainWindowHandle == IntPtr.Zero)
        {
            return false;
        }

        IntPtr handle = process.MainWindowHandle;
        if (!GetClientRect(handle, out Rect rect))
        {
            return false;
        }

        var origin = new PointStruct { X = 0, Y = 0 };
        if (!ClientToScreen(handle, ref origin))
        {
            return false;
        }

        bounds = new Rectangle(
            origin.X,
            origin.Y,
            Math.Max(0, rect.Right - rect.Left),
            Math.Max(0, rect.Bottom - rect.Top));
        return bounds.Width > 0 && bounds.Height > 0;
    }

    public bool TryClickClient(int x, int y)
    {
        return TryClickClient(x, y, out _);
    }

    public bool TryClickClient(int x, int y, out Size clientSize)
    {
        Process? process = TerrariaProcessFinder.FindNewest();
        if (process is null || process.MainWindowHandle == IntPtr.Zero)
        {
            clientSize = Size.Empty;
            return false;
        }

        IntPtr handle = process.MainWindowHandle;
        if (IsIconic(handle))
        {
            ShowWindow(handle, SwRestore);
        }

        SetForegroundWindow(handle);
        Sleep(ClickFocusDelayMilliseconds);
        clientSize = GetClientSize(handle);
        var point = new PointStruct { X = x, Y = y };
        if (!ClientToScreen(handle, ref point))
        {
            return false;
        }

        SetCursorPos(point.X, point.Y);
        mouse_event(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(InputPressDurationMilliseconds);
        mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
        return true;
    }

    private static Size GetClientSize(IntPtr handle)
    {
        if (!GetClientRect(handle, out Rect rect))
        {
            return Size.Empty;
        }

        return new Size(rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    public bool TryClickClientRatio(float x, float y)
    {
        if (!TryActivate(out Size clientSize))
        {
            return false;
        }

        return TryClickClient(
            (int)Math.Round(clientSize.Width * Math.Clamp(x, 0f, 1f)),
            (int)Math.Round(clientSize.Height * Math.Clamp(y, 0f, 1f)));
    }

    public bool TryMoveScreenCursor(int x, int y)
    {
        return SetCursorPos(x, y);
    }

    public void PressKey(Keys key)
    {
        byte virtualKey = (byte)key;
        keybd_event(virtualKey, 0, 0, UIntPtr.Zero);
        Thread.Sleep(InputPressDurationMilliseconds);
        keybd_event(virtualKey, 0, KeyEventKeyUp, UIntPtr.Zero);
    }

    public void PressModifiedKey(Keys modifier, Keys key)
    {
        byte modifierKey = (byte)modifier;
        byte virtualKey = (byte)key;
        keybd_event(modifierKey, 0, 0, UIntPtr.Zero);
        Thread.Sleep(20);
        keybd_event(virtualKey, 0, 0, UIntPtr.Zero);
        Thread.Sleep(InputPressDurationMilliseconds);
        keybd_event(virtualKey, 0, KeyEventKeyUp, UIntPtr.Zero);
        Thread.Sleep(20);
        keybd_event(modifierKey, 0, KeyEventKeyUp, UIntPtr.Zero);
    }

    private static void Sleep(int milliseconds)
    {
        if (milliseconds > 0)
        {
            Thread.Sleep(milliseconds);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointStruct
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref PointStruct lpPoint);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
}
