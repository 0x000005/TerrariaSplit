namespace TerrariaSplit;

internal static class PyramidFilterItemMatcher
{
    private static readonly Dictionary<string, int[]> ItemTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [AutoCreatePyramidFilterItem.SandstormInABottle] = [PyramidChestItemNames.SandstormInABottle],
        [AutoCreatePyramidFilterItem.FlyingCarpet] = [PyramidChestItemNames.FlyingCarpet],
        [AutoCreatePyramidFilterItem.PharaohSet] = [PyramidChestItemNames.PharaohMask, PyramidChestItemNames.PharaohRobe]
    };

    public static bool HasItemRequirement(AutoCreateWorldSettings settings)
    {
        return AutoCreatePyramidFilterItem.NormalizeMask(settings.PyramidFilterItemMask) != 0;
    }

    public static bool Matches(PyramidChestScanResult scanResult, AutoCreateWorldSettings settings)
    {
        return Matches(scanResult, settings.PyramidFilterItemMask);
    }

    public static bool Matches(PyramidChestScanResult scanResult, int requiredItemMask)
    {
        IReadOnlyList<string> requiredItems = AutoCreatePyramidFilterItem.FromMask(requiredItemMask);
        if (requiredItems.Count == 0)
        {
            return true;
        }

        return requiredItems.Any(item => MatchesItem(scanResult, item));
    }

    public static string FormatRequiredItems(AutoCreateWorldSettings settings)
    {
        return FormatRequiredItems(settings.PyramidFilterItemMask);
    }

    public static string FormatRequiredItems(int requiredItemMask)
    {
        IReadOnlyList<string> requiredItems = AutoCreatePyramidFilterItem.FromMask(requiredItemMask);
        return requiredItems.Count == 0
            ? "any"
            : string.Join(", ", requiredItems);
    }

    private static bool MatchesItem(PyramidChestScanResult scanResult, string item)
    {
        if (!ItemTypes.TryGetValue(item, out int[]? itemTypes))
        {
            return false;
        }

        return string.Equals(item, AutoCreatePyramidFilterItem.PharaohSet, StringComparison.OrdinalIgnoreCase)
            ? itemTypes.All(scanResult.ContainsItem)
            : itemTypes.Any(scanResult.ContainsItem);
    }
}
