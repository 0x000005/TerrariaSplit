using TerrariaSplit.UI.Rendering;

namespace TerrariaSplit.UI;

internal static class BossIconAssetLoader
{
    private static readonly string[] PackagedCategories = ["Bosses", "Items", "NPCs", "Biomes"];

    public static void LoadInitialAssets(
        BossIconAssetRegistry registry,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        IAppLogger logger)
    {
        StartupDiagnostics.RecordTrace("IconPreloadStarted");
        LoadDefinitions(registry, statuses.Select(status => status.Definition), logger);
        StartupDiagnostics.RecordTrace("IconPreloadCompleted");
    }

    public static void LoadDefinitions(
        BossIconAssetRegistry registry,
        IEnumerable<SplitDefinition> definitions,
        IAppLogger logger)
    {
        foreach (SplitDefinition definition in definitions)
        {
            for (int iconIndex = 0; iconIndex < definition.IconFileNames.Count; iconIndex++)
            {
                string fileName = definition.IconFileNames[iconIndex];
                string iconKey = iconIndex < definition.IconKeys.Count
                    ? definition.IconKeys[iconIndex]
                    : definition.Id;
                if (!TryResolveIconPath(fileName, iconKey, out string path, out string? packagedCategory))
                {
                    continue;
                }

                try
                {
                    byte[] data = File.ReadAllBytes(path);
                    registry.RegisterOverride(
                        definition,
                        iconIndex,
                        path,
                        data);
                    if (packagedCategory is not null)
                    {
                        registry.RegisterPackaged(
                            packagedCategory,
                            Path.GetFileName(path),
                            path,
                            data);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Failed to load overlay icon: {fileName}");
                }
            }
        }
    }

    private static bool TryResolveIconPath(
        string fileName,
        string iconKey,
        out string path,
        out string? packagedCategory)
    {
        path = string.Empty;
        packagedCategory = null;
        if (!string.IsNullOrWhiteSpace(fileName) && File.Exists(fileName))
        {
            path = Path.GetFullPath(fileName);
            return true;
        }

        if (SplitCatalog.TryGetReferenceIconFileName(iconKey, out string referenceFileName) &&
            TryResolvePackagedIconPath(referenceFileName, iconKey, out path, out packagedCategory))
        {
            return true;
        }

        return TryResolvePackagedIconPath(fileName, iconKey, out path, out packagedCategory);
    }

    private static bool TryResolvePackagedIconPath(
        string fileName,
        string iconKey,
        out string path,
        out string? category)
    {
        path = string.Empty;
        category = null;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        string preferredCategory = GetPreferredCategory(iconKey);
        foreach (string candidateCategory in EnumerateCategories(preferredCategory))
        {
            string candidate = Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                "Icons",
                candidateCategory,
                fileName);
            if (File.Exists(candidate))
            {
                path = candidate;
                category = candidateCategory;
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumerateCategories(string preferredCategory)
    {
        yield return preferredCategory;
        foreach (string category in PackagedCategories)
        {
            if (!string.Equals(category, preferredCategory, StringComparison.OrdinalIgnoreCase))
            {
                yield return category;
            }
        }
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
}
