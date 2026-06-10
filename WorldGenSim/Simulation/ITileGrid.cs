namespace WorldGenSim.Simulation;

internal interface ITileGrid
{
    int Width { get; }

    int Height { get; }

    ref TileData this[int x, int y] { get; }

    void Clear();
}
