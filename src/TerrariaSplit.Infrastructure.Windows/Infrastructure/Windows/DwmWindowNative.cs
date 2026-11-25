using System.Runtime.InteropServices;

namespace TerrariaSplit.Infrastructure.Windows;

public enum WindowBackdropKind
{
    None = 0,
    Mica = 2,
    Acrylic = 3,
    Tabbed = 4
}

public static class DwmWindowNative
{
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaSystemBackdropType = 38;

    private const int DwmwcpDefault = 0;
    private const int DwmwcpDoNotRound = 1;
    private const int DwmwcpRound = 2;

    private const int GwlStyle = -16;
    private const long WsSysMenu = 0x00080000L;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;

    public static bool IsWindows11 =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);

    public static bool SupportsSystemBackdrop =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);

    public static bool RemoveWindowTitlebarContents(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        long style = GetWindowLongPtr(hwnd, GwlStyle).ToInt64();
        long updated = style & ~WsSysMenu;
        if (updated == style)
        {
            return true;
        }

        _ = SetWindowLongPtr(hwnd, GwlStyle, new IntPtr(updated));
        return SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }

    public static bool ApplyDarkMode(IntPtr hwnd, bool enabled = true)
    {
        if (hwnd == IntPtr.Zero || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return false;
        }

        int value = enabled ? 1 : 0;
        int attribute = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18985)
            ? DwmwaUseImmersiveDarkMode
            : DwmwaUseImmersiveDarkModeBefore20H1;
        return DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int)) == 0;
    }

    public static bool ApplyRoundedCorners(IntPtr hwnd, bool rounded = true)
    {
        if (hwnd == IntPtr.Zero || !IsWindows11)
        {
            return false;
        }

        int preference = rounded ? DwmwcpRound : DwmwcpDoNotRound;
        return DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref preference, sizeof(int)) == 0;
    }

    public static bool ResetRoundedCorners(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindows11)
        {
            return false;
        }

        int preference = DwmwcpDefault;
        return DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref preference, sizeof(int)) == 0;
    }

    public static bool ApplyBorderColor(IntPtr hwnd, byte red, byte green, byte blue)
    {
        if (hwnd == IntPtr.Zero || !IsWindows11)
        {
            return false;
        }

        int colorRef = red | (green << 8) | (blue << 16);
        return DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref colorRef, sizeof(int)) == 0;
    }

    public static bool ApplySystemBackdrop(IntPtr hwnd, WindowBackdropKind kind)
    {
        if (hwnd == IntPtr.Zero || !SupportsSystemBackdrop)
        {
            return false;
        }

        int backdrop = kind switch
        {
            WindowBackdropKind.Mica => 2,
            WindowBackdropKind.Acrylic => 3,
            WindowBackdropKind.Tabbed => 4,
            _ => 1
        };

        if (kind != WindowBackdropKind.None)
        {
            var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            _ = DwmExtendFrameIntoClientArea(hwnd, ref margins);
        }

        return DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int)) == 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmExtendFrameIntoClientArea(
        IntPtr hwnd,
        ref Margins pMarInset);

    private static IntPtr GetWindowLongPtr(IntPtr hwnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, nIndex) : new IntPtr(GetWindowLong32(hwnd, nIndex));

    private static IntPtr SetWindowLongPtr(IntPtr hwnd, int nIndex, IntPtr dwNewLong) =>
        IntPtr.Size == 8 ? SetWindowLongPtr64(hwnd, nIndex, dwNewLong) : new IntPtr(SetWindowLong32(hwnd, nIndex, dwNewLong.ToInt32()));

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hwnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hwnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr hwndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);
}
