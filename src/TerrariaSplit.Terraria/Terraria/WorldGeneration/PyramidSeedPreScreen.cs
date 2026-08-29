using System.Diagnostics;
using System.Globalization;
using TerrariaSplit.Terraria.WorldGeneration.Simulation;

namespace TerrariaSplit.Terraria.WorldGeneration;

internal enum PyramidSeedPreScreenStatus
{
    Complete,
    UnsupportedScope,
    InvalidSeed,
    IncompleteSimulation,
    Error,
}

internal readonly record struct PyramidSeedPreScreenResult(
    PyramidSeedPreScreenStatus Status,
    string SeedText,
    bool HasTargetPyramid,
    bool MatchesRequirements,
    string TargetClass,
    string LootSummary,
    PyramidFeatureSummary Features,
    string Detail,
    long DurationMilliseconds);

internal static class PyramidSeedPreScreen
{
    public const int SandstormInABottleMask = 1;
    public const int FlyingCarpetMask = 2;
    public const int PharaohSetMask = 4;
    public const int AllMask = SandstormInABottleMask | FlyingCarpetMask | PharaohSetMask;

    public static PyramidSeedPreScreenResult EvaluateSmallCrimson(
        string seedText,
        int difficultyCode,
        int requiredItemMask,
        TerrariaWorldGenerationVersion version = TerrariaWorldGenerationVersion.Modern1458,
        string pyramidDepth = AutoCreatePyramidDepth.None,
        int coinPileMinimum = 0)
    {
        return Evaluate(
            seedText,
            sizeCode: 1,
            difficultyCode,
            hasCrimson: true,
            specialSeedMask: 0,
            requiredItemMask,
            version,
            pyramidDepth,
            coinPileMinimum);
    }

    public static PyramidSeedPreScreenResult Evaluate(
        string seedText,
        int sizeCode,
        int difficultyCode,
        bool hasCrimson,
        int specialSeedMask,
        int requiredItemMask,
        TerrariaWorldGenerationVersion version = TerrariaWorldGenerationVersion.Modern1458,
        string pyramidDepth = AutoCreatePyramidDepth.None,
        int coinPileMinimum = 0)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        string normalizedSeedText = seedText.Trim();

        if (!int.TryParse(normalizedSeedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            return CreateResult(
                PyramidSeedPreScreenStatus.InvalidSeed,
                normalizedSeedText,
                hasTargetPyramid: false,
                matchesRequirements: false,
                targetClass: string.Empty,
                lootSummary: string.Empty,
                PyramidFeatureSummary.Empty,
                detail: "Seed is not a numeric Terraria world seed.",
                stopwatch);
        }

        WorldSeedMetadata metadata = new(normalizedSeedText, sizeCode, difficultyCode, hasCrimson, specialSeedMask);
        WorldOptions options = WorldOptions.FromMetadata(metadata);
        if (!options.IsTargetScope)
        {
            return CreateResult(
                PyramidSeedPreScreenStatus.UnsupportedScope,
                normalizedSeedText,
                hasTargetPyramid: false,
                matchesRequirements: false,
                targetClass: string.Empty,
                lootSummary: string.Empty,
                PyramidFeatureSummary.Empty,
                detail: "Only small crimson worlds without special seeds are supported.",
                stopwatch);
        }

        try
        {
            StageOneReplicaResult result = new StageOneReplicaSimulator().Generate(metadata, version);
            if (!result.IsComplete)
            {
                return CreateResult(
                    PyramidSeedPreScreenStatus.IncompleteSimulation,
                    normalizedSeedText,
                    hasTargetPyramid: false,
                    matchesRequirements: false,
                    targetClass: string.Empty,
                    lootSummary: string.Empty,
                    PyramidFeatureSummary.Empty,
                    detail: string.IsNullOrWhiteSpace(result.Detail)
                        ? "Simulation stopped before pyramid scan was complete."
                        : result.Detail,
                    stopwatch);
            }

            PyramidChestSet chests = result.State.ScanTargetPyramidChests();
            bool hasTargetPyramid = chests.Chests.Count > 0;
            PyramidChest? matchingChest = null;
            foreach (PyramidChest chest in chests.Chests)
            {
                if (MatchesRequirements(chest, requiredItemMask, pyramidDepth, coinPileMinimum))
                {
                    matchingChest = chest;
                    break;
                }
            }
            bool matchesRequirements = matchingChest.HasValue;
            PyramidChest? featureChest = matchingChest ?? (hasTargetPyramid ? chests.Chests[0] : null);

            return CreateResult(
                PyramidSeedPreScreenStatus.Complete,
                normalizedSeedText,
                hasTargetPyramid,
                matchesRequirements,
                chests.FormatTargetClass(),
                chests.FormatLootSummary(),
                featureChest.HasValue ? PyramidFeatureSummary.From(featureChest.Value) : PyramidFeatureSummary.Empty,
                string.Empty,
                stopwatch);
        }
        catch (Exception ex)
        {
            return CreateResult(
                PyramidSeedPreScreenStatus.Error,
                normalizedSeedText,
                hasTargetPyramid: false,
                matchesRequirements: false,
                targetClass: string.Empty,
                lootSummary: string.Empty,
                PyramidFeatureSummary.Empty,
                detail: ex.Message,
                stopwatch);
        }
    }

    private static PyramidSeedPreScreenResult CreateResult(
        PyramidSeedPreScreenStatus status,
        string seedText,
        bool hasTargetPyramid,
        bool matchesRequirements,
        string targetClass,
        string lootSummary,
        PyramidFeatureSummary features,
        string detail,
        Stopwatch stopwatch)
    {
        stopwatch.Stop();
        return new PyramidSeedPreScreenResult(
            status,
            seedText,
            hasTargetPyramid,
            matchesRequirements,
            targetClass,
            lootSummary,
            features,
            detail,
            stopwatch.ElapsedMilliseconds);
    }

    internal static bool MatchesRequirements(
        PyramidChest chest,
        int requiredItemMask,
        string pyramidDepth,
        int coinPileMinimum)
    {
        return MatchesRequiredItems(chest, requiredItemMask) &&
            AutoCreatePyramidDepth.Matches(chest.TunnelSurfaceDistance, pyramidDepth) &&
            AutoCreatePyramidCoinPileMinimum.Matches(chest.CoinPileCounts.Total, coinPileMinimum);
    }

    internal static bool MatchesRequiredItems(PyramidChest chest, int requiredItemMask)
    {
        int normalizedMask = NormalizeMaskOrAll(requiredItemMask);

        if ((normalizedMask & SandstormInABottleMask) != 0 && ContainsItem(chest, PyramidChestItemNames.SandstormInABottle))
        {
            return true;
        }

        if ((normalizedMask & FlyingCarpetMask) != 0 && ContainsItem(chest, PyramidChestItemNames.FlyingCarpet))
        {
            return true;
        }

        if ((normalizedMask & PharaohSetMask) != 0 &&
            ContainsItem(chest, PyramidChestItemNames.PharaohsMask) &&
            ContainsItem(chest, PyramidChestItemNames.PharaohsRobe))
        {
            return true;
        }

        return false;
    }

    private static int NormalizeMaskOrAll(int mask)
    {
        int normalized = mask & AllMask;
        return normalized == 0 ? AllMask : normalized;
    }

    private static bool ContainsItem(PyramidChest chest, int itemType)
    {
        foreach (PyramidChestItem item in chest.Items)
        {
            if (item.Type == itemType)
            {
                return true;
            }
        }

        return false;
    }
}
