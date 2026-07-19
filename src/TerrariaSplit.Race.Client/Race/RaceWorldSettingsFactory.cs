using TerrariaSplit.Configuration;
using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.Race.Client;

public static class RaceWorldSettingsFactory
{
    private const int JourneyWorldDifficultyCode = 4;

    public static bool HasCompatibleJourneyDifficulties(RaceWorldSettings settings)
    {
        bool journeyWorld = settings.DifficultyCode == JourneyWorldDifficultyCode;
        bool journeyPlayer = settings.PlayerDifficultyCode == RacePlayerDifficultyCodes.Journey;
        return journeyWorld == journeyPlayer;
    }

    public static bool HasActiveFilters(RaceWorldSettings settings)
    {
        RaceCheatSettings cheats = settings.EffectiveCheats;
        return cheats.Enabled &&
            (cheats.PyramidEnabled ||
             cheats.CrimsonEnabled ||
             AutoCreateJungleRouteDepth.Normalize(cheats.JungleRouteDepth) != AutoCreateJungleRouteDepth.None ||
             AutoCreateResourceFilterItem.NormalizeMask(cheats.ResourceItemMask) != 0 ||
             AutoCreateResourceMinimum.NormalizeLifeCrystals(cheats.LifeCrystalMinimum) > 0 ||
             AutoCreateResourceMinimum.NormalizePotions(cheats.SpelunkerPotionMinimum) > 0 ||
             AutoCreateResourceMinimum.NormalizePotions(cheats.FeatherfallPotionMinimum) > 0);
    }

    public static bool IsPyramidFilterEnabled(RaceWorldSettings settings)
    {
        RaceCheatSettings cheats = settings.EffectiveCheats;
        return cheats.Enabled && cheats.PyramidEnabled;
    }

    public static string ToPlayerDifficulty(int difficultyCode)
    {
        return RacePlayerDifficultyCodes.Normalize(difficultyCode) switch
        {
            RacePlayerDifficultyCodes.Mediumcore => AutoCreatePlayerDifficulty.Mediumcore,
            RacePlayerDifficultyCodes.Hardcore => AutoCreatePlayerDifficulty.Hardcore,
            RacePlayerDifficultyCodes.Journey => AutoCreatePlayerDifficulty.Journey,
            _ => AutoCreatePlayerDifficulty.Softcore
        };
    }

    public static AutoCreateWorldSettings ToAutoCreateWorldSettings(RaceWorldSettings settings)
    {
        RaceCheatSettings cheats = settings.EffectiveCheats;
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
            EnableCheats = cheats.Enabled,
            EnablePyramidFilter = cheats.PyramidEnabled,
            PyramidFilterItemMask = AutoCreatePyramidFilterItem.NormalizeMask(cheats.PyramidItemMask),
            RequireCrimsonBetweenDungeonAndSpawn = cheats.CrimsonEnabled,
            CrimsonDistance = AutoCreateCrimsonDistance.Normalize(cheats.CrimsonDistance),
            JungleRouteDepth = AutoCreateJungleRouteDepth.Normalize(cheats.JungleRouteDepth),
            ResourceFilterItemMask = AutoCreateResourceFilterItem.NormalizeMask(cheats.ResourceItemMask),
            ResourceFilterLifeCrystalMinimum = AutoCreateResourceMinimum.NormalizeLifeCrystals(cheats.LifeCrystalMinimum),
            ResourceFilterSpelunkerPotionMinimum = AutoCreateResourceMinimum.NormalizePotions(cheats.SpelunkerPotionMinimum),
            ResourceFilterFeatherfallPotionMinimum = AutoCreateResourceMinimum.NormalizePotions(cheats.FeatherfallPotionMinimum),
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
