namespace TerrariaSplit.Configuration;

internal readonly record struct TerrariaWorldSeedMetadata(
    string SeedText,
    int SizeCode,
    int DifficultyCode,
    bool HasCrimson,
    int SpecialSeedMask)
{
    public bool MatchesWorldOptions(AutoCreateWorldSettings settings)
    {
        if (SizeCode != TerrariaWorldSeedOptions.SizeCode(settings.WorldSize) ||
            DifficultyCode != TerrariaWorldSeedOptions.CopiedDifficultyCode(settings.WorldDifficulty) ||
            SpecialSeedMask != TerrariaWorldSeedOptions.SpecialSeedMask(settings.SpecialSeeds))
        {
            return false;
        }

        return TerrariaWorldSeedOptions.EvilMatches(settings.WorldEvil, HasCrimson);
    }

    public string FormatWorldOptions()
    {
        return $"size={SizeCode}, difficulty={DifficultyCode}, evil={(HasCrimson ? 2 : 1)}, special={SpecialSeedMask}";
    }

    public static string FormatExpectedWorldOptions(AutoCreateWorldSettings settings)
    {
        return $"size={TerrariaWorldSeedOptions.SizeCode(settings.WorldSize)}, " +
            $"difficulty={TerrariaWorldSeedOptions.CopiedDifficultyCode(settings.WorldDifficulty)}, " +
            $"evil={TerrariaWorldSeedOptions.FormatExpectedEvil(settings.WorldEvil)}, " +
            $"special={TerrariaWorldSeedOptions.SpecialSeedMask(settings.SpecialSeeds)}";
    }
}
