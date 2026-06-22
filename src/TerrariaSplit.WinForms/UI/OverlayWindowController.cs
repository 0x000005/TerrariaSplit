using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

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
    private const int WsExNoActivate = 0x08000000;

    private readonly Form owner;
    private readonly Func<Graphics, bool> draw;
    private readonly Action<TimeSpan> recordPaint;
    private readonly Action<Action> dispatch;
    private readonly Func<Bitmap, bool> updateLayeredBitmap;
    private readonly LayeredWindowRenderTarget? renderTarget;
    private readonly Action renderQueuedCallback;
    private bool renderPending;
    private bool renderInProgress;
    private bool disposed;

    public OverlayWindowController(
        Form owner,
        Func<Graphics, bool> draw,
        Action<TimeSpan> recordPaint,
        Action<Action>? dispatch = null,
        Func<Bitmap, bool>? updateLayeredBitmap = null,
        Action<LayeredWindowUpdateDiagnostics>? recordLayeredUpdate = null)
    {
        this.owner = owner;
        this.draw = draw;
        this.recordPaint = recordPaint;
        this.dispatch = dispatch ?? (callback => owner.BeginInvoke(callback));
        renderQueuedCallback = RenderQueued;
        if (updateLayeredBitmap is null)
        {
            renderTarget = new LayeredWindowRenderTarget(recordLayeredUpdate);
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
            dispatch(renderQueuedCallback);
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
                StaticAppLogger.Instance.Info($"Layered overlay update failed. Win32Error={Marshal.GetLastWin32Error()}.");
            }
        }
        catch (Exception ex)
        {
            StaticAppLogger.Instance.Error(ex, "Layered overlay render failed.");
        }
        finally
        {
            renderInProgress = false;
            recordPaint(Stopwatch.GetElapsedTime(startTimestamp));
        }
    }

    /// <summary>
    /// Redraws only <paramref name="dirtyRect"/> on the persistent layered
    /// surface. Returns false when no persistent render target is available so
    /// the caller can fall back to a full render.
    /// </summary>
    public bool RenderRegionImmediately(Rectangle dirtyRect)
    {
        return RenderRegionImmediatelyCore(
            target => target.RenderRegion(owner, draw, ConfigureGraphics, dirtyRect));
    }

    /// <summary>
    /// Redraws a dirty region measured on the configured render graphics.
    /// Returns false when no persistent render target is available so the
    /// caller can fall back to a full render.
    /// </summary>
    public bool RenderRegionImmediately(Func<Graphics, Rectangle> resolveDirtyRect)
    {
        return RenderRegionImmediatelyCore(
            target => target.RenderRegion(owner, draw, ConfigureGraphics, resolveDirtyRect));
    }

    private bool RenderRegionImmediatelyCore(Func<LayeredWindowRenderTarget, bool> render)
    {
        if (!owner.IsHandleCreated || owner.IsDisposed || owner.Disposing || disposed)
        {
            renderPending = false;
            return true;
        }

        if (renderTarget is null)
        {
            return false;
        }

        if (renderInProgress)
        {
            return true;
        }

        renderPending = false;
        renderInProgress = true;
        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            if (owner.ClientSize.Width <= 0 || owner.ClientSize.Height <= 0)
            {
                return true;
            }

            if (!render(renderTarget))
            {
                StaticAppLogger.Instance.Info($"Layered overlay region update failed. Win32Error={Marshal.GetLastWin32Error()}.");
            }

            return true;
        }
        catch (Exception ex)
        {
            StaticAppLogger.Instance.Error(ex, "Layered overlay region render failed.");
            return true;
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

    public void ApplyWindowStyle(bool mouseClickThrough, bool noActivate = false)
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
        int extendedStyle = ComposeExtendedStyle(style, mouseClickThrough, noActivate);
        if (extendedStyle != style)
        {
            SetWindowLong(handle, GwlExStyle, extendedStyle);
            SetWindowPos(
                handle,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
        }
    }

    public void Dispose()
    {
        disposed = true;
        renderTarget?.Dispose();
    }

    internal static int ComposeExtendedStyle(int existingStyle, bool mouseClickThrough, bool noActivate = false)
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

        if (noActivate)
        {
            style |= WsExNoActivate;
        }
        else
        {
            style &= ~WsExNoActivate;
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
