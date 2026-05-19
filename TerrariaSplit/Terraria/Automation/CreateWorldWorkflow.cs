using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class CreateWorldWorkflow : IDisposable
{
    private static readonly TimeSpan PlayerCreateTimeout = TimeSpan.FromSeconds(1.0);
    private static readonly TimeSpan SavePollInterval = TimeSpan.FromMilliseconds(50);

    private readonly TerrariaSavePreparation savePreparation = new();
    private readonly TerrariaAutomationContext automation = new("Create world");
    private readonly ZenithStarCatchAutomation zenithStarCatchAutomation;
    private TimeSpan shortActionDelay = TimeSpan.FromMilliseconds(AutoCreateWorldSettings.DefaultShortActionDelayMilliseconds);
    private TimeSpan menuActionDelay = TimeSpan.FromMilliseconds(AutoCreateWorldSettings.DefaultMenuActionDelayMilliseconds);

    public CreateWorldWorkflow()
    {
        zenithStarCatchAutomation = new ZenithStarCatchAutomation(automation);
    }

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        return RunAsync(new AppSettings(), cancellationToken);
    }

    public async Task RunAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        automation.BeginRun();
        try
        {
            AutoCreateWorldSettings autoCreate = settings.AutoCreate;
            ApplyTiming(autoCreate);
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

            if (!await automation.ClickAsync("new world", geometry.SelectMenuNewButton(), menuActionDelay, cancellationToken))
            {
                return;
            }

            if (!await ApplyWorldOptionsAsync(autoCreate, geometry, cancellationToken))
            {
                return;
            }

            if (!await ApplyWorldSeedOptionsAsync(autoCreate, geometry, cancellationToken))
            {
                return;
            }

            if (!await automation.ClickAsync("create world", geometry.CreateWorldButton(), shortActionDelay, cancellationToken))
            {
                return;
            }

            await zenithStarCatchAutomation.RunAsync(autoCreate, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Create world automation failed.");
        }
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

    private async Task<bool> ApplyWorldSeedOptionsAsync(
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        if (!await automation.ClickAsync("advanced seed menu", geometry.WorldAdvancedSeedButton(), menuActionDelay, cancellationToken))
        {
            return false;
        }

        if (!await ApplySpecialSeedsAsync(settings.SpecialSeeds, geometry, cancellationToken))
        {
            return false;
        }

        if (!await ApplySecretSeedsAsync(settings.SecretSeeds, geometry, cancellationToken))
        {
            return false;
        }

        if (!await automation.ClickAsync("randomize visible seed", geometry.AdvancedSeedRandomizeButton(), shortActionDelay, cancellationToken))
        {
            return false;
        }

        return await automation.ClickAsync("apply visible seed", geometry.WorldAdvancedApplyButton(), menuActionDelay, cancellationToken);
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

    private void ApplyTiming(AutoCreateWorldSettings settings)
    {
        shortActionDelay = TimeSpan.FromMilliseconds(settings.ShortActionDelayMilliseconds);
        menuActionDelay = TimeSpan.FromMilliseconds(settings.MenuActionDelayMilliseconds);
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
