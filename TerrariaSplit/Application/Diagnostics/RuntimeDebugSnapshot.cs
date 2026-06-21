namespace TerrariaSplit;

internal readonly record struct RuntimeDebugSnapshot(
    TerrariaWatchSnapshot WatchSnapshot,
    TerrariaWatcherDiagnostics WatcherDiagnostics,
    RuntimePerformanceDiagnostics Performance,
    SplitTimerPhase TimerPhase)
{
    public static RuntimeDebugSnapshot Empty => new(
        new TerrariaWatchSnapshot(
            false,
            null,
            false,
            null,
            TerrariaGameFacts.Unknown,
            TerrariaWorldGenerationState.Unknown,
            false,
            "waiting for Terraria.exe"),
        TerrariaWatcherDiagnosticsDefaults.Empty,
        RuntimePerformanceDiagnostics.Empty,
        SplitTimerPhase.NotStarted);
}
