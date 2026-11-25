using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using FormsWin32Window = System.Windows.Forms.IWin32Window;

namespace TerrariaSplit.UI.Wpf.Interop;

internal static class WpfWindowInterop
{
    public static IntPtr EnsureHandle(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return new WindowInteropHelper(window).EnsureHandle();
    }

    public static IntPtr GetHandle(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return new WindowInteropHelper(window).Handle;
    }

    public static void AttachOwner(Window window, FormsWin32Window? owner)
    {
        if (owner is null || owner.Handle == IntPtr.Zero)
        {
            return;
        }

        AttachOwner(window, owner.Handle);
    }

    public static void AttachOwner(Window window, IntPtr ownerHandle)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (ownerHandle == IntPtr.Zero)
        {
            return;
        }

        new WindowInteropHelper(window).Owner = ownerHandle;
    }

    public static FormsWin32Window AsWin32Window(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return new WpfWin32WindowAdapter(window);
    }

    public static void ShowModeless(Window window, FormsWin32Window? owner = null)
    {
        ArgumentNullException.ThrowIfNull(window);
        WpfBootstrap.EnableModelessKeyboardInterop(window);
        AttachOwner(window, owner);
        window.Show();
    }

    public static void CenterOnPixelBounds(Window window, Rectangle ownerPixelBounds)
    {
        ArgumentNullException.ThrowIfNull(window);

        Rectangle targetPixels = ownerPixelBounds.Width > 0 && ownerPixelBounds.Height > 0
            ? ownerPixelBounds
            : Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Rectangle screenPixels = Screen.FromRectangle(targetPixels).WorkingArea;

        DpiScale dpi = VisualTreeHelper.GetDpi(window);
        double scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1d;
        double scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1d;

        double targetLeft = targetPixels.Left / scaleX;
        double targetTop = targetPixels.Top / scaleY;
        double targetWidth = targetPixels.Width / scaleX;
        double targetHeight = targetPixels.Height / scaleY;

        double screenLeft = screenPixels.Left / scaleX;
        double screenTop = screenPixels.Top / scaleY;
        double screenRight = screenPixels.Right / scaleX;
        double screenBottom = screenPixels.Bottom / scaleY;

        double windowWidth = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        double windowHeight = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
        if (double.IsNaN(windowWidth) || windowWidth <= 0)
        {
            windowWidth = window.MinWidth > 0 ? window.MinWidth : 640;
        }

        if (double.IsNaN(windowHeight) || windowHeight <= 0)
        {
            windowHeight = window.MinHeight > 0 ? window.MinHeight : 420;
        }

        double desiredLeft = targetLeft + (targetWidth - windowWidth) / 2d;
        double desiredTop = targetTop + (targetHeight - windowHeight) / 2d;

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = Math.Clamp(desiredLeft, screenLeft, Math.Max(screenLeft, screenRight - windowWidth));
        window.Top = Math.Clamp(desiredTop, screenTop, Math.Max(screenTop, screenBottom - windowHeight));
    }

    public static void FitToWorkingArea(Window window)
    {
        Rectangle pixels = Screen.FromHandle(EnsureHandle(window)).WorkingArea;
        DpiScale dpi = VisualTreeHelper.GetDpi(window);
        double scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1d;
        double scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1d;
        FitToWorkingArea(window, new Rect(pixels.Left / scaleX, pixels.Top / scaleY,
            pixels.Width / scaleX, pixels.Height / scaleY));
    }

    internal static void FitToWorkingArea(Window window, Rect workingArea)
    {
        window.MinWidth = Math.Min(window.MinWidth, workingArea.Width);
        window.MinHeight = Math.Min(window.MinHeight, workingArea.Height);
        if (window.SizeToContent is SizeToContent.Width or SizeToContent.WidthAndHeight)
        {
            window.MaxWidth = Math.Min(window.MaxWidth, workingArea.Width);
        }

        if (window.SizeToContent is SizeToContent.Height or SizeToContent.WidthAndHeight)
        {
            window.MaxHeight = Math.Min(window.MaxHeight, workingArea.Height);
        }

        if (window.WindowState != WindowState.Normal)
        {
            return;
        }

        if (double.IsFinite(window.Width))
        {
            window.Width = Math.Min(window.Width, workingArea.Width);
        }

        if (double.IsFinite(window.Height))
        {
            window.Height = Math.Min(window.Height, workingArea.Height);
        }

        window.UpdateLayout();
        double width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        double height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
        if (!double.IsFinite(width))
        {
            width = window.MinWidth;
        }

        if (!double.IsFinite(height))
        {
            height = window.MinHeight;
        }

        if (double.IsFinite(window.Left))
        {
            window.Left = Math.Clamp(window.Left, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - width));
        }

        if (double.IsFinite(window.Top))
        {
            window.Top = Math.Clamp(window.Top, workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - height));
        }
    }

    private sealed class WpfWin32WindowAdapter(Window window) : FormsWin32Window
    {
        public IntPtr Handle => GetHandle(window);
    }
}
