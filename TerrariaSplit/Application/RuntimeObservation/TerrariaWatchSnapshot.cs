namespace TerrariaSplit.Application;

internal readonly record struct TerrariaWatchSnapshot(
    bool IsAttached,
    int? ProcessId,
    bool IsReady,
    bool? IsGameMenu,
    TerrariaGameFacts Facts,
    TerrariaWorldGenerationState WorldGeneration,
    bool EnteredWorld,
    string Status);
