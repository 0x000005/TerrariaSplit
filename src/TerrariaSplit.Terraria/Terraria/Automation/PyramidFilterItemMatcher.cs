namespace TerrariaSplit.Terraria.Automation;

internal static class PyramidFilterItemMatcher
{
    private static readonly Dictionary<string, int[]> ItemTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [AutoCreatePyramidFilterItem.SandstormInABottle] = [PyramidChestItemNames.SandstormInABottle],
        [AutoCreatePyramidFilterItem.FlyingCarpet] = [PyramidChestItemNames.FlyingCarpet],
        [AutoCreatePyramidFilterItem.PharaohSet] = [PyramidChestItemNames.PharaohMask, PyramidChestItemNames.PharaohRobe]
    };

    public static int ResolveRequiredMaskOrAll(int requiredItemMask)
    {
        return AutoCreatePyramidFilterItem.NormalizeMaskOrAll(requiredItemMask);
    }

    public static bool Matches(PyramidChestScanResult scanResult, AutoCreateWorldSettings settings)
    {
        return Matches(scanResult, settings.PyramidFilterItemMask);
    }

    public static bool Matches(PyramidChestScanResult scanResult, int requiredItemMask)
    {
        IReadOnlyList<string> requiredItems = AutoCreatePyramidFilterItem.FromMask(ResolveRequiredMaskOrAll(requiredItemMask));
        return requiredItems.Any(item => MatchesItem(scanResult, item));
    }

    public static string FormatRequiredItems(AutoCreateWorldSettings settings)
    {
        return FormatRequiredItems(settings.PyramidFilterItemMask);
    }

    public static string FormatRequiredItems(int requiredItemMask)
    {
        IReadOnlyList<string> requiredItems = AutoCreatePyramidFilterItem.FromMask(ResolveRequiredMaskOrAll(requiredItemMask));
        return string.Join(", ", requiredItems);
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
