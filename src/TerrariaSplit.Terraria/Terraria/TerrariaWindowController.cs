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
    private static readonly IntPtr DpiAwarenessContextPerMonitorAwareV2 = new(-4);
    private static readonly IntPtr DpiAwarenessContextUnawareGdiScaled = new(-5);

    private IntPtr activatedWindowHandle;

    public int WindowActivationDelayMilliseconds { get; set; } = AppSettingsDefaults.Automation.AutoCreate.WindowActivationDelayMilliseconds;
    public int ClickFocusDelayMilliseconds { get; set; } = AppSettingsDefaults.Automation.AutoCreate.ClickFocusDelayMilliseconds;
    public int InputPressDurationMilliseconds { get; set; } = AppSettingsDefaults.Automation.AutoCreate.InputPressDurationMilliseconds;

    public string LastCoordinateDiagnostic { get; private set; } = string.Empty;

    public bool TryActivate(out Size clientSize)
    {
        return TryActivate(out clientSize, WindowActivationDelayMilliseconds);
    }

    public bool TryActivate(out Size clientSize, int activationDelayMilliseconds)
    {
        clientSize = Size.Empty;
        if (!TryResolveWindowHandle(out IntPtr handle, preferActivatedWindow: false))
        {
            return false;
        }

        if (IsIconic(handle))
        {
            ShowWindow(handle, SwRestore);
        }

        SetForegroundWindow(handle);
        Sleep(activationDelayMilliseconds);
        if (!TryGetClientCoordinateSpace(handle, out ClientCoordinateSpace coordinateSpace, out _))
        {
            return false;
        }

        activatedWindowHandle = handle;
        clientSize = coordinateSpace.LogicalClientSize;
        LastCoordinateDiagnostic = coordinateSpace.Diagnostic;
        return clientSize.Width > 0 && clientSize.Height > 0;
    }

    public bool TryGetClientScreenBounds(out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (!TryResolveWindowHandle(out IntPtr handle, preferActivatedWindow: true))
        {
            return false;
        }

        if (!TryGetClientCoordinateSpace(handle, out ClientCoordinateSpace coordinateSpace, out _))
        {
            return false;
        }

        bounds = coordinateSpace.PhysicalClientBounds;
        LastCoordinateDiagnostic = coordinateSpace.Diagnostic;
        return bounds.Width > 0 && bounds.Height > 0;
    }

    public bool TryClickClient(int x, int y)
    {
        return TryClickClient(x, y, out _);
    }

    public bool TryClickClient(int x, int y, out Size clientSize)
    {
        return TryClickClient(x, y, out clientSize, out _);
    }

    public bool TryClickClient(
        int x,
        int y,
        out Size clientSize,
        out string failureDetail)
    {
        return TryClickClient(
            _ => new Point(x, y),
            out _,
            out clientSize,
            out failureDetail);
    }

    internal bool TryClickClient(
        Func<Size, Point> resolvePoint,
        out Point resolvedPoint,
        out Size clientSize,
        out string failureDetail)
    {
        ArgumentNullException.ThrowIfNull(resolvePoint);
        resolvedPoint = Point.Empty;
        if (!TryResolveWindowHandle(out IntPtr handle, preferActivatedWindow: true))
        {
            clientSize = Size.Empty;
            failureDetail = "Terraria main window was not found.";
            return false;
        }

        if (IsIconic(handle))
        {
            ShowWindow(handle, SwRestore);
        }

        SetForegroundWindow(handle);
        Sleep(ClickFocusDelayMilliseconds);
        if (!TryGetClientCoordinateSpace(handle, out ClientCoordinateSpace coordinateSpace, out failureDetail))
        {
            clientSize = Size.Empty;
            return false;
        }

        activatedWindowHandle = handle;
        clientSize = coordinateSpace.LogicalClientSize;
        resolvedPoint = resolvePoint(clientSize);
        if (resolvedPoint.X < 0 || resolvedPoint.Y < 0 ||
            resolvedPoint.X >= clientSize.Width || resolvedPoint.Y >= clientSize.Height)
        {
            failureDetail =
                $"Terraria UI point ({resolvedPoint.X},{resolvedPoint.Y}) is outside logical client " +
                $"{clientSize.Width}x{clientSize.Height}. {coordinateSpace.Diagnostic}";
            return false;
        }

        Point physicalPoint = MapLogicalClientPoint(coordinateSpace, resolvedPoint);

        if (!SetCursorPos(physicalPoint.X, physicalPoint.Y))
        {
            failureDetail =
                $"SetCursorPos failed with Win32 error {Marshal.GetLastWin32Error()}. " +
                coordinateSpace.Diagnostic;
            return false;
        }

        mouse_event(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(InputPressDurationMilliseconds);
        mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
        LastCoordinateDiagnostic =
            $"{coordinateSpace.Diagnostic}; logicalPoint={resolvedPoint.X},{resolvedPoint.Y}; " +
            $"physicalPoint={physicalPoint.X},{physicalPoint.Y}";
        failureDetail = string.Empty;
        return true;
    }

    public bool TryClickClientRatio(float x, float y)
    {
        if (!TryActivate(out Size clientSize))
        {
            return false;
        }

        return TryClickClient(
            (int)Math.Round((clientSize.Width - 1) * Math.Clamp(x, 0f, 1f)),
            (int)Math.Round((clientSize.Height - 1) * Math.Clamp(y, 0f, 1f)));
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

    private bool TryResolveWindowHandle(out IntPtr handle, bool preferActivatedWindow)
    {
        if (preferActivatedWindow && activatedWindowHandle != IntPtr.Zero && IsWindow(activatedWindowHandle))
        {
            handle = activatedWindowHandle;
            return true;
        }

        using Process? process = TerrariaProcessFinder.FindNewest();
        handle = process?.MainWindowHandle ?? IntPtr.Zero;
        return handle != IntPtr.Zero && IsWindow(handle);
    }

    private static bool TryGetClientCoordinateSpace(
        IntPtr handle,
        out ClientCoordinateSpace coordinateSpace,
        out string failureDetail)
    {
        coordinateSpace = default;
        failureDetail = string.Empty;
        IntPtr previousDpiContext = SetThreadDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2);
        if (previousDpiContext == IntPtr.Zero)
        {
            failureDetail =
                $"SetThreadDpiAwarenessContext(PER_MONITOR_AWARE_V2) failed with Win32 error " +
                $"{Marshal.GetLastWin32Error()}.";
            return false;
        }

        try
        {
            if (!GetClientRect(handle, out Rect clientRect))
            {
                failureDetail = $"GetClientRect failed with Win32 error {Marshal.GetLastWin32Error()}.";
                return false;
            }

            var physicalOrigin = new PointStruct { X = clientRect.Left, Y = clientRect.Top };
            var physicalEnd = new PointStruct { X = clientRect.Right, Y = clientRect.Bottom };
            if (!ClientToScreen(handle, ref physicalOrigin) || !ClientToScreen(handle, ref physicalEnd))
            {
                failureDetail = $"ClientToScreen failed with Win32 error {Marshal.GetLastWin32Error()}.";
                return false;
            }

            int physicalWidth = physicalEnd.X - physicalOrigin.X;
            int physicalHeight = physicalEnd.Y - physicalOrigin.Y;
            if (physicalWidth <= 0 || physicalHeight <= 0)
            {
                failureDetail = $"Terraria physical client size was {physicalWidth}x{physicalHeight}.";
                return false;
            }

            var logicalOrigin = physicalOrigin;
            var logicalEnd = physicalEnd;
            if (!PhysicalToLogicalPointForPerMonitorDPI(handle, ref logicalOrigin) ||
                !PhysicalToLogicalPointForPerMonitorDPI(handle, ref logicalEnd))
            {
                failureDetail =
                    $"PhysicalToLogicalPointForPerMonitorDPI failed with Win32 error " +
                    $"{Marshal.GetLastWin32Error()}.";
                return false;
            }

            int logicalWidth = logicalEnd.X - logicalOrigin.X;
            int logicalHeight = logicalEnd.Y - logicalOrigin.Y;
            if (logicalWidth <= 0 || logicalHeight <= 0)
            {
                failureDetail = $"Terraria logical client size was {logicalWidth}x{logicalHeight}.";
                return false;
            }

            string awareness = DescribeWindowDpiAwareness(handle);
            uint windowDpi = GetDpiForWindow(handle);
            var physicalBounds = new Rectangle(
                physicalOrigin.X,
                physicalOrigin.Y,
                physicalWidth,
                physicalHeight);
            var logicalSize = new Size(logicalWidth, logicalHeight);
            string diagnostic =
                $"hwnd=0x{handle.ToInt64():X}; dpiAwareness={awareness}; windowDpi={windowDpi}; " +
                $"logicalClient={logicalWidth}x{logicalHeight}@{logicalOrigin.X},{logicalOrigin.Y}; " +
                $"physicalClient={physicalWidth}x{physicalHeight}@{physicalOrigin.X},{physicalOrigin.Y}";
            coordinateSpace = new ClientCoordinateSpace(
                physicalBounds,
                logicalSize,
                diagnostic);
            return true;
        }
        finally
        {
            _ = SetThreadDpiAwarenessContext(previousDpiContext);
        }
    }

    private static string DescribeWindowDpiAwareness(IntPtr handle)
    {
        IntPtr context = GetWindowDpiAwarenessContext(handle);
        if (context == IntPtr.Zero)
        {
            return "unknown";
        }

        if (AreDpiAwarenessContextsEqual(context, DpiAwarenessContextUnawareGdiScaled))
        {
            return "unaware-gdi-scaled";
        }

        return GetAwarenessFromDpiAwarenessContext(context) switch
        {
            0 => "unaware",
            1 => "system-aware",
            2 => "per-monitor-aware",
            _ => "unknown"
        };
    }

    private static Point MapLogicalClientPoint(
        ClientCoordinateSpace coordinateSpace,
        Point logicalClientPoint)
    {
        Rectangle physicalBounds = coordinateSpace.PhysicalClientBounds;
        Size logicalSize = coordinateSpace.LogicalClientSize;
        int physicalOffsetX = (int)Math.Round(
            logicalClientPoint.X * (double)physicalBounds.Width / logicalSize.Width,
            MidpointRounding.AwayFromZero);
        int physicalOffsetY = (int)Math.Round(
            logicalClientPoint.Y * (double)physicalBounds.Height / logicalSize.Height,
            MidpointRounding.AwayFromZero);
        return new Point(
            physicalBounds.Left + Math.Clamp(physicalOffsetX, 0, physicalBounds.Width - 1),
            physicalBounds.Top + Math.Clamp(physicalOffsetY, 0, physicalBounds.Height - 1));
    }

    internal static bool TryInspectCoordinateTransform(
        IntPtr handle,
        out Size logicalClientSize,
        out Rectangle physicalClientBounds,
        out Point logicalCenter,
        out Point physicalCenter,
        out string detail)
    {
        logicalClientSize = Size.Empty;
        physicalClientBounds = Rectangle.Empty;
        logicalCenter = Point.Empty;
        physicalCenter = Point.Empty;
        if (!TryGetClientCoordinateSpace(handle, out ClientCoordinateSpace coordinateSpace, out detail))
        {
            return false;
        }

        logicalClientSize = coordinateSpace.LogicalClientSize;
        physicalClientBounds = coordinateSpace.PhysicalClientBounds;
        logicalCenter = new Point(logicalClientSize.Width / 2, logicalClientSize.Height / 2);
        physicalCenter = MapLogicalClientPoint(coordinateSpace, logicalCenter);
        detail = coordinateSpace.Diagnostic;
        return true;
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

    private readonly record struct ClientCoordinateSpace(
        Rectangle PhysicalClientBounds,
        Size LogicalClientSize,
        string Diagnostic);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ClientToScreen(IntPtr hWnd, ref PointStruct lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetClientRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr value);

    [DllImport("user32.dll")]
    private static extern bool AreDpiAwarenessContextsEqual(IntPtr dpiContextA, IntPtr dpiContextB);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PhysicalToLogicalPointForPerMonitorDPI(IntPtr hWnd, ref PointStruct lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
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
