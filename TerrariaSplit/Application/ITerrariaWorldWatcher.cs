namespace TerrariaSplit;

internal interface ITerrariaWorldWatcher : IDisposable
{
    TerrariaWatchSnapshot Poll();
}
