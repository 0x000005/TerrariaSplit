using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using Process = System.Diagnostics.Process;

namespace TerrariaSplit.Terraria;

internal static class TerrariaWindowProbe
{
    public static TerrariaWindowSnapshot Read()
    {
        Process? process = TerrariaProcessFinder.FindNewest();
        if (process is null)
        {
            return new TerrariaWindowSnapshot(
                false,
                null,
                null,
                false,
                false,
                IntPtr.Zero,
                string.Empty,
                false,
                false,
                false,
                false,
                null,
                null,
                "waiting for Terraria.exe");
        }

        try
        {
            int? processId = TryGetProcessId(process);
            DateTime? processStartTime = TryGetStartTime(process);
            bool isResponding = TryGetResponding(process);
            IntPtr handle = TryGetMainWindowHandle(process);
            string title = TryGetMainWindowTitle(process);

            if (handle == IntPtr.Zero)
            {
                return new TerrariaWindowSnapshot(
                    true,
                    processId,
                    processStartTime,
                    isResponding,
                    false,
                    IntPtr.Zero,
                    title,
                    false,
                    false,
                    false,
                    false,
                    null,
                    null,
                    FormatStatus(processId, "process detected, main window not ready"));
            }

            Rectangle? bounds = TryGetWindowBounds(handle, out Rectangle windowBounds)
                ? windowBounds
                : null;
            Size? clientSize = TryGetClientSize(handle, out Size size)
                ? size
                : null;

            string status = clientSize is null
                ? FormatStatus(processId, $"window handle 0x{handle.ToInt64():X}, client rect unavailable")
                : FormatStatus(processId, $"window handle 0x{handle.ToInt64():X}");

            return new TerrariaWindowSnapshot(
                true,
                processId,
                processStartTime,
                isResponding,
                true,
                handle,
                title,
                IsWindowVisible(handle),
                IsIconic(handle),
                IsZoomed(handle),
                GetForegroundWindow() == handle,
                bounds,
                clientSize,
                status);
        }
        catch (InvalidOperationException ex)
        {
            return new TerrariaWindowSnapshot(
                false,
                null,
                null,
                false,
                false,
                IntPtr.Zero,
                string.Empty,
                false,
                false,
                false,
                false,
                null,
                null,
                $"Terraria process changed while reading window state: {ex.Message}");
        }
        finally
        {
            process.Dispose();
        }
    }

    private static string FormatStatus(int? processId, string detail)
    {
        return processId.HasValue
            ? $"attached to Terraria PID {processId.Value}, {detail}"
            : $"attached to Terraria process, {detail}";
    }

    private static int? TryGetProcessId(Process process)
    {
        try
        {
            return process.Id;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static DateTime? TryGetStartTime(Process process)
    {
        try
        {
            return process.StartTime;
        }
        catch (Win32Exception)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static bool TryGetResponding(Process process)
    {
        try
        {
            return process.Responding;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static IntPtr TryGetMainWindowHandle(Process process)
    {
        try
        {
            return process.MainWindowHandle;
        }
        catch (InvalidOperationException)
        {
            return IntPtr.Zero;
        }
    }

    private static string TryGetMainWindowTitle(Process process)
    {
        try
        {
            return process.MainWindowTitle ?? string.Empty;
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
    }

    private static bool TryGetClientSize(IntPtr handle, out Size clientSize)
    {
        clientSize = Size.Empty;
        if (!GetClientRect(handle, out Rect rect))
        {
            return false;
        }

        clientSize = new Size(rect.Right - rect.Left, rect.Bottom - rect.Top);
        return clientSize.Width >= 0 && clientSize.Height >= 0;
    }

    private static bool TryGetWindowBounds(IntPtr handle, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (!GetWindowRect(handle, out Rect rect))
        {
            return false;
        }

        bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
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

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);
}
