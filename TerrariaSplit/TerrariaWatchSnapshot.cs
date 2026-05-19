namespace TerrariaSplit;

internal readonly record struct TerrariaWatchSnapshot(
    bool IsAttached,
    int? ProcessId,
    bool IsReady,
    bool? IsGameMenu,
    TerrariaBossStates BossStates,
    TerrariaWorldGenerationState WorldGeneration,
    bool EnteredWorld,
    string Status);
