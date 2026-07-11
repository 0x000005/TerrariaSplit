using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Process = System.Diagnostics.Process;

namespace TerrariaSplit.Terraria.Memory;

internal sealed class ProcessMemoryReader : IProcessMemoryReader
{
    private static readonly UIntPtr MemoryBasicInformationSize =
        (UIntPtr)Marshal.SizeOf<MemoryBasicInformation>();

    private readonly SafeProcessHandle processHandle;

    public ProcessMemoryReader(Process process)
        : this(process, DetermineIs64Bit(process))
    {
    }

    internal ProcessMemoryReader(Process process, bool is64Bit)
    {
        processHandle = process.SafeHandle;
        Is64Bit = is64Bit;
    }

    public bool Is64Bit { get; }

    public bool TryReadBool(IntPtr address, out bool value)
    {
        value = false;
        if (!TryGetLiveProcessHandle(out SafeProcessHandle processHandle) ||
            !NativeMethods.ReadProcessMemory(
                processHandle,
                address,
                out byte rawValue,
                (UIntPtr)sizeof(byte),
                out UIntPtr bytesRead) ||
            bytesRead != (UIntPtr)sizeof(byte))
        {
            return false;
        }

        value = rawValue != 0;
        return true;
    }

    public bool TryReadInt32(IntPtr address, out int value)
    {
        value = 0;
        if (!TryGetLiveProcessHandle(out SafeProcessHandle processHandle) ||
            !NativeMethods.ReadProcessMemory(
                processHandle,
                address,
                out int rawValue,
                (UIntPtr)sizeof(int),
                out UIntPtr bytesRead) ||
            bytesRead != (UIntPtr)sizeof(int))
        {
            return false;
        }

        value = rawValue;
        return true;
    }

    public bool TryReadDouble(IntPtr address, out double value)
    {
        value = 0d;
        if (!TryGetLiveProcessHandle(out SafeProcessHandle processHandle) ||
            !NativeMethods.ReadProcessMemory(
                processHandle,
                address,
                out double rawValue,
                (UIntPtr)sizeof(double),
                out UIntPtr bytesRead) ||
            bytesRead != (UIntPtr)sizeof(double))
        {
            return false;
        }

        value = rawValue;
        return true;
    }

    public bool TryReadPointer(IntPtr address, out IntPtr value)
    {
        return TryReadPointerCore(address, out value) && value != IntPtr.Zero;
    }

    public bool TryReadPointerValue(IntPtr address, out IntPtr value)
    {
        return TryReadPointerCore(address, out value);
    }

    public bool TryReadBytes(IntPtr address, int count, [NotNullWhen(true)] out byte[]? bytes)
    {
        bytes = null;
        if (count <= 0 || !TryGetLiveProcessHandle(out SafeProcessHandle processHandle))
        {
            return false;
        }

        var buffer = new byte[count];
        try
        {
            if (!NativeMethods.ReadProcessMemory(
                    processHandle,
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

    private bool TryReadPointerCore(IntPtr address, out IntPtr value)
    {
        value = IntPtr.Zero;
        if (!TryGetLiveProcessHandle(out SafeProcessHandle processHandle))
        {
            return false;
        }

        if (Is64Bit)
        {
            if (!NativeMethods.ReadProcessMemory(
                    processHandle,
                    address,
                    out long rawValue,
                    (UIntPtr)sizeof(long),
                    out UIntPtr bytesRead) ||
                bytesRead != (UIntPtr)sizeof(long))
            {
                return false;
            }

            value = new IntPtr(rawValue);
            return true;
        }

        if (!NativeMethods.ReadProcessMemory(
                processHandle,
                address,
                out uint rawValue32,
                (UIntPtr)sizeof(uint),
                out UIntPtr bytesRead32) ||
            bytesRead32 != (UIntPtr)sizeof(uint))
        {
            return false;
        }

        value = new IntPtr(rawValue32);
        return true;
    }

    private bool TryGetLiveProcessHandle(out SafeProcessHandle handle)
    {
        handle = processHandle;
        return !handle.IsClosed && !handle.IsInvalid;
    }

    public IEnumerable<MemoryPage> ExecutablePages()
    {
        long address = 0x10000L;
        long maxAddress = Is64Bit ? 0x00007FFFFFFEFFFFL : 0x7FFEFFFFL;

        while (address < maxAddress && !processHandle.IsClosed && !processHandle.IsInvalid)
        {
            UIntPtr result;
            MemoryBasicInformation info;
            try
            {
                result = NativeMethods.VirtualQueryEx(
                    processHandle,
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

        if (!NativeMethods.IsWow64Process(process.SafeHandle, out bool isWow64))
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
