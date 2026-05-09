namespace TerrariaSplit;

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
    IntPtr MatchAddress)
{
    public bool MatchFound => MatchAddress != IntPtr.Zero;
}
