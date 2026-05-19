using TerrariaSplit;

namespace TerrariaSplit.Tests;

internal static class TestSnapshots
{
    public static TerrariaWatchSnapshot Terraria(
        bool? isGameMenu,
        TerrariaBossStates? bossStates = null,
        bool enteredWorld = false)
    {
        return new TerrariaWatchSnapshot(
            IsAttached: true,
            ProcessId: 1,
            IsReady: true,
            IsGameMenu: isGameMenu,
            BossStates: bossStates ?? TerrariaBossStates.Unknown,
            WorldGeneration: TerrariaWorldGenerationState.Unknown,
            EnteredWorld: enteredWorld,
            Status: "test");
    }
}
