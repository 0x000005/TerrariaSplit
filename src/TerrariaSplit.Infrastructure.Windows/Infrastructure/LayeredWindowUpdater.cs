using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static TerrariaSplit.Infrastructure.Windows.LayeredWindowNative;

namespace TerrariaSplit.Infrastructure;

public static class LayeredWindowUpdater
{
    public static bool Update(Form form, Bitmap bitmap)
    {
        IntPtr screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            return false;
        }

        IntPtr memoryDc = IntPtr.Zero;
        IntPtr bitmapHandle = IntPtr.Zero;
        IntPtr oldBitmap = IntPtr.Zero;
        try
        {
            memoryDc = CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero)
            {
                return false;
            }

            bitmapHandle = CreateLayeredBitmapHandle(bitmap, screenDc);
            if (bitmapHandle == IntPtr.Zero)
            {
                return false;
            }

            oldBitmap = SelectObject(memoryDc, bitmapHandle);
            if (oldBitmap == IntPtr.Zero)
            {
                return false;
            }

            var destination = new NativePoint(form.Left, form.Top);
            var size = new NativeSize(bitmap.Width, bitmap.Height);
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
                ref size,
                memoryDc,
                ref source,
                0,
                ref blend,
                UlwAlpha);
        }
        finally
        {
            if (oldBitmap != IntPtr.Zero)
            {
                SelectObject(memoryDc, oldBitmap);
            }

            if (bitmapHandle != IntPtr.Zero)
            {
                DeleteObject(bitmapHandle);
            }

            if (memoryDc != IntPtr.Zero)
            {
                DeleteDC(memoryDc);
            }

            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static IntPtr CreateLayeredBitmapHandle(Bitmap bitmap, IntPtr deviceContext)
    {
        var bitmapInfo = new BitmapInfo
        {
            Header = new BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                Width = bitmap.Width,
                Height = -bitmap.Height,
                Planes = 1,
                BitCount = 32,
                Compression = BiRgb,
                SizeImage = (uint)(bitmap.Width * bitmap.Height * 4)
            }
        };

        IntPtr bitmapHandle = CreateDIBSection(
            deviceContext,
            ref bitmapInfo,
            DibRgbColors,
            out IntPtr bits,
            IntPtr.Zero,
            0);
        if (bitmapHandle == IntPtr.Zero || bits == IntPtr.Zero)
        {
            if (bitmapHandle != IntPtr.Zero)
            {
                DeleteObject(bitmapHandle);
            }

            return IntPtr.Zero;
        }

        CopyBitmapPixels(bitmap, bits);
        return bitmapHandle;
    }

    private static void CopyBitmapPixels(Bitmap bitmap, IntPtr destination)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            int rowBytes = bitmap.Width * 4;
            byte[] buffer = new byte[rowBytes];
            int sourceStride = data.Stride;
            for (int y = 0; y < bitmap.Height; y++)
            {
                IntPtr sourceRow = sourceStride >= 0
                    ? IntPtr.Add(data.Scan0, y * sourceStride)
                    : IntPtr.Add(data.Scan0, (bitmap.Height - 1 - y) * -sourceStride);
                IntPtr destinationRow = IntPtr.Add(destination, y * rowBytes);
                Marshal.Copy(sourceRow, buffer, 0, rowBytes);
                Marshal.Copy(buffer, 0, destinationRow, rowBytes);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

}
