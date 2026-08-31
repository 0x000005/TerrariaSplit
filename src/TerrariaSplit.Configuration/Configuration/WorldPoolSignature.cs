using System.Globalization;
using System.Text;

namespace TerrariaSplit.Configuration;

// A stable fingerprint of the world-generation inputs and filters that affect whether
// a pooled world file is valid for the current auto-create settings.
public static class WorldPoolSignature
{
    // Terraria's world generator is version-sensitive. Keep this as a visible pool
    // signature component instead of an opaque "v1" format marker.
    private const string DefaultTerrariaVersion = "1.4.5.8";

    public static string From(AppSettings settings)
    {
        return From(settings, DefaultTerrariaVersion);
    }

    public static string From(AppSettings settings, string? terrariaVersion)
    {
        return From(settings.Automation.AutoCreate, settings.General.Language, terrariaVersion);
    }

    public static string From(AutoCreateWorldSettings autoCreate)
    {
        return From(autoCreate, LanguageNames.English, DefaultTerrariaVersion);
    }

    public static string From(AutoCreateWorldSettings autoCreate, string? appLanguage)
    {
        return From(autoCreate, appLanguage, DefaultTerrariaVersion);
    }

    public static string From(AutoCreateWorldSettings autoCreate, string? appLanguage, string? terrariaVersion)
    {
        string size = AutoCreateWorldSize.Normalize(autoCreate.WorldSize);
        string difficulty = AutoCreateWorldDifficulty.Normalize(autoCreate.WorldDifficulty);
        string evil = AutoCreateWorldEvil.Normalize(autoCreate.WorldEvil);
        string specialSeeds = string.Join(",", AutoCreateSpecialWorldSeed.ParseList(autoCreate.SpecialSeeds));
        string secretSeeds = string.Join(",", AutoCreateSeedList.Parse(autoCreate.SecretSeeds));
        string fixedSeed = "fixedSeed=" + Convert.ToBase64String(
            Encoding.UTF8.GetBytes(autoCreate.FixedSeed?.Trim() ?? string.Empty));
        bool cheatsEnabled = autoCreate.EnableCheats;
        string cheats = cheatsEnabled ? "cheats=1" : "cheats=0";
        bool pyramidEnabled = cheatsEnabled && autoCreate.EnablePyramidFilter;
        string pyramid = pyramidEnabled ? "pyramid=1" : "pyramid=0";
        int pyramidItemMask = pyramidEnabled
            ? AutoCreatePyramidFilterItem.NormalizeMaskOrAll(autoCreate.PyramidFilterItemMask)
            : 0;
        string pyramidItems = "pyramidItems=" + pyramidItemMask.ToString(CultureInfo.InvariantCulture);
        string pyramidDepth = "pyramidMaxDepth=" + (pyramidEnabled
            ? AutoCreatePyramidFilterDepth.MaximumTunnelSurfaceDistance
            : 0).ToString(CultureInfo.InvariantCulture);
        int pyramidCoinPileMinimum = pyramidEnabled
            ? AutoCreatePyramidCoinPileMinimum.Normalize(autoCreate.PyramidFilterCoinPileMinimum)
            : 0;
        string pyramidCoinPiles = "pyramidGoldCoinPiles=" + pyramidCoinPileMinimum.ToString(CultureInfo.InvariantCulture);
        bool advancedFiltersEligible = AutoCreateAdvancedFilterEligibility.IsEligible(autoCreate);
        bool crimsonCorridorEnabled = cheatsEnabled && advancedFiltersEligible &&
            autoCreate.RequireCrimsonBetweenDungeonAndSpawn;
        string crimsonCorridor = crimsonCorridorEnabled
            ? "crimsonCorridor=" + AutoCreateCrimsonDistance.Normalize(autoCreate.CrimsonDistance)
            : "crimsonCorridor=0";
        bool resourceFilterEnabled = cheatsEnabled && advancedFiltersEligible &&
            AutoCreateResourceFilter.HasRequirements(autoCreate);
        string resourceFilter = resourceFilterEnabled
            ? string.Join(
                ",",
                "resource=1",
                "jungleDepth=" + AutoCreateJungleRouteDepth.Normalize(autoCreate.JungleRouteDepth),
                "items=" + AutoCreateResourceFilterItem.NormalizeMask(autoCreate.ResourceFilterItemMask).ToString(CultureInfo.InvariantCulture),
                "life=" + AutoCreateResourceMinimum.NormalizeLifeCrystals(autoCreate.ResourceFilterLifeCrystalMinimum).ToString(CultureInfo.InvariantCulture),
                "spelunker=" + AutoCreateResourceMinimum.NormalizePotions(autoCreate.ResourceFilterSpelunkerPotionMinimum).ToString(CultureInfo.InvariantCulture),
                "featherfall=" + AutoCreateResourceMinimum.NormalizePotions(autoCreate.ResourceFilterFeatherfallPotionMinimum).ToString(CultureInfo.InvariantCulture))
            : "resource=0";
        string nameLanguage = "name=" + TerrariaLanguageCodes.FromAppLanguage(appLanguage);
        return string.Join("|", NormalizeTerrariaVersion(terrariaVersion), size, difficulty, evil, specialSeeds, secretSeeds, fixedSeed, cheats, pyramid, pyramidItems, pyramidDepth, pyramidCoinPiles, crimsonCorridor, resourceFilter, nameLanguage);
    }

    public static string NormalizeTerrariaVersion(string? terrariaVersion)
    {
        if (string.IsNullOrWhiteSpace(terrariaVersion))
        {
            return DefaultTerrariaVersion;
        }

        string normalized = terrariaVersion.Trim();
        return normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase) && normalized.Length > 1
            ? normalized[1..]
            : normalized;
    }
}
