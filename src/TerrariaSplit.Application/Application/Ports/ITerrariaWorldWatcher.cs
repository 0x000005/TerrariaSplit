namespace TerrariaSplit.Application.Ports;

public interface ITerrariaWorldWatcher : IDisposable
{
    void SetObservedFactKeys(IReadOnlySet<string> factKeys)
    {
    }

    TerrariaWatchSnapshot Poll();

    TerrariaWatcherDiagnostics GetDiagnostics();
}
