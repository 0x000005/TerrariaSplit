using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class WorldCreationMenuDriver
{
    private static readonly TimeSpan PlayerCreateTimeout = TimeSpan.FromSeconds(1.0);
    private static readonly TimeSpan SavePollInterval = TimeSpan.FromMilliseconds(50);

    private readonly TerrariaSavePreparation savePreparation;
    private readonly TerrariaAutomationContext automation;
    private readonly WindowActivationService windowActivation;
    private readonly PyramidSeedPreScreenAutomation pyramidSeedPreScreenAutomation;
    private TimeSpan shortActionDelay = TimeSpan.FromMilliseconds(AppSettingsDefaults.Automation.AutoCreate.ShortActionDelayMilliseconds);
    private TimeSpan menuActionDelay = TimeSpan.FromMilliseconds(AppSettingsDefaults.Automation.AutoCreate.MenuActionDelayMilliseconds);
    private int pyramidFilterPostDelayMilliseconds = AppSettingsDefaults.Automation.AutoCreate.PyramidFilterPostDelayMilliseconds;
    private AutomationResult? lastFailure;

    public WorldCreationMenuDriver(
        TerrariaSavePreparation savePreparation,
        TerrariaAutomationContext automation,
        WindowActivationService windowActivation,
        PyramidSeedPreScreenAutomation pyramidSeedPreScreenAutomation)
    {
        this.savePreparation = savePreparation;
        this.automation = automation;
        this.windowActivation = windowActivation;
        this.pyramidSeedPreScreenAutomation = pyramidSeedPreScreenAutomation;
    }

    public void ConfigureTiming(AutoCreateWorldSettings settings)
    {
        shortActionDelay = TimeSpan.FromMilliseconds(settings.ShortActionDelayMilliseconds);
        menuActionDelay = TimeSpan.FromMilliseconds(settings.MenuActionDelayMilliseconds);
        pyramidFilterPostDelayMilliseconds = settings.PyramidFilterPostDelayMilliseconds;
    }

    public void ClearFailure()
    {
        lastFailure = null;
    }

    public AutomationResult BuildFailure(string userMessage, string diagnostic)
    {
        AutomationResult? failure = lastFailure;
        lastFailure = null;
        return failure is { Failed: true }
            ? failure
            : AutomationResult.Failure(userMessage, diagnostic);
    }

    public async Task<bool> CreatePlayerAndOpenWorldSelectAsync(
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry,
        TerrariaSaveCleanupResult cleanup,
        CancellationToken cancellationToken)
    {
        if (!await automation.ClickAsync("Single Player", geometry.MainMenuSinglePlayer(), menuActionDelay, cancellationToken))
        {
            return false;
        }

        Dictionary<string, DateTime> playersBefore = savePreparation.SnapshotSaveFiles("Players", "*.plr");
        if (!await automation.ClickAsync("new player", geometry.SelectMenuNewButton(), menuActionDelay, cancellationToken))
        {
            return false;
        }

        if (!await ApplyPlayerTemplateAsync(settings, geometry, cancellationToken))
        {
            return false;
        }

        if (!await ApplyPlayerDifficultyAsync(settings.PlayerDifficulty, geometry, cancellationToken))
        {
            return false;
        }

        if (!await automation.ClickAsync("create player", geometry.CreatePlayerButton(), menuActionDelay, cancellationToken))
        {
            return false;
        }

        if (!await ConfirmPlayerNameAsync(settings.PlayerName, geometry, cancellationToken))
        {
            return false;
        }

        if (!await WaitForNewOrChangedSaveFileAsync("player file", playersBefore, "Players", "*.plr", PlayerCreateTimeout, cancellationToken))
        {
            return false;
        }

        if (!await ClickPlayerAsync(geometry, cleanup.FavoritePlayers, cancellationToken))
        {
            return false;
        }

        await automation.DelayAsync(menuActionDelay, cancellationToken);
        return true;
    }

    public async Task<CreateWorldAttemptResult> CreateOneWorldAsync(
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

    public async Task<bool> ReturnToMainMenuByBackTwiceAsync(
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        if (!windowActivation.TryReactivate(
                "before clicking back out of a rejected world",
                pyramidFilterPostDelayMilliseconds))
        {
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

    public async Task<bool> ReturnToMainMenuFromAdvancedSeedPageAsync(
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        if (!windowActivation.TryReactivate(
                "before returning from rejected pre-screen seed",
                pyramidFilterPostDelayMilliseconds))
        {
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

    public async Task<bool> PrepareRejectedWorldSelectRetryAsync(CancellationToken cancellationToken)
    {
        if (!windowActivation.TryReactivate(
                "before retrying a rejected world",
                pyramidFilterPostDelayMilliseconds))
        {
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

        StaticAppLogger.Instance.Info(
            $"Create world automation removed {cleanup.MovedWorlds} non-favorite world(s) " +
            $"before retrying from world select; favoriteWorlds={cleanup.FavoriteWorlds}.");
        return true;
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

        using ClipboardBackupScope? clipboard = TrySetClipboardText(settings.PlayerTemplateCode);
        if (clipboard is null)
        {
            return false;
        }

        return await automation.ClickAsync("character clothing tab", geometry.CharacterClothingCategoryButton(), shortActionDelay, cancellationToken) &&
            await automation.ClickAsync("paste player template", geometry.CharacterTemplatePasteButton(), menuActionDelay, cancellationToken);
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
            StaticAppLogger.Instance.Info($"Create world automation will return to main menu after pyramid seed pre-screen rejection: {preScreenResult.Detail}");
            return WorldSeedOptionsResult.RetryFromMainMenu;
        }

        if (!preScreenResult.CanCreateWorld)
        {
            RecordFailure(AutomationResult.Failure(
                "Could not choose an accepted world seed.",
                $"Create world automation pyramid seed pre-screen failed: {preScreenResult.Detail}"));
            return WorldSeedOptionsResult.Failed;
        }

        if (preScreenResult.Status == PyramidSeedPreScreenAutomationStatus.ContinueWithoutPreScreen)
        {
            StaticAppLogger.Instance.Info($"Create world automation will continue without pyramid seed pre-screen result: {preScreenResult.Detail}");
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
                StaticAppLogger.Instance.Info($"Create world automation found an unknown special seed: {rawSeed}");
                RecordFailure(AutomationResult.Failure(
                    $"Unknown Terraria special seed: {rawSeed}",
                    $"Create world automation found an unknown special seed: {rawSeed}"));
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
        using ClipboardBackupScope? clipboard = TrySetClipboardText(worldSeed);
        if (clipboard is null)
        {
            return false;
        }

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

    private async Task<bool> ClickPlayerAsync(
        TerrariaMenuGeometry geometry,
        int favoritePlayers,
        CancellationToken cancellationToken)
    {
        Point point = geometry.PlayerPlayButton(favoritePlayers);
        return await automation.ClickOnceAsync("first non-favorite player play button", point, menuActionDelay, cancellationToken);
    }

    private async Task<bool> ConfirmPlayerNameAsync(
        string playerName,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        string normalizedName = string.IsNullOrWhiteSpace(playerName) ? "1" : playerName.Trim();

        using ClipboardBackupScope? clipboard = TrySetClipboardText(normalizedName);
        if (clipboard is null)
        {
            return false;
        }

        automation.ThrowIfCancellationRequested(cancellationToken);
        automation.Window.PressModifiedKey(Keys.ControlKey, Keys.A);
        await automation.DelayAsync(shortActionDelay, cancellationToken);
        automation.ThrowIfCancellationRequested(cancellationToken);
        automation.Window.PressModifiedKey(Keys.ControlKey, Keys.V);
        await automation.DelayAsync(shortActionDelay, cancellationToken);
        return await automation.ClickAsync("submit player name", geometry.VirtualKeyboardSubmitButton(), menuActionDelay, cancellationToken);
    }

    private ClipboardBackupScope? TrySetClipboardText(string text)
    {
        AutomationResult result = ClipboardBackupScope.TrySetText(text, out ClipboardBackupScope? scope);
        if (result.Failed)
        {
            RecordFailure(result);
        }

        return scope;
    }

    private void RecordFailure(AutomationResult result)
    {
        if (result.Failed)
        {
            lastFailure = result;
        }
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

        StaticAppLogger.Instance.Info($"Create world automation {step} was not created or updated.");
        return false;
    }
}
