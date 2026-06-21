namespace TerrariaSplit.Terraria.WorldGeneration.Simulation;

internal struct FastRandomReplica
{
    private const ulong RandomMultiplier = 25214903917UL;
    private const ulong RandomAdd = 11UL;
    private const ulong RandomMask = 281474976710655UL;

    public FastRandomReplica(ulong seed)
    {
        Seed = seed;
    }

    public ulong Seed { get; private set; }

    public FastRandomReplica WithModifier(ulong modifier)
    {
        return new FastRandomReplica(NextSeed(modifier) ^ Seed);
    }

    public FastRandomReplica WithModifier(int x, int y)
    {
        return WithModifier((ulong)(x + 2654435769u + ((long)y << 6)) + ((ulong)y >> 2));
    }

    public double NextDouble()
    {
        return (float)NextBits(32) * 4.656613E-10f;
    }

    public int Next(int max)
    {
        if ((max & -max) == max)
        {
            return (int)((long)max * (long)NextBits(31) >> 31);
        }

        int value;
        int remainder;
        do
        {
            value = NextBits(31);
            remainder = value % max;
        }
        while (value - remainder + (max - 1) < 0);

        return remainder;
    }

    private int NextBits(int bits)
    {
        Seed = NextSeed(Seed);
        return (int)(Seed >> 48 - bits);
    }

    private static ulong NextSeed(ulong seed)
    {
        return (seed * RandomMultiplier + RandomAdd) & RandomMask;
    }
}
