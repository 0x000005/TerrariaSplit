namespace TerrariaSplit.Configuration;

public static class AutoCreateAdvancedFilterEligibility
{
    public static bool IsEligible(
        string? worldSize,
        string? worldEvil,
        string? specialSeeds,
        string? secretSeeds) =>
        string.Equals(
            AutoCreateWorldSize.Normalize(worldSize),
            AutoCreateWorldSize.Small,
            StringComparison.Ordinal) &&
        string.Equals(
            AutoCreateWorldEvil.Normalize(worldEvil),
            AutoCreateWorldEvil.Crimson,
            StringComparison.Ordinal);

    public static bool IsEligible(AutoCreateWorldSettings settings) =>
        string.IsNullOrWhiteSpace(settings.FixedSeed) &&
        IsEligible(
            settings.WorldSize,
            settings.WorldEvil,
            settings.SpecialSeeds,
            settings.SecretSeeds);

    public static bool IsEligible(RaceWorldSetupSettings settings) =>
        IsEligible(
            settings.WorldSize,
            settings.WorldEvil,
            settings.SpecialSeeds,
            settings.SecretSeeds);

    public static bool IsEligible(
        int worldSizeCode,
        bool hasCrimson,
        int specialSeedMask,
        string? secretSeeds) =>
        worldSizeCode == 1 &&
        hasCrimson;

}
