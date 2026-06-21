using System.Diagnostics.CodeAnalysis;

namespace TerrariaSplit.Terraria.Memory;

internal interface IProcessMemoryReader
{
    bool Is64Bit { get; }

    bool TryReadBool(IntPtr address, out bool value);

    bool TryReadInt32(IntPtr address, out int value);

    bool TryReadDouble(IntPtr address, out double value);

    bool TryReadPointer(IntPtr address, out IntPtr value);

    bool TryReadPointerValue(IntPtr address, out IntPtr value);

    bool TryReadBytes(IntPtr address, int count, [NotNullWhen(true)] out byte[]? bytes);

    IEnumerable<MemoryPage> ExecutablePages();

    IEnumerable<MemoryPage> ExecutablePrivatePages();
}
