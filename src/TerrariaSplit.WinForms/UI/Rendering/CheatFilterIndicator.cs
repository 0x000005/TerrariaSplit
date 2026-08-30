using System.Drawing;
using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.UI.Rendering;

internal enum CheatFilterIndicatorLevel
{
    None,
    Pyramid,
    Terrain,
    Resource
}

internal static class CheatFilterIndicator
{
    private static readonly Color PyramidColor = Color.FromArgb(255, 217, 166, 46);
    private static readonly Color TerrainColor = Color.FromArgb(255, 240, 138, 50);
    private static readonly Color ResourceColor = Color.FromArgb(255, 213, 72, 72);

    public static CheatFilterIndicatorLevel Resolve(AutoCreateWorldSettings settings)
    {
        if (!settings.EnableCheats)
        {
            return CheatFilterIndicatorLevel.None;
        }

        if (AutoCreateResourceFilterItem.NormalizeMask(settings.ResourceFilterItemMask) != 0 ||
            AutoCreateResourceMinimum.NormalizeLifeCrystals(settings.ResourceFilterLifeCrystalMinimum) > 0 ||
            AutoCreateResourceMinimum.NormalizePotions(settings.ResourceFilterSpelunkerPotionMinimum) > 0 ||
            AutoCreateResourceMinimum.NormalizePotions(settings.ResourceFilterFeatherfallPotionMinimum) > 0)
        {
            return CheatFilterIndicatorLevel.Resource;
        }

        if ((settings.EnablePyramidFilter &&
                AutoCreatePyramidCoinPileMinimum.Normalize(settings.PyramidFilterCoinPileMinimum) > 0) ||
            settings.RequireCrimsonBetweenDungeonAndSpawn ||
            AutoCreateJungleRouteDepth.Normalize(settings.JungleRouteDepth) != AutoCreateJungleRouteDepth.None)
        {
            return CheatFilterIndicatorLevel.Terrain;
        }

        return CheatFilterIndicatorLevel.Pyramid;
    }

    public static CheatFilterIndicatorLevel Resolve(RaceCheatSettings settings)
    {
        if (!settings.Enabled)
        {
            return CheatFilterIndicatorLevel.None;
        }

        if (AutoCreateResourceFilterItem.NormalizeMask(settings.ResourceItemMask) != 0 ||
            AutoCreateResourceMinimum.NormalizeLifeCrystals(settings.LifeCrystalMinimum) > 0 ||
            AutoCreateResourceMinimum.NormalizePotions(settings.SpelunkerPotionMinimum) > 0 ||
            AutoCreateResourceMinimum.NormalizePotions(settings.FeatherfallPotionMinimum) > 0)
        {
            return CheatFilterIndicatorLevel.Resource;
        }

        if ((settings.PyramidEnabled &&
                AutoCreatePyramidCoinPileMinimum.Normalize(settings.PyramidCoinPileMinimum) > 0) ||
            settings.CrimsonEnabled ||
            AutoCreateJungleRouteDepth.Normalize(settings.JungleRouteDepth) != AutoCreateJungleRouteDepth.None)
        {
            return CheatFilterIndicatorLevel.Terrain;
        }

        return CheatFilterIndicatorLevel.Pyramid;
    }

    public static CheatFilterIndicatorLevel Max(
        CheatFilterIndicatorLevel left,
        CheatFilterIndicatorLevel right) =>
        left >= right ? left : right;

    public static Color GetColor(CheatFilterIndicatorLevel level) => level switch
    {
        CheatFilterIndicatorLevel.Pyramid => PyramidColor,
        CheatFilterIndicatorLevel.Terrain => TerrainColor,
        CheatFilterIndicatorLevel.Resource => ResourceColor,
        _ => Color.Transparent
    };
}
