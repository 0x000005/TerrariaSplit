using System.Diagnostics;

namespace TerrariaSplit.Terraria.Memory;

internal static class SignatureScanner
{
    private const int MaxRegionReadSize = 64 * 1024 * 1024;

    public static IntPtr Scan(
        IProcessMemoryReader reader,
        SignaturePattern pattern,
        string scopeDescription,
        out SignatureScanDiagnostics diagnostics)
    {
        long startTimestamp = Stopwatch.GetTimestamp();
        int privateExecutablePagesSeen = 0;
        int privateExecutablePagesScanned = 0;
        long privateExecutableBytesScanned = 0;
        int imageExecutablePagesSeen = 0;
        int imageExecutablePagesScanned = 0;
        long imageExecutableBytesScanned = 0;
        int oversizedPagesSkipped = 0;
        int readFailures = 0;
        IntPtr matchAddress = IntPtr.Zero;

        foreach (MemoryPage page in reader.ExecutablePages())
        {
            if (page.Type != MemoryPageType.Private)
            {
                continue;
            }

            privateExecutablePagesSeen++;

            if (page.RegionSize <= 0 || page.RegionSize > MaxRegionReadSize)
            {
                oversizedPagesSkipped++;
                continue;
            }

            if (!reader.TryReadBytes(page.BaseAddress, checked((int)page.RegionSize), out byte[]? bytes))
            {
                readFailures++;
                continue;
            }

            privateExecutablePagesScanned++;
            privateExecutableBytesScanned += page.RegionSize;
            int offset = pattern.FindIn(bytes);
            if (offset >= 0)
            {
                matchAddress = IntPtr.Add(page.BaseAddress, offset);
                break;
            }
        }

        if (matchAddress == IntPtr.Zero)
        {
            foreach (MemoryPage page in reader.ExecutablePages())
            {
                if (page.Type != MemoryPageType.Image)
                {
                    continue;
                }

                imageExecutablePagesSeen++;

                if (page.RegionSize <= 0 || page.RegionSize > MaxRegionReadSize)
                {
                    oversizedPagesSkipped++;
                    continue;
                }

                if (!reader.TryReadBytes(page.BaseAddress, checked((int)page.RegionSize), out byte[]? bytes))
                {
                    readFailures++;
                    continue;
                }

                imageExecutablePagesScanned++;
                imageExecutableBytesScanned += page.RegionSize;
                int offset = pattern.FindIn(bytes);
                if (offset >= 0)
                {
                    matchAddress = IntPtr.Add(page.BaseAddress, offset);
                    break;
                }
            }
        }

        diagnostics = new SignatureScanDiagnostics(
            scopeDescription,
            privateExecutablePagesSeen,
            privateExecutablePagesScanned,
            privateExecutableBytesScanned,
            imageExecutablePagesSeen,
            imageExecutablePagesScanned,
            imageExecutableBytesScanned,
            oversizedPagesSkipped,
            readFailures,
            matchAddress,
            Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);

        return matchAddress;
    }
}
