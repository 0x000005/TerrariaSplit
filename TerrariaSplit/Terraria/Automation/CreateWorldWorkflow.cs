using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class CreateWorldWorkflow : IDisposable
{
    private static readonly TimeSpan MenuStateTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PlayerSelectTransitionTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PlayerCreateTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SavePollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly int[] MainMenuModes = { 0 };

    private readonly TerrariaSavePreparation savePreparation = new();
    private readonly TerrariaMenuNavigator navigator = new();
    private TimeSpan shortActionDelay = TimeSpan.FromMilliseconds(AutoCreateWorldSettings.DefaultShortActionDelayMilliseconds);
    private TimeSpan menuActionDelay = TimeSpan.FromMilliseconds(AutoCreateWorldSettings.DefaultMenuActionDelayMilliseconds);

    public bool IsRunning { get; private set; }

    public bool IsAtMainMenu()
    {
        return !IsRunning && navigator.IsAtMenuMode(MainMenuModes);
    }

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        return RunAsync(new AppSettings(), cancellationToken);
    }

    public async Task RunAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        try
        {
            AutoCreateWorldSettings autoCreate = settings.AutoCreate;
            ApplyTiming(autoCreate);
            TerrariaSaveCleanupResult cleanup = default;
            if (!await RunStepAsync(
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

            Size clientSize = Size.Empty;
            if (!await RunStepAsync(
                    "activate Terraria window",
                    _ =>
                    {
                        if (!navigator.TryActivate(out Size activatedSize))
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
            if (!await RequireMenuModeAsync("main menu before Single Player", TimeSpan.FromSeconds(1), cancellationToken, 0))
            {
                return;
            }

            if (!await ClickAsync("Single Player", geometry.MainMenuSinglePlayer(), menuActionDelay, cancellationToken))
            {
                return;
            }

            if (!await RequireMenuModeAsync("Single Player", MenuStateTimeout, cancellationToken, 888))
            {
                return;
            }

            Dictionary<string, DateTime> playersBefore = savePreparation.SnapshotSaveFiles("Players", "*.plr");
            if (!await ClickAsync("new player", geometry.SelectMenuNewButton(), menuActionDelay, cancellationToken))
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

            if (!await ClickAsync("create player", geometry.CreatePlayerButton(), menuActionDelay, cancellationToken))
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

            if (!await ObserveMenuModeAsync("player creation return transition", TimeSpan.FromSeconds(2), cancellationToken, 1))
            {
                return;
            }

            if (!await RequireMenuModeAsync("player select after creating player", MenuStateTimeout, cancellationToken, 888))
            {
                return;
            }

            if (!await ClickPlayerAndRequireWorldSelectAsync(geometry, cleanup.FavoritePlayers, cancellationToken))
            {
                return;
            }

            await DelayAsync(menuActionDelay, cancellationToken);

            if (!await ClickAsync("new world", geometry.SelectMenuNewButton(), menuActionDelay, cancellationToken))
            {
                return;
            }

            if (!await ApplyWorldOptionsAsync(autoCreate, geometry, cancellationToken))
            {
                return;
            }

            if (!await RandomizeVisibleSeedAsync(geometry, cancellationToken))
            {
                return;
            }

            if (!await ClickAsync("create world", geometry.CreateWorldButton(), shortActionDelay, cancellationToken))
            {
                return;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Create world automation failed.");
        }
        finally
        {
            IsRunning = false;
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
            return await ClickAsync("character clothing tab", geometry.CharacterClothingCategoryButton(), shortActionDelay, cancellationToken) &&
                await ClickAsync("paste player template", geometry.CharacterTemplatePasteButton(), menuActionDelay, cancellationToken);
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

        return await ClickAsync("character info tab", geometry.CharacterInfoCategoryButton(), shortActionDelay, cancellationToken) &&
            await ClickAsync($"player difficulty {difficulty}", geometry.PlayerDifficultyButton(difficulty), shortActionDelay, cancellationToken);
    }

    private async Task<bool> ApplyWorldOptionsAsync(
        AutoCreateWorldSettings settings,
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        return await ClickAsync($"world size {settings.WorldSize}", geometry.WorldSizeButton(settings.WorldSize), shortActionDelay, cancellationToken) &&
            await ClickAsync($"world difficulty {settings.WorldDifficulty}", geometry.WorldDifficultyButton(settings.WorldDifficulty), shortActionDelay, cancellationToken) &&
            await ClickAsync($"world evil {settings.WorldEvil}", geometry.WorldEvilButton(settings.WorldEvil), shortActionDelay, cancellationToken);
    }

    private async Task<bool> RandomizeVisibleSeedAsync(
        TerrariaMenuGeometry geometry,
        CancellationToken cancellationToken)
    {
        if (!await ClickAsync("advanced seed menu", geometry.WorldAdvancedSeedButton(), menuActionDelay, cancellationToken))
        {
            return false;
        }

        if (!await ClickAsync("randomize visible seed", geometry.AdvancedSeedRandomizeButton(), shortActionDelay, cancellationToken))
        {
            return false;
        }

        return await ClickAsync("apply visible seed", geometry.WorldAdvancedApplyButton(), menuActionDelay, cancellationToken);
    }

    private async Task<bool> ClickPlayerAndRequireWorldSelectAsync(
        TerrariaMenuGeometry geometry,
        int favoritePlayers,
        CancellationToken cancellationToken)
    {
        Point point = geometry.PlayerPlayButton(favoritePlayers);
        if (!await ClickOnceAsync("first non-favorite player play button", point, shortActionDelay, cancellationToken))
        {
            return false;
        }

        if (!await ObserveMenuModeOnceAsync("player selection transition", PlayerSelectTransitionTimeout, cancellationToken, 6))
        {
            return false;
        }

        if (!await RequireMenuModeOnceAsync("world select after player selection", MenuStateTimeout, cancellationToken, 888))
        {
            return false;
        }

        await DelayAsync(menuActionDelay, cancellationToken);
        return true;
    }

    private void ApplyTiming(AutoCreateWorldSettings settings)
    {
        shortActionDelay = TimeSpan.FromMilliseconds(settings.ShortActionDelayMilliseconds);
        menuActionDelay = TimeSpan.FromMilliseconds(settings.MenuActionDelayMilliseconds);
        navigator.ApplyTiming(settings);
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
            navigator.PressModifiedKey(Keys.ControlKey, Keys.A);
            await DelayAsync(shortActionDelay, cancellationToken);
            navigator.PressModifiedKey(Keys.ControlKey, Keys.V);
            await DelayAsync(shortActionDelay, cancellationToken);
            return await ClickAsync("submit player name", geometry.VirtualKeyboardSubmitButton(), menuActionDelay, cancellationToken);
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
            AppLogger.Error(ex, "Create world automation failed to set player template clipboard text.");
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
        navigator.Dispose();
    }

    private async Task<bool> RunStepAsync(
        string step,
        Func<CancellationToken, Task<bool>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            return await action(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"Create world automation step '{step}' failed.");
            return false;
        }
    }

    private async Task<bool> ClickAsync(string step, Point point, TimeSpan delay, CancellationToken cancellationToken)
    {
        return await RunStepAsync(
            $"click {step}",
            ct => ClickOnceAsync(step, point, delay, ct),
            cancellationToken);
    }

    private async Task<bool> ClickOnceAsync(
        string step,
        Point point,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        return await navigator.ClickAsync(step, point, delay, cancellationToken);
    }

    private async Task<bool> RequireMenuModeAsync(
        string step,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        params int[] expectedModes)
    {
        return await RunStepAsync(
            $"wait for {step}",
            ct => RequireMenuModeOnceAsync(step, timeout, ct, expectedModes),
            cancellationToken);
    }

    private async Task<bool> RequireMenuModeOnceAsync(
        string step,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        params int[] expectedModes)
    {
        return await navigator.RequireMenuModeAsync(step, timeout, cancellationToken, expectedModes);
    }

    private async Task<bool> ObserveMenuModeAsync(
        string step,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        params int[] expectedModes)
    {
        return await RunStepAsync(
            $"observe {step}",
            ct => ObserveMenuModeOnceAsync(step, timeout, ct, expectedModes),
            cancellationToken);
    }

    private async Task<bool> ObserveMenuModeOnceAsync(
        string step,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        params int[] expectedModes)
    {
        return await navigator.ObserveMenuModeAsync(step, timeout, cancellationToken, expectedModes);
    }

    private async Task<bool> WaitForNewOrChangedSaveFileAsync(
        string step,
        Dictionary<string, DateTime> before,
        string directoryName,
        string pattern,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        return await RunStepAsync(
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
            Dictionary<string, DateTime> after = savePreparation.SnapshotSaveFiles(directoryName, pattern);
            if (after.Any(pair => !before.TryGetValue(pair.Key, out DateTime previousWriteTime) || pair.Value > previousWriteTime))
            {
                return true;
            }

            await DelayAsync(SavePollInterval, cancellationToken);
        }

        AppLogger.Info($"Create world automation {step} was not created or updated.");
        return false;
    }

    private static Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);
    }

}
