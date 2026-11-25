using System.Drawing;
using System.Windows.Forms;
using TerrariaSplit.Terraria.Memory;

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
    }

    public void ClearFailure()
    {
        lastFailure = null;
    }

    public AutomationResult BuildFailure(string userMessage, string diagnostic)
    {
        AutomationResult? failure = lastFailure;
        lastFailure = null;
        if (!string.IsNullOrWhiteSpace(automation.LastFailureDiagnostic))
            diagnostic += Environment.NewLine + automation.LastFailureDiagnostic;
        return failure is { Failed: true }
            ? failure with { Stage = failure.Stage ?? automation.LastFailureStage }
            : AutomationResult.Failure(userMessage, diagnostic, failureCode: "automation.step.failed", stage: automation.LastFailureStage);
    }

    public async Task<bool> CreatePlayerAndOpenWorldSelectAsync(
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry,
        TerrariaSaveCleanupResult cleanup,
        CancellationToken cancellationToken)
    {
        string normalizedPlayerName = NormalizePlayerName(settings.PlayerName);
        if (!await automation.ClickAsync("Single Player", geometry, static current => current.MainMenuSinglePlayer(), menuActionDelay, cancellationToken,
            completion: MenuExpectation.Page(TerrariaMenuPage.Main, TerrariaMenuPage.PlayerSelect), button: new(TerrariaMenuButton.SinglePlayer)))
        {
            return false;
        }

        Dictionary<string, DateTime> playersBefore = savePreparation.SnapshotSaveFiles("Players", "*.plr");
        if (!await automation.ClickAsync("new player", geometry, static current => current.SelectMenuNewButton(), menuActionDelay, cancellationToken,
            completion: MenuExpectation.Page(TerrariaMenuPage.PlayerSelect, TerrariaMenuPage.Character), button: new(TerrariaMenuButton.NewPlayer)))
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

        if (!await automation.ClickAsync("create player", geometry, static current => current.CreatePlayerButton(), menuActionDelay, cancellationToken,
            completion: MenuExpectation.Page(TerrariaMenuPage.Character, TerrariaMenuPage.Keyboard), button: new(TerrariaMenuButton.CreatePlayer)))
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

        if (automation.ModernMenu is null) await automation.DelayAsync(menuActionDelay, cancellationToken);
        return true;
    }

    public async Task<CreateWorldAttemptResult> CreateOneWorldAsync(
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        if (WorldSeedFilterEvaluator.IsEnabledFor(settings))
            settings = TerrariaWorldSeedOptions.ResolveRandomEvil(settings, () => TerrariaSeedRandom.NextShared(2) + 1);
        if (!await automation.ClickAsync("new world", geometry, static current => current.SelectMenuNewButton(), menuActionDelay, cancellationToken,
            completion: MenuExpectation.Page(TerrariaMenuPage.WorldSelect, TerrariaMenuPage.World), button: new(TerrariaMenuButton.NewWorld)))
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
        if (!await ApplySecretSeedBootstrapAsync(settings.SecretSeeds, geometry, cancellationToken))
        {
            return CreateWorldAttemptResult.Failed;
        }

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

        if (!await automation.ClickAsync($"world size {settings.WorldSize}", geometry, current => current.WorldSizeButton(settings.WorldSize), menuActionDelay, cancellationToken))
        {
            return CreateWorldAttemptResult.Failed;
        }

        if (!await automation.ClickAsync($"world difficulty {settings.WorldDifficulty}", geometry, current => current.WorldDifficultyButton(settings.WorldDifficulty), menuActionDelay, cancellationToken))
        {
            return CreateWorldAttemptResult.Failed;
        }

        if (!await automation.ClickAsync($"world evil {settings.WorldEvil}", geometry, current => current.WorldEvilButton(settings.WorldEvil), shortActionDelay, cancellationToken))
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
                $"Create world automation 1.4.4.9 pyramid seed pre-screen failed: {preScreenResult.Detail}",
                preScreenResult.Exception,
                failureCode: "filter.failed", stage: "Seed filtering"));
            return CreateWorldAttemptResult.Failed;
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
        AppLog.ForSource().LogInformation("Create world automation dispatching Terraria final create-world click.");
        bool clicked = await automation.ClickAsync(
            "create world",
            geometry,
            static current => current.CreateWorldButton(),
            shortActionDelay,
            cancellationToken,
            completion: MenuExpectation.Page(TerrariaMenuPage.World, TerrariaMenuPage.Generating), button: new(TerrariaMenuButton.CreateWorld));
        TimeSpan elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(startedTimestamp);
        AppLog.ForSource().LogInformation(
            $"Create world automation final create-world click returned; clicked={clicked}, elapsedMs={elapsed.TotalMilliseconds:F0}.");
        return clicked;
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

        string templateCategoryStep = geometry.Profile.Kind == TerrariaMenuProfileKind.Legacy1449
            ? "character gender tab"
            : "character clothing tab";
        if (!await automation.ClickAsync(templateCategoryStep, geometry, static current => current.CharacterTemplateCategoryButton(), shortActionDelay, cancellationToken,
                completion: MenuExpectation.Ensure(TerrariaMenuPage.Character, s => s.CharacterCategory == 1, "clothing tab", s => s.CharacterCategory.HasValue),
                button: new(TerrariaMenuButton.CharacterClothing))) return false;
        await using ClipboardBackupScope clipboard = await ClipboardBackupScope.SetTextAsync(
            settings.PlayerTemplateCode, automation.DelayAsync, cancellationToken);
        return await automation.ClickAsync("paste player template", geometry, static current => current.CharacterTemplatePasteButton(), menuActionDelay, cancellationToken,
                completion: automation.ModernMenu is null ? null : MenuExpectation.Ensure(TerrariaMenuPage.Character,
                    ModernMenuAutomation.TemplateMatches(settings.PlayerTemplateCode), "player template", s => s.PlayerValues.Count == 11),
                beforePress: clipboard.EnsureTextAsync, button: new(TerrariaMenuButton.PasteTemplate));
    }

    private async Task<bool> ApplyPlayerDifficultyAsync(
        string playerDifficulty,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        string difficulty = AutoCreatePlayerDifficulty.Normalize(playerDifficulty);
        if (difficulty == AutoCreatePlayerDifficulty.Softcore && automation.ModernMenu is null)
        {
            return true;
        }

        if (geometry.Profile.UsesLegacyCharacterCreationWizard)
        {
            return await automation.ClickAsync("player difficulty menu", geometry, static current => current.PlayerDifficultyMenuButton(), menuActionDelay, cancellationToken) &&
                await automation.ClickAsync($"player difficulty {difficulty}", geometry, current => current.PlayerDifficultyButton(difficulty), menuActionDelay, cancellationToken);
        }

        int difficultyCode = difficulty == AutoCreatePlayerDifficulty.Journey ? 3 : difficulty == AutoCreatePlayerDifficulty.Mediumcore ? 1 : difficulty == AutoCreatePlayerDifficulty.Hardcore ? 2 : 0;
        return await automation.ClickAsync("character info tab", geometry, static current => current.PlayerDifficultyMenuButton(), shortActionDelay, cancellationToken,
                completion: MenuExpectation.Ensure(TerrariaMenuPage.Character, s => s.CharacterCategory == 0, "character info", s => s.CharacterCategory.HasValue),
                button: new(TerrariaMenuButton.CharacterInfo)) &&
            await automation.ClickAsync($"player difficulty {difficulty}", geometry, current => current.PlayerDifficultyButton(difficulty), shortActionDelay, cancellationToken,
                completion: MenuExpectation.Ensure(TerrariaMenuPage.Character,
                    s => s.PlayerDifficulty == difficultyCode,
                    "player difficulty=" + difficulty, s => s.PlayerDifficulty.HasValue), button: new(TerrariaMenuButton.PlayerDifficulty, difficultyCode));
    }

    private async Task<bool> ApplyWorldOptionsAsync(
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        return await automation.ClickAsync($"world size {settings.WorldSize}", geometry, current => current.WorldSizeButton(settings.WorldSize), shortActionDelay, cancellationToken,
                completion: MenuExpectation.Ensure(TerrariaMenuPage.World, s => s.WorldWidth == AutoCreateWorldSize.Width(settings.WorldSize), "world size=" + settings.WorldSize),
                button: new(TerrariaMenuButton.WorldSize, TerrariaWorldSeedOptions.SizeCode(settings.WorldSize))) &&
            await automation.ClickAsync($"world difficulty {settings.WorldDifficulty}", geometry, current => current.WorldDifficultyButton(settings.WorldDifficulty), shortActionDelay, cancellationToken,
                completion: MenuExpectation.Ensure(TerrariaMenuPage.World, s => s.GameMode == TerrariaWorldSeedOptions.CopiedDifficultyCode(settings.WorldDifficulty) - 1, "world difficulty=" + settings.WorldDifficulty),
                button: new(TerrariaMenuButton.WorldDifficulty, TerrariaWorldSeedOptions.CopiedDifficultyCode(settings.WorldDifficulty) - 1)) &&
            await automation.ClickAsync($"world evil {settings.WorldEvil}", geometry, current => current.WorldEvilButton(settings.WorldEvil), shortActionDelay, cancellationToken,
                completion: MenuExpectation.Ensure(TerrariaMenuPage.World, s => s.Evil == (settings.WorldEvil == AutoCreateWorldEvil.Random ? -1 : settings.WorldEvil == AutoCreateWorldEvil.Crimson ? 1 : 0), "world evil=" + settings.WorldEvil),
                button: new(TerrariaMenuButton.WorldEvil, settings.WorldEvil == AutoCreateWorldEvil.Random ? -1 : settings.WorldEvil == AutoCreateWorldEvil.Crimson ? 1 : 0));
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
            AppLog.ForSource().LogInformation($"Create world automation could not build 1.4.4.9 seed text: {detail}");
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(seedText))
        {
            AppLog.ForSource().LogInformation("Create world automation will leave the 1.4.4.9 seed field unchanged so Terraria keeps its random seed.");
            return string.Empty;
        }

        AppLog.ForSource().LogInformation($"Create world automation will submit seed text through the 1.4.4.9 world seed field: {detail}");
        return seedText;
    }

    private async Task<WorldSeedOptionsResult> ApplyWorldSeedOptionsAsync(
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        if (!await automation.ClickAsync("advanced seed menu", geometry, static current => current.WorldAdvancedSeedButton(), menuActionDelay, cancellationToken,
            completion: MenuExpectation.Page(TerrariaMenuPage.World, TerrariaMenuPage.Advanced), button: new(TerrariaMenuButton.AdvancedMenu)))
        {
            return WorldSeedOptionsResult.Failed;
        }

        if (!await ApplySpecialSeedsAsync(settings.SpecialSeeds, geometry, cancellationToken))
        {
            return WorldSeedOptionsResult.Failed;
        }

        string fixedSeed = settings.FixedSeed?.Trim() ?? string.Empty;
        if (fixedSeed.Length > 0)
        {
            if (!await EnterWorldSeedAsync(fixedSeed, geometry, useWorldCreationPageSeedField: false, cancellationToken))
            {
                return WorldSeedOptionsResult.Failed;
            }
        }
        else
        {
            pyramidSeedPreScreenAutomation.BeginVisibleSeedReaderPreparation(settings);
            PyramidSeedPreScreenAutomationResult preScreenResult = await pyramidSeedPreScreenAutomation.RandomizeUntilAcceptedAsync(
                settings,
                geometry,
                shortActionDelay,
                cancellationToken);
            if (!preScreenResult.CanCreateWorld)
            {
                RecordFailure(AutomationResult.Failure(
                    "Could not choose an accepted world seed.",
                    $"Create world automation pyramid seed pre-screen failed: {preScreenResult.Detail}",
                    preScreenResult.Exception,
                    failureCode: "filter.failed", stage: "Seed filtering"));
                return WorldSeedOptionsResult.Failed;
            }


            if (!windowActivation.TryReactivate(
                    "after world seed pre-screen",
                    automation.ModernMenu is null ? settings.WindowActivationDelayMilliseconds : 0))
            {
                RecordFailure(AutomationResult.Failure(
                    "Could not reactivate Terraria after screening the world seed.",
                    "Create world automation could not reactivate Terraria after the world seed pre-screen worker round was released."));
                return WorldSeedOptionsResult.Failed;
            }
        }

        return await automation.ClickAsync("apply visible seed", geometry, static current => current.WorldAdvancedApplyButton(), menuActionDelay, cancellationToken,
                completion: MenuExpectation.Page(TerrariaMenuPage.Advanced, TerrariaMenuPage.World), button: new(TerrariaMenuButton.ApplySeed))
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
                AppLog.ForSource().LogInformation($"Create world automation found an unknown special seed: {rawSeed}");
                RecordFailure(AutomationResult.Failure(
                    $"Unknown Terraria special seed: {rawSeed}",
                    $"Create world automation found an unknown special seed: {rawSeed}"));
                return false;
            }
        }

        foreach (string specialSeed in AutoCreateSpecialWorldSeed.ParseList(specialSeeds))
        {
            int seedIndex = AutoCreateSpecialWorldSeed.MenuIndex(specialSeed);
            if (!await automation.ClickAsync($"special seed {specialSeed}", geometry, current => current.AdvancedSpecialSeedButton(specialSeed), shortActionDelay, cancellationToken,
                completion: MenuExpectation.Ensure(TerrariaMenuPage.Advanced,
                    s => s.SpecialSeeds is { } selected && seedIndex < selected.Length && selected[seedIndex],
                    "enabled " + specialSeed, ModernMenuAutomation.SpecialSeedsReadable), button: new(TerrariaMenuButton.SpecialSeed, seedIndex)))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> ApplySecretSeedBootstrapAsync(
        string? secretSeeds,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        string copiedSeedText = TerrariaCopiedSeedBuilder.BuildSecretSeedBootstrapText(secretSeeds);
        if (copiedSeedText.Length == 0)
        {
            return true;
        }

        return await EnterWorldSeedAsync(
            copiedSeedText,
            geometry,
            useWorldCreationPageSeedField: true,
            cancellationToken);
    }

    private async Task<bool> EnterWorldSeedAsync(
        string worldSeed,
        TerrariaMenuGeometry geometry,
        bool useWorldCreationPageSeedField,
        CancellationToken cancellationToken)
    {
        if (!await automation.ClickAsync(
                "world seed field",
                geometry,
                current => useWorldCreationPageSeedField
                    ? current.WorldSeedFieldButton()
                    : current.AdvancedSeedTextButton(),
                menuActionDelay,
                cancellationToken,
                completion: MenuExpectation.Page(useWorldCreationPageSeedField ? TerrariaMenuPage.World : TerrariaMenuPage.Advanced, TerrariaMenuPage.Keyboard),
                button: new(useWorldCreationPageSeedField ? TerrariaMenuButton.WorldSeed : TerrariaMenuButton.AdvancedSeed)))
        {
            return false;
        }

        return await EnterVirtualKeyboardTextAsync("world seed", worldSeed, geometry, allowEmpty: false, cancellationToken,
            useWorldCreationPageSeedField ? TerrariaMenuPage.World : TerrariaMenuPage.Advanced,
            s => SeedSubmissionMatches(s, worldSeed));
    }

    private async Task<bool> ClickPlayerAsync(
        TerrariaMenuGeometry geometry,
        int playerListIndex,
        string? createdPlayerFileName,
        CancellationToken cancellationToken)
    {
        string step = string.IsNullOrWhiteSpace(createdPlayerFileName)
            ? "created player play button"
            : $"created player play button ({createdPlayerFileName})";
        return await automation.ClickOnceAsync(
            step,
            geometry,
            current => current.PlayerPlayButton(playerListIndex),
            menuActionDelay,
            cancellationToken,
            completion: new(TerrariaMenuPage.PlayerSelect, TerrariaMenuPage.WorldSelect,
                s => createdPlayerFileName is not null && string.Equals(Path.GetFileName(s.ActivePlayerPath), createdPlayerFileName, StringComparison.OrdinalIgnoreCase),
                "selected player=" + createdPlayerFileName), button: new(TerrariaMenuButton.PlayPlayer, playerListIndex));
    }

    private async Task<bool> ConfirmPlayerNameAsync(
        string playerName,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        string normalizedName = NormalizePlayerName(playerName);
        return await EnterVirtualKeyboardTextAsync("player name", normalizedName, geometry, allowEmpty: false, cancellationToken, TerrariaMenuPage.PlayerSelect);
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
        AppLog.ForSource().LogInformation(
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
        CancellationToken cancellationToken,
        TerrariaMenuPage returnPage,
        Func<TerrariaMenuSnapshot, bool>? submitted = null)
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

            return await SubmitVirtualKeyboardAsync($"submit empty {label}", geometry, cancellationToken, returnPage);
        }

        automation.ThrowIfCancellationRequested(cancellationToken);
        if (automation.ModernMenu is { } modern)
        {
            await modern.EnterTextAsync(normalizedText, cancellationToken);
            return await SubmitVirtualKeyboardAsync($"submit {label}", geometry, cancellationToken, returnPage, normalizedText, submitted);
        }
        using ClipboardBackupScope? clipboard = TrySetClipboardText(normalizedText);
        if (clipboard is null) return false;
        automation.Window.PressModifiedKey(Keys.ControlKey, Keys.A);
        await automation.DelayAsync(shortActionDelay, cancellationToken);
        automation.ThrowIfCancellationRequested(cancellationToken);
        automation.Window.PressModifiedKey(Keys.ControlKey, Keys.V);
        await automation.DelayAsync(shortActionDelay, cancellationToken);
        return await SubmitVirtualKeyboardAsync($"submit {label}", geometry, cancellationToken, returnPage);
    }

    private Task<bool> SubmitVirtualKeyboardAsync(
        string step,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken,
        TerrariaMenuPage returnPage,
        string? expectedText = null,
        Func<TerrariaMenuSnapshot, bool>? submitted = null)
    {
        return automation.ClickAsync(step, geometry, static current => current.VirtualKeyboardSubmitButton(), menuActionDelay, cancellationToken,
            completion: new(TerrariaMenuPage.Keyboard, returnPage, submitted, "submitted " + step,
                SourceCondition: s => expectedText is null || s.Text == expectedText), button: new(TerrariaMenuButton.Submit));
    }

    internal static bool SeedSubmissionMatches(TerrariaMenuSnapshot snapshot, string input)
    {
        string text = input.Trim();
        string remaining = text;
        static bool ReadCode(ref string value, out int code)
        {
            int separator = value.IndexOf('.');
            code = 0;
            if (separator < 0 || !int.TryParse(value.AsSpan(0, separator), out code)) return false;
            value = value[(separator + 1)..];
            return true;
        }
        // Copied seeds are parsed by Terraria into settings, a secret-seed list, and the numeric/text seed.
        if (ReadCode(ref remaining, out int size) && size is >= 1 and <= 3 &&
            ReadCode(ref remaining, out int difficulty) && difficulty is >= 1 and <= 4 &&
            ReadCode(ref remaining, out int evil) && evil is >= 1 and <= 2)
        {
            _ = ReadCode(ref remaining, out int mask); // Terraria permits the special-seed mask to be omitted.
            if (snapshot.SpecialSeedLayoutError is { } error) throw new InvalidDataException(error);
            string[] serializedOrder = [AutoCreateSpecialWorldSeed.Drunk, AutoCreateSpecialWorldSeed.NotTheBees,
                AutoCreateSpecialWorldSeed.ForTheWorthy, AutoCreateSpecialWorldSeed.Celebration, AutoCreateSpecialWorldSeed.TheConstant,
                AutoCreateSpecialWorldSeed.Remix, AutoCreateSpecialWorldSeed.NoTraps, AutoCreateSpecialWorldSeed.Zenith, AutoCreateSpecialWorldSeed.Skyblock];
            var expectedOptions = new bool[serializedOrder.Length + 1];
            for (int bit = 0; bit < serializedOrder.Length; bit++)
                expectedOptions[AutoCreateSpecialWorldSeed.MenuIndex(serializedOrder[bit])] = (mask & (1 << bit)) != 0;
            int zenithIndex = AutoCreateSpecialWorldSeed.MenuIndex(AutoCreateSpecialWorldSeed.Zenith);
            if (expectedOptions[zenithIndex]) Array.Fill(expectedOptions, true, 1, zenithIndex - 1);
            expectedOptions[zenithIndex] = expectedOptions.Skip(1).Take(zenithIndex - 1).All(enabled => enabled);
            expectedOptions[0] = !expectedOptions.Skip(1).Any(enabled => enabled);
            return snapshot.SecretSeeds is { } secrets && snapshot.Seed is { } seed &&
                string.Join("|", secrets.Append(seed)) == remaining &&
                snapshot.SpecialSeeds is { } options && options.SequenceEqual(expectedOptions) &&
                snapshot.WorldWidth == (size == 1 ? 4200 : size == 2 ? 6400 : 8400) &&
                snapshot.GameMode == difficulty - 1 && snapshot.Evil == evil - 1;
        }
        return snapshot.Seed == text || snapshot.Seed == "" && snapshot.SecretSeeds is { } names && names.Contains(text);
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

        automation.RecordFailure(step, $"No new or changed save file was observed within {timeout.TotalMilliseconds:F0} ms.");
        return false;
    }
}
