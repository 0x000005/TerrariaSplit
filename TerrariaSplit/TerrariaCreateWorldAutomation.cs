using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal enum AutoCreateWorldDisplayState
{
    Creating,
    Failed,
    Created
}

internal sealed class TerrariaCreateWorldAutomation : IDisposable
{
    private static readonly TimeSpan FinalStatusDuration = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MenuStateTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PlayerSelectTransitionTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PlayerCreateTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FastPollInterval = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly int[] MainMenuModes = { 0 };

    private readonly TerrariaSaveFileCleaner saveCleaner = new();
    private readonly TerrariaMenuStateReader menuState = new();
    private readonly TerrariaWindowController window = new();
    private TimeSpan shortActionDelay = TimeSpan.FromMilliseconds(AutoCreateWorldSettings.DefaultShortActionDelayMilliseconds);
    private TimeSpan menuActionDelay = TimeSpan.FromMilliseconds(AutoCreateWorldSettings.DefaultMenuActionDelayMilliseconds);
    private AutoCreateWorldDisplayState? displayState;
    private DateTime? displayStateExpiresUtc;

    public bool IsRunning { get; private set; }

    public bool TryGetDisplayStatus(out AutoCreateWorldDisplayState state)
    {
        if (displayState is not AutoCreateWorldDisplayState activeState)
        {
            state = default;
            return false;
        }

        if (displayStateExpiresUtc is DateTime expiresUtc && DateTime.UtcNow >= expiresUtc)
        {
            ClearDisplayStatus();
            state = default;
            return false;
        }

        state = activeState;
        return true;
    }

    public bool IsAtMainMenu()
    {
        return !IsRunning &&
            menuState.TryReadMenuMode(out int menuMode, MainMenuModes) &&
            menuMode == 0;
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
        SetPersistentDisplayStatus(AutoCreateWorldDisplayState.Creating);
        try
        {
            AutoCreateWorldSettings autoCreate = settings.AutoCreate;
            ApplyTiming(autoCreate);
            TerrariaSaveCleanupResult cleanup = default;
            if (!await RunStepAsync(
                    "save cleanup",
                    _ =>
                    {
                        cleanup = saveCleaner.MoveNonFavoritesToBackup();
                        return Task.FromResult(true);
                    },
                    cancellationToken))
            {
                ShowFailedStatus();
                return;
            }

            Size clientSize = Size.Empty;
            if (!await RunStepAsync(
                    "activate Terraria window",
                    _ =>
                    {
                        if (!window.TryActivate(out Size activatedSize))
                        {
                            AppLogger.Info("Create world automation could not activate Terraria window.");
                            return Task.FromResult(false);
                        }

                        clientSize = activatedSize;
                        return Task.FromResult(true);
                    },
                    cancellationToken))
            {
                ShowFailedStatus();
                return;
            }

            TerrariaMenuGeometry geometry = TerrariaMenuGeometry.From(clientSize);
            if (!await RequireMenuModeAsync("main menu before Single Player", TimeSpan.FromSeconds(1), cancellationToken, 0))
            {
                ShowFailedStatus();
                return;
            }

            if (!await ClickAsync("Single Player", geometry.MainMenuSinglePlayer(), menuActionDelay, cancellationToken))
            {
                ShowFailedStatus();
                return;
            }

            if (!await RequireMenuModeAsync("Single Player", MenuStateTimeout, cancellationToken, 888))
            {
                ShowFailedStatus();
                return;
            }

            Dictionary<string, DateTime> playersBefore = SnapshotSaveFiles("Players", "*.plr");
            if (!await ClickAsync("new player", geometry.SelectMenuNewButton(), menuActionDelay, cancellationToken))
            {
                ShowFailedStatus();
                return;
            }

            if (!await ApplyPlayerTemplateAsync(autoCreate, geometry, cancellationToken))
            {
                ShowFailedStatus();
                return;
            }

            if (!await ApplyPlayerDifficultyAsync(autoCreate.PlayerDifficulty, geometry, cancellationToken))
            {
                ShowFailedStatus();
                return;
            }

            if (!await ClickAsync("create player", geometry.CreatePlayerButton(), menuActionDelay, cancellationToken))
            {
                ShowFailedStatus();
                return;
            }

            if (!await ConfirmPlayerNameAsync(autoCreate.PlayerName, cancellationToken))
            {
                ShowFailedStatus();
                return;
            }

            if (!await WaitForNewOrChangedSaveFileAsync("player file", playersBefore, "Players", "*.plr", PlayerCreateTimeout, cancellationToken))
            {
                ShowFailedStatus();
                return;
            }

            if (!await ObserveMenuModeAsync("player creation return transition", TimeSpan.FromSeconds(2), cancellationToken, 1))
            {
                ShowFailedStatus();
                return;
            }

            if (!await RequireMenuModeAsync("player select after creating player", MenuStateTimeout, cancellationToken, 888))
            {
                ShowFailedStatus();
                return;
            }

            if (!await ClickPlayerAndRequireWorldSelectAsync(geometry, cleanup.FavoritePlayers, cancellationToken))
            {
                ShowFailedStatus();
                return;
            }

            await DelayAsync(menuActionDelay, cancellationToken);

            if (!await ClickAsync("new world", geometry.SelectMenuNewButton(), menuActionDelay, cancellationToken))
            {
                ShowFailedStatus();
                return;
            }

            if (!await ApplyWorldOptionsAsync(autoCreate, geometry, cancellationToken))
            {
                ShowFailedStatus();
                return;
            }

            if (!await RandomizeVisibleSeedAsync(geometry, cancellationToken))
            {
                ShowFailedStatus();
                return;
            }

            if (!await ClickAsync("create world", geometry.CreateWorldButton(), shortActionDelay, cancellationToken))
            {
                ShowFailedStatus();
                return;
            }

            ShowCreatedStatus();
        }
        catch (OperationCanceledException)
        {
            ClearDisplayStatus();
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Create world automation failed.");
            ShowFailedStatus();
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

        AppLogger.Info("Create world automation pressing Escape to apply visible seed.");
        window.PressKey(Keys.Escape);
        await DelayAsync(menuActionDelay, cancellationToken);
        return true;
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
        window.InputPressDurationMilliseconds = settings.InputPressDurationMilliseconds;
    }

    private async Task<bool> ConfirmPlayerNameAsync(string playerName, CancellationToken cancellationToken)
    {
        string normalizedName = playerName.Trim();
        if (normalizedName.Length == 0)
        {
            window.PressKey(Keys.D1);
            await DelayAsync(shortActionDelay, cancellationToken);
            window.PressKey(Keys.Return);
            return true;
        }

        if (!TrySetClipboardTextWithBackup(normalizedName, out string? previousText, out bool hadPreviousText))
        {
            return false;
        }

        try
        {
            window.PressModifiedKey(Keys.ControlKey, Keys.A);
            await DelayAsync(shortActionDelay, cancellationToken);
            window.PressModifiedKey(Keys.ControlKey, Keys.V);
            await DelayAsync(shortActionDelay, cancellationToken);
            window.PressKey(Keys.Return);
            return true;
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
        menuState.Dispose();
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

    private void SetPersistentDisplayStatus(AutoCreateWorldDisplayState state)
    {
        displayState = state;
        displayStateExpiresUtc = null;
    }

    private void ShowFailedStatus()
    {
        displayState = AutoCreateWorldDisplayState.Failed;
        displayStateExpiresUtc = DateTime.UtcNow + FinalStatusDuration;
    }

    private void ShowCreatedStatus()
    {
        displayState = AutoCreateWorldDisplayState.Created;
        displayStateExpiresUtc = DateTime.UtcNow + FinalStatusDuration;
    }

    private void ClearDisplayStatus()
    {
        displayState = null;
        displayStateExpiresUtc = null;
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
        AppLogger.Info($"Create world automation clicking {step} at client ({point.X}, {point.Y}).");
        if (!window.TryClickClient(point.X, point.Y))
        {
            AppLogger.Info($"Create world automation could not click {step} at client ({point.X}, {point.Y}).");
            return false;
        }

        await DelayAsync(delay, cancellationToken);
        return true;
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
        DateTime deadline = DateTime.UtcNow + timeout;
        int? lastMode = null;
        while (DateTime.UtcNow <= deadline)
        {
            if (menuState.TryReadMenuMode(out int mode, expectedModes))
            {
                lastMode = mode;
                if (expectedModes.Contains(mode))
                {
                    return true;
                }
            }

            await DelayAsync(PollInterval, cancellationToken);
        }

        string expected = string.Join(", ", expectedModes);
        AppLogger.Info($"Create world automation {step} did not reach menuMode [{expected}], last read {(lastMode?.ToString() ?? "unavailable")}.");
        return false;
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
        DateTime deadline = DateTime.UtcNow + timeout;
        int? lastMode = null;
        while (DateTime.UtcNow <= deadline)
        {
            if (menuState.TryReadMenuMode(out int mode, expectedModes))
            {
                lastMode = mode;
                if (expectedModes.Contains(mode))
                {
                    return true;
                }
            }

            await DelayAsync(FastPollInterval, cancellationToken);
        }

        string expected = string.Join(", ", expectedModes);
        AppLogger.Info($"Create world automation {step} did not observe menuMode [{expected}], last read {(lastMode?.ToString() ?? "unavailable")}.");
        return false;
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
            Dictionary<string, DateTime> after = SnapshotSaveFiles(directoryName, pattern);
            if (after.Any(pair => !before.TryGetValue(pair.Key, out DateTime previousWriteTime) || pair.Value > previousWriteTime))
            {
                return true;
            }

            await DelayAsync(PollInterval, cancellationToken);
        }

        AppLogger.Info($"Create world automation {step} was not created or updated.");
        return false;
    }

    private static Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);
    }

    private static Dictionary<string, DateTime> SnapshotSaveFiles(string directoryName, string pattern)
    {
        string directory = Path.Combine(GetTerrariaSaveRoot(), directoryName);
        if (!Directory.Exists(directory))
        {
            return new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        }

        return Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
            .ToDictionary(
                path => Path.GetFileName(path),
                File.GetLastWriteTimeUtc,
                StringComparer.OrdinalIgnoreCase);
    }

    private static string GetTerrariaSaveRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games",
            "Terraria");
    }

    private readonly record struct TerrariaMenuGeometry(float Scale, float LogicalWidth, float LogicalHeight)
    {
        public static TerrariaMenuGeometry From(Size clientSize)
        {
            // Terraria's PreDrawMenu scales menu UI up to a logical 900px height unless disabled in config.
            float scale = GetMainMenuScale(clientSize.Height);
            return new TerrariaMenuGeometry(scale, clientSize.Width / scale, clientSize.Height / scale);
        }

        public Point MainMenuSinglePlayer()
        {
            return ToClient(LogicalWidth / 2f, 245f);
        }

        public Point SelectMenuNewButton()
        {
            float outerWidth = GetSelectListOuterWidth();
            return ToClient(LogicalWidth / 2f + outerWidth / 4f + 5f, LogicalHeight - 70f);
        }

        public Point CreatePlayerButton()
        {
            return ToClient(LogicalWidth / 2f + 130f, 534f);
        }

        public Point CharacterClothingCategoryButton()
        {
            return ToClient(LogicalWidth / 2f - 176f, 294f);
        }

        public Point CharacterInfoCategoryButton()
        {
            return ToClient(LogicalWidth / 2f - 224f, 294f);
        }

        public Point CharacterTemplatePasteButton()
        {
            return ToClient(LogicalWidth / 2f + 110f, 475f);
        }

        public Point PlayerDifficultyButton(string playerDifficulty)
        {
            float y = AutoCreatePlayerDifficulty.Normalize(playerDifficulty) switch
            {
                AutoCreatePlayerDifficulty.Journey => 403f,
                AutoCreatePlayerDifficulty.Mediumcore => 458f,
                AutoCreatePlayerDifficulty.Hardcore => 485f,
                _ => 430f
            };
            return ToClient(LogicalWidth / 2f - 146f, y);
        }

        public Point CreateWorldButton()
        {
            return ToClient(LogicalWidth / 2f + 130f, 534f);
        }

        public Point WorldSizeButton(string worldSize)
        {
            float x = AutoCreateWorldSize.Normalize(worldSize) switch
            {
                AutoCreateWorldSize.Small => -164f,
                AutoCreateWorldSize.Large => 164f,
                _ => 0f
            };
            return ToClient(LogicalWidth / 2f + x, 331f);
        }

        public Point WorldDifficultyButton(string worldDifficulty)
        {
            float x = AutoCreateWorldDifficulty.Normalize(worldDifficulty) switch
            {
                AutoCreateWorldDifficulty.Journey => -182f,
                AutoCreateWorldDifficulty.Expert => 61f,
                AutoCreateWorldDifficulty.Master => 182f,
                _ => -61f
            };
            return ToClient(LogicalWidth / 2f + x, 379f);
        }

        public Point WorldEvilButton(string worldEvil)
        {
            float x = AutoCreateWorldEvil.Normalize(worldEvil) switch
            {
                AutoCreateWorldEvil.Corruption => 0f,
                AutoCreateWorldEvil.Crimson => 164f,
                _ => -164f
            };
            return ToClient(LogicalWidth / 2f + x, 427f);
        }

        public Point WorldAdvancedSeedButton()
        {
            return ToClient(LogicalWidth / 2f - 220f, 274f);
        }

        public Point AdvancedSeedRandomizeButton()
        {
            return ToClient(LogicalWidth / 2f - 220f, 230f);
        }

        public Point PlayerPlayButton(int favoritePlayers)
        {
            float outerWidth = GetSelectListOuterWidth();
            float left = LogicalWidth / 2f - outerWidth / 2f;
            float itemTop = 232f + favoritePlayers * 101f;
            return ToClient(left + 33f, itemTop + 79f);
        }

        private Point ToClient(float logicalX, float logicalY)
        {
            return new Point(
                (int)Math.Round(logicalX * Scale),
                (int)Math.Round(logicalY * Scale));
        }

        private float GetSelectListOuterWidth()
        {
            return Math.Min(LogicalWidth * 0.8f, 650f);
        }

        private static float GetMainMenuScale(int clientHeight)
        {
            if (IsMainMenuUpscaleDisabled())
            {
                return 1f;
            }

            return Math.Max(1f, clientHeight / 900f);
        }

        private static bool IsMainMenuUpscaleDisabled()
        {
            try
            {
                string configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "My Games",
                    "Terraria",
                    "config.json");
                if (!File.Exists(configPath))
                {
                    return false;
                }

                using FileStream stream = File.OpenRead(configPath);
                using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(stream);
                return document.RootElement.TryGetProperty("SettingDontScaleMainMenuUp", out System.Text.Json.JsonElement value) &&
                    value.ValueKind == System.Text.Json.JsonValueKind.True;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Failed to read Terraria main menu scale setting.");
                return false;
            }
        }
    }
}
