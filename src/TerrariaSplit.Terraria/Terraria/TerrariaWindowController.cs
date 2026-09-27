using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TerrariaSplit.Terraria.Automation;

namespace TerrariaSplit.Terraria;

public sealed class TerrariaWindowController
{
    private const int SwRestore = 9;
    private const uint InputMouse = 0;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint MonitorDefaultToNearest = 0x00000002;
    private const int EnumCurrentSettings = -1;
    private const int EnumRegistrySettings = -2;
    private const int GwlStyle = -16;
    private const long WsCaption = 0x00C00000L;
    private const long WsThickFrame = 0x00040000L;
    private const int RestoredWindowPollMilliseconds = 25;
    private const int RestoredWindowStableSampleCount = 3;
    private const int RestoredWindowTimeoutMilliseconds = 5000;
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

        bool restoredFromMinimized = IsIconic(handle);
        if (restoredFromMinimized)
        {
            ShowWindow(handle, SwRestore);
        }

        if (!TryRequestForegroundAndWait(
                handle,
                activationDelayMilliseconds,
                restoredFromMinimized,
                out string foregroundDiagnostic))
        {
            LastCoordinateDiagnostic = foregroundDiagnostic;
            return false;
        }

        if (!TryGetClientCoordinateSpace(handle, out ClientCoordinateSpace coordinateSpace, out _))
        {
            return false;
        }

        activatedWindowHandle = handle;
        clientSize = coordinateSpace.LogicalClientSize;
        LastCoordinateDiagnostic = $"{foregroundDiagnostic}; {coordinateSpace.Diagnostic}";
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
        out string failureDetail,
        bool skipPreClickDelay = false)
    {
        ArgumentNullException.ThrowIfNull(resolvePoint);
        resolvedPoint = Point.Empty;
        if (!TryResolveWindowHandle(out IntPtr handle, preferActivatedWindow: true))
        {
            clientSize = Size.Empty;
            failureDetail = "Terraria main window was not found.";
            return false;
        }

        bool restoredFromMinimized = IsIconic(handle);
        if (restoredFromMinimized)
        {
            ShowWindow(handle, SwRestore);
        }

        if (!TryRequestForegroundAndWait(
                handle,
                0,
                restoredFromMinimized,
                out string foregroundDiagnostic))
        {
            clientSize = Size.Empty;
            failureDetail = foregroundDiagnostic;
            return false;
        }

        if (!TryGetClientCoordinateSpace(handle, out ClientCoordinateSpace coordinateSpace, out failureDetail))
        {
            clientSize = Size.Empty;
            failureDetail = $"{foregroundDiagnostic}; {failureDetail}";
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
                $"{clientSize.Width}x{clientSize.Height}. {foregroundDiagnostic}; {coordinateSpace.Diagnostic}";
            return false;
        }

        if (!TrySetXnaMousePosition(
                handle,
                resolvedPoint,
                out Point screenPoint,
                out Point actualClientPoint,
                out string mousePositionFailure))
        {
            failureDetail =
                $"{mousePositionFailure} " +
                $"{foregroundDiagnostic}; {coordinateSpace.Diagnostic}; " +
                $"requestedXnaClientPoint={resolvedPoint.X},{resolvedPoint.Y}";
            return false;
        }

        string actualCursorDiagnostic =
            $"requestedXnaScreenPoint={screenPoint.X},{screenPoint.Y}; " +
            $"actualXnaClientPoint={actualClientPoint.X},{actualClientPoint.Y}; " +
            $"xnaClientPointMatchedRequest={actualClientPoint == resolvedPoint}";

        // One configurable pre-click wait, after activation and cursor positioning.
        if (!skipPreClickDelay) Sleep(ClickFocusDelayMilliseconds);
        if (GetForegroundWindow() != handle)
        {
            failureDetail = "Terraria lost foreground focus during the pre-click wait.";
            return false;
        }
        if (!TrySendMouseInput(MouseEventLeftDown, out int mouseDownError))
        {
            failureDetail =
                $"SendInput(left-down) failed with Win32 error {mouseDownError}. " +
                $"{foregroundDiagnostic}; {coordinateSpace.Diagnostic}; " +
                $"logicalPoint={resolvedPoint.X},{resolvedPoint.Y}; " +
                actualCursorDiagnostic;
            return false;
        }

        Thread.Sleep(InputPressDurationMilliseconds);
        if (!TrySendMouseInput(MouseEventLeftUp, out int mouseUpError))
        {
            failureDetail =
                $"SendInput(left-up) failed with Win32 error {mouseUpError}. " +
                $"{foregroundDiagnostic}; {coordinateSpace.Diagnostic}; " +
                $"logicalPoint={resolvedPoint.X},{resolvedPoint.Y}; " +
                actualCursorDiagnostic;
            return false;
        }

        LastCoordinateDiagnostic =
            $"{foregroundDiagnostic}; {coordinateSpace.Diagnostic}; " +
            $"logicalPoint={resolvedPoint.X},{resolvedPoint.Y}; " +
            actualCursorDiagnostic;
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

    private static bool TryRequestForegroundAndWait(
        IntPtr handle,
        int delayMilliseconds,
        bool restoredFromMinimized,
        out string diagnostic)
    {
        IntPtr foregroundBefore = GetForegroundWindow();
        bool requestSucceeded = SetForegroundWindow(handle);
        Sleep(delayMilliseconds);
        IntPtr foregroundAfter = GetForegroundWindow();
        string foregroundDiagnostic =
            $"foregroundBefore=0x{foregroundBefore.ToInt64():X}; " +
            $"setForegroundSucceeded={requestSucceeded}; " +
            $"foregroundAfter=0x{foregroundAfter.ToInt64():X}; " +
            $"foregroundMatchesTerraria={foregroundAfter == handle}; foregroundDelayMs={delayMilliseconds}";

        if (foregroundAfter != handle)
        {
            diagnostic = foregroundDiagnostic;
            return false;
        }

        bool coordinateStabilityRequired = restoredFromMinimized || foregroundBefore != handle;
        bool requireFullscreenCoordinateSpace = TerrariaMenuProfile.IsExclusiveFullscreenConfigured();
        if (!coordinateStabilityRequired &&
            TryReadRestoredWindowInputSpace(
                handle,
                requireFullscreenCoordinateSpace,
                out RestoredWindowInputSpace immediateState) &&
            immediateState.IsReady)
        {
            diagnostic =
                $"{foregroundDiagnostic}; restoredWindowWait=not-required; " +
                immediateState.Diagnostic;
            return true;
        }

        bool ready = TryWaitForRestoredWindowInputSpace(
            handle,
            requireFullscreenCoordinateSpace,
            out string restoredWindowDiagnostic);
        diagnostic = $"{foregroundDiagnostic}; {restoredWindowDiagnostic}";
        return ready;
    }

    private static bool TryWaitForRestoredWindowInputSpace(
        IntPtr handle,
        bool requireFullscreenCoordinateSpace,
        out string diagnostic)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        string? stableSignature = null;
        int stableSampleCount = 0;
        string lastState = "unavailable";

        while (stopwatch.ElapsedMilliseconds <= RestoredWindowTimeoutMilliseconds)
        {
            if (TryReadRestoredWindowInputSpace(
                    handle,
                    requireFullscreenCoordinateSpace,
                    out RestoredWindowInputSpace state))
            {
                lastState = state.Diagnostic;
                if (state.IsReady && string.Equals(stableSignature, state.Signature, StringComparison.Ordinal))
                {
                    stableSampleCount++;
                }
                else
                {
                    stableSignature = state.IsReady ? state.Signature : null;
                    stableSampleCount = state.IsReady ? 1 : 0;
                }

                if (stableSampleCount >= RestoredWindowStableSampleCount)
                {
                    diagnostic =
                        $"restoredWindowWait=ready; restoredWindowWaitMs={stopwatch.ElapsedMilliseconds}; " +
                        $"stableSamples={stableSampleCount}; {lastState}";
                    return true;
                }
            }
            else
            {
                stableSignature = null;
                stableSampleCount = 0;
            }

            Sleep(RestoredWindowPollMilliseconds);
        }

        diagnostic =
            $"restoredWindowWait=timed-out; restoredWindowWaitMs={stopwatch.ElapsedMilliseconds}; " +
            $"stableSamples={stableSampleCount}; {lastState}";
        return false;
    }

    private static bool TryReadRestoredWindowInputSpace(
        IntPtr handle,
        bool requireFullscreenCoordinateSpace,
        out RestoredWindowInputSpace state)
    {
        state = default;
        IntPtr previousDpiContext = SetThreadDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2);
        if (previousDpiContext == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (!TryGetPhysicalClientBounds(handle, out Rectangle clientBounds))
            {
                return false;
            }

            IntPtr monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero)
            {
                return false;
            }

            var monitorInfo = new MonitorInfoEx
            {
                Size = (uint)Marshal.SizeOf<MonitorInfoEx>(),
                DeviceName = string.Empty
            };
            if (!GetMonitorInfo(monitor, ref monitorInfo))
            {
                return false;
            }

            var currentMode = CreateDeviceMode();
            bool currentModeAvailable = EnumDisplaySettingsEx(
                monitorInfo.DeviceName,
                EnumCurrentSettings,
                ref currentMode,
                0);
            Rectangle monitorBounds = ToRectangle(monitorInfo.Monitor);
            long style = GetWindowLongPtr(handle, GwlStyle).ToInt64();
            bool borderlessWindow = (style & (WsCaption | WsThickFrame)) == 0;
            bool fullscreenLikeWindow = requireFullscreenCoordinateSpace || borderlessWindow;
            bool coordinateSpaceReady = !fullscreenLikeWindow ||
                IsFullscreenCoordinateSpaceReady(
                    clientBounds,
                    monitorBounds,
                    currentModeAvailable,
                    new Size((int)currentMode.PelsWidth, (int)currentMode.PelsHeight),
                    new Point(currentMode.PositionX, currentMode.PositionY));
            bool foreground = GetForegroundWindow() == handle;
            string signature =
                $"{clientBounds.Width}x{clientBounds.Height}@{clientBounds.Left},{clientBounds.Top}|" +
                $"{monitorBounds.Width}x{monitorBounds.Height}@{monitorBounds.Left},{monitorBounds.Top}|" +
                $"{currentMode.PelsWidth}x{currentMode.PelsHeight}@{currentMode.PositionX},{currentMode.PositionY}|" +
                $"style=0x{style:X}";
            string stateDiagnostic =
                $"foreground={foreground}; requireFullscreenCoordinateSpace={requireFullscreenCoordinateSpace}; " +
                $"borderlessWindow={borderlessWindow}; " +
                $"client={clientBounds.Width}x{clientBounds.Height}@{clientBounds.Left},{clientBounds.Top}; " +
                $"monitor={monitorBounds.Width}x{monitorBounds.Height}@{monitorBounds.Left},{monitorBounds.Top}; " +
                $"currentMode={DescribeDisplayMode(currentModeAvailable, currentMode)}; style=0x{style:X}";
            state = new RestoredWindowInputSpace(
                foreground && coordinateSpaceReady,
                signature,
                stateDiagnostic);
            return true;
        }
        finally
        {
            _ = SetThreadDpiAwarenessContext(previousDpiContext);
        }
    }

    private static bool TryGetPhysicalClientBounds(IntPtr handle, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (!GetClientRect(handle, out Rect clientRect))
        {
            return false;
        }

        var origin = new PointStruct { X = clientRect.Left, Y = clientRect.Top };
        var end = new PointStruct { X = clientRect.Right, Y = clientRect.Bottom };
        if (!ClientToScreen(handle, ref origin) || !ClientToScreen(handle, ref end))
        {
            return false;
        }

        int width = end.X - origin.X;
        int height = end.Y - origin.Y;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        bounds = new Rectangle(origin.X, origin.Y, width, height);
        return true;
    }

    internal static bool IsFullscreenCoordinateSpaceReady(
        Rectangle clientBounds,
        Rectangle monitorBounds,
        bool currentModeAvailable,
        Size currentModeSize,
        Point currentModePosition)
    {
        return currentModeAvailable &&
            clientBounds == monitorBounds &&
            currentModeSize == monitorBounds.Size &&
            currentModePosition == monitorBounds.Location;
    }

    private static bool TrySendMouseInput(uint flags, out int win32Error)
    {
        Input[] inputs =
        [
            new Input
            {
                Type = InputMouse,
                Data = new InputUnion
                {
                    Mouse = new MouseInput { Flags = flags }
                }
            }
        ];
        if (SendInput(1, inputs, Marshal.SizeOf<Input>()) == 1)
        {
            win32Error = 0;
            return true;
        }

        win32Error = Marshal.GetLastWin32Error();
        return false;
    }

    private static bool TrySetXnaMousePosition(
        IntPtr handle,
        Point clientPoint,
        out Point screenPoint,
        out Point actualClientPoint,
        out string failureDetail)
    {
        return TryProcessXnaMousePosition(
            handle,
            clientPoint,
            moveCursor: true,
            out screenPoint,
            out actualClientPoint,
            out failureDetail);
    }

    internal static bool TryRoundTripXnaClientPoint(
        IntPtr handle,
        Point clientPoint,
        out Point screenPoint,
        out Point roundTrippedClientPoint,
        out string failureDetail)
    {
        return TryProcessXnaMousePosition(
            handle,
            clientPoint,
            moveCursor: false,
            out screenPoint,
            out roundTrippedClientPoint,
            out failureDetail);
    }

    private static bool TryProcessXnaMousePosition(
        IntPtr handle,
        Point clientPoint,
        bool moveCursor,
        out Point screenPoint,
        out Point actualClientPoint,
        out string failureDetail)
    {
        screenPoint = Point.Empty;
        actualClientPoint = Point.Empty;
        failureDetail = string.Empty;

        IntPtr windowDpiContext = GetWindowDpiAwarenessContext(handle);
        if (windowDpiContext == IntPtr.Zero)
        {
            failureDetail =
                $"GetWindowDpiAwarenessContext failed with Win32 error {Marshal.GetLastWin32Error()}.";
            return false;
        }

        IntPtr previousDpiContext = SetThreadDpiAwarenessContext(windowDpiContext);
        if (previousDpiContext == IntPtr.Zero)
        {
            failureDetail =
                $"SetThreadDpiAwarenessContext(window) failed with Win32 error {Marshal.GetLastWin32Error()}.";
            return false;
        }

        try
        {
            var target = new PointStruct { X = clientPoint.X, Y = clientPoint.Y };
            if (!ClientToScreen(handle, ref target))
            {
                failureDetail = $"ClientToScreen failed with Win32 error {Marshal.GetLastWin32Error()}.";
                return false;
            }

            screenPoint = new Point(target.X, target.Y);
            if (moveCursor && !SetCursorPos(target.X, target.Y))
            {
                failureDetail = $"SetCursorPos failed with Win32 error {Marshal.GetLastWin32Error()}.";
                return false;
            }

            PointStruct actual = target;
            if (moveCursor && !GetCursorPos(out actual))
            {
                failureDetail = $"GetCursorPos failed with Win32 error {Marshal.GetLastWin32Error()}.";
                return false;
            }

            if (!ScreenToClient(handle, ref actual))
            {
                failureDetail = $"ScreenToClient failed with Win32 error {Marshal.GetLastWin32Error()}.";
                return false;
            }

            actualClientPoint = new Point(actual.X, actual.Y);
            return true;
        }
        finally
        {
            _ = SetThreadDpiAwarenessContext(previousDpiContext);
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
                $"physicalClient={physicalWidth}x{physicalHeight}@{physicalOrigin.X},{physicalOrigin.Y}; " +
                $"automationPhysical={physicalBounds.Width}x{physicalBounds.Height}" +
                $"@{physicalBounds.Left},{physicalBounds.Top}; " +
                $"automationClient={logicalSize.Width}x{logicalSize.Height}; " +
                $"coordinateSource=DPI-logical active client; " +
                DescribeDisplayState(handle);
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

    private static string DescribeDisplayState(IntPtr handle)
    {
        IntPtr monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return "displayState=unavailable(monitor-not-found)";
        }

        var monitorInfo = new MonitorInfoEx
        {
            Size = (uint)Marshal.SizeOf<MonitorInfoEx>(),
            DeviceName = string.Empty
        };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return $"displayState=unavailable(get-monitor-info-win32Error={Marshal.GetLastWin32Error()})";
        }

        var currentMode = CreateDeviceMode();
        bool currentSucceeded = EnumDisplaySettingsEx(
            monitorInfo.DeviceName,
            EnumCurrentSettings,
            ref currentMode,
            0);
        var registeredMode = CreateDeviceMode();
        bool registeredSucceeded = EnumDisplaySettingsEx(
            monitorInfo.DeviceName,
            EnumRegistrySettings,
            ref registeredMode,
            0);

        return
            $"displayDevice={monitorInfo.DeviceName}; " +
            $"monitorBounds={DescribeBounds(monitorInfo.Monitor)}; " +
            $"currentDisplayMode={DescribeDisplayMode(currentSucceeded, currentMode)}; " +
            $"registeredDisplayMode={DescribeDisplayMode(registeredSucceeded, registeredMode)}";
    }

    private static DeviceMode CreateDeviceMode()
    {
        return new DeviceMode
        {
            DeviceName = string.Empty,
            Size = (ushort)Marshal.SizeOf<DeviceMode>(),
            FormName = string.Empty
        };
    }

    private static string DescribeBounds(Rect bounds)
    {
        return $"{bounds.Right - bounds.Left}x{bounds.Bottom - bounds.Top}@{bounds.Left},{bounds.Top}";
    }

    private static Rectangle ToRectangle(Rect bounds)
    {
        return new Rectangle(
            bounds.Left,
            bounds.Top,
            bounds.Right - bounds.Left,
            bounds.Bottom - bounds.Top);
    }

    private static string DescribeDisplayMode(bool succeeded, DeviceMode mode)
    {
        return succeeded
            ? $"{mode.PelsWidth}x{mode.PelsHeight}@{mode.PositionX},{mode.PositionY}/{mode.DisplayFrequency}Hz"
            : "unavailable";
    }

    private static Point MapLogicalClientPoint(
        ClientCoordinateSpace coordinateSpace,
        Point logicalClientPoint)
    {
        return MapAutomationClientPoint(
            coordinateSpace.PhysicalClientBounds,
            coordinateSpace.LogicalClientSize,
            logicalClientPoint);
    }

    internal static Point MapAutomationClientPoint(
        Rectangle physicalBounds,
        Size logicalSize,
        Point logicalClientPoint)
    {
        double scaleX = physicalBounds.Width / (double)logicalSize.Width;
        double scaleY = physicalBounds.Height / (double)logicalSize.Height;
        int physicalOffsetX = (int)Math.Round(
            logicalClientPoint.X * scaleX,
            MidpointRounding.AwayFromZero);
        int physicalOffsetY = (int)Math.Round(
            logicalClientPoint.Y * scaleY,
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

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public uint Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DeviceMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        public ushort SpecVersion;
        public ushort DriverVersion;
        public ushort Size;
        public ushort DriverExtra;
        public uint Fields;
        public int PositionX;
        public int PositionY;
        public uint DisplayOrientation;
        public uint DisplayFixedOutput;
        public short Color;
        public short Duplex;
        public short YResolution;
        public short TTOption;
        public short Collate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FormName;

        public ushort LogPixels;
        public uint BitsPerPel;
        public uint PelsWidth;
        public uint PelsHeight;
        public uint DisplayFlags;
        public uint DisplayFrequency;
        public uint ICMMethod;
        public uint ICMIntent;
        public uint MediaType;
        public uint DitherType;
        public uint Reserved1;
        public uint Reserved2;
        public uint PanningWidth;
        public uint PanningHeight;
    }

    private readonly record struct ClientCoordinateSpace(
        Rectangle PhysicalClientBounds,
        Size LogicalClientSize,
        string Diagnostic);

    private readonly record struct RestoredWindowInputSpace(
        bool IsReady,
        string Signature,
        string Diagnostic);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ClientToScreen(IntPtr hWnd, ref PointStruct lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetClientRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettingsEx(
        string lpszDeviceName,
        int iModeNum,
        ref DeviceMode lpDevMode,
        uint dwFlags);

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out PointStruct lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ScreenToClient(IntPtr hWnd, ref PointStruct lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint cInputs, Input[] pInputs, int cbSize);
}
