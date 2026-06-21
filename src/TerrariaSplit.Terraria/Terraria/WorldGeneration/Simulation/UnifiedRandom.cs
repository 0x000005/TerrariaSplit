namespace TerrariaSplit.Terraria.WorldGeneration.Simulation;

// Terraria.Utilities.UnifiedRandom-compatible implementation.
// Keeping this exact RNG shape is required for pass RandNext validation.
internal sealed class UnifiedRandom
{
    private const int MSeed = 161803398;

    private readonly int[] seedArray = new int[56];
    private uint inext;

    public UnifiedRandom(int seed)
    {
        SetSeed(seed);
    }

    public void SetSeed(int seed)
    {
        Array.Clear(seedArray);
        int num = seed == int.MinValue ? int.MaxValue : Math.Abs(seed);
        int num2 = MSeed - num;
        seedArray[55] = num2;
        int num3 = 1;
        for (int j = 1; j < 55; j++)
        {
            int num4 = 21 * j % 55;
            seedArray[num4] = num3;
            num3 = num2 - num3;
            if (num3 < 0)
            {
                num3 += int.MaxValue;
            }

            num2 = seedArray[num4];
        }

        for (int k = 1; k < 5; k++)
        {
            for (int l = 1; l < 56; l++)
            {
                seedArray[l] -= seedArray[1 + (l + 30) % 55];
                if (seedArray[l] < 0)
                {
                    seedArray[l] += int.MaxValue;
                }
            }
        }

        inext = 0u;
    }

    public int Next()
    {
        return InternalSample();
    }

    public int Next(int maxValue)
    {
        if (maxValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxValue), "maxValue must be positive.");
        }

        return (int)(Sample() * maxValue);
    }

    public int Next(int minValue, int maxValue)
    {
        if (minValue > maxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(minValue), "minValue must be less than maxValue");
        }

        long range = (long)maxValue - minValue;
        return range <= int.MaxValue
            ? (int)(Sample() * range) + minValue
            : (int)((long)(GetSampleForLargeRange() * range) + minValue);
    }

    public double NextDouble()
    {
        return Sample();
    }

    public float NextFloat()
    {
        return (float)Sample();
    }

    private double Sample()
    {
        return InternalSample() * 4.656612875245797E-10;
    }

    private int InternalSample()
    {
        uint num = inext + 1;
        if (num > 55)
        {
            num = 1u;
        }

        uint num2 = num + 21;
        if (num2 > 55)
        {
            num2 -= 55;
        }

        int num3 = seedArray[num] - seedArray[num2];
        if (num3 == int.MaxValue)
        {
            num3--;
        }

        num3 = seedArray[num] = num3 + ((num3 >> 31) & 0x7FFFFFFF);
        inext = num;
        return num3;
    }

    private double GetSampleForLargeRange()
    {
        int num = InternalSample();
        if (InternalSample() % 2 == 0)
        {
            num = -num;
        }

        return (num + 2147483646.0) / 4294967293.0;
    }
}
