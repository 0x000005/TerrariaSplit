using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace TerrariaSplit.Infrastructure.Windows;

public static class ProcessCpuTopology
{
    public static int AvailablePhysicalCoreCount()
    {
        if (!OperatingSystem.IsWindows()) return 1;
        try
        {
            nint process = GetCurrentProcess();
            if (!GetProcessAffinityMask(process, out nuint mask, out _) || mask == 0) return 1;
            GetCurrentProcessorNumberEx(out ProcessorNumber processor);
            _ = GetSystemCpuSetInformation(null, 0, out uint required, process, 0);
            if (required == 0 || required > int.MaxValue) return 1;
            byte[] data = new byte[required];
            if (!GetSystemCpuSetInformation(data, required, out uint written, process, 0) || written > required) return 1;
            return CountPhysicalCores(data.AsSpan(0, (int)written), (ulong)mask, processor.Group);
        }
        catch (EntryPointNotFoundException) { return 1; }
        catch (DllNotFoundException) { return 1; }
    }

    internal static int CountPhysicalCores(ReadOnlySpan<byte> data, ulong affinityMask, ushort group)
    {
        Span<bool> seen = stackalloc bool[256];
        seen.Clear();
        int count = 0;
        for (int offset = 0; offset < data.Length;)
        {
            if (data.Length - offset < 8) return 1;
            ReadOnlySpan<byte> record = data[offset..];
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(record);
            uint type = BinaryPrimitives.ReadUInt32LittleEndian(record[4..]);
            if (size < 8 || size > record.Length) return 1;
            if (type == 0)
            {
                if (size < 32) return 1;
                ushort cpuGroup = BinaryPrimitives.ReadUInt16LittleEndian(record[12..]);
                byte logical = record[14], core = record[15], flags = record[19];
                bool allocatedElsewhere = (flags & 2) != 0 && (flags & 4) == 0;
                if (cpuGroup == group && logical < 64 && !allocatedElsewhere &&
                    (affinityMask & (1UL << logical)) != 0 && !seen[core])
                {
                    seen[core] = true;
                    count++;
                }
            }
            offset += (int)size;
        }
        return Math.Max(1, count);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessorNumber { public ushort Group; public byte Number; public byte Reserved; }

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessAffinityMask(nint process, out nuint processMask, out nuint systemMask);
    [DllImport("kernel32.dll")]
    private static extern void GetCurrentProcessorNumberEx(out ProcessorNumber processor);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemCpuSetInformation([Out] byte[]? information, uint length, out uint returnedLength, nint process, uint flags);
}
