namespace TerrariaSplit.Terraria.Automation;

internal sealed class WorldPoolInstallWorkflow
{
    private readonly IWorldPoolStore? worldPool;
    private readonly TerrariaWorldFilePyramidScanner worldFileScanner;

    public WorldPoolInstallWorkflow(
        IWorldPoolStore? worldPool,
        TerrariaWorldFilePyramidScanner? worldFileScanner = null)
    {
        this.worldPool = worldPool;
        this.worldFileScanner = worldFileScanner ?? new TerrariaWorldFilePyramidScanner();
    }

    public WorldPoolInstallResult TryInstall(AutoCreateWorldSettings settings, string signature)
    {
        if (worldPool is null ||
            !settings.EnableWorldPool)
        {
            return WorldPoolInstallResult.NotInstalled();
        }

        while (worldPool.TryPeekFirst(signature, out WorldPoolItem item))
        {
            TerrariaWorldSeedMetadata storedMetadata = item.Metadata;
            if (!storedMetadata.MatchesWorldOptions(settings))
            {
                StaticAppLogger.Instance.Info(
                    $"World pool discarded world {item.WorldFileName}: stored metadata " +
                    $"({storedMetadata.FormatWorldOptions()}) does not match current settings " +
                    $"({TerrariaWorldSeedMetadata.FormatExpectedWorldOptions(settings)}).");
                worldPool.RemoveFirst(signature, item);
                continue;
            }

            if (!worldPool.TryGetWorldPath(item, out string pooledWorldPath))
            {
                StaticAppLogger.Instance.Info($"World pool discarded world {item.WorldFileName}: pooled world file is missing.");
                worldPool.RemoveFirst(signature, item);
                continue;
            }

            if (!worldFileScanner.TryReadWorldSeedMetadata(pooledWorldPath, out TerrariaWorldSeedMetadata actualMetadata, out string detail) ||
                !actualMetadata.Equals(storedMetadata) ||
                !actualMetadata.MatchesWorldOptions(settings))
            {
                StaticAppLogger.Instance.Info(
                    $"World pool discarded world {item.WorldFileName}: actual metadata " +
                    $"({(detail.Length > 0 ? detail : actualMetadata.FormatWorldOptions())}) does not match stored/current settings " +
                    $"({TerrariaWorldSeedMetadata.FormatExpectedWorldOptions(settings)}).");
                worldPool.RemoveFirst(signature, item);
                continue;
            }

            string worldsPath = Path.Combine(TerrariaSavePaths.SaveRoot(), "Worlds");
            if (worldPool.TryInstallWorld(item, worldsPath, out string installedPath, out string message))
            {
                StaticAppLogger.Instance.Info(
                    $"Create world automation installed pooled world {item.WorldFileName} " +
                    $"to '{Path.GetFileName(installedPath)}' ({actualMetadata.FormatWorldOptions()}).");
                return WorldPoolInstallResult.Installed(item);
            }

            StaticAppLogger.Instance.Info($"Create world automation could not install pooled world {item.WorldFileName}: {message}");
            return WorldPoolInstallResult.Failed(
                "Could not install a pooled Terraria world.",
                $"Create world automation could not install pooled world {item.WorldFileName}: {message}");
        }

        return WorldPoolInstallResult.NotInstalled();
    }

    public void RemoveInstalled(string signature, WorldPoolItem installedWorld)
    {
        worldPool?.RemoveFirst(signature, installedWorld);
    }
}

internal readonly record struct WorldPoolInstallResult(
    bool Succeeded,
    WorldPoolItem? InstalledWorld,
    string UserMessage,
    string DiagnosticMessage)
{
    public static WorldPoolInstallResult NotInstalled()
    {
        return new WorldPoolInstallResult(true, null, string.Empty, string.Empty);
    }

    public static WorldPoolInstallResult Installed(WorldPoolItem installedWorld)
    {
        return new WorldPoolInstallResult(true, installedWorld, string.Empty, string.Empty);
    }

    public static WorldPoolInstallResult Failed(string userMessage, string diagnosticMessage)
    {
        return new WorldPoolInstallResult(false, null, userMessage, diagnosticMessage);
    }
}
