using System.Runtime.InteropServices;

namespace TerrariaSplit.Infrastructure.Windows;

[Flags]
public enum MemoryPageProtect : uint
{
    PageNoAccess = 0x01,
    PageReadOnly = 0x02,
    PageReadWrite = 0x04,
    PageWriteCopy = 0x08,
    PageExecute = 0x10,
    PageExecuteRead = 0x20,
    PageExecuteReadWrite = 0x40,
    PageExecuteWriteCopy = 0x80,
    PageGuard = 0x100,
    PageNoCache = 0x200,
    PageWriteCombine = 0x400
}

public enum MemoryPageState : uint
{
    Commit = 0x1000,
    Reserve = 0x2000,
    Free = 0x10000
}

public enum MemoryPageType : uint
{
    Private = 0x20000,
    Mapped = 0x40000,
    Image = 0x1000000
}

[StructLayout(LayoutKind.Sequential)]
public struct MemoryBasicInformation
{
    public IntPtr BaseAddress;
    public IntPtr AllocationBase;
    public MemoryPageProtect AllocationProtect;
    public UIntPtr RegionSize;
    public MemoryPageState State;
    public MemoryPageProtect Protect;
    public MemoryPageType Type;
}

public readonly record struct MemoryPage(
    IntPtr BaseAddress,
    long RegionSize,
    MemoryPageProtect Protect,
    MemoryPageType Type);
