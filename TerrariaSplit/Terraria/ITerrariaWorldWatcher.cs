namespace TerrariaSplit.Terraria;

internal interface ITerrariaWorldWatcher : IDisposable
{
    TerrariaWatchSnapshot Poll();

    TerrariaWatcherDiagnostics GetDiagnostics();
}
