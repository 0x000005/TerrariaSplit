namespace TerrariaSplit.Application.Ports;

public interface ITerrariaWorldWatcher : IDisposable
{
    TerrariaWatchSnapshot Poll();

    TerrariaWatcherDiagnostics GetDiagnostics();
}
