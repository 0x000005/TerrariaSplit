namespace TerrariaSplit.Application;

internal interface ITerrariaWorldWatcher : IDisposable
{
    TerrariaWatchSnapshot Poll();

    TerrariaWatcherDiagnostics GetDiagnostics();
}
