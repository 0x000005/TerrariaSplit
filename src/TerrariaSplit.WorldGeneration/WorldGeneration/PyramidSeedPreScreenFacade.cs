using TerrariaSplit.Terraria.WorldGeneration;

namespace TerrariaSplit.WorldGeneration;

public enum PublicPyramidSeedPreScreenStatus
{
    Complete,
    UnsupportedScope,
    InvalidSeed,
    IncompleteSimulation,
    Error
}

public readonly record struct PublicPyramidSeedPreScreenResult(
    PublicPyramidSeedPreScreenStatus Status,
    string SeedText,
    bool HasTargetPyramid,
    bool MatchesRequiredItems,
    string TargetClass,
    string LootSummary,
    string Detail,
    long DurationMilliseconds);

public static class PyramidSeedPreScreenFacade
{
    public static PublicPyramidSeedPreScreenResult Evaluate(
        string seedText,
        int sizeCode,
        int difficultyCode,
        bool hasCrimson,
        int specialSeedMask,
        int requiredItemMask,
        string terrariaVersion)
    {
        PyramidSeedPreScreenResult result = PyramidSeedPreScreen.Evaluate(
            seedText,
            sizeCode,
            difficultyCode,
            hasCrimson,
            specialSeedMask,
            requiredItemMask,
            ResolveVersion(terrariaVersion));
        return new PublicPyramidSeedPreScreenResult(
            MapStatus(result.Status),
            result.SeedText,
            result.HasTargetPyramid,
            result.MatchesRequiredItems,
            result.TargetClass,
            result.LootSummary,
            result.Detail,
            result.DurationMilliseconds);
    }

    private static TerrariaWorldGenerationVersion ResolveVersion(string version)
    {
        return version.Contains("1.4.4.9", StringComparison.OrdinalIgnoreCase) ||
            version.Contains("1449", StringComparison.OrdinalIgnoreCase)
                ? TerrariaWorldGenerationVersion.Legacy1449
                : TerrariaWorldGenerationVersion.Modern1456;
    }

    private static PublicPyramidSeedPreScreenStatus MapStatus(PyramidSeedPreScreenStatus status)
    {
        return status switch
        {
            PyramidSeedPreScreenStatus.Complete => PublicPyramidSeedPreScreenStatus.Complete,
            PyramidSeedPreScreenStatus.UnsupportedScope => PublicPyramidSeedPreScreenStatus.UnsupportedScope,
            PyramidSeedPreScreenStatus.InvalidSeed => PublicPyramidSeedPreScreenStatus.InvalidSeed,
            PyramidSeedPreScreenStatus.IncompleteSimulation => PublicPyramidSeedPreScreenStatus.IncompleteSimulation,
            PyramidSeedPreScreenStatus.Error => PublicPyramidSeedPreScreenStatus.Error,
            _ => PublicPyramidSeedPreScreenStatus.Error
        };
    }
}
