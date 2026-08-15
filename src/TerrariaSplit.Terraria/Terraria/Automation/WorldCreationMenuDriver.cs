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
        string normalizedPlayerName = NormalizePlayerName(settings.PlayerName);
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

        if (!await ConfirmPlayerNameAsync(normalizedPlayerName, geometry, cancellationToken))
        {
            return false;
        }

        if (!await WaitForNewOrChangedSaveFileAsync("player file", playersBefore, "Players", "*.plr", PlayerCreateTimeout, cancellationToken))
        {
            return false;
        }

        Dictionary<string, DateTime> playersAfter = savePreparation.SnapshotSaveFiles("Players", "*.plr");
        string? createdPlayerFileName = TerrariaSavePreparation.FindNewOrChangedSaveFile(playersBefore, playersAfter);
        int playerListIndex = ResolveCreatedPlayerListIndex(
            geometry,
            cleanup.FavoritePlayers,
            createdPlayerFileName,
            normalizedPlayerName);
        if (!await ClickPlayerAsync(geometry, playerListIndex, createdPlayerFileName, cancellationToken))
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

        if (geometry.Profile.UsesLegacyWorldCreationWizard)
        {
            return await CreateOneWorldLegacy1449Async(settings, geometry, cancellationToken);
        }

        return await CreateOneWorldModernAsync(settings, geometry, cancellationToken);
    }

    private async Task<CreateWorldAttemptResult> CreateOneWorldModernAsync(
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        if (!await ApplyWorldOptionsAsync(settings, geometry, cancellationToken))
        {
            return CreateWorldAttemptResult.Failed;
        }

        WorldSeedOptionsResult seedOptionsResult = await ApplyWorldSeedOptionsAsync(settings, geometry, cancellationToken);
        if (seedOptionsResult == WorldSeedOptionsResult.Failed)
        {
            return CreateWorldAttemptResult.Failed;
        }

        return await ClickCreateWorldAsync(geometry, cancellationToken)
            ? CreateWorldAttemptResult.Created
            : CreateWorldAttemptResult.Failed;
    }

    private async Task<CreateWorldAttemptResult> CreateOneWorldLegacy1449Async(
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        if (!ValidateLegacy1449WorldOptions(settings, geometry))
        {
            return CreateWorldAttemptResult.Failed;
        }

        if (!await automation.ClickAsync($"world size {settings.WorldSize}", geometry.WorldSizeButton(settings.WorldSize), menuActionDelay, cancellationToken))
        {
            return CreateWorldAttemptResult.Failed;
        }

        if (!await automation.ClickAsync($"world difficulty {settings.WorldDifficulty}", geometry.WorldDifficultyButton(settings.WorldDifficulty), menuActionDelay, cancellationToken))
        {
            return CreateWorldAttemptResult.Failed;
        }

        if (!await automation.ClickAsync($"world evil {settings.WorldEvil}", geometry.WorldEvilButton(settings.WorldEvil), shortActionDelay, cancellationToken))
        {
            return CreateWorldAttemptResult.Failed;
        }

        pyramidSeedPreScreenAutomation.BeginVisibleSeedReaderPreparation(settings);
        PyramidSeedPreScreenAutomationResult preScreenResult =
            await pyramidSeedPreScreenAutomation.RandomizeCurrentSeedUntilAcceptedAsync(settings, geometry, shortActionDelay, cancellationToken);
        if (!preScreenResult.CanCreateWorld)
        {
            RecordFailure(AutomationResult.Failure(
                "Could not choose an accepted world seed.",
                $"Create world automation 1.4.4.9 pyramid seed pre-screen failed: {preScreenResult.Detail}"));
            return CreateWorldAttemptResult.Failed;
        }

        if (preScreenResult.Status == PyramidSeedPreScreenAutomationStatus.ContinueWithoutPreScreen)
        {
            FileAppLogger.Instance.Info($"Create world automation will continue without 1.4.4.9 pyramid seed pre-screen result: {preScreenResult.Detail}");
        }

        string worldSeed = BuildLegacy1449WorldSeed(settings);
        if (!string.IsNullOrWhiteSpace(worldSeed) &&
            !await EnterWorldSeedAsync(worldSeed, geometry, useWorldCreationPageSeedField: true, cancellationToken))
        {
            return CreateWorldAttemptResult.Failed;
        }

        return await ClickCreateWorldAsync(geometry, cancellationToken)
            ? CreateWorldAttemptResult.Created
            : CreateWorldAttemptResult.Failed;
    }

    private async Task<bool> ClickCreateWorldAsync(
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        long startedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        FileAppLogger.Instance.Info("Create world automation dispatching Terraria final create-world click.");
        bool clicked = await automation.ClickAsync(
            "create world",
            geometry.CreateWorldButton(),
            shortActionDelay,
            cancellationToken);
        TimeSpan elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(startedTimestamp);
        FileAppLogger.Instance.Info(
            $"Create world automation final create-world click returned; clicked={clicked}, elapsedMs={elapsed.TotalMilliseconds:F0}.");
        return clicked;
    }

    public async Task<bool> PrepareRejectedWorldSelectRetryAsync(
        AutoCreateWorldSettings settings,
        CancellationToken cancellationToken)
    {
        if (!windowActivation.TryReactivate(
                "before retrying a rejected world",
                pyramidFilterPostDelayMilliseconds))
        {
            return false;
        }

        if (settings.PreserveExistingSaves)
        {
            FileAppLogger.Instance.Info(
                "Create world automation preserved the rejected world before retrying from world select.");
            return true;
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

        FileAppLogger.Instance.Info(
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

        if (!geometry.Profile.SupportsPlayerTemplatePaste)
        {
            RecordFailure(AutomationResult.Failure(
                "Player template paste is not available in the current character creation menu.",
                "Create world automation stopped because the detected character creation menu profile has no template paste control."));
            return false;
        }

        using ClipboardBackupScope? clipboard = TrySetClipboardText(settings.PlayerTemplateCode);
        if (clipboard is null)
        {
            return false;
        }

        string templateCategoryStep = geometry.Profile.Kind == TerrariaMenuProfileKind.Legacy1449
            ? "character gender tab"
            : "character clothing tab";
        return await automation.ClickAsync(templateCategoryStep, geometry.CharacterTemplateCategoryButton(), shortActionDelay, cancellationToken) &&
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

        if (geometry.Profile.UsesLegacyCharacterCreationWizard)
        {
            return await automation.ClickAsync("player difficulty menu", geometry.PlayerDifficultyMenuButton(), menuActionDelay, cancellationToken) &&
                await automation.ClickAsync($"player difficulty {difficulty}", geometry.PlayerDifficultyButton(difficulty), menuActionDelay, cancellationToken);
        }

        return await automation.ClickAsync("character info tab", geometry.PlayerDifficultyMenuButton(), shortActionDelay, cancellationToken) &&
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

    private bool ValidateLegacy1449WorldOptions(
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry)
    {
        string difficulty = AutoCreateWorldDifficulty.Normalize(settings.WorldDifficulty);
        if (!geometry.Profile.SupportsJourneyWorldDifficulty &&
            string.Equals(difficulty, AutoCreateWorldDifficulty.Journey, StringComparison.OrdinalIgnoreCase))
        {
            RecordFailure(AutomationResult.Failure(
                "Journey world creation is not adapted for the current 1.4.4.9 automation path yet.",
                "Create world automation stopped because the current 1.4.4.9 world creation path has not been mapped to the Journey world button."));
            return false;
        }

        if (!geometry.Profile.SupportsSpecialSeedButtons &&
            !TerrariaLegacy1449SeedText.TryBuild(settings, out _, out string seedTextDetail))
        {
            RecordFailure(AutomationResult.Failure(
                "Terraria 1.4.4.9 cannot use one of the selected seed options.",
                $"Create world automation stopped before filling the 1.4.4.9 world seed field: {seedTextDetail}"));
            return false;
        }

        return true;
    }

    private static string BuildLegacy1449WorldSeed(AutoCreateWorldSettings settings)
    {
        if (!TerrariaLegacy1449SeedText.TryBuild(settings, out string seedText, out string detail))
        {
            FileAppLogger.Instance.Info($"Create world automation could not build 1.4.4.9 seed text: {detail}");
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(seedText))
        {
            FileAppLogger.Instance.Info("Create world automation will leave the 1.4.4.9 seed field unchanged so Terraria keeps its random seed.");
            return string.Empty;
        }

        FileAppLogger.Instance.Info($"Create world automation will submit seed text through the 1.4.4.9 world seed field: {detail}");
        return seedText;
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

        pyramidSeedPreScreenAutomation.BeginVisibleSeedReaderPreparation(settings);
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
        if (!preScreenResult.CanCreateWorld)
        {
            RecordFailure(AutomationResult.Failure(
                "Could not choose an accepted world seed.",
                $"Create world automation pyramid seed pre-screen failed: {preScreenResult.Detail}"));
            return WorldSeedOptionsResult.Failed;
        }

        if (preScreenResult.Status == PyramidSeedPreScreenAutomationStatus.ContinueWithoutPreScreen)
        {
            FileAppLogger.Instance.Info($"Create world automation will continue without pyramid seed pre-screen result: {preScreenResult.Detail}");
        }

        if (!windowActivation.TryReactivate(
                "after world seed pre-screen",
                settings.WindowActivationDelayMilliseconds))
        {
            RecordFailure(AutomationResult.Failure(
                "Could not reactivate Terraria after screening the world seed.",
                "Create world automation could not reactivate Terraria after the world seed pre-screen worker round was released."));
            return WorldSeedOptionsResult.Failed;
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
                FileAppLogger.Instance.Info($"Create world automation found an unknown special seed: {rawSeed}");
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

        return await EnterWorldSeedAsync(seedText, geometry, useWorldCreationPageSeedField: false, cancellationToken);
    }

    private async Task<bool> EnterWorldSeedAsync(
        string worldSeed,
        TerrariaMenuGeometry geometry,
        bool useWorldCreationPageSeedField,
        CancellationToken cancellationToken)
    {
        Point seedField = useWorldCreationPageSeedField
            ? geometry.WorldSeedFieldButton()
            : geometry.AdvancedSeedTextButton();
        if (!await automation.ClickAsync("world seed field", seedField, menuActionDelay, cancellationToken))
        {
            return false;
        }

        return await EnterVirtualKeyboardTextAsync("world seed", worldSeed, geometry, allowEmpty: false, cancellationToken);
    }

    private async Task<bool> ClickPlayerAsync(
        TerrariaMenuGeometry geometry,
        int playerListIndex,
        string? createdPlayerFileName,
        CancellationToken cancellationToken)
    {
        Point point = geometry.PlayerPlayButton(playerListIndex);
        string step = string.IsNullOrWhiteSpace(createdPlayerFileName)
            ? "created player play button"
            : $"created player play button ({createdPlayerFileName})";
        return await automation.ClickOnceAsync(step, point, menuActionDelay, cancellationToken);
    }

    private async Task<bool> ConfirmPlayerNameAsync(
        string playerName,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        string normalizedName = NormalizePlayerName(playerName);
        return await EnterVirtualKeyboardTextAsync("player name", normalizedName, geometry, allowEmpty: false, cancellationToken);
    }

    private int ResolveCreatedPlayerListIndex(
        TerrariaMenuGeometry geometry,
        int fallbackIndex,
        string? createdPlayerFileName,
        string normalizedPlayerName)
    {
        IReadOnlyList<TerrariaPlayerSelectionEntry> players = savePreparation.ReadPlayerSelectionEntries(
            createdPlayerFileName,
            normalizedPlayerName);
        int resolvedIndex = TerrariaPlayerSelectionIndexResolver.ResolveCreatedPlayerIndex(
            geometry.Profile,
            players,
            createdPlayerFileName,
            fallbackIndex);
        FileAppLogger.Instance.Info(
            $"Create world automation selected player index {resolvedIndex}; " +
            $"profile={geometry.Profile.Kind}, createdFile={createdPlayerFileName ?? "<unknown>"}, " +
            $"playerCount={players.Count}, fallbackIndex={fallbackIndex}.");
        return resolvedIndex;
    }

    private static string NormalizePlayerName(string playerName)
    {
        return string.IsNullOrWhiteSpace(playerName) ? "1" : playerName.Trim();
    }

    private async Task<bool> EnterVirtualKeyboardTextAsync(
        string label,
        string text,
        TerrariaMenuGeometry geometry,
        bool allowEmpty,
        CancellationToken cancellationToken)
    {
        string normalizedText = text.Trim();
        if (normalizedText.Length == 0)
        {
            if (!allowEmpty)
            {
                RecordFailure(AutomationResult.Failure(
                    $"Could not enter {label}.",
                    $"Create world automation received an empty {label} for a required virtual keyboard prompt."));
                return false;
            }

            return await SubmitVirtualKeyboardAsync($"submit empty {label}", geometry, cancellationToken);
        }

        using ClipboardBackupScope? clipboard = TrySetClipboardText(normalizedText);
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
        return await SubmitVirtualKeyboardAsync($"submit {label}", geometry, cancellationToken);
    }

    private Task<bool> SubmitVirtualKeyboardAsync(
        string step,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        return automation.ClickAsync(step, geometry.VirtualKeyboardSubmitButton(), menuActionDelay, cancellationToken);
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
            if (TerrariaSavePreparation.FindNewOrChangedSaveFile(before, after) is not null)
            {
                return true;
            }

            await automation.DelayAsync(SavePollInterval, cancellationToken);
        }

        FileAppLogger.Instance.Info($"Create world automation {step} was not created or updated.");
        return false;
    }
}
