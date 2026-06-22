namespace TerrariaSplit.Configuration;

public static class TerrariaWorldSeedOptions
{
    public const int CorruptionEvilCode = 1;
    public const int CrimsonEvilCode = 2;

    private const int DrunkMask = 1;
    private const int NotTheBeesMask = 2;
    private const int ForTheWorthyMask = 4;
    private const int CelebrationMask = 8;
    private const int TheConstantMask = 16;
    private const int RemixMask = 32;
    private const int NoTrapsMask = 64;
    private const int ZenithMask = 128;
    private const int SkyblockMask = 256;
    private const int ZenithDependencyMask =
        DrunkMask |
        NotTheBeesMask |
        ForTheWorthyMask |
        CelebrationMask |
        TheConstantMask |
        RemixMask |
        NoTrapsMask;

    public static int SizeCode(string worldSize)
    {
        return AutoCreateWorldSize.Normalize(worldSize) switch
        {
            AutoCreateWorldSize.Small => 1,
            AutoCreateWorldSize.Large => 3,
            _ => 2
        };
    }

    public static int CopiedDifficultyCode(string worldDifficulty)
    {
        return AutoCreateWorldDifficulty.Normalize(worldDifficulty) switch
        {
            AutoCreateWorldDifficulty.Expert => 2,
            AutoCreateWorldDifficulty.Master => 3,
            AutoCreateWorldDifficulty.Journey => 4,
            _ => 1
        };
    }

    public static int ServerDifficultyCode(string worldDifficulty)
    {
        return CopiedDifficultyCode(worldDifficulty) - 1;
    }

    public static int EvilCode(string worldEvil, Func<int> nextRandomEvilCode)
    {
        string evil = AutoCreateWorldEvil.Normalize(worldEvil);
        return evil switch
        {
            AutoCreateWorldEvil.Corruption => CorruptionEvilCode,
            AutoCreateWorldEvil.Crimson => CrimsonEvilCode,
            _ => nextRandomEvilCode()
        };
    }

    public static bool EvilMatches(string worldEvil, bool hasCrimson)
    {
        string evil = AutoCreateWorldEvil.Normalize(worldEvil);
        return evil == AutoCreateWorldEvil.Random ||
            hasCrimson == string.Equals(evil, AutoCreateWorldEvil.Crimson, StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatExpectedEvil(string worldEvil)
    {
        string evil = AutoCreateWorldEvil.Normalize(worldEvil);
        return evil == AutoCreateWorldEvil.Random
            ? "1/2"
            : string.Equals(evil, AutoCreateWorldEvil.Crimson, StringComparison.OrdinalIgnoreCase) ? "2" : "1";
    }

    public static int SpecialSeedMask(string? specialSeeds)
    {
        int mask = 0;
        foreach (string seed in AutoCreateSpecialWorldSeed.ParseList(specialSeeds))
        {
            mask |= seed switch
            {
                AutoCreateSpecialWorldSeed.Drunk => DrunkMask,
                AutoCreateSpecialWorldSeed.NotTheBees => NotTheBeesMask,
                AutoCreateSpecialWorldSeed.ForTheWorthy => ForTheWorthyMask,
                AutoCreateSpecialWorldSeed.Celebration => CelebrationMask,
                AutoCreateSpecialWorldSeed.TheConstant => TheConstantMask,
                AutoCreateSpecialWorldSeed.Remix => RemixMask,
                AutoCreateSpecialWorldSeed.NoTraps => NoTrapsMask,
                AutoCreateSpecialWorldSeed.Zenith => ZenithDependencyMask | ZenithMask,
                AutoCreateSpecialWorldSeed.Skyblock => SkyblockMask,
                _ => 0
            };
        }

        return mask;
    }
}
