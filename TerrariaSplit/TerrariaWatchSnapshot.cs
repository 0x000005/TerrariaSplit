namespace TerrariaSplit;

internal readonly record struct TerrariaWatchSnapshot(
    bool IsAttached,
    int? ProcessId,
    bool IsReady,
    bool? IsGameMenu,
    TerrariaBossStates BossStates,
    bool EnteredWorld,
    string Status);
