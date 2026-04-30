namespace TerrariaSplit;

internal static class SignatureScanner
{
    private const int MaxRegionReadSize = 64 * 1024 * 1024;

    public static IntPtr Scan(ProcessMemoryReader reader, SignaturePattern pattern)
    {
        foreach (MemoryPage page in reader.ExecutablePrivatePages())
        {
            if (page.RegionSize <= 0 || page.RegionSize > MaxRegionReadSize)
            {
                continue;
            }

            if (!reader.TryReadBytes(page.BaseAddress, checked((int)page.RegionSize), out byte[]? bytes))
            {
                continue;
            }

            int offset = pattern.FindIn(bytes);
            if (offset >= 0)
            {
                return IntPtr.Add(page.BaseAddress, offset);
            }
        }

        return IntPtr.Zero;
    }
}
