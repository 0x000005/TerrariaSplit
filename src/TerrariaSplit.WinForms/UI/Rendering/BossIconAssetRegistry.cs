using System.Collections.Concurrent;

namespace TerrariaSplit.UI.Rendering;

internal sealed class BossIconAssetRegistry
{
    private static readonly string[] PackagedCategories = ["Bosses", "Items", "NPCs", "Biomes"];

    private readonly ConcurrentDictionary<string, BossIconAsset> packagedAssets =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<BossIconRequestKey, BossIconAsset> requestOverrides =
        new(BossIconRequestKeyComparer.Instance);

    public static BossIconAssetRegistry Shared { get; } = new();

    public void RegisterPackaged(string category, string fileName, string sourceId, byte[] data)
    {
        if (string.IsNullOrWhiteSpace(category) ||
            string.IsNullOrWhiteSpace(fileName) ||
            data.Length == 0)
        {
            return;
        }

        packagedAssets[GetPackagedKey(category, fileName)] =
            new BossIconAsset($"icon:{sourceId}", data);
    }

    public void RegisterOverride(
        SplitDefinition definition,
        int iconIndex,
        string sourceId,
        byte[] data)
    {
        if (data.Length == 0)
        {
            return;
        }

        requestOverrides[BossIconRequestKey.From(definition, iconIndex)] =
            new BossIconAsset($"icon:{sourceId}", data);
    }

    public bool TryResolve(
        SplitDefinition definition,
        int iconIndex,
        out BossIconAsset asset)
    {
        BossIconRequestKey request = BossIconRequestKey.From(definition, iconIndex);
        if (requestOverrides.TryGetValue(request, out asset))
        {
            return true;
        }

        if (SplitCatalog.TryGetReferenceIconFileName(request.IconKey, out string referenceFileName) &&
            TryGetPackaged(referenceFileName, request.IconKey, out asset))
        {
            return true;
        }

        return TryGetPackaged(request.FileName, request.IconKey, out asset);
    }

    private bool TryGetPackaged(string fileName, string iconKey, out BossIconAsset asset)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            asset = default;
            return false;
        }

        string preferredCategory = GetPreferredCategory(iconKey);
        if (packagedAssets.TryGetValue(GetPackagedKey(preferredCategory, fileName), out asset))
        {
            return true;
        }

        foreach (string category in PackagedCategories)
        {
            if (!string.Equals(category, preferredCategory, StringComparison.OrdinalIgnoreCase) &&
                packagedAssets.TryGetValue(GetPackagedKey(category, fileName), out asset))
            {
                return true;
            }
        }

        asset = default;
        return false;
    }

    private static string GetPreferredCategory(string iconKey)
    {
        if (SplitCatalog.TryGetBossFact(iconKey, out _))
        {
            return "Bosses";
        }

        if (SplitCatalog.TryParseItemTargetId(iconKey, out _))
        {
            return "Items";
        }

        if (SplitCatalog.TryParseNpcTargetId(iconKey, out _))
        {
            return "NPCs";
        }

        if (SplitCatalog.TryParseBiomeTargetId(iconKey, out _))
        {
            return "Biomes";
        }

        return "Bosses";
    }

    private static string GetPackagedKey(string category, string fileName)
    {
        return $"{category}\u001f{fileName}";
    }
}

internal readonly record struct BossIconAsset(string CacheKey, byte[] Data);

internal readonly record struct BossIconRequestKey(string DefinitionId, string FileName, string IconKey)
{
    public static BossIconRequestKey From(SplitDefinition definition, int iconIndex)
    {
        if ((uint)iconIndex >= (uint)definition.IconFileNames.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(iconIndex));
        }

        string iconKey = iconIndex < definition.IconKeys.Count
            ? definition.IconKeys[iconIndex]
            : definition.Id;
        return new BossIconRequestKey(
            definition.Id,
            definition.IconFileNames[iconIndex],
            iconKey);
    }
}

internal sealed class BossIconRequestKeyComparer : IEqualityComparer<BossIconRequestKey>
{
    public static BossIconRequestKeyComparer Instance { get; } = new();

    public bool Equals(BossIconRequestKey x, BossIconRequestKey y)
    {
        return string.Equals(x.DefinitionId, y.DefinitionId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.FileName, y.FileName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.IconKey, y.IconKey, StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode(BossIconRequestKey obj)
    {
        return HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.DefinitionId ?? string.Empty),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.FileName ?? string.Empty),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.IconKey ?? string.Empty));
    }
}
