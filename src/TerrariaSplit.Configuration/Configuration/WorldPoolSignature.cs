using System.Globalization;

namespace TerrariaSplit.Configuration;

// A stable fingerprint of the world-generation inputs and filters that affect whether
// a pooled world file is valid for the current auto-create settings.
public static class WorldPoolSignature
{
    // Terraria's world generator is version-sensitive. Keep this as a visible pool
    // signature component instead of an opaque "v1" format marker.
    private const string DefaultTerrariaVersion = "1.4.5.6";

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
        string pyramid = autoCreate.EnablePyramidFilter ? "pyramid=1" : "pyramid=0";
        int pyramidItemMask = autoCreate.EnablePyramidFilter
            ? AutoCreatePyramidFilterItem.NormalizeMaskOrAll(autoCreate.PyramidFilterItemMask)
            : AutoCreatePyramidFilterItem.NormalizeMask(autoCreate.PyramidFilterItemMask);
        string pyramidItems = "pyramidItems=" + pyramidItemMask.ToString(CultureInfo.InvariantCulture);
        string nameLanguage = "name=" + TerrariaLanguageCodes.FromAppLanguage(appLanguage);
        return string.Join("|", NormalizeTerrariaVersion(terrariaVersion), size, difficulty, evil, specialSeeds, secretSeeds, pyramid, pyramidItems, nameLanguage);
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
