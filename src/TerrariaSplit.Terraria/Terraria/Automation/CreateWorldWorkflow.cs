using System.Drawing;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class CreateWorldWorkflow : IDisposable
{
    private readonly TerrariaSavePreparation savePreparation = new();
    private readonly TerrariaAutomationContext automation = new("Create world");
    private readonly WindowActivationService windowActivation;
    private readonly ZenithStarCatchAutomation zenithStarCatchAutomation;
    private readonly PyramidFilterAutomation pyramidFilterAutomation;
    private readonly PyramidSeedPreScreenAutomation pyramidSeedPreScreenAutomation;
    private readonly WorldPoolInstallWorkflow worldPoolInstallWorkflow;
    private readonly WorldCreationMenuDriver menuDriver;

    public CreateWorldWorkflow(WorldPoolStore? worldPool = null)
    {
        windowActivation = new WindowActivationService(automation, "Create world");
        zenithStarCatchAutomation = new ZenithStarCatchAutomation(automation);
        pyramidFilterAutomation = new PyramidFilterAutomation(automation);
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
            while (true)
            {
                CreateWorldActivationStep activation = await ActivateTerrariaAsync(cancellationToken);
                if (!activation.Succeeded)
                {
                    return AutomationResult.Failure(
                        "Could not activate the Terraria window.",
                        "Create world automation could not activate Terraria window.");
                }

                TerrariaMenuGeometry geometry = TerrariaMenuGeometry.From(activation.ClientSize);

                CreateWorldCleanupStep cleanupStep = await RunSaveCleanupAsync(autoCreate, cancellationToken);
                if (!cleanupStep.Succeeded)
                {
                    return AutomationResult.Failure(
                        "Could not prepare Terraria save files.",
                        "Create world automation save cleanup step failed.");
                }

                string worldGenSignature = WorldPoolSignature.From(settings);
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

                if (poolInstall.InstalledWorld is not null)
                {
                    worldPoolInstallWorkflow.RemoveInstalled(worldGenSignature, poolInstall.InstalledWorld);
                    StaticAppLogger.Instance.Info(
                        $"Create world automation installed pooled world {poolInstall.InstalledWorld.WorldFileName}; " +
                        "stopped at world select.");
                    return AutomationResult.Success(
                        $"Create world automation installed pooled world {poolInstall.InstalledWorld.WorldFileName}.");
                }

                CreateWorldLoopResult loopResult = await RunWorldCreationLoopAsync(autoCreate, geometry, cancellationToken);
                if (!loopResult.ContinueFromMainMenu)
                {
                    return loopResult.Result;
                }
            }
        }
        catch (OperationCanceledException)
        {
            return AutomationResult.CancelledByUser("Create world automation was cancelled.");
        }
        catch (Exception ex)
        {
            StaticAppLogger.Instance.Error(ex, "Create world automation failed.");
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

    private async Task<CreateWorldLoopResult> RunWorldCreationLoopAsync(
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            automation.ThrowIfCancellationRequested(cancellationToken);
            Dictionary<string, DateTime> worldsBefore = savePreparation.SnapshotSaveFiles("Worlds", "*.wld");
            CreateWorldAttemptResult createResult = await menuDriver.CreateOneWorldAsync(settings, geometry, cancellationToken);
            if (createResult == CreateWorldAttemptResult.RetryFromMainMenu)
            {
                return await menuDriver.ReturnToMainMenuFromAdvancedSeedPageAsync(geometry, cancellationToken)
                    ? CreateWorldLoopResult.Continue()
                    : CreateWorldLoopResult.Failure(
                        "Could not return Terraria to the main menu after seed pre-screening.",
                        "Create world automation failed to return to the main menu after pyramid seed pre-screen rejection.");
            }

            if (createResult == CreateWorldAttemptResult.Failed)
            {
                return CreateWorldLoopResult.Failure(menuDriver.BuildFailure(
                    "Could not create the Terraria world.",
                    "Create world automation failed while configuring or creating the world."));
            }

            await zenithStarCatchAutomation.RunAsync(settings, cancellationToken);

            PyramidFilterOutcome outcome = await pyramidFilterAutomation.RunAsync(settings, worldsBefore, cancellationToken);
            StaticAppLogger.Instance.Info($"Create world automation pyramid filter outcome: {outcome}.");
            if (outcome != PyramidFilterOutcome.Rejected)
            {
                return CreateWorldLoopResult.Finished(
                    $"Create world automation stopped with pyramid filter outcome {outcome}.");
            }

            if (settings.ReturnToMainMenuOnFilterFailure)
            {
                return await menuDriver.ReturnToMainMenuByBackTwiceAsync(geometry, cancellationToken)
                    ? CreateWorldLoopResult.Continue()
                    : CreateWorldLoopResult.Failure(
                        "Could not return Terraria to the main menu after rejecting the world.",
                        "Create world automation failed to return to the main menu after pyramid filter rejection.");
            }

            if (!await menuDriver.PrepareRejectedWorldSelectRetryAsync(settings, cancellationToken))
            {
                return CreateWorldLoopResult.Failure(
                    "Could not prepare Terraria for another world creation attempt.",
                    "Create world automation failed to prepare world select before retrying.");
            }
        }
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
        StaticAppLogger.Instance.Info(
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
    }
}

internal readonly record struct CreateWorldActivationStep(bool Succeeded, Size ClientSize);

internal readonly record struct CreateWorldCleanupStep(bool Succeeded, TerrariaSaveCleanupResult Cleanup);

internal readonly record struct CreateWorldLoopResult(
    bool ContinueFromMainMenu,
    AutomationResult Result)
{
    public static CreateWorldLoopResult Continue()
    {
        return new CreateWorldLoopResult(true, AutomationResult.Success());
    }

    public static CreateWorldLoopResult Finished(string diagnostic)
    {
        return new CreateWorldLoopResult(false, AutomationResult.Success(diagnostic));
    }

    public static CreateWorldLoopResult Failure(string userMessage, string diagnostic)
    {
        return new CreateWorldLoopResult(false, AutomationResult.Failure(userMessage, diagnostic));
    }

    public static CreateWorldLoopResult Failure(AutomationResult result)
    {
        return new CreateWorldLoopResult(false, result);
    }
}

internal enum CreateWorldAttemptResult
{
    Created,
    RetryFromMainMenu,
    Failed
}

internal enum WorldSeedOptionsResult
{
    Applied,
    RetryFromMainMenu,
    Failed
}
