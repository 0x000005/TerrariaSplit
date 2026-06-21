using System.Runtime.CompilerServices;

namespace TerrariaSplit.Terraria.WorldGeneration.Simulation;

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

            return ref tiles[(x * Height) + y];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TileData GetUnchecked(int x, int y)
    {
        return ref tiles[(x * Height) + y];
    }

    public Span<TileData> GetColumnUnchecked(int x)
    {
        return tiles.AsSpan(x * Height, Height);
    }

    public void Clear()
    {
        Array.Clear(tiles);
    }
}
