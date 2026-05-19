using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TerrariaSplit;

internal static class DisplayRefreshRateResolver
{
    private const int EnumCurrentSettings = -1;

    public static int ResolveForBounds(Rectangle bounds)
    {
        Screen screen = Screen.FromRectangle(bounds);
        var mode = new DevMode
        {
            dmSize = (short)Marshal.SizeOf<DevMode>()
        };

        return EnumDisplaySettings(screen.DeviceName, EnumCurrentSettings, ref mode) && mode.dmDisplayFrequency > 1
            ? mode.dmDisplayFrequency
            : 60;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(
        string deviceName,
        int modeNum,
        ref DevMode devMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }
}
