namespace WorldGenSim.Simulation;

internal readonly record struct WorldDimensions(int Width, int Height)
{
    public static WorldDimensions Small { get; } = new(4200, 1200);

    public static WorldDimensions FromSizeCode(int sizeCode)
    {
        return sizeCode switch
        {
            1 => Small,
            3 => new WorldDimensions(8400, 2400),
            _ => new WorldDimensions(6400, 1800)
        };
    }
}
