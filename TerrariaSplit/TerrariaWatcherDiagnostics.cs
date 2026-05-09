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
    string CompatibilityHint);
