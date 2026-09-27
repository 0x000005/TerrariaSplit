using System.Drawing;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class CreateWorldWorkflow : IDisposable
{
    private readonly TerrariaSavePreparation savePreparation = new();
    private readonly TerrariaAutomationContext automation = new("Create world");
    private readonly WindowActivationService windowActivation;
    private readonly ZenithStarCatchAutomation zenithStarCatchAutomation;
    private readonly PyramidSeedPreScreenAutomation pyramidSeedPreScreenAutomation;
    private readonly WorldPoolInstallWorkflow worldPoolInstallWorkflow;
    private readonly WorldCreationMenuDriver menuDriver;

    public CreateWorldWorkflow(IWorldPoolStore? worldPool = null)
    {
        windowActivation = new WindowActivationService(automation, "Create world");
        zenithStarCatchAutomation = new ZenithStarCatchAutomation(automation);
        pyramidSeedPreScreenAutomation = new PyramidSeedPreScreenAutomation(automation);
        worldPoolInstallWorkflow = new WorldPoolInstallWorkflow(worldPool);
        menuDriver = new WorldCreationMenuDriver(
            savePreparation,
            automation,
            windowActivation,
            pyramidSeedPreScreenAutomation);
    }

    public Task<AutomationResult> RunAsync(CancellationToken cancellationToken = default)
    {
        return RunAsync(AppSettingsDefaults.Create(), cancellationToken);
    }

    public async Task<AutomationResult> RunAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        automation.BeginRun();
        menuDriver.ClearFailure();
        try
        {
            AutoCreateWorldSettings autoCreate = settings.Automation.AutoCreate;
            ApplyTiming(autoCreate);
            CreateWorldActivationStep activation = await ActivateTerrariaAsync(cancellationToken);
            if (!activation.Succeeded)
            {
                return AutomationResult.Failure(
                    "Could not activate the Terraria window.",
                    "Create world automation could not activate Terraria window.");
            }

            TerrariaMenuProfile menuProfile = TerrariaMenuProfile.ResolveRunningProcess();
            TerrariaMenuGeometry geometry = TerrariaMenuGeometry.From(activation.ClientSize, menuProfile);
            FileAppLogger.Instance.Info($"Create world automation using menu profile: {menuProfile.Name}.");

            // Cleanup must precede pooled-world installation and player creation:
            // both produce non-favorite saves that a later cleanup would remove.
            CreateWorldCleanupStep cleanupStep = await RunSaveCleanupAsync(autoCreate, cancellationToken);
            if (!cleanupStep.Succeeded)
            {
                return AutomationResult.Failure(
                    "Could not prepare Terraria save files.",
                    "Create world automation save cleanup step failed.");
            }

            string worldGenSignature = WorldPoolRuntimeVersion.SignatureFromCurrentRuntime(settings);
            WorldPoolInstallResult poolInstall = await InstallPooledWorldAsync(autoCreate, worldGenSignature, cancellationToken);
            if (!poolInstall.Succeeded)
            {
                return menuDriver.BuildFailure(poolInstall.UserMessage, poolInstall.DiagnosticMessage);
            }

            if (!await menuDriver.CreatePlayerAndOpenWorldSelectAsync(autoCreate, geometry, cleanupStep.Cleanup, cancellationToken))
            {
                return menuDriver.BuildFailure(
                    "Could not create or select the Terraria player.",
                    "Create world automation failed before world selection.");
            }

            if (poolInstall.InstalledWorld is WorldPoolItem installedWorld)
            {
                worldPoolInstallWorkflow.RemoveInstalled(worldGenSignature, installedWorld);
                FileAppLogger.Instance.Info(
                    $"Create world automation installed pooled world {installedWorld.WorldFileName}; " +
                    "stopped at world select.");
                return AutomationResult.Success(
                    $"Create world automation installed pooled world {installedWorld.WorldFileName}.");
            }

            return await CreateWorldAsync(autoCreate, geometry, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return AutomationResult.CancelledByUser("Create world automation was cancelled.");
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, "Create world automation failed.");
            return AutomationResult.Failure(
                "Create world automation failed.",
                "Create world automation threw an unhandled exception.",
                ex);
        }
    }

    private async Task<CreateWorldActivationStep> ActivateTerrariaAsync(CancellationToken cancellationToken)
    {
        WindowActivationResult activation = await windowActivation.ActivateAsync(cancellationToken);
        return new CreateWorldActivationStep(activation.Succeeded, activation.ClientSize);
    }

    private async Task<CreateWorldCleanupStep> RunSaveCleanupAsync(
        AutoCreateWorldSettings settings,
        CancellationToken cancellationToken)
    {
        TerrariaSaveCleanupResult cleanup = default;
        bool succeeded = await automation.RunStepAsync(
            settings.PreserveExistingSaves ? "save inventory snapshot" : "save cleanup",
            _ =>
            {
                cleanup = settings.PreserveExistingSaves
                    ? ReadPreservedSaveCleanupSnapshot()
                    : savePreparation.MoveNonFavoritesToBackup();
                return Task.FromResult(true);
            },
            cancellationToken);
        return new CreateWorldCleanupStep(succeeded, cleanup);
    }

    private async Task<WorldPoolInstallResult> InstallPooledWorldAsync(
        AutoCreateWorldSettings settings,
        string worldGenSignature,
        CancellationToken cancellationToken)
    {
        WorldPoolInstallResult installStep = WorldPoolInstallResult.NotInstalled();
        bool succeeded = await automation.RunStepAsync(
            "install pooled world",
            _ =>
            {
                installStep = worldPoolInstallWorkflow.TryInstall(settings, worldGenSignature);
                return Task.FromResult(installStep.Succeeded);
            },
            cancellationToken);
        return succeeded
            ? installStep
            : WorldPoolInstallResult.Failed(
                installStep.UserMessage,
                installStep.DiagnosticMessage);
    }

    private async Task<AutomationResult> CreateWorldAsync(
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        automation.ThrowIfCancellationRequested(cancellationToken);
        CreateWorldAttemptResult createResult = await menuDriver.CreateOneWorldAsync(settings, geometry, cancellationToken);
        if (createResult == CreateWorldAttemptResult.Failed)
        {
            return menuDriver.BuildFailure(
                "Could not create the Terraria world.",
                "Create world automation failed while configuring or creating the world.");
        }

        FileAppLogger.Instance.Info(
            $"Create world automation entered post-click stage; " +
            $"zenith={settings.EnableZenithStarCatch}, cheats={settings.EnableCheats}.");
        await zenithStarCatchAutomation.RunAsync(settings, cancellationToken);
        return AutomationResult.Success("Create world automation completed.");
    }

    private void ApplyTiming(AutoCreateWorldSettings settings)
    {
        automation.ConfigureTiming(settings);
        menuDriver.ConfigureTiming(settings);
    }

    private TerrariaSaveCleanupResult ReadPreservedSaveCleanupSnapshot()
    {
        TerrariaSaveInventorySnapshot inventory = savePreparation.ReadInventorySnapshot();
        string saveRoot = TerrariaSavePaths.SaveRoot();
        FileAppLogger.Instance.Info(
            $"Create world automation preserved existing save files; " +
            $"players={inventory.PlayerFiles}, worlds={inventory.WorldFiles}, " +
            $"favoritePlayers={inventory.FavoritePlayers}, favoriteWorlds={inventory.FavoriteWorlds}.");
        return new TerrariaSaveCleanupResult(
            saveRoot,
            string.Empty,
            inventory.FavoritePlayers,
            inventory.FavoriteWorlds,
            MovedPlayers: 0,
            MovedWorlds: 0);
    }

    public void Dispose()
    {
        pyramidSeedPreScreenAutomation.Dispose();
    }
}

internal readonly record struct CreateWorldActivationStep(bool Succeeded, Size ClientSize);

internal readonly record struct CreateWorldCleanupStep(bool Succeeded, TerrariaSaveCleanupResult Cleanup);

internal enum CreateWorldAttemptResult
{
    Created,
    Failed
}

internal enum WorldSeedOptionsResult
{
    Applied,
    Failed
}
