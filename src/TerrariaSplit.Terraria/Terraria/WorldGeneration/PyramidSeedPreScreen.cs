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
    bool MatchesRequiredItems,
    string TargetClass,
    string LootSummary,
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
        int requiredItemMask)
    {
        return Evaluate(seedText, sizeCode: 1, difficultyCode, hasCrimson: true, specialSeedMask: 0, requiredItemMask);
    }

    public static PyramidSeedPreScreenResult Evaluate(
        string seedText,
        int sizeCode,
        int difficultyCode,
        bool hasCrimson,
        int specialSeedMask,
        int requiredItemMask)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        string normalizedSeedText = seedText.Trim();

        if (!int.TryParse(normalizedSeedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            return CreateResult(
                PyramidSeedPreScreenStatus.InvalidSeed,
                normalizedSeedText,
                hasTargetPyramid: false,
                matchesRequiredItems: false,
                targetClass: string.Empty,
                lootSummary: string.Empty,
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
                matchesRequiredItems: false,
                targetClass: string.Empty,
                lootSummary: string.Empty,
                detail: "Only small crimson worlds without special seeds are supported.",
                stopwatch);
        }

        try
        {
            StageOneReplicaResult result = new StageOneReplicaSimulator().Generate(metadata);
            if (!result.IsComplete)
            {
                return CreateResult(
                    PyramidSeedPreScreenStatus.IncompleteSimulation,
                    normalizedSeedText,
                    hasTargetPyramid: false,
                    matchesRequiredItems: false,
                    targetClass: string.Empty,
                    lootSummary: string.Empty,
                    detail: string.IsNullOrWhiteSpace(result.Detail)
                        ? "Simulation stopped before pyramid scan was complete."
                        : result.Detail,
                    stopwatch);
            }

            PyramidChestSet chests = result.State.ScanTargetPyramidChests();
            bool hasTargetPyramid = chests.Chests.Count > 0;
            bool matchesRequiredItems = MatchesRequiredItems(chests, requiredItemMask);

            return CreateResult(
                PyramidSeedPreScreenStatus.Complete,
                normalizedSeedText,
                hasTargetPyramid,
                matchesRequiredItems,
                chests.FormatTargetClass(),
                chests.FormatLootSummary(),
                string.Empty,
                stopwatch);
        }
        catch (Exception ex)
        {
            return CreateResult(
                PyramidSeedPreScreenStatus.Error,
                normalizedSeedText,
                hasTargetPyramid: false,
                matchesRequiredItems: false,
                targetClass: string.Empty,
                lootSummary: string.Empty,
                detail: ex.Message,
                stopwatch);
        }
    }

    private static PyramidSeedPreScreenResult CreateResult(
        PyramidSeedPreScreenStatus status,
        string seedText,
        bool hasTargetPyramid,
        bool matchesRequiredItems,
        string targetClass,
        string lootSummary,
        string detail,
        Stopwatch stopwatch)
    {
        stopwatch.Stop();
        return new PyramidSeedPreScreenResult(
            status,
            seedText,
            hasTargetPyramid,
            matchesRequiredItems,
            targetClass,
            lootSummary,
            detail,
            stopwatch.ElapsedMilliseconds);
    }

    private static bool MatchesRequiredItems(PyramidChestSet chests, int requiredItemMask)
    {
        int normalizedMask = NormalizeMaskOrAll(requiredItemMask);

        if ((normalizedMask & SandstormInABottleMask) != 0 && ContainsItem(chests, PyramidChestItemNames.SandstormInABottle))
        {
            return true;
        }

        if ((normalizedMask & FlyingCarpetMask) != 0 && ContainsItem(chests, PyramidChestItemNames.FlyingCarpet))
        {
            return true;
        }

        if ((normalizedMask & PharaohSetMask) != 0 &&
            ContainsItem(chests, PyramidChestItemNames.PharaohsMask) &&
            ContainsItem(chests, PyramidChestItemNames.PharaohsRobe))
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

    private static bool ContainsItem(PyramidChestSet chests, int itemType)
    {
        foreach (PyramidChest chest in chests.Chests)
        {
            foreach (PyramidChestItem item in chest.Items)
            {
                if (item.Type == itemType)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
