using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class CreateWorldWorkflow : IDisposable
{
    private static readonly TimeSpan PlayerCreateTimeout = TimeSpan.FromSeconds(1.0);
    private static readonly TimeSpan SavePollInterval = TimeSpan.FromMilliseconds(50);

    private readonly TerrariaSavePreparation savePreparation = new();
    private readonly TerrariaAutomationContext automation = new("Create world");
    private readonly ZenithStarCatchAutomation zenithStarCatchAutomation;
    private readonly PyramidFilterAutomation pyramidFilterAutomation;
    private readonly PyramidSeedPreScreenAutomation pyramidSeedPreScreenAutomation;
    private readonly TerrariaWorldFilePyramidScanner worldFileScanner = new();
    private readonly WorldPoolStore? worldPool;
    private TimeSpan shortActionDelay = TimeSpan.FromMilliseconds(AppSettingsDefaults.AutoCreate.ShortActionDelayMilliseconds);
    private TimeSpan menuActionDelay = TimeSpan.FromMilliseconds(AppSettingsDefaults.AutoCreate.MenuActionDelayMilliseconds);
    private int pyramidFilterPostDelayMilliseconds = AppSettingsDefaults.AutoCreate.PyramidFilterPostDelayMilliseconds;

    public CreateWorldWorkflow(WorldPoolStore? worldPool = null)
    {
        this.worldPool = worldPool;
        zenithStarCatchAutomation = new ZenithStarCatchAutomation(automation);
        pyramidFilterAutomation = new PyramidFilterAutomation(automation);
        pyramidSeedPreScreenAutomation = new PyramidSeedPreScreenAutomation(automation);
    }

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        return RunAsync(AppSettingsDefaults.Create(), cancellationToken);
    }

    public async Task RunAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        automation.BeginRun();
        try
        {
            AutoCreateWorldSettings autoCreate = settings.AutoCreate;
            ApplyTiming(autoCreate);
            while (true)
            {
                Size clientSize = Size.Empty;
                if (!await automation.RunStepAsync(
                        "activate Terraria window",
                        _ =>
                        {
                            if (!automation.TryActivate(out Size activatedSize))
                            {
                                AppLogger.Info("Create world automation could not activate Terraria window.");
                                return Task.FromResult(false);
                            }

                            clientSize = activatedSize;
                            return Task.FromResult(true);
                        },
                        cancellationToken))
                {
                    return;
                }

                TerrariaMenuGeometry geometry = TerrariaMenuGeometry.From(clientSize);

                TerrariaSaveCleanupResult cleanup = default;
                if (!await automation.RunStepAsync(
                        "save cleanup",
                        _ =>
                        {
                            cleanup = savePreparation.MoveNonFavoritesToBackup();
                            return Task.FromResult(true);
                        },
                        cancellationToken))
                {
                    return;
                }

                string worldGenSignature = WorldPoolSignature.From(settings);
                WorldPoolEntry? installedPooledWorld = null;
                if (!await automation.RunStepAsync(
                        "install pooled world",
                        _ =>
                        {
                            installedPooledWorld = TryInstallPooledWorld(autoCreate, worldGenSignature);
                            return Task.FromResult(true);
                        },
                        cancellationToken))
                {
                    return;
                }

                if (!await automation.ClickAsync("Single Player", geometry.MainMenuSinglePlayer(), menuActionDelay, cancellationToken))
                {
                    return;
                }

                Dictionary<string, DateTime> playersBefore = savePreparation.SnapshotSaveFiles("Players", "*.plr");
                if (!await automation.ClickAsync("new player", geometry.SelectMenuNewButton(), menuActionDelay, cancellationToken))
                {
                    return;
                }

                if (!await ApplyPlayerTemplateAsync(autoCreate, geometry, cancellationToken))
                {
                    return;
                }

                if (!await ApplyPlayerDifficultyAsync(autoCreate.PlayerDifficulty, geometry, cancellationToken))
                {
                    return;
                }

                if (!await automation.ClickAsync("create player", geometry.CreatePlayerButton(), menuActionDelay, cancellationToken))
                {
                    return;
                }

                if (!await ConfirmPlayerNameAsync(autoCreate.PlayerName, geometry, cancellationToken))
                {
                    return;
                }

                if (!await WaitForNewOrChangedSaveFileAsync("player file", playersBefore, "Players", "*.plr", PlayerCreateTimeout, cancellationToken))
                {
                    return;
                }

                if (!await ClickPlayerAsync(geometry, cleanup.FavoritePlayers, cancellationToken))
                {
                    return;
                }

                await automation.DelayAsync(menuActionDelay, cancellationToken);

                if (installedPooledWorld is not null)
                {
                    worldPool?.RemoveFirst(worldGenSignature, installedPooledWorld);
                    AppLogger.Info(
                        $"Create world automation installed pooled world {installedPooledWorld.WorldFileName}; " +
                        "stopped at world select.");
                    return;
                }

                if (!await RunWorldCreationLoopAsync(autoCreate, geometry, cancellationToken))
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Create world automation failed.");
        }
    }

    private async Task<bool> RunWorldCreationLoopAsync(
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            automation.ThrowIfCancellationRequested(cancellationToken);
            Dictionary<string, DateTime> worldsBefore = savePreparation.SnapshotSaveFiles("Worlds", "*.wld");
            CreateWorldAttemptResult createResult = await CreateOneWorldAsync(settings, geometry, cancellationToken);
            if (createResult == CreateWorldAttemptResult.RetryFromMainMenu)
            {
                return await ReturnToMainMenuFromAdvancedSeedPageAsync(geometry, cancellationToken);
            }

            if (createResult == CreateWorldAttemptResult.Failed)
            {
                return false;
            }

            await zenithStarCatchAutomation.RunAsync(settings, cancellationToken);

            PyramidFilterOutcome outcome = await pyramidFilterAutomation.RunAsync(settings, worldsBefore, cancellationToken);
            AppLogger.Info($"Create world automation pyramid filter outcome: {outcome}.");
            if (outcome != PyramidFilterOutcome.Rejected)
            {
                return false;
            }

            if (settings.ReturnToMainMenuOnFilterFailure)
            {
                return await ReturnToMainMenuByBackTwiceAsync(geometry, cancellationToken);
            }

            if (!await PrepareRejectedWorldSelectRetryAsync(cancellationToken))
            {
                return false;
            }
        }
    }

    private WorldPoolEntry? TryInstallPooledWorld(AutoCreateWorldSettings settings, string signature)
    {
        if (worldPool is null ||
            !settings.EnableWorldPool)
        {
            return null;
        }

        while (worldPool.TryPeekFirst(signature, out WorldPoolEntry entry))
        {
            TerrariaWorldSeedMetadata storedMetadata = entry.ToMetadata();
            if (!storedMetadata.MatchesWorldOptions(settings))
            {
                AppLogger.Info(
                    $"World pool discarded world {entry.WorldFileName}: stored metadata " +
                    $"({storedMetadata.FormatWorldOptions()}) does not match current settings " +
                    $"({TerrariaWorldSeedMetadata.FormatExpectedWorldOptions(settings)}).");
                worldPool.RemoveFirst(signature, entry);
                continue;
            }

            if (!worldPool.TryGetWorldPath(entry, out string pooledWorldPath))
            {
                AppLogger.Info($"World pool discarded world {entry.WorldFileName}: pooled world file is missing.");
                worldPool.RemoveFirst(signature, entry);
                continue;
            }

            if (!worldFileScanner.TryReadWorldSeedMetadata(pooledWorldPath, out TerrariaWorldSeedMetadata actualMetadata, out string detail) ||
                !actualMetadata.Equals(storedMetadata) ||
                !actualMetadata.MatchesWorldOptions(settings))
            {
                AppLogger.Info(
                    $"World pool discarded world {entry.WorldFileName}: actual metadata " +
                    $"({(detail.Length > 0 ? detail : actualMetadata.FormatWorldOptions())}) does not match stored/current settings " +
                    $"({TerrariaWorldSeedMetadata.FormatExpectedWorldOptions(settings)}).");
                worldPool.RemoveFirst(signature, entry);
                continue;
            }

            string worldsPath = Path.Combine(TerrariaSavePaths.SaveRoot(), "Worlds");
            if (worldPool.TryInstallWorld(entry, worldsPath, out string installedPath, out string message))
            {
                AppLogger.Info(
                    $"Create world automation installed pooled world {entry.WorldFileName} " +
                    $"to '{Path.GetFileName(installedPath)}' ({actualMetadata.FormatWorldOptions()}).");
                return entry;
            }

            AppLogger.Info($"Create world automation could not install pooled world {entry.WorldFileName}: {message}");
            return null;
        }

        return null;
    }

    private async Task<CreateWorldAttemptResult> CreateOneWorldAsync(
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        if (!await automation.ClickAsync("new world", geometry.SelectMenuNewButton(), menuActionDelay, cancellationToken))
        {
            return CreateWorldAttemptResult.Failed;
        }

        if (!await ApplyWorldOptionsAsync(settings, geometry, cancellationToken))
        {
            return CreateWorldAttemptResult.Failed;
        }

        WorldSeedOptionsResult seedOptionsResult = await ApplyWorldSeedOptionsAsync(settings, geometry, cancellationToken);
        if (seedOptionsResult == WorldSeedOptionsResult.RetryFromMainMenu)
        {
            return CreateWorldAttemptResult.RetryFromMainMenu;
        }

        if (seedOptionsResult == WorldSeedOptionsResult.Failed)
        {
            return CreateWorldAttemptResult.Failed;
        }

        return await automation.ClickAsync("create world", geometry.CreateWorldButton(), shortActionDelay, cancellationToken)
            ? CreateWorldAttemptResult.Created
            : CreateWorldAttemptResult.Failed;
    }

    private async Task<bool> ApplyPlayerTemplateAsync(
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.PlayerTemplateCode))
        {
            return true;
        }

        if (!TrySetClipboardTextWithBackup(settings.PlayerTemplateCode, out string? previousText, out bool hadPreviousText))
        {
            return false;
        }

        try
        {
            return await automation.ClickAsync("character clothing tab", geometry.CharacterClothingCategoryButton(), shortActionDelay, cancellationToken) &&
                await automation.ClickAsync("paste player template", geometry.CharacterTemplatePasteButton(), menuActionDelay, cancellationToken);
        }
        finally
        {
            RestoreClipboardText(previousText, hadPreviousText);
        }
    }

    private async Task<bool> ApplyPlayerDifficultyAsync(
        string playerDifficulty,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        string difficulty = AutoCreatePlayerDifficulty.Normalize(playerDifficulty);
        if (difficulty == AutoCreatePlayerDifficulty.Softcore)
        {
            return true;
        }

        return await automation.ClickAsync("character info tab", geometry.CharacterInfoCategoryButton(), shortActionDelay, cancellationToken) &&
            await automation.ClickAsync($"player difficulty {difficulty}", geometry.PlayerDifficultyButton(difficulty), shortActionDelay, cancellationToken);
    }

    private async Task<bool> ApplyWorldOptionsAsync(
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        return await automation.ClickAsync($"world size {settings.WorldSize}", geometry.WorldSizeButton(settings.WorldSize), shortActionDelay, cancellationToken) &&
            await automation.ClickAsync($"world difficulty {settings.WorldDifficulty}", geometry.WorldDifficultyButton(settings.WorldDifficulty), shortActionDelay, cancellationToken) &&
            await automation.ClickAsync($"world evil {settings.WorldEvil}", geometry.WorldEvilButton(settings.WorldEvil), shortActionDelay, cancellationToken);
    }

    private async Task<WorldSeedOptionsResult> ApplyWorldSeedOptionsAsync(
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        if (!await automation.ClickAsync("advanced seed menu", geometry.WorldAdvancedSeedButton(), menuActionDelay, cancellationToken))
        {
            return WorldSeedOptionsResult.Failed;
        }

        if (!await ApplySpecialSeedsAsync(settings.SpecialSeeds, geometry, cancellationToken))
        {
            return WorldSeedOptionsResult.Failed;
        }

        if (!await ApplySecretSeedsAsync(settings.SecretSeeds, geometry, cancellationToken))
        {
            return WorldSeedOptionsResult.Failed;
        }

        PyramidSeedPreScreenAutomationResult preScreenResult = await pyramidSeedPreScreenAutomation.RandomizeUntilAcceptedAsync(
                settings,
                geometry,
                shortActionDelay,
                cancellationToken);
        if (preScreenResult.Status == PyramidSeedPreScreenAutomationStatus.RetryFromMainMenu)
        {
            AppLogger.Info($"Create world automation will return to main menu after pyramid seed pre-screen rejection: {preScreenResult.Detail}");
            return WorldSeedOptionsResult.RetryFromMainMenu;
        }

        if (!preScreenResult.CanCreateWorld)
        {
            return WorldSeedOptionsResult.Failed;
        }

        if (preScreenResult.Status == PyramidSeedPreScreenAutomationStatus.ContinueWithoutPreScreen)
        {
            AppLogger.Info($"Create world automation will continue without pyramid seed pre-screen result: {preScreenResult.Detail}");
        }

        return await automation.ClickAsync("apply visible seed", geometry.WorldAdvancedApplyButton(), menuActionDelay, cancellationToken)
            ? WorldSeedOptionsResult.Applied
            : WorldSeedOptionsResult.Failed;
    }

    private async Task<bool> ApplySpecialSeedsAsync(
        string? specialSeeds,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        foreach (string rawSeed in AutoCreateSeedList.Parse(specialSeeds))
        {
            if (!AutoCreateSpecialWorldSeed.TryNormalize(rawSeed, out string specialSeed))
            {
                AppLogger.Info($"Create world automation found an unknown special seed: {rawSeed}");
                return false;
            }
        }

        foreach (string specialSeed in AutoCreateSpecialWorldSeed.ParseList(specialSeeds))
        {
            if (!await automation.ClickAsync($"special seed {specialSeed}", geometry.AdvancedSpecialSeedButton(specialSeed), shortActionDelay, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> ApplySecretSeedsAsync(
        string? secretSeeds,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        string seedText = secretSeeds?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(seedText))
        {
            return true;
        }

        return await EnterWorldSeedAsync(seedText, geometry, cancellationToken);
    }

    private async Task<bool> EnterWorldSeedAsync(
        string worldSeed,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        if (!TrySetClipboardTextWithBackup(worldSeed, out string? previousText, out bool hadPreviousText))
        {
            return false;
        }

        try
        {
            if (!await automation.ClickAsync("world seed field", geometry.AdvancedSeedTextButton(), menuActionDelay, cancellationToken))
            {
                return false;
            }

            automation.ThrowIfCancellationRequested(cancellationToken);
            automation.Window.PressModifiedKey(Keys.ControlKey, Keys.A);
            await automation.DelayAsync(shortActionDelay, cancellationToken);
            automation.ThrowIfCancellationRequested(cancellationToken);
            automation.Window.PressModifiedKey(Keys.ControlKey, Keys.V);
            await automation.DelayAsync(shortActionDelay, cancellationToken);
            return await automation.ClickAsync("submit world seed", geometry.VirtualKeyboardSubmitButton(), menuActionDelay, cancellationToken);
        }
        finally
        {
            RestoreClipboardText(previousText, hadPreviousText);
        }
    }

    private async Task<bool> ClickPlayerAsync(
        TerrariaMenuGeometry geometry,
        int favoritePlayers,
        CancellationToken cancellationToken)
    {
        Point point = geometry.PlayerPlayButton(favoritePlayers);
        return await automation.ClickOnceAsync("first non-favorite player play button", point, menuActionDelay, cancellationToken);
    }

    private async Task<bool> ReturnToMainMenuByBackTwiceAsync(
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        if (!automation.TryActivate(out _, pyramidFilterPostDelayMilliseconds))
        {
            AppLogger.Info("Create world automation could not reactivate Terraria before clicking back out of a rejected world.");
            return false;
        }

        if (!await automation.ClickOnceAsync(
                "back from world select after rejected world",
                geometry.SelectMenuBackButton(),
                menuActionDelay,
                cancellationToken))
        {
            return false;
        }

        if (!await automation.ClickOnceAsync(
                "back from character select after rejected world",
                geometry.SelectMenuBackButton(),
                menuActionDelay,
                cancellationToken))
        {
            return false;
        }

        return true;
    }

    private async Task<bool> ReturnToMainMenuFromAdvancedSeedPageAsync(
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        if (!automation.TryActivate(out _, pyramidFilterPostDelayMilliseconds))
        {
            AppLogger.Info("Create world automation could not reactivate Terraria before returning from rejected pre-screen seed.");
            return false;
        }

        if (!await automation.ClickOnceAsync(
                "apply visible seed after rejected pre-screen seed",
                geometry.WorldAdvancedApplyButton(),
                menuActionDelay,
                cancellationToken))
        {
            return false;
        }

        if (!await automation.ClickOnceAsync(
                "back from world creation after rejected pre-screen seed",
                geometry.CreateWorldBackButton(),
                menuActionDelay,
                cancellationToken))
        {
            return false;
        }

        return await ReturnToMainMenuByBackTwiceAsync(geometry, cancellationToken);
    }

    private async Task<bool> PrepareRejectedWorldSelectRetryAsync(CancellationToken cancellationToken)
    {
        if (!automation.TryActivate(out _, pyramidFilterPostDelayMilliseconds))
        {
            AppLogger.Info("Create world automation could not reactivate Terraria before retrying a rejected world.");
            return false;
        }

        TerrariaWorldCleanupResult cleanup = default;
        if (!await automation.RunStepAsync(
                "rejected world cleanup",
                _ =>
                {
                    cleanup = savePreparation.MoveNonFavoriteWorldsToBackup();
                    return Task.FromResult(true);
                },
                cancellationToken))
        {
            return false;
        }

        AppLogger.Info(
            $"Create world automation removed {cleanup.MovedWorlds} non-favorite world(s) " +
            $"before retrying from world select; favoriteWorlds={cleanup.FavoriteWorlds}.");
        return true;
    }

    private void ApplyTiming(AutoCreateWorldSettings settings)
    {
        shortActionDelay = TimeSpan.FromMilliseconds(settings.ShortActionDelayMilliseconds);
        menuActionDelay = TimeSpan.FromMilliseconds(settings.MenuActionDelayMilliseconds);
        pyramidFilterPostDelayMilliseconds = settings.PyramidFilterPostDelayMilliseconds;
        automation.ConfigureTiming(settings);
    }

    private async Task<bool> ConfirmPlayerNameAsync(
        string playerName,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        string normalizedName = string.IsNullOrWhiteSpace(playerName) ? "1" : playerName.Trim();

        if (!TrySetClipboardTextWithBackup(normalizedName, out string? previousText, out bool hadPreviousText))
        {
            return false;
        }

        try
        {
            automation.ThrowIfCancellationRequested(cancellationToken);
            automation.Window.PressModifiedKey(Keys.ControlKey, Keys.A);
            await automation.DelayAsync(shortActionDelay, cancellationToken);
            automation.ThrowIfCancellationRequested(cancellationToken);
            automation.Window.PressModifiedKey(Keys.ControlKey, Keys.V);
            await automation.DelayAsync(shortActionDelay, cancellationToken);
            return await automation.ClickAsync("submit player name", geometry.VirtualKeyboardSubmitButton(), menuActionDelay, cancellationToken);
        }
        finally
        {
            RestoreClipboardText(previousText, hadPreviousText);
        }
    }

    private static bool TrySetClipboardTextWithBackup(string text, out string? previousText, out bool hadPreviousText)
    {
        return TrySetClipboardText(text, out previousText, out hadPreviousText);
    }

    private static bool TrySetClipboardText(string text, out string? previousText, out bool hadPreviousText)
    {
        previousText = null;
        hadPreviousText = false;
        try
        {
            hadPreviousText = Clipboard.ContainsText();
            if (hadPreviousText)
            {
                previousText = Clipboard.GetText();
            }

            Clipboard.SetText(text);
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Create world automation failed to set clipboard text.");
            return false;
        }
    }

    private static void RestoreClipboardText(string? previousText, bool hadPreviousText)
    {
        try
        {
            if (hadPreviousText && previousText is not null)
            {
                Clipboard.SetText(previousText);
            }
            else
            {
                Clipboard.Clear();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Create world automation failed to restore clipboard text.");
        }
    }

    public void Dispose()
    {
    }

    private async Task<bool> WaitForNewOrChangedSaveFileAsync(
        string step,
        Dictionary<string, DateTime> before,
        string directoryName,
        string pattern,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        return await automation.RunStepAsync(
            $"wait for {step}",
            ct => WaitForNewOrChangedSaveFileOnceAsync(step, before, directoryName, pattern, timeout, ct),
            cancellationToken);
    }

    private async Task<bool> WaitForNewOrChangedSaveFileOnceAsync(
        string step,
        Dictionary<string, DateTime> before,
        string directoryName,
        string pattern,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow <= deadline)
        {
            automation.ThrowIfCancellationRequested(cancellationToken);
            Dictionary<string, DateTime> after = savePreparation.SnapshotSaveFiles(directoryName, pattern);
            if (after.Any(pair => !before.TryGetValue(pair.Key, out DateTime previousWriteTime) || pair.Value > previousWriteTime))
            {
                return true;
            }

            await automation.DelayAsync(SavePollInterval, cancellationToken);
        }

        AppLogger.Info($"Create world automation {step} was not created or updated.");
        return false;
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
