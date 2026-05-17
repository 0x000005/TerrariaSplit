using TerrariaSplit;

namespace TerrariaSplit.Tests;

internal static class TestSnapshots
{
    public static TerrariaWatchSnapshot Terraria(bool? isGameMenu)
    {
        return new TerrariaWatchSnapshot(
            IsAttached: true,
            ProcessId: 1,
            IsReady: true,
            IsGameMenu: isGameMenu,
            BossStates: TerrariaBossStates.Unknown,
            EnteredWorld: false,
            Status: "test");
    }
}
