using TerrariaSplit.Configuration;
using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.Race.Client;

public static class RaceWorldSettingsFactory
{
    public static RaceWorldSettings Create(AppSettings settings, string terrariaVersion)
    {
        AutoCreateWorldSettings autoCreate = settings.Automation.AutoCreate;
        int hasCrimsonEvilCode = TerrariaWorldSeedOptions.EvilCode(
            autoCreate.WorldEvil,
            () => TerrariaWorldSeedOptions.CrimsonEvilCode);
        return new RaceWorldSettings(
            terrariaVersion,
            TerrariaWorldSeedOptions.SizeCode(autoCreate.WorldSize),
            TerrariaWorldSeedOptions.CopiedDifficultyCode(autoCreate.WorldDifficulty),
            hasCrimsonEvilCode == TerrariaWorldSeedOptions.CrimsonEvilCode,
            TerrariaWorldSeedOptions.SpecialSeedMask(autoCreate.SpecialSeeds),
            AutoCreatePyramidFilterItem.NormalizeMaskOrAll(autoCreate.PyramidFilterItemMask),
            SecretSeeds: autoCreate.SecretSeeds?.Trim() ?? string.Empty);
    }

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
            EnablePyramidFilter = settings.RequiredPyramidItemMask != 0,
            PyramidFilterItemMask = AutoCreatePyramidFilterItem.NormalizeMaskOrAll(settings.RequiredPyramidItemMask),
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
