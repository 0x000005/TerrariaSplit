using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class OverlayWindowController : IDisposable
{
    private const int GwlExStyle = -20;
    private const int GwlStyle = -16;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const int WsBorder = 0x00800000;
    private const int WsCaption = 0x00C00000;
    private const int WsDlgFrame = 0x00400000;
    private const int WsThickFrame = 0x00040000;
    private const int WsExTransparent = 0x20;
    private const int WsExLayered = 0x80000;

    private readonly Form owner;
    private readonly Func<Graphics, bool> draw;
    private readonly Action<TimeSpan> recordPaint;
    private readonly Action<Action> dispatch;
    private readonly Func<Bitmap, bool> updateLayeredBitmap;
    private readonly LayeredWindowRenderTarget? renderTarget;
    private bool renderPending;
    private bool renderInProgress;
    private bool disposed;

    public OverlayWindowController(
        Form owner,
        Func<Graphics, bool> draw,
        Action<TimeSpan> recordPaint,
        Action<Action>? dispatch = null,
        Func<Bitmap, bool>? updateLayeredBitmap = null)
    {
        this.owner = owner;
        this.draw = draw;
        this.recordPaint = recordPaint;
        this.dispatch = dispatch ?? (callback => owner.BeginInvoke(callback));
        if (updateLayeredBitmap is null)
        {
            renderTarget = new LayeredWindowRenderTarget();
            this.updateLayeredBitmap = bitmap => LayeredWindowUpdater.Update(owner, bitmap);
        }
        else
        {
            this.updateLayeredBitmap = updateLayeredBitmap;
        }
    }

    public void QueueRender()
    {
        if (!owner.IsHandleCreated || owner.IsDisposed || owner.Disposing || disposed || renderPending)
        {
            return;
        }

        renderPending = true;
        try
        {
            dispatch(RenderQueued);
        }
        catch (ObjectDisposedException)
        {
            renderPending = false;
        }
        catch (InvalidOperationException)
        {
            renderPending = false;
        }
    }

    public void RenderImmediately()
    {
        if (!owner.IsHandleCreated || owner.IsDisposed || owner.Disposing || disposed)
        {
            renderPending = false;
            return;
        }

        if (renderInProgress)
        {
            return;
        }

        renderPending = false;
        renderInProgress = true;
        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            if (!RenderNow())
            {
                AppLogger.Info($"Layered overlay update failed. Win32Error={Marshal.GetLastWin32Error()}.");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Layered overlay render failed.");
        }
        finally
        {
            renderInProgress = false;
            recordPaint(Stopwatch.GetElapsedTime(startTimestamp));
        }
    }

    public bool RenderNow(Func<Graphics, bool>? drawOverride = null)
    {
        if (!owner.IsHandleCreated || owner.ClientSize.Width <= 0 || owner.ClientSize.Height <= 0)
        {
            return false;
        }

        if (drawOverride is null && renderTarget is not null)
        {
            return renderTarget.Render(owner, draw, ConfigureGraphics);
        }

        using var bitmap = new Bitmap(owner.ClientSize.Width, owner.ClientSize.Height, PixelFormat.Format32bppPArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            ConfigureGraphics(graphics);
            graphics.Clear(Color.Transparent);
            if (!(drawOverride ?? draw)(graphics))
            {
                return false;
            }
        }

        return updateLayeredBitmap(bitmap);
    }

    public void ApplyWindowStyle(bool mouseClickThrough)
    {
        if (!owner.IsHandleCreated)
        {
            return;
        }

        IntPtr handle = owner.Handle;
        int windowStyle = GetWindowLong(handle, GwlStyle);
        int borderlessStyle = ComposeBorderlessStyle(windowStyle);
        if (borderlessStyle != windowStyle)
        {
            SetWindowLong(handle, GwlStyle, borderlessStyle);
            SetWindowPos(
                handle,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
        }

        int style = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, ComposeExtendedStyle(style, mouseClickThrough));
    }

    public void Dispose()
    {
        disposed = true;
        renderTarget?.Dispose();
    }

    internal static int ComposeExtendedStyle(int existingStyle, bool mouseClickThrough)
    {
        int style = existingStyle | WsExLayered;
        if (mouseClickThrough)
        {
            style |= WsExTransparent;
        }
        else
        {
            style &= ~WsExTransparent;
        }

        return style;
    }

    internal static int ComposeBorderlessStyle(int existingStyle)
    {
        return existingStyle & ~(WsCaption | WsBorder | WsDlgFrame | WsThickFrame);
    }

    private void RenderQueued()
    {
        RenderImmediately();
    }

    private static void ConfigureGraphics(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.CompositingMode = CompositingMode.SourceOver;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);
}
