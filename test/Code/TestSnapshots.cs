using TerrariaSplit;

namespace TerrariaSplit.Tests;

internal static class TestSnapshots
{
    public static TerrariaWatchSnapshot Terraria(
        bool? isGameMenu,
        TerrariaGameFacts? bossStates = null,
        bool enteredWorld = false)
    {
        return new TerrariaWatchSnapshot(
            IsAttached: true,
            ProcessId: 1,
            IsReady: true,
            IsGameMenu: isGameMenu,
            Facts: bossStates ?? TerrariaGameFacts.Unknown,
            WorldGeneration: TerrariaWorldGenerationState.Unknown,
            EnteredWorld: enteredWorld,
            Status: "test");
    }
}
