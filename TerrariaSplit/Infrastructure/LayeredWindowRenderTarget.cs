using System.Drawing;
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

    private Bitmap? bitmap;
    private IntPtr memoryDc;
    private IntPtr bitmapHandle;
    private IntPtr oldBitmap;
    private IntPtr bits;
    private Size size;

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

    public void Dispose()
    {
        bitmap?.Dispose();
        bitmap = null;

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
