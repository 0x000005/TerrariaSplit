using System.Globalization;

namespace TerrariaSplit.Terraria.Automation;

internal readonly record struct TerrariaCopiedSeed(string Text, TerrariaWorldSeedMetadata Metadata);

internal static class TerrariaCopiedSeedBuilder
{
    public static TerrariaCopiedSeed Create(AutoCreateWorldSettings settings)
    {
        int visibleSeed = TerrariaSeedRandom.NextShared();
        int evilCode = TerrariaWorldSeedOptions.EvilCode(settings.WorldEvil, () => TerrariaSeedRandom.NextShared(2) + 1);
        return Create(settings, visibleSeed.ToString(CultureInfo.InvariantCulture), evilCode);
    }

    internal static TerrariaCopiedSeed Create(AutoCreateWorldSettings settings, string visibleSeed, int evilCode)
    {
        string seedText = BuildSeedText(settings.SecretSeeds, visibleSeed);
        int sizeCode = TerrariaWorldSeedOptions.SizeCode(settings.WorldSize);
        int difficultyCode = TerrariaWorldSeedOptions.CopiedDifficultyCode(settings.WorldDifficulty);
        int specialSeedMask = TerrariaWorldSeedOptions.SpecialSeedMask(settings.SpecialSeeds);
        string copiedSeed = string.Join(
            ".",
            sizeCode.ToString(CultureInfo.InvariantCulture),
            difficultyCode.ToString(CultureInfo.InvariantCulture),
            evilCode.ToString(CultureInfo.InvariantCulture),
            specialSeedMask.ToString(CultureInfo.InvariantCulture),
            seedText);

        return new TerrariaCopiedSeed(
            copiedSeed,
            new TerrariaWorldSeedMetadata(
                seedText,
                sizeCode,
                difficultyCode,
                evilCode == TerrariaWorldSeedOptions.CrimsonEvilCode,
                specialSeedMask));
    }

    private static string BuildSeedText(string? secretSeeds, string visibleSeed)
    {
        IReadOnlyList<string> secretSeedList = AutoCreateSeedList.Parse(secretSeeds);
        if (secretSeedList.Count == 0)
        {
            return visibleSeed;
        }

        return string.Join("|", secretSeedList.Concat([visibleSeed]));
    }
}

internal sealed class TerrariaSeedRandom
{
    private const int Big = int.MaxValue;
    private const int SeedOffset = 161803398;
    private static readonly object SharedSync = new();
    private static readonly TerrariaSeedRandom Shared = new(Environment.TickCount);

    private readonly int[] seedArray = new int[56];
    private uint inext;

    public TerrariaSeedRandom(int seed)
    {
        SetSeed(seed);
    }

    public static int NextShared()
    {
        lock (SharedSync)
        {
            return Shared.Next();
        }
    }

    public static int NextShared(int maxValue)
    {
        lock (SharedSync)
        {
            return Shared.Next(maxValue);
        }
    }

    public static T WithShared<T>(Func<TerrariaSeedRandom, T> action)
    {
        lock (SharedSync)
        {
            return action(Shared);
        }
    }

    public void SetSeed(int seed)
    {
        Array.Clear(seedArray);
        int normalizedSeed = seed == int.MinValue ? int.MaxValue : Math.Abs(seed);
        int subtraction = SeedOffset - normalizedSeed;
        seedArray[55] = subtraction;
        int nextValue = 1;
        for (int i = 1; i < 55; i++)
        {
            int index = 21 * i % 55;
            seedArray[index] = nextValue;
            nextValue = subtraction - nextValue;
            if (nextValue < 0)
            {
                nextValue += Big;
            }

            subtraction = seedArray[index];
        }

        for (int pass = 1; pass < 5; pass++)
        {
            for (int index = 1; index < 56; index++)
            {
                seedArray[index] -= seedArray[1 + (index + 30) % 55];
                if (seedArray[index] < 0)
                {
                    seedArray[index] += Big;
                }
            }
        }

        inext = 0;
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

    private double Sample()
    {
        return InternalSample() * 4.656612875245797E-10;
    }

    private int InternalSample()
    {
        uint nextIndex = inext + 1;
        if (nextIndex > 55)
        {
            nextIndex = 1;
        }

        uint secondIndex = nextIndex + 21;
        if (secondIndex > 55)
        {
            secondIndex -= 55;
        }

        int firstArrayIndex = (int)nextIndex;
        int secondArrayIndex = (int)secondIndex;
        int value = seedArray[firstArrayIndex] - seedArray[secondArrayIndex];
        if (value == Big)
        {
            value--;
        }

        value = seedArray[firstArrayIndex] = value + ((value >> 31) & 0x7FFFFFFF);
        inext = nextIndex;
        return value;
    }
}
