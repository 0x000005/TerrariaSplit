using System.Globalization;

namespace TerrariaSplit.UI.Settings;

internal static class SplitSettingsRouteIdFactory
{
    public static string CreateSplitId(SplitTargetDefinition target)
    {
        return target.Kind == SplitTargetKind.Item && SplitCatalog.TryParseItemTargetId(target.Id, out int itemId)
            ? $"split:item-{itemId.ToString(CultureInfo.InvariantCulture)}"
            : target.Kind == SplitTargetKind.Npc && SplitCatalog.TryParseNpcTargetId(target.Id, out int npcId)
                ? $"split:npc-{npcId.ToString(CultureInfo.InvariantCulture)}"
            : $"split:{target.Id.Replace(':', '-')}";
    }

    public static string CreateUniqueSplitId(string preferredId, HashSet<string> seenIds, int index)
    {
        string baseId = string.IsNullOrWhiteSpace(preferredId)
            ? $"split:custom-{index.ToString(CultureInfo.InvariantCulture)}"
            : preferredId.Trim();
        string id = baseId;
        int suffix = index;
        while (!seenIds.Add(id))
        {
            id = $"{baseId}-{suffix.ToString(CultureInfo.InvariantCulture)}";
            suffix++;
        }

        return id;
    }

    public static string CreateSplitId(SplitRouteEntry entry, int index)
    {
        foreach (string factKey in (entry.Condition ?? SplitCondition.All([])).GetFactKeys())
        {
            if (SplitCatalog.TryGetTargetByFactKey(factKey, out SplitTargetDefinition target))
            {
                return CreateSplitId(target);
            }
        }

        return $"split:custom-{index.ToString(CultureInfo.InvariantCulture)}";
    }
}
