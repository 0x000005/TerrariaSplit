using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class LayeredWindowRenderTarget : IDisposable
{
    private const byte AcSrcOver = 0x00;
    private const byte AcSrcAlpha = 0x01;
    private const int UlwAlpha = 0x00000002;
    private const uint BiRgb = 0;
    private const uint DibRgbColors = 0;

    // Offsets into the unmanaged scratch block used for the pointer members of
    // UPDATELAYEREDWINDOWINFO (POINT dst, SIZE size, POINT src, BLENDFUNCTION,
    // RECT dirty).
    private const int ScratchDestinationOffset = 0;
    private const int ScratchSizeOffset = 8;
    private const int ScratchSourceOffset = 16;
    private const int ScratchBlendOffset = 24;
    private const int ScratchDirtyRectOffset = 28;
    private const int ScratchLength = 44;

    private Bitmap? bitmap;
    private IntPtr memoryDc;
    private IntPtr bitmapHandle;
    private IntPtr oldBitmap;
    private IntPtr bits;
    private Size size;
    private IntPtr scratch;
    private SolidBrush? clearBrush;

    public bool Render(Form form, Func<Graphics, bool> draw, Action<Graphics> configureGraphics)
    {
        Size clientSize = form.ClientSize;
        if (clientSize.Width <= 0 || clientSize.Height <= 0)
        {
            return false;
        }

        if (!EnsureTarget(clientSize))
        {
            return false;
        }

        using (Graphics graphics = Graphics.FromImage(bitmap!))
        {
            configureGraphics(graphics);
            graphics.Clear(Color.Transparent);
            if (!draw(graphics))
            {
                return false;
            }
        }

        return UpdateWindow(form);
    }

    /// <summary>
    /// Redraws only <paramref name="dirtyRect"/> on the persistent surface and
    /// pushes that region to the compositor. Requires a previous full
    /// <see cref="Render"/> at the current client size; falls back to a full
    /// render otherwise. The draw callback runs with the clip set to the dirty
    /// region, over a region cleared back to transparent.
    /// </summary>
    public bool RenderRegion(Form form, Func<Graphics, bool> draw, Action<Graphics> configureGraphics, Rectangle dirtyRect)
    {
        Size clientSize = form.ClientSize;
        if (bitmap is null || size != clientSize)
        {
            return Render(form, draw, configureGraphics);
        }

        dirtyRect.Intersect(new Rectangle(Point.Empty, size));
        if (dirtyRect.Width <= 0 || dirtyRect.Height <= 0)
        {
            return true;
        }

        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            configureGraphics(graphics);
            graphics.SetClip(dirtyRect);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.FillRectangle(clearBrush ??= new SolidBrush(Color.Transparent), dirtyRect);
            graphics.CompositingMode = CompositingMode.SourceOver;
            if (!draw(graphics))
            {
                return false;
            }

            graphics.ResetClip();
        }

        return UpdateWindowRegion(form, dirtyRect) || UpdateWindow(form);
    }

    public void Dispose()
    {
        bitmap?.Dispose();
        bitmap = null;

        clearBrush?.Dispose();
        clearBrush = null;

        if (oldBitmap != IntPtr.Zero && memoryDc != IntPtr.Zero)
        {
            SelectObject(memoryDc, oldBitmap);
            oldBitmap = IntPtr.Zero;
        }

        if (bitmapHandle != IntPtr.Zero)
        {
            DeleteObject(bitmapHandle);
            bitmapHandle = IntPtr.Zero;
        }

        if (memoryDc != IntPtr.Zero)
        {
            DeleteDC(memoryDc);
            memoryDc = IntPtr.Zero;
        }

        if (scratch != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(scratch);
            scratch = IntPtr.Zero;
        }

        bits = IntPtr.Zero;
        size = Size.Empty;
    }

    private bool EnsureTarget(Size targetSize)
    {
        if (bitmap is not null && size == targetSize)
        {
            return true;
        }

        Dispose();

        IntPtr screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            return false;
        }

        Bitmap? newBitmap = null;
        IntPtr newMemoryDc = IntPtr.Zero;
        IntPtr newBitmapHandle = IntPtr.Zero;
        IntPtr newOldBitmap = IntPtr.Zero;
        IntPtr newBits = IntPtr.Zero;

        try
        {
            newMemoryDc = CreateCompatibleDC(screenDc);
            if (newMemoryDc == IntPtr.Zero)
            {
                return false;
            }

            var bitmapInfo = new BitmapInfo
            {
                Header = new BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                    Width = targetSize.Width,
                    Height = -targetSize.Height,
                    Planes = 1,
                    BitCount = 32,
                    Compression = BiRgb,
                    SizeImage = (uint)(targetSize.Width * targetSize.Height * 4)
                }
            };

            newBitmapHandle = CreateDIBSection(
                screenDc,
                ref bitmapInfo,
                DibRgbColors,
                out newBits,
                IntPtr.Zero,
                0);
            if (newBitmapHandle == IntPtr.Zero || newBits == IntPtr.Zero)
            {
                return false;
            }

            newOldBitmap = SelectObject(newMemoryDc, newBitmapHandle);
            if (newOldBitmap == IntPtr.Zero)
            {
                return false;
            }

            newBitmap = new Bitmap(
                targetSize.Width,
                targetSize.Height,
                targetSize.Width * 4,
                PixelFormat.Format32bppPArgb,
                newBits);

            bitmap = newBitmap;
            memoryDc = newMemoryDc;
            bitmapHandle = newBitmapHandle;
            oldBitmap = newOldBitmap;
            bits = newBits;
            size = targetSize;

            newBitmap = null;
            newMemoryDc = IntPtr.Zero;
            newBitmapHandle = IntPtr.Zero;
            newOldBitmap = IntPtr.Zero;
            newBits = IntPtr.Zero;

            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Failed to create layered window render target.");
            return false;
        }
        finally
        {
            DisposeTarget(newBitmap, newMemoryDc, newBitmapHandle, newOldBitmap);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static void DisposeTarget(Bitmap? targetBitmap, IntPtr targetDc, IntPtr targetBitmapHandle, IntPtr targetOldBitmap)
    {
        targetBitmap?.Dispose();

        if (targetOldBitmap != IntPtr.Zero && targetDc != IntPtr.Zero)
        {
            SelectObject(targetDc, targetOldBitmap);
        }

        if (targetBitmapHandle != IntPtr.Zero)
        {
            DeleteObject(targetBitmapHandle);
        }

        if (targetDc != IntPtr.Zero)
        {
            DeleteDC(targetDc);
        }
    }

    private bool UpdateWindow(Form form)
    {
        IntPtr screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var destination = new NativePoint(form.Left, form.Top);
            var nativeSize = new NativeSize(size.Width, size.Height);
            var source = new NativePoint(0, 0);
            var blend = new BlendFunction
            {
                BlendOp = AcSrcOver,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = AcSrcAlpha
            };

            return UpdateLayeredWindow(
                form.Handle,
                screenDc,
                ref destination,
                ref nativeSize,
                memoryDc,
                ref source,
                0,
                ref blend,
                UlwAlpha);
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private bool UpdateWindowRegion(Form form, Rectangle dirtyRect)
    {
        IntPtr screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (scratch == IntPtr.Zero)
            {
                scratch = Marshal.AllocHGlobal(ScratchLength);
            }

            Marshal.StructureToPtr(
                new NativePoint(form.Left, form.Top),
                scratch + ScratchDestinationOffset,
                fDeleteOld: false);
            Marshal.StructureToPtr(
                new NativeSize(size.Width, size.Height),
                scratch + ScratchSizeOffset,
                fDeleteOld: false);
            Marshal.StructureToPtr(
                new NativePoint(0, 0),
                scratch + ScratchSourceOffset,
                fDeleteOld: false);
            Marshal.StructureToPtr(
                new BlendFunction
                {
                    BlendOp = AcSrcOver,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = AcSrcAlpha
                },
                scratch + ScratchBlendOffset,
                fDeleteOld: false);
            Marshal.StructureToPtr(
                new NativeRect(dirtyRect.Left, dirtyRect.Top, dirtyRect.Right, dirtyRect.Bottom),
                scratch + ScratchDirtyRectOffset,
                fDeleteOld: false);

            var info = new UpdateLayeredWindowInfo
            {
                Size = (uint)Marshal.SizeOf<UpdateLayeredWindowInfo>(),
                DestinationDc = screenDc,
                DestinationPoint = scratch + ScratchDestinationOffset,
                WindowSize = scratch + ScratchSizeOffset,
                SourceDc = memoryDc,
                SourcePoint = scratch + ScratchSourceOffset,
                ColorKey = 0,
                BlendFunction = scratch + ScratchBlendOffset,
                Flags = UlwAlpha,
                DirtyRect = scratch + ScratchDirtyRectOffset
            };

            return UpdateLayeredWindowIndirect(form.Handle, ref info);
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteDC(IntPtr hDc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr SelectObject(IntPtr hDc, IntPtr hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateDIBSection(
        IntPtr hdc,
        ref BitmapInfo pbmi,
        uint usage,
        out IntPtr bits,
        IntPtr section,
        uint offset);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindow(
        IntPtr hWnd,
        IntPtr hdcDst,
        ref NativePoint pptDst,
        ref NativeSize psize,
        IntPtr hdcSrc,
        ref NativePoint pptSrc,
        int crKey,
        ref BlendFunction pblend,
        int dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindowIndirect(
        IntPtr hWnd,
        ref UpdateLayeredWindowInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct UpdateLayeredWindowInfo
    {
        public uint Size;
        public IntPtr DestinationDc;
        public IntPtr DestinationPoint;
        public IntPtr WindowSize;
        public IntPtr SourceDc;
        public IntPtr SourcePoint;
        public uint ColorKey;
        public IntPtr BlendFunction;
        public uint Flags;
        public IntPtr DirtyRect;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public NativeRect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;

        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Width;
        public int Height;

        public NativeSize(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }
}
