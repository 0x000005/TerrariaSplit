using TerrariaSplit.Terraria.WorldGeneration;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class PyramidSeedPreScreenEvaluator
{
    public static bool IsEnabledFor(AutoCreateWorldSettings settings)
    {
        return settings.EnableCheats && settings.EnablePyramidFilter &&
            AutoCreateWorldSize.Normalize(settings.WorldSize) == AutoCreateWorldSize.Small &&
            AutoCreateWorldEvil.Normalize(settings.WorldEvil) == AutoCreateWorldEvil.Crimson &&
            !AutoCreateSpecialWorldSeed.ParseList(settings.SpecialSeeds).Any() &&
            !AutoCreateSeedList.Parse(settings.SecretSeeds).Any();
    }

    public static bool IsSupportedTerrariaVersion(string? fileVersion)
    {
        _ = fileVersion;
        return true;
    }

    public static TerrariaWorldGenerationVersion WorldGenerationVersionFromTerrariaVersion(string? fileVersion)
    {
        return WorldGenerationVersionFromMenuProfile(TerrariaMenuProfile.FromVersion(fileVersion));
    }

    public static TerrariaWorldGenerationVersion WorldGenerationVersionFromMenuProfile(TerrariaMenuProfile profile)
    {
        return profile.Kind == TerrariaMenuProfileKind.Legacy1449
            ? TerrariaWorldGenerationVersion.Legacy1449
            : TerrariaWorldGenerationVersion.Modern1456;
    }

    public PyramidSeedPreScreenPrediction Evaluate(
        AutoCreateWorldSettings settings,
        string seedText,
        TerrariaWorldGenerationVersion worldGenerationVersion)
    {
        int difficultyCode = TerrariaWorldSeedOptions.CopiedDifficultyCode(settings.WorldDifficulty);
        int requiredItemMask = AutoCreatePyramidFilterItem.NormalizeMaskOrAll(settings.PyramidFilterItemMask);
        string requiredItems = PyramidFilterItemMatcher.FormatRequiredItems(requiredItemMask);

        PyramidSeedPreScreenResult result = PyramidSeedPreScreen.EvaluateSmallCrimson(
            seedText,
            difficultyCode,
            requiredItemMask,
            worldGenerationVersion);

        if (result.Status != PyramidSeedPreScreenStatus.Complete)
        {
            return new PyramidSeedPreScreenPrediction(
                result,
                requiredItems,
                CanUsePrediction: false,
                AcceptSeed: false,
                RejectReason: $"prediction status {result.Status}");
        }

        return new PyramidSeedPreScreenPrediction(
            result,
            requiredItems,
            CanUsePrediction: true,
            AcceptSeed: result.MatchesRequiredItems,
            RejectReason: RejectReasonFor(result));
    }

    private static string RejectReasonFor(PyramidSeedPreScreenResult result)
    {
        if (!result.HasTargetPyramid)
        {
            return "no pyramid";
        }

        return result.MatchesRequiredItems ? string.Empty : "item mismatch";
    }
}

internal readonly record struct PyramidSeedPreScreenPrediction(
    PyramidSeedPreScreenResult Result,
    string RequiredItems,
    bool CanUsePrediction,
    bool AcceptSeed,
    string RejectReason);
