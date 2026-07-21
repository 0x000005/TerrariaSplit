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
            StringComparison.Ordinal) &&
        AutoCreateSpecialWorldSeed.ParseList(specialSeeds).Count == 0 &&
        AutoCreateSeedList.Parse(secretSeeds).Count == 0;

    public static bool IsEligible(AutoCreateWorldSettings settings) =>
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
        hasCrimson &&
        specialSeedMask == 0 &&
        AutoCreateSeedList.Parse(secretSeeds).Count == 0;

    public static void ClearUnsupportedFilters(AutoCreateWorldSettings settings)
    {
        if (IsEligible(settings))
        {
            return;
        }

        settings.RequireCrimsonBetweenDungeonAndSpawn = false;
        settings.JungleRouteDepth = AutoCreateJungleRouteDepth.None;
        settings.ResourceFilterItemMask = 0;
        settings.ResourceFilterLifeCrystalMinimum = 0;
        settings.ResourceFilterSpelunkerPotionMinimum = 0;
        settings.ResourceFilterFeatherfallPotionMinimum = 0;
    }

    public static void ClearUnsupportedFilters(RaceWorldSetupSettings settings)
    {
        if (IsEligible(settings))
        {
            return;
        }

        settings.CrimsonEnabled = false;
        settings.JungleRouteDepth = AutoCreateJungleRouteDepth.None;
        settings.ResourceItemMask = 0;
        settings.LifeCrystalMinimum = 0;
        settings.SpelunkerPotionMinimum = 0;
        settings.FeatherfallPotionMinimum = 0;
    }
}
