namespace TerrariaSplit.Terraria.WorldGeneration.Simulation;

internal sealed class WorldGenContext
{
    public WorldGenContext(int seed)
    {
        Seed = seed;
        Random = new UnifiedRandom(seed);
    }

    public WorldGenContext(WorldGenState state)
        : this(state.Options.Seed)
    {
        State = state;
    }

    public int Seed { get; }

    public UnifiedRandom Random { get; private set; }

    public WorldGenState? State { get; }

    public void ResetPassRandom()
    {
        Random = new UnifiedRandom(Seed);
    }
}
