namespace TerrariaSplit.Application.Ports;

internal interface ITerrariaWorldWatcher : IDisposable
{
    TerrariaWatchSnapshot Poll();

    TerrariaWatcherDiagnostics GetDiagnostics();
}
