namespace TerrariaSplit;

internal readonly record struct TerrariaWatcherDiagnostics(
    string Stage,
    string SupportedVersion,
    string SignatureProfile,
    bool? IsProcess64Bit,
    string ProcessArchitecture,
    string? ProcessPath,
    string? ProcessVersion,
    IntPtr MainModuleBaseAddress,
    int? MainModuleSize,
    int SignatureScanAttempts,
    DateTime? LastSignatureScanUtc,
    SignatureScanDiagnostics? LastSignatureScan,
    IntPtr UpdateTimeAddress,
    IntPtr GameMenuAddress,
    IntPtr GameMenuSecondaryAddress,
    IntPtr BossFlagsBaseAddress,
    IntPtr HardmodeAddress,
    IntPtr CurrentGenerationProgressAddress,
    IntPtr CurrentControllerAddress,
    string CompatibilityHint);

internal static class TerrariaWatcherDiagnosticsDefaults
{
    public static TerrariaWatcherDiagnostics Empty => new(
        Stage: "waiting for process",
        SupportedVersion: string.Empty,
        SignatureProfile: string.Empty,
        IsProcess64Bit: null,
        ProcessArchitecture: string.Empty,
        ProcessPath: null,
        ProcessVersion: null,
        MainModuleBaseAddress: IntPtr.Zero,
        MainModuleSize: null,
        SignatureScanAttempts: 0,
        LastSignatureScanUtc: null,
        LastSignatureScan: null,
        UpdateTimeAddress: IntPtr.Zero,
        GameMenuAddress: IntPtr.Zero,
        GameMenuSecondaryAddress: IntPtr.Zero,
        BossFlagsBaseAddress: IntPtr.Zero,
        HardmodeAddress: IntPtr.Zero,
        CurrentGenerationProgressAddress: IntPtr.Zero,
        CurrentControllerAddress: IntPtr.Zero,
        CompatibilityHint: string.Empty);
}
