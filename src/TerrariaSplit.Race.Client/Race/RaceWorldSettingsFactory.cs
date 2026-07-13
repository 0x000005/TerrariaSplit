using TerrariaSplit.Configuration;
using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.Race.Client;

public static class RaceWorldSettingsFactory
{
    public static bool HasActiveFilters(RaceWorldSettings settings)
    {
        RaceCheatSettings cheats = settings.Cheats;
        return cheats.Enabled &&
            (cheats.PyramidEnabled ||
             cheats.CrimsonEnabled ||
             AutoCreateResourceFilterItem.NormalizeMask(cheats.ResourceItemMask) != 0 ||
             AutoCreateResourceMinimum.NormalizeLifeCrystals(cheats.LifeCrystalMinimum) > 0 ||
             AutoCreateResourceHook.Normalize(cheats.HookMinimum) != AutoCreateResourceHook.None ||
             AutoCreateResourceMinimum.NormalizePotions(cheats.SpelunkerPotionMinimum) > 0 ||
             AutoCreateResourceMinimum.NormalizePotions(cheats.FeatherfallPotionMinimum) > 0);
    }

    public static bool IsPyramidFilterEnabled(RaceWorldSettings settings) =>
        settings.Cheats.Enabled && settings.Cheats.PyramidEnabled;

    public static AutoCreateWorldSettings ToAutoCreateWorldSettings(RaceWorldSettings settings)
    {
        return new AutoCreateWorldSettings
        {
            WorldSize = settings.SizeCode switch
            {
                1 => AutoCreateWorldSize.Small,
                3 => AutoCreateWorldSize.Large,
                _ => AutoCreateWorldSize.Medium
            },
            WorldDifficulty = settings.DifficultyCode switch
            {
                2 => AutoCreateWorldDifficulty.Expert,
                3 => AutoCreateWorldDifficulty.Master,
                4 => AutoCreateWorldDifficulty.Journey,
                _ => AutoCreateWorldDifficulty.Classic
            },
            WorldEvil = settings.HasCrimson ? AutoCreateWorldEvil.Crimson : AutoCreateWorldEvil.Corruption,
            SpecialSeeds = SpecialSeedsFromMask(settings.SpecialSeedMask),
            SecretSeeds = settings.SecretSeeds?.Trim() ?? string.Empty,
            EnableCheats = settings.Cheats.Enabled,
            EnablePyramidFilter = settings.Cheats.PyramidEnabled,
            PyramidFilterItemMask = AutoCreatePyramidFilterItem.NormalizeMask(settings.Cheats.PyramidItemMask),
            RequireCrimsonBetweenDungeonAndSpawn = settings.Cheats.CrimsonEnabled,
            CrimsonDistance = AutoCreateCrimsonDistance.Normalize(settings.Cheats.CrimsonDistance),
            ResourceFilterItemMask = AutoCreateResourceFilterItem.NormalizeMask(settings.Cheats.ResourceItemMask),
            ResourceFilterLifeCrystalMinimum = AutoCreateResourceMinimum.NormalizeLifeCrystals(settings.Cheats.LifeCrystalMinimum),
            ResourceFilterHookMinimum = AutoCreateResourceHook.Normalize(settings.Cheats.HookMinimum),
            ResourceFilterSpelunkerPotionMinimum = AutoCreateResourceMinimum.NormalizePotions(settings.Cheats.SpelunkerPotionMinimum),
            ResourceFilterFeatherfallPotionMinimum = AutoCreateResourceMinimum.NormalizePotions(settings.Cheats.FeatherfallPotionMinimum),
            PreserveExistingSaves = true
        };
    }

    private static string SpecialSeedsFromMask(int mask)
    {
        List<string> seeds = new();
        AddIfSet(mask, 1, AutoCreateSpecialWorldSeed.Drunk, seeds);
        AddIfSet(mask, 2, AutoCreateSpecialWorldSeed.NotTheBees, seeds);
        AddIfSet(mask, 4, AutoCreateSpecialWorldSeed.ForTheWorthy, seeds);
        AddIfSet(mask, 8, AutoCreateSpecialWorldSeed.Celebration, seeds);
        AddIfSet(mask, 16, AutoCreateSpecialWorldSeed.TheConstant, seeds);
        AddIfSet(mask, 32, AutoCreateSpecialWorldSeed.Remix, seeds);
        AddIfSet(mask, 64, AutoCreateSpecialWorldSeed.NoTraps, seeds);
        AddIfSet(mask, 128, AutoCreateSpecialWorldSeed.Zenith, seeds);
        AddIfSet(mask, 256, AutoCreateSpecialWorldSeed.Skyblock, seeds);
        return string.Join(", ", seeds.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static void AddIfSet(int mask, int bit, string seed, List<string> seeds)
    {
        if ((mask & bit) != 0)
        {
            seeds.Add(seed);
        }
    }
}
