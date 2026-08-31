using TerrariaSplit.Terraria.WorldGeneration;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class PyramidSeedPreScreenEvaluator
{
    public static bool IsEnabledFor(AutoCreateWorldSettings settings)
    {
        return settings.EnableCheats && settings.EnablePyramidFilter &&
            AutoCreateWorldSize.Normalize(settings.WorldSize) == AutoCreateWorldSize.Small &&
            AutoCreateWorldEvil.Normalize(settings.WorldEvil) == AutoCreateWorldEvil.Crimson &&
            // Special and secret seeds may use the fast pyramid pre-screen; the
            // generated world-file check remains the authoritative second pass.
            string.IsNullOrWhiteSpace(settings.FixedSeed);
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
            : TerrariaWorldGenerationVersion.Modern1458;
    }

    public PyramidSeedPreScreenPrediction Evaluate(
        AutoCreateWorldSettings settings,
        string seedText,
        TerrariaWorldGenerationVersion worldGenerationVersion)
    {
        int difficultyCode = TerrariaWorldSeedOptions.CopiedDifficultyCode(settings.WorldDifficulty);
        int requiredItemMask = AutoCreatePyramidFilterItem.NormalizeMaskOrAll(settings.PyramidFilterItemMask);
        string requiredItems = PyramidFilterItemMatcher.FormatRequiredItems(requiredItemMask);
        int requiredCoinPileMinimum = AutoCreatePyramidCoinPileMinimum.Normalize(settings.PyramidFilterCoinPileMinimum);

        // Tunnel depth is authoritative only after Terraria has generated the .wld file.
        PyramidSeedPreScreenResult result = PyramidSeedPreScreen.EvaluateSmallCrimson(
            seedText,
            difficultyCode,
            requiredItemMask,
            worldGenerationVersion,
            requiredCoinPileMinimum);

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
            AcceptSeed: result.MatchesRequirements,
            RejectReason: RejectReasonFor(result, requiredCoinPileMinimum));
    }

    private static string RejectReasonFor(
        PyramidSeedPreScreenResult result,
        int requiredCoinPileMinimum)
    {
        if (!result.HasTargetPyramid)
        {
            return "no pyramid";
        }

        if (result.MatchesRequirements)
        {
            return string.Empty;
        }

        bool filtersCoinPiles = requiredCoinPileMinimum > 0;
        return filtersCoinPiles ? "item or gold coin pile mismatch" : "item mismatch";
    }
}

internal readonly record struct PyramidSeedPreScreenPrediction(
    PyramidSeedPreScreenResult Result,
    string RequiredItems,
    bool CanUsePrediction,
    bool AcceptSeed,
    string RejectReason);
