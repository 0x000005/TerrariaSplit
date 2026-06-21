namespace TerrariaSplit.Application.Diagnostics;

internal readonly record struct SignatureScanDiagnostics(
    string ScopeDescription,
    int PrivateExecutablePagesSeen,
    int PrivateExecutablePagesScanned,
    long PrivateExecutableBytesScanned,
    int ImageExecutablePagesSeen,
    int ImageExecutablePagesScanned,
    long ImageExecutableBytesScanned,
    int OversizedPagesSkipped,
    int ReadFailures,
    IntPtr MatchAddress,
    double ElapsedMilliseconds)
{
    public bool MatchFound => MatchAddress != IntPtr.Zero;

    public long TotalExecutableBytesScanned => PrivateExecutableBytesScanned + ImageExecutableBytesScanned;
}
