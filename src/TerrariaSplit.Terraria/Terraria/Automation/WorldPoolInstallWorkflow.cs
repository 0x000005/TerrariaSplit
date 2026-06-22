namespace TerrariaSplit.Terraria.Automation;

internal sealed class WorldPoolInstallWorkflow
{
    private readonly WorldPoolStore? worldPool;
    private readonly TerrariaWorldFilePyramidScanner worldFileScanner;

    public WorldPoolInstallWorkflow(
        WorldPoolStore? worldPool,
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

        while (worldPool.TryPeekFirst(signature, out WorldPoolEntry entry))
        {
            TerrariaWorldSeedMetadata storedMetadata = entry.ToMetadata();
            if (!storedMetadata.MatchesWorldOptions(settings))
            {
                StaticAppLogger.Instance.Info(
                    $"World pool discarded world {entry.WorldFileName}: stored metadata " +
                    $"({storedMetadata.FormatWorldOptions()}) does not match current settings " +
                    $"({TerrariaWorldSeedMetadata.FormatExpectedWorldOptions(settings)}).");
                worldPool.RemoveFirst(signature, entry);
                continue;
            }

            if (!worldPool.TryGetWorldPath(entry, out string pooledWorldPath))
            {
                StaticAppLogger.Instance.Info($"World pool discarded world {entry.WorldFileName}: pooled world file is missing.");
                worldPool.RemoveFirst(signature, entry);
                continue;
            }

            if (!worldFileScanner.TryReadWorldSeedMetadata(pooledWorldPath, out TerrariaWorldSeedMetadata actualMetadata, out string detail) ||
                !actualMetadata.Equals(storedMetadata) ||
                !actualMetadata.MatchesWorldOptions(settings))
            {
                StaticAppLogger.Instance.Info(
                    $"World pool discarded world {entry.WorldFileName}: actual metadata " +
                    $"({(detail.Length > 0 ? detail : actualMetadata.FormatWorldOptions())}) does not match stored/current settings " +
                    $"({TerrariaWorldSeedMetadata.FormatExpectedWorldOptions(settings)}).");
                worldPool.RemoveFirst(signature, entry);
                continue;
            }

            string worldsPath = Path.Combine(TerrariaSavePaths.SaveRoot(), "Worlds");
            if (worldPool.TryInstallWorld(entry, worldsPath, out string installedPath, out string message))
            {
                StaticAppLogger.Instance.Info(
                    $"Create world automation installed pooled world {entry.WorldFileName} " +
                    $"to '{Path.GetFileName(installedPath)}' ({actualMetadata.FormatWorldOptions()}).");
                return WorldPoolInstallResult.Installed(entry);
            }

            StaticAppLogger.Instance.Info($"Create world automation could not install pooled world {entry.WorldFileName}: {message}");
            return WorldPoolInstallResult.Failed(
                "Could not install a pooled Terraria world.",
                $"Create world automation could not install pooled world {entry.WorldFileName}: {message}");
        }

        return WorldPoolInstallResult.NotInstalled();
    }

    public void RemoveInstalled(string signature, WorldPoolEntry installedWorld)
    {
        worldPool?.RemoveFirst(signature, installedWorld);
    }
}

internal readonly record struct WorldPoolInstallResult(
    bool Succeeded,
    WorldPoolEntry? InstalledWorld,
    string UserMessage,
    string DiagnosticMessage)
{
    public static WorldPoolInstallResult NotInstalled()
    {
        return new WorldPoolInstallResult(true, null, string.Empty, string.Empty);
    }

    public static WorldPoolInstallResult Installed(WorldPoolEntry installedWorld)
    {
        return new WorldPoolInstallResult(true, installedWorld, string.Empty, string.Empty);
    }

    public static WorldPoolInstallResult Failed(string userMessage, string diagnosticMessage)
    {
        return new WorldPoolInstallResult(false, null, userMessage, diagnosticMessage);
    }
}
