using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace TerrariaSplit;

internal sealed class ProcessMemoryReader : IProcessMemoryReader
{
    private static readonly UIntPtr MemoryBasicInformationSize =
        (UIntPtr)Marshal.SizeOf<MemoryBasicInformation>();

    private readonly Process process;

    public ProcessMemoryReader(Process process)
    {
        this.process = process;
        Is64Bit = DetermineIs64Bit(process);
    }

    public bool Is64Bit { get; }

    public bool TryReadBool(IntPtr address, out bool value)
    {
        value = false;
        if (!TryReadBytes(address, 1, out byte[]? bytes))
        {
            return false;
        }

        value = bytes[0] != 0;
        return true;
    }

    public bool TryReadInt32(IntPtr address, out int value)
    {
        value = 0;
        if (!TryReadBytes(address, 4, out byte[]? bytes))
        {
            return false;
        }

        value = BitConverter.ToInt32(bytes, 0);
        return true;
    }

    public bool TryReadDouble(IntPtr address, out double value)
    {
        value = 0d;
        if (!TryReadBytes(address, 8, out byte[]? bytes))
        {
            return false;
        }

        value = BitConverter.ToDouble(bytes, 0);
        return true;
    }

    public bool TryReadPointer(IntPtr address, out IntPtr value)
    {
        value = IntPtr.Zero;

        int size = Is64Bit ? 8 : 4;
        if (!TryReadBytes(address, size, out byte[]? bytes))
        {
            return false;
        }

        value = Is64Bit
            ? new IntPtr(BitConverter.ToInt64(bytes, 0))
            : new IntPtr(BitConverter.ToUInt32(bytes, 0));
        return value != IntPtr.Zero;
    }

    public bool TryReadPointerValue(IntPtr address, out IntPtr value)
    {
        value = IntPtr.Zero;

        int size = Is64Bit ? 8 : 4;
        if (!TryReadBytes(address, size, out byte[]? bytes))
        {
            return false;
        }

        value = Is64Bit
            ? new IntPtr(BitConverter.ToInt64(bytes, 0))
            : new IntPtr(BitConverter.ToUInt32(bytes, 0));
        return true;
    }

    public bool TryReadBytes(IntPtr address, int count, [NotNullWhen(true)] out byte[]? bytes)
    {
        bytes = null;
        if (count <= 0 || process.HasExited)
        {
            return false;
        }

        var buffer = new byte[count];
        try
        {
            if (!NativeMethods.ReadProcessMemory(
                    process.Handle,
                    address,
                    buffer,
                    (UIntPtr)buffer.Length,
                    out UIntPtr bytesRead)
                || bytesRead != (UIntPtr)buffer.Length)
            {
                return false;
            }
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Win32Exception)
        {
            return false;
        }

        bytes = buffer;
        return true;
    }

    public IEnumerable<MemoryPage> ExecutablePages()
    {
        long address = 0x10000L;
        long maxAddress = Is64Bit ? 0x00007FFFFFFEFFFFL : 0x7FFEFFFFL;

        while (address < maxAddress && !process.HasExited)
        {
            UIntPtr result;
            MemoryBasicInformation info;
            try
            {
                result = NativeMethods.VirtualQueryEx(
                    process.Handle,
                    new IntPtr(address),
                    out info,
                    MemoryBasicInformationSize);
            }
            catch (InvalidOperationException)
            {
                yield break;
            }
            catch (Win32Exception)
            {
                yield break;
            }

            if (result == UIntPtr.Zero)
            {
                yield break;
            }

            long regionSize = unchecked((long)info.RegionSize.ToUInt64());
            if (regionSize <= 0)
            {
                yield break;
            }

            if (IsExecutableScannable(info))
            {
                yield return new MemoryPage(info.BaseAddress, regionSize, info.Protect, info.Type);
            }

            address = info.BaseAddress.ToInt64() + regionSize;
        }
    }

    public IEnumerable<MemoryPage> ExecutablePrivatePages()
    {
        foreach (MemoryPage page in ExecutablePages())
        {
            if (page.Type == MemoryPageType.Private)
            {
                yield return page;
            }
        }
    }

    private static bool DetermineIs64Bit(Process process)
    {
        if (!Environment.Is64BitOperatingSystem)
        {
            return false;
        }

        if (!NativeMethods.IsWow64Process(process.Handle, out bool isWow64))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return !isWow64;
    }

    private static bool IsExecutableScannable(MemoryBasicInformation info)
    {
        if (info.State != MemoryPageState.Commit)
        {
            return false;
        }

        if (info.Type != MemoryPageType.Private && info.Type != MemoryPageType.Image)
        {
            return false;
        }

        if ((info.Protect & MemoryPageProtect.PageGuard) != 0 ||
            (info.Protect & MemoryPageProtect.PageNoAccess) != 0)
        {
            return false;
        }

        const MemoryPageProtect executable =
            MemoryPageProtect.PageExecute |
            MemoryPageProtect.PageExecuteRead |
            MemoryPageProtect.PageExecuteReadWrite |
            MemoryPageProtect.PageExecuteWriteCopy;

        return (info.Protect & executable) != 0;
    }
}
