namespace TerrariaSplit.Configuration;

public static class AutoCreateAdvancedFilterEligibility
{
    public static bool IsEligible(
        string? worldSize,
        string? worldEvil,
        string? specialSeeds,
        string? secretSeeds) =>
        AutoCreateWorldSize.All.Contains(AutoCreateWorldSize.Normalize(worldSize)) &&
        AutoCreateWorldEvil.All.Contains(AutoCreateWorldEvil.Normalize(worldEvil));

    public static bool IsCrimsonEligible(string? worldEvil) =>
        AutoCreateWorldEvil.Normalize(worldEvil) == AutoCreateWorldEvil.Crimson;

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
        worldSizeCode is >= 1 and <= 3;

}
