using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class TerrariaWindowController
{
    private const int SwRestore = 9;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint KeyEventKeyUp = 0x0002;

    public int WindowActivationDelayMilliseconds { get; set; } = AutoCreateWorldSettings.DefaultWindowActivationDelayMilliseconds;
    public int ClickFocusDelayMilliseconds { get; set; } = AutoCreateWorldSettings.DefaultClickFocusDelayMilliseconds;
    public int InputPressDurationMilliseconds { get; set; } = AutoCreateWorldSettings.DefaultInputPressDurationMilliseconds;

    public bool TryActivate(out Size clientSize)
    {
        clientSize = Size.Empty;
        Process? process = FindTerrariaProcess();
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
        Sleep(WindowActivationDelayMilliseconds);
        if (!GetClientRect(handle, out Rect rect))
        {
            return false;
        }

        clientSize = new Size(rect.Right - rect.Left, rect.Bottom - rect.Top);
        return clientSize.Width > 0 && clientSize.Height > 0;
    }

    public bool TryClickClient(int x, int y)
    {
        Process? process = FindTerrariaProcess();
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
        Sleep(ClickFocusDelayMilliseconds);
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

    private static Process? FindTerrariaProcess()
    {
        Process[] processes = Process.GetProcessesByName(Terraria1456Memory.ProcessName);
        if (processes.Length == 0)
        {
            return null;
        }

        Process selected = processes
            .OrderByDescending(ProcessStartTimeOrMinValue)
            .First();

        foreach (Process process in processes)
        {
            if (!ReferenceEquals(process, selected))
            {
                process.Dispose();
            }
        }

        return selected;
    }

    private static DateTime ProcessStartTimeOrMinValue(Process process)
    {
        try
        {
            return process.StartTime;
        }
        catch
        {
            return DateTime.MinValue;
        }
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
