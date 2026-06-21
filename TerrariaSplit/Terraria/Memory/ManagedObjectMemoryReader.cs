using System.Text;

namespace TerrariaSplit.Terraria.Memory;

internal static class ManagedObjectMemoryReader
{
    private const int X86StringLengthOffset = 4;
    private const int X86StringCharsOffset = 8;
    private const int MaxManagedStringLength = 512;

    public static bool TryReadManagedString(
        IProcessMemoryReader memory,
        IntPtr objectAddress,
        out string? value)
    {
        value = null;
        if (objectAddress == IntPtr.Zero || memory.Is64Bit)
        {
            return false;
        }

        if (!memory.TryReadInt32(IntPtr.Add(objectAddress, X86StringLengthOffset), out int length) ||
            length < 0 ||
            length > MaxManagedStringLength)
        {
            return false;
        }

        if (length == 0)
        {
            value = string.Empty;
            return true;
        }

        int byteCount = checked(length * 2);
        if (!memory.TryReadBytes(IntPtr.Add(objectAddress, X86StringCharsOffset), byteCount, out byte[]? bytes))
        {
            return false;
        }

        value = Encoding.Unicode.GetString(bytes);
        return true;
    }
}
