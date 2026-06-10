using System.Runtime.CompilerServices;

namespace WorldGenSim.Simulation;

internal sealed class DenseTileGrid
{
    private readonly TileData[] tiles;

    public DenseTileGrid(WorldDimensions dimensions)
    {
        Width = dimensions.Width;
        Height = dimensions.Height;
        tiles = new TileData[Width * Height];
    }

    public int Width { get; }

    public int Height { get; }

    public ref TileData this[int x, int y]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            {
                throw new ArgumentOutOfRangeException($"Tile coordinate ({x}, {y}) outside {Width}x{Height} world.");
            }

            return ref tiles[(y * Width) + x];
        }
    }

    public void Clear()
    {
        Array.Clear(tiles);
    }
}
