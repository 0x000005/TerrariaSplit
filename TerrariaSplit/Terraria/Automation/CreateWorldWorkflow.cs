using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class CreateWorldWorkflow : IDisposable
{
    private static readonly TimeSpan PlayerCreateTimeout = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan SavePollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan EscapePollInterval = TimeSpan.FromMilliseconds(25);

    private readonly TerrariaSavePreparation savePreparation = new();
    private readonly TerrariaWindowController window = new();
    private TimeSpan shortActionDelay = TimeSpan.FromMilliseconds(AutoCreateWorldSettings.DefaultShortActionDelayMilliseconds);
    private TimeSpan menuActionDelay = TimeSpan.FromMilliseconds(AutoCreateWorldSettings.DefaultMenuActionDelayMilliseconds);
    private bool escapeCancellationLogged;

    public bool IsRunning { get; private set; }

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
        escapeCancellationLogged = false;
        ClearEscapeKeyState();
        try
        {
            AutoCreateWorldSettings autoCreate = settings.AutoCreate;
            ApplyTiming(autoCreate);
            Size clientSize = Size.Empty;
            if (!await RunStepAsync(
                    "activate Terraria window",
                    _ =>
                    {
                        if (!TryActivate(out Size activatedSize))
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

            if (!await ClickAsync("Single Player", geometry.MainMenuSinglePlayer(), menuActionDelay, cancellationToken))
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

            if (!await ClickPlayerAsync(geometry, cleanup.FavoritePlayers, cancellationToken))
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

    private async Task<bool> ClickPlayerAsync(
        TerrariaMenuGeometry geometry,
        int favoritePlayers,
        CancellationToken cancellationToken)
    {
        Point point = geometry.PlayerPlayButton(favoritePlayers);
        return await ClickOnceAsync("first non-favorite player play button", point, menuActionDelay, cancellationToken);
    }

    private void ApplyTiming(AutoCreateWorldSettings settings)
    {
        shortActionDelay = TimeSpan.FromMilliseconds(settings.ShortActionDelayMilliseconds);
        menuActionDelay = TimeSpan.FromMilliseconds(settings.MenuActionDelayMilliseconds);
        window.WindowActivationDelayMilliseconds = settings.WindowActivationDelayMilliseconds;
        window.ClickFocusDelayMilliseconds = settings.ClickFocusDelayMilliseconds;
        window.InputPressDurationMilliseconds = settings.InputPressDurationMilliseconds;
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
            ThrowIfCancellationRequested(cancellationToken);
            window.PressModifiedKey(Keys.ControlKey, Keys.A);
            await DelayAsync(shortActionDelay, cancellationToken);
            ThrowIfCancellationRequested(cancellationToken);
            window.PressModifiedKey(Keys.ControlKey, Keys.V);
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
    }

    private bool TryActivate(out Size clientSize)
    {
        bool success = window.TryActivate(out clientSize);
        Log(new AutomationStepResult(
            "activate Terraria window",
            success,
            ClientSize: clientSize,
            Detail: success ? null : "window activation failed"));
        return success;
    }

    private async Task<bool> RunStepAsync(
        string step,
        Func<CancellationToken, Task<bool>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            ThrowIfCancellationRequested(cancellationToken);
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
        ThrowIfCancellationRequested(cancellationToken);
        bool clicked = window.TryClickClient(point.X, point.Y, out Size clientSize);
        Log(new AutomationStepResult(
            $"click {step}",
            clicked,
            point,
            clientSize,
            Detail: clicked ? null : "window click failed"));
        if (!clicked)
        {
            return false;
        }

        await DelayAsync(delay, cancellationToken);
        ThrowIfCancellationRequested(cancellationToken);
        return true;
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
            ThrowIfCancellationRequested(cancellationToken);
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

    private async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        ThrowIfCancellationRequested(cancellationToken);
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        DateTime deadline = DateTime.UtcNow + delay;
        while (true)
        {
            ThrowIfCancellationRequested(cancellationToken);
            TimeSpan remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            TimeSpan interval = remaining < EscapePollInterval ? remaining : EscapePollInterval;
            await Task.Delay(interval, cancellationToken);
        }
    }

    private void ThrowIfCancellationRequested(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsEscapePressed())
        {
            return;
        }

        if (!escapeCancellationLogged)
        {
            escapeCancellationLogged = true;
            AppLogger.Info("Create world automation cancelled by Escape.");
        }

        throw new OperationCanceledException("Create world automation cancelled by Escape.", cancellationToken);
    }

    private static bool IsEscapePressed()
    {
        short state = NativeMethods.GetAsyncKeyState((int)Keys.Escape);
        return (state & 0x8000) != 0 || (state & 0x0001) != 0;
    }

    private static void ClearEscapeKeyState()
    {
        _ = NativeMethods.GetAsyncKeyState((int)Keys.Escape);
    }

    private static void Log(AutomationStepResult result)
    {
        AppLogger.Info(result.ToLogMessage());
    }

}
