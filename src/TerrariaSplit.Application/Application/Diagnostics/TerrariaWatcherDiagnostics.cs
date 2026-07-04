namespace TerrariaSplit.Application.Diagnostics;

public readonly record struct TerrariaWatcherDiagnostics(
    string Stage,
    string LayoutStatus,
    bool? IsProcess64Bit,
    string ProcessArchitecture,
    string? ProcessPath,
    string? ProcessVersion,
    IntPtr MainModuleBaseAddress,
    int? MainModuleSize,
    TerrariaLayoutProbeDiagnostics LayoutProbe,
    IntPtr GameMenuAddress,
    IntPtr StatusTextAddress,
    IntPtr MenuUiAddress,
    int BossFactAddressCount,
    IntPtr HardmodeAddress,
    IntPtr CurrentGenerationProgressAddress,
    IntPtr CurrentControllerAddress,
    bool HasSeedUiLayout,
    TerrariaWorldCreationSeedSnapshot WorldCreationSeed,
    string CompatibilityHint);

public readonly record struct TerrariaLayoutProbeDiagnostics(
    int Attempts,
    DateTime? LastAttemptUtc,
    DateTime? LastSuccessUtc,
    string Status,
    int? LastExitCode,
    string? LastError,
    int ResolvedFieldCount)
{
    public static TerrariaLayoutProbeDiagnostics Empty => new(
        Attempts: 0,
        LastAttemptUtc: null,
        LastSuccessUtc: null,
        Status: "unavailable",
        LastExitCode: null,
        LastError: null,
        ResolvedFieldCount: 0);
}

public static class TerrariaWatcherDiagnosticsDefaults
{
    public static TerrariaWatcherDiagnostics Empty => new(
        Stage: "waiting for process",
        LayoutStatus: "unavailable",
        IsProcess64Bit: null,
        ProcessArchitecture: string.Empty,
        ProcessPath: null,
        ProcessVersion: null,
        MainModuleBaseAddress: IntPtr.Zero,
        MainModuleSize: null,
        LayoutProbe: TerrariaLayoutProbeDiagnostics.Empty,
        GameMenuAddress: IntPtr.Zero,
        StatusTextAddress: IntPtr.Zero,
        MenuUiAddress: IntPtr.Zero,
        BossFactAddressCount: 0,
        HardmodeAddress: IntPtr.Zero,
        CurrentGenerationProgressAddress: IntPtr.Zero,
        CurrentControllerAddress: IntPtr.Zero,
        HasSeedUiLayout: false,
        WorldCreationSeed: TerrariaWorldCreationSeedSnapshot.Unknown,
        CompatibilityHint: string.Empty);
}
