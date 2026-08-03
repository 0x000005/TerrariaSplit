namespace TerrariaSplit.UI;

internal enum RaceLocalPreparationStage
{
    None,
    DownloadWorld,
    ValidateWorld,
    AnalyzeWorld,
    WaitForGame,
    PrepareMemoryControl,
    CreateRacePlayer,
    AlmostReady,
    ConnectToServer,
    WaitForManualReady,
    Ready
}
