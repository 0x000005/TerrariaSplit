using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class DebugSettingsPage : SettingsPageBase
{
    private const int RefreshIntervalMilliseconds = 500;
    private const int HeavyRefreshIntervalMilliseconds = 2000;
    private const int SequenceBoxHeight = 220;
    private static readonly Color QuickStatusNormalColor = UiTheme.Accent;
    private static readonly Color QuickStatusProblemColor = Color.FromArgb(225, 92, 88);
    private static readonly Color QuickStatusMenuColor = Color.FromArgb(107, 157, 216);

    public override SettingsPageId Id => SettingsPageId.Debug;

    protected override Control BuildPage(SettingsPageContext context)
    {
        SettingsForm owner = context.Owner;
        Label lastUpdatedValue = CreateValueLabel();
        Label processDetectedValue = CreateValueLabel();
        Label windowDetectedValue = CreateValueLabel();
        Label watcherAttachedValue = CreateValueLabel();
        Label memoryReadyValue = CreateValueLabel();
        Label bossFlagsReadyValue = CreateValueLabel();
        Label gameStateValue = CreateValueLabel();
        Label windowStatusValue = CreateValueLabel();

        Label controlTickValue = CreateValueLabel();
        Label watcherPollValue = CreateValueLabel();
        Label statusPaintValue = CreateValueLabel();
        Label timerPaintValue = CreateValueLabel();

        Label processIdValue = CreateValueLabel();
        Label processStartTimeValue = CreateValueLabel();
        Label processPathValue = CreateValueLabel();
        Label processArchitectureValue = CreateValueLabel();
        Label processVersionValue = CreateValueLabel();
        Label windowHandleValue = CreateValueLabel();
        Label windowTitleValue = CreateValueLabel();
        Label respondingValue = CreateValueLabel();
        Label visibleValue = CreateValueLabel();
        Label minimizedValue = CreateValueLabel();
        Label maximizedValue = CreateValueLabel();
        Label foregroundValue = CreateValueLabel();
        Label windowBoundsValue = CreateValueLabel();
        Label clientSizeValue = CreateValueLabel();
        Label menuScaleValue = CreateValueLabel();
        Label logicalMenuSizeValue = CreateValueLabel();

        Label playerFilesValue = CreateValueLabel();
        Label worldFilesValue = CreateValueLabel();
        Label favoritePlayersValue = CreateValueLabel();
        Label favoriteWorldsValue = CreateValueLabel();
        Label playerNameValue = CreateValueLabel();
        Label playerDifficultyValue = CreateValueLabel();
        Label worldSizeValue = CreateValueLabel();
        Label worldDifficultyValue = CreateValueLabel();
        Label worldEvilValue = CreateValueLabel();
        Label shortActionDelayValue = CreateValueLabel();
        Label menuActionDelayValue = CreateValueLabel();
        Label windowActivationDelayValue = CreateValueLabel();
        Label clickFocusDelayValue = CreateValueLabel();
        Label inputPressDurationValue = CreateValueLabel();
        TextBox autoCreateSequenceValue = CreateMultilineValueBox(SequenceBoxHeight);

        Label skeletronValue = CreateValueLabel();
        Label wallOfFleshValue = CreateValueLabel();
        Label destroyerValue = CreateValueLabel();
        Label twinsValue = CreateValueLabel();
        Label skeletronPrimeValue = CreateValueLabel();
        Label planteraValue = CreateValueLabel();
        Label golemValue = CreateValueLabel();
        Label lunaticCultistValue = CreateValueLabel();
        Label moonLordValue = CreateValueLabel();

        Label currentPassValue = CreateValueLabel();
        Label progressMessageValue = CreateValueLabel();
        Label currentProgressValue = CreateValueLabel();
        Label totalProgressValue = CreateValueLabel();

        Label scanAttemptsValue = CreateValueLabel();
        Label lastScanValue = CreateValueLabel();
        Label scanPageStatsValue = CreateValueLabel();
        Label scanFailuresValue = CreateValueLabel();
        Label mainModuleBaseValue = CreateValueLabel();
        Label mainModuleSizeValue = CreateValueLabel();
        Label updateTimeAddressValue = CreateValueLabel();
        Label bossFlagsAddressValue = CreateValueLabel();
        Label hardmodeAddressValue = CreateValueLabel();
        Label generationProgressAddressValue = CreateValueLabel();
        Label generationControllerAddressValue = CreateValueLabel();
        Label failureStageValue = CreateValueLabel();

        Button copyAllButton = CreateActionButton(owner, "Copy all information");

        var savePreparation = new TerrariaSavePreparation();
        TerrariaWindowSnapshot latestWindow = default;
        TerrariaWatchSnapshot latestSnapshot = default;
        TerrariaWatcherDiagnostics latestDiagnostics = TerrariaWatcherDiagnosticsDefaults.Empty;
        TerrariaSaveInventorySnapshot latestInventory = default;
        DateTime nextHeavyRefreshUtc = DateTime.MinValue;
        int heavyRefreshQueued = 0;

        Control page = context.BuildScrollPage(content =>
        {
            FlowLayoutPanel actionBar = CreateActionBar(owner);
            actionBar.Controls.Add(copyAllButton);
            AddSection(content, actionBar);

            TableLayoutPanel overviewSection = CreateSection(owner, "Quick Status");
            TableLayoutPanel overviewGrid = CreateGrid(owner);
            AddValueRow(overviewGrid, owner, "Terraria process", processDetectedValue);
            AddValueRow(overviewGrid, owner, "Window", windowDetectedValue);
            AddValueRow(overviewGrid, owner, "Window status", windowStatusValue);
            AddValueRow(overviewGrid, owner, "Watcher attached", watcherAttachedValue);
            AddValueRow(overviewGrid, owner, "Memory ready", memoryReadyValue);
            AddValueRow(overviewGrid, owner, "Boss flags ready", bossFlagsReadyValue);
            AddValueRow(overviewGrid, owner, "Game state", gameStateValue);
            AddValueRow(overviewGrid, owner, "Last updated", lastUpdatedValue);
            AddSectionControl(overviewSection, overviewGrid);
            AddSection(content, overviewSection);

            TableLayoutPanel performanceSection = CreateSection(owner, "Performance");
            TableLayoutPanel performanceGrid = CreateGrid(owner);
            AddValueRow(performanceGrid, owner, "Sampling frequency", watcherPollValue);
            AddValueRow(performanceGrid, owner, "Control frequency", controlTickValue);
            AddValueRow(performanceGrid, owner, "Split timer refresh rate", statusPaintValue);
            AddValueRow(performanceGrid, owner, "Main timer refresh rate", timerPaintValue);
            AddSectionControl(performanceSection, performanceGrid);
            AddSection(content, performanceSection);

            TableLayoutPanel windowSection = CreateSection(owner, "Window & Coordinates");
            TableLayoutPanel windowGrid = CreateGrid(owner);
            AddValueRow(windowGrid, owner, "PID", processIdValue);
            AddValueRow(windowGrid, owner, "Start time", processStartTimeValue);
            AddValueRow(windowGrid, owner, "Process path", processPathValue);
            AddValueRow(windowGrid, owner, "Process architecture", processArchitectureValue);
            AddValueRow(windowGrid, owner, "Process version", processVersionValue);
            AddValueRow(windowGrid, owner, "Window handle", windowHandleValue);
            AddValueRow(windowGrid, owner, "Window title", windowTitleValue);
            AddValueRow(windowGrid, owner, "Responding", respondingValue);
            AddValueRow(windowGrid, owner, "Visible", visibleValue);
            AddValueRow(windowGrid, owner, "Minimized", minimizedValue);
            AddValueRow(windowGrid, owner, "Maximized", maximizedValue);
            AddValueRow(windowGrid, owner, "Foreground", foregroundValue);
            AddValueRow(windowGrid, owner, "Window bounds", windowBoundsValue);
            AddValueRow(windowGrid, owner, "Client size", clientSizeValue);
            AddValueRow(windowGrid, owner, "Menu scale", menuScaleValue);
            AddValueRow(windowGrid, owner, "Logical menu size", logicalMenuSizeValue);
            AddSectionControl(windowSection, windowGrid);
            AddSection(content, windowSection);

            TableLayoutPanel automationSection = CreateSection(owner, "Auto Create Route");
            TableLayoutPanel automationGrid = CreateGrid(owner);
            AddValueRow(automationGrid, owner, "Player files", playerFilesValue);
            AddValueRow(automationGrid, owner, "World files", worldFilesValue);
            AddValueRow(automationGrid, owner, "Favorite players", favoritePlayersValue);
            AddValueRow(automationGrid, owner, "Favorite worlds", favoriteWorldsValue);
            AddValueRow(automationGrid, owner, "Player name", playerNameValue);
            AddValueRow(automationGrid, owner, "Player difficulty", playerDifficultyValue);
            AddValueRow(automationGrid, owner, "World size", worldSizeValue);
            AddValueRow(automationGrid, owner, "World difficulty", worldDifficultyValue);
            AddValueRow(automationGrid, owner, "World evil", worldEvilValue);
            AddValueRow(automationGrid, owner, "Short action delay ms", shortActionDelayValue);
            AddValueRow(automationGrid, owner, "Menu action delay ms", menuActionDelayValue);
            AddValueRow(automationGrid, owner, "Window activation wait ms", windowActivationDelayValue);
            AddValueRow(automationGrid, owner, "Click focus wait ms", clickFocusDelayValue);
            AddValueRow(automationGrid, owner, "Mouse / key press ms", inputPressDurationValue);
            AddSectionControl(automationSection, automationGrid);
            AddSectionControl(automationSection, CreateMutedLabel(owner, "Click sequence"));
            AddSectionControl(automationSection, autoCreateSequenceValue);
            AddSection(content, automationSection);

            TableLayoutPanel bossSection = CreateSection(owner, "Boss Progress");
            TableLayoutPanel bossGrid = CreateGrid(owner);
            AddValueRow(bossGrid, owner, "Skeletron", skeletronValue);
            AddValueRow(bossGrid, owner, "Wall of Flesh", wallOfFleshValue);
            AddValueRow(bossGrid, owner, "Destroyer", destroyerValue);
            AddValueRow(bossGrid, owner, "The Twins", twinsValue);
            AddValueRow(bossGrid, owner, "Skeletron Prime", skeletronPrimeValue);
            AddValueRow(bossGrid, owner, "Plantera", planteraValue);
            AddValueRow(bossGrid, owner, "Golem", golemValue);
            AddValueRow(bossGrid, owner, "Lunatic Cultist", lunaticCultistValue);
            AddValueRow(bossGrid, owner, "Moon Lord", moonLordValue);
            AddSectionControl(bossSection, bossGrid);
            AddSection(content, bossSection);

            TableLayoutPanel worldGenerationSection = CreateSection(owner, "World Generation");
            TableLayoutPanel worldGenerationGrid = CreateGrid(owner);
            AddValueRow(worldGenerationGrid, owner, "Current pass", currentPassValue);
            AddValueRow(worldGenerationGrid, owner, "Progress message", progressMessageValue);
            AddValueRow(worldGenerationGrid, owner, "Current progress", currentProgressValue);
            AddValueRow(worldGenerationGrid, owner, "Total progress", totalProgressValue);
            AddSectionControl(worldGenerationSection, worldGenerationGrid);
            AddSection(content, worldGenerationSection);

            TableLayoutPanel memorySection = CreateSection(owner, "Memory & Signatures");
            TableLayoutPanel memoryGrid = CreateGrid(owner);
            AddValueRow(memoryGrid, owner, "Scan attempts", scanAttemptsValue);
            AddValueRow(memoryGrid, owner, "Last scan", lastScanValue);
            AddValueRow(memoryGrid, owner, "Scan page stats", scanPageStatsValue);
            AddValueRow(memoryGrid, owner, "Scan failures", scanFailuresValue);
            AddValueRow(memoryGrid, owner, "Main module base", mainModuleBaseValue);
            AddValueRow(memoryGrid, owner, "Main module size", mainModuleSizeValue);
            AddValueRow(memoryGrid, owner, "UpdateTime address", updateTimeAddressValue);
            AddValueRow(memoryGrid, owner, "Boss flags address", bossFlagsAddressValue);
            AddValueRow(memoryGrid, owner, "Hardmode address", hardmodeAddressValue);
            AddValueRow(memoryGrid, owner, "Generation progress address", generationProgressAddressValue);
            AddValueRow(memoryGrid, owner, "Generation controller address", generationControllerAddressValue);
            AddValueRow(memoryGrid, owner, "Failure stage", failureStageValue);
            AddSectionControl(memorySection, memoryGrid);
            AddSection(content, memorySection);
        });

        copyAllButton.Click += (_, _) => CopyAllInformation();

        void Refresh(bool forceHeavyRefresh = false)
        {
            if (!page.Visible)
            {
                return;
            }

            RequestHeavyRefresh(forceHeavyRefresh);
            RuntimeDebugSnapshot debugSnapshot = owner.GetRuntimeDebugSnapshot();
            TerrariaWindowSnapshot window = latestWindow;
            TerrariaWatchSnapshot snapshot = debugSnapshot.WatchSnapshot;
            TerrariaWatcherDiagnostics diagnostics = debugSnapshot.WatcherDiagnostics;
            TerrariaSaveInventorySnapshot inventory = latestInventory;
            AutoCreateWorldSettings autoCreate = owner.Result.AutoCreate;
            latestWindow = window;
            latestSnapshot = snapshot;
            latestDiagnostics = diagnostics;
            latestInventory = inventory;

            page.SuspendLayout();
            try
            {
                bool bossFlagsReady = HasAnyBossState(snapshot.BossStates);

                SetValue(
                    lastUpdatedValue,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    UiTheme.MutedText);
                SetQuickBool(processDetectedValue, owner, window.HasProcess);
                SetQuickBool(windowDetectedValue, owner, window.HasWindow);
                SetQuickBool(watcherAttachedValue, owner, snapshot.IsAttached);
                SetQuickBool(memoryReadyValue, owner, snapshot.IsReady);
                SetQuickBool(bossFlagsReadyValue, owner, bossFlagsReady);
                SetQuickGameState(gameStateValue, owner, snapshot.IsGameMenu);
                SetQuickStatus(windowStatusValue, window.Status, owner);

                SetValue(
                    controlTickValue,
                    FormatControlTickSummary(debugSnapshot, owner));
                SetValue(
                    watcherPollValue,
                    FormatWatcherPollSummary(debugSnapshot, owner));
                SetValue(
                    statusPaintValue,
                    FormatStatusPaintSummary(debugSnapshot, owner));
                SetValue(
                    timerPaintValue,
                    FormatTimerPaintSummary(debugSnapshot, owner));
                SetValue(processIdValue, FormatProcessId(window.ProcessId, owner));
                SetValue(processStartTimeValue, FormatDateTime(window.ProcessStartTime, owner));
                SetValue(processPathValue, FormatText(diagnostics.ProcessPath, owner));
                SetValue(processArchitectureValue, FormatText(diagnostics.ProcessArchitecture, owner));
                SetValue(processVersionValue, FormatText(diagnostics.ProcessVersion, owner));
                SetValue(windowHandleValue, window.HasWindow ? $"0x{window.WindowHandle.ToInt64():X}" : owner.Localize("Unknown"));
                SetValue(windowTitleValue, string.IsNullOrWhiteSpace(window.WindowTitle) ? owner.Localize("Unknown") : window.WindowTitle);
                SetOptionalBool(respondingValue, owner, window.HasProcess ? window.IsResponding : null);
                SetOptionalBool(visibleValue, owner, window.HasWindow ? window.IsVisible : null);
                SetOptionalBool(minimizedValue, owner, window.HasWindow ? window.IsMinimized : null);
                SetOptionalBool(maximizedValue, owner, window.HasWindow ? window.IsMaximized : null);
                SetOptionalBool(foregroundValue, owner, window.HasWindow ? window.IsForeground : null);
                SetValue(windowBoundsValue, FormatBounds(window.WindowBounds, owner));
                SetValue(clientSizeValue, FormatSize(window.ClientSize, owner));

                if (TryCreateGeometry(window.ClientSize, out TerrariaMenuGeometry geometry))
                {
                    SetValue(menuScaleValue, FormatScale(geometry.Scale));
                    SetValue(logicalMenuSizeValue, FormatLogicalSize(geometry));
                    SetSequenceText(autoCreateSequenceValue, BuildAutoCreateSequenceText(autoCreate, geometry, inventory.FavoritePlayers, owner));
                }
                else
                {
                    SetValue(menuScaleValue, owner.Localize("Unknown"));
                    SetValue(logicalMenuSizeValue, owner.Localize("Unknown"));
                    SetSequenceText(autoCreateSequenceValue, owner.Localize("Unavailable because client size is unknown."));
                }

                SetValue(playerFilesValue, inventory.PlayerFiles.ToString(CultureInfo.InvariantCulture));
                SetValue(worldFilesValue, inventory.WorldFiles.ToString(CultureInfo.InvariantCulture));
                SetValue(favoritePlayersValue, inventory.FavoritePlayers.ToString(CultureInfo.InvariantCulture));
                SetValue(favoriteWorldsValue, inventory.FavoriteWorlds.ToString(CultureInfo.InvariantCulture));
                SetValue(playerNameValue, FormatPlayerName(autoCreate.PlayerName));
                SetValue(playerDifficultyValue, owner.Localize(AutoCreatePlayerDifficulty.Normalize(autoCreate.PlayerDifficulty)));
                SetValue(worldSizeValue, owner.Localize(AutoCreateWorldSize.Normalize(autoCreate.WorldSize)));
                SetValue(worldDifficultyValue, owner.Localize(AutoCreateWorldDifficulty.Normalize(autoCreate.WorldDifficulty)));
                SetValue(worldEvilValue, owner.Localize(AutoCreateWorldEvil.Normalize(autoCreate.WorldEvil)));
                SetValue(shortActionDelayValue, autoCreate.ShortActionDelayMilliseconds.ToString(CultureInfo.InvariantCulture));
                SetValue(menuActionDelayValue, autoCreate.MenuActionDelayMilliseconds.ToString(CultureInfo.InvariantCulture));
                SetValue(windowActivationDelayValue, autoCreate.WindowActivationDelayMilliseconds.ToString(CultureInfo.InvariantCulture));
                SetValue(clickFocusDelayValue, autoCreate.ClickFocusDelayMilliseconds.ToString(CultureInfo.InvariantCulture));
                SetValue(inputPressDurationValue, autoCreate.InputPressDurationMilliseconds.ToString(CultureInfo.InvariantCulture));

                SetBossState(skeletronValue, snapshot.BossStates.Skeletron, owner);
                SetBossState(wallOfFleshValue, snapshot.BossStates.WallOfFlesh, owner);
                SetBossState(destroyerValue, snapshot.BossStates.Destroyer, owner);
                SetBossState(twinsValue, snapshot.BossStates.Twins, owner);
                SetBossState(skeletronPrimeValue, snapshot.BossStates.SkeletronPrime, owner);
                SetBossState(planteraValue, snapshot.BossStates.Plantera, owner);
                SetBossState(golemValue, snapshot.BossStates.Golem, owner);
                SetBossState(lunaticCultistValue, snapshot.BossStates.LunaticCultist, owner);
                SetBossState(moonLordValue, snapshot.BossStates.MoonLord, owner);

                SetValue(
                    currentPassValue,
                    FormatWorldGenerationText(
                        snapshot.WorldGeneration.CurrentPassName,
                        diagnostics.CurrentControllerAddress,
                        owner));
                SetValue(
                    progressMessageValue,
                    FormatWorldGenerationText(
                        snapshot.WorldGeneration.ProgressMessage,
                        diagnostics.CurrentGenerationProgressAddress,
                        owner));
                SetValue(
                    currentProgressValue,
                    FormatWorldGenerationPercent(
                        snapshot.WorldGeneration.CurrentProgress,
                        diagnostics.CurrentGenerationProgressAddress,
                        owner));
                SetValue(
                    totalProgressValue,
                    FormatWorldGenerationPercent(
                        snapshot.WorldGeneration.TotalProgress,
                        diagnostics.CurrentGenerationProgressAddress,
                        owner));

                SetValue(scanAttemptsValue, diagnostics.SignatureScanAttempts.ToString(CultureInfo.InvariantCulture));
                SetValue(lastScanValue, FormatTimestamp(diagnostics.LastSignatureScanUtc, owner));
                SetValue(scanPageStatsValue, FormatScanStats(diagnostics.LastSignatureScan, owner));
                SetValue(scanFailuresValue, FormatScanFailures(diagnostics.LastSignatureScan, owner));
                SetValue(mainModuleBaseValue, FormatAddress(diagnostics.MainModuleBaseAddress, owner));
                SetValue(mainModuleSizeValue, FormatByteCount(diagnostics.MainModuleSize, owner));
                SetValue(updateTimeAddressValue, FormatAddress(diagnostics.UpdateTimeAddress, owner));
                SetValue(bossFlagsAddressValue, FormatAddress(diagnostics.BossFlagsBaseAddress, owner));
                SetValue(hardmodeAddressValue, FormatAddress(diagnostics.HardmodeAddress, owner));
                SetValue(generationProgressAddressValue, FormatAddress(diagnostics.CurrentGenerationProgressAddress, owner));
                SetValue(generationControllerAddressValue, FormatAddress(diagnostics.CurrentControllerAddress, owner));
                SetValue(failureStageValue, LocalizeStage(diagnostics.Stage, owner));
            }
            finally
            {
                page.ResumeLayout(false);
            }
        }

        void RequestHeavyRefresh(bool force = false)
        {
            DateTime nowUtc = DateTime.UtcNow;
            if (!force && nowUtc < nextHeavyRefreshUtc)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref heavyRefreshQueued, 1, 0) != 0)
            {
                return;
            }

            nextHeavyRefreshUtc = nowUtc.AddMilliseconds(HeavyRefreshIntervalMilliseconds);
            _ = Task.Run(() => (
                    Window: TerrariaWindowProbe.Read(),
                    Inventory: savePreparation.ReadInventorySnapshot()))
                .ContinueWith(
                    task =>
                    {
                        Interlocked.Exchange(ref heavyRefreshQueued, 0);
                        if (page.IsDisposed)
                        {
                            return;
                        }

                        void ApplyHeavySnapshot()
                        {
                            if (task.IsCompletedSuccessfully)
                            {
                                latestWindow = task.Result.Window;
                                latestInventory = task.Result.Inventory;
                                Refresh();
                                return;
                            }

                            if (task.Exception is not null)
                            {
                                AppLogger.Error(task.Exception, "Failed to refresh debug page heavy snapshot.");
                            }
                        }

                        try
                        {
                            if (owner.IsHandleCreated)
                            {
                                owner.BeginInvoke((Action)ApplyHeavySnapshot);
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    },
                    TaskScheduler.Default);
        }

        void CopyAllInformation()
        {
            RuntimeDebugSnapshot debugSnapshot = owner.GetRuntimeDebugSnapshot();
            latestSnapshot = debugSnapshot.WatchSnapshot;
            latestDiagnostics = debugSnapshot.WatcherDiagnostics;
            latestWindow = TerrariaWindowProbe.Read();
            latestInventory = savePreparation.ReadInventorySnapshot();

            try
            {
                Clipboard.SetText(BuildDiagnosticReport(latestWindow, debugSnapshot, latestDiagnostics, latestInventory, owner.Result.AutoCreate, owner));
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Failed to copy Terraria debug information.");
            }
        }

        var refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = RefreshIntervalMilliseconds
        };
        refreshTimer.Tick += (_, _) => Refresh();

        page.VisibleChanged += (_, _) =>
        {
            if (page.Visible)
            {
                Refresh();
                refreshTimer.Start();
            }
            else
            {
                refreshTimer.Stop();
            }
        };

        page.Disposed += (_, _) =>
        {
            refreshTimer.Stop();
            refreshTimer.Dispose();
        };

        Refresh();
        if (page.Visible)
        {
            refreshTimer.Start();
        }
        return page;
    }

    private static string BuildDiagnosticReport(
        TerrariaWindowSnapshot window,
        RuntimeDebugSnapshot debugSnapshot,
        TerrariaWatcherDiagnostics diagnostics,
        TerrariaSaveInventorySnapshot inventory,
        AutoCreateWorldSettings autoCreate,
        SettingsForm owner)
    {
        var lines = new List<string>();
        TerrariaWatchSnapshot snapshot = debugSnapshot.WatchSnapshot;
        bool bossFlagsReady = HasAnyBossState(snapshot.BossStates);

        AppendReportSection(
            lines,
            owner,
            "Quick Status",
            [
                ("Terraria process", FormatBool(window.HasProcess, owner)),
                ("Window", FormatBool(window.HasWindow, owner)),
                ("Window status", LocalizeStatus(window.Status, owner)),
                ("Watcher attached", FormatBool(snapshot.IsAttached, owner)),
                ("Memory ready", FormatBool(snapshot.IsReady, owner)),
                ("Boss flags ready", FormatBool(bossFlagsReady, owner)),
                ("Game state", owner.Localize(FormatGameState(snapshot.IsGameMenu))),
                ("Last updated", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
            ]);

        AppendReportSection(
            lines,
            owner,
            "Performance",
            [
                ("Sampling frequency", FormatWatcherPollSummary(debugSnapshot, owner)),
                ("Control frequency", FormatControlTickSummary(debugSnapshot, owner)),
                ("Split timer refresh rate", FormatStatusPaintSummary(debugSnapshot, owner)),
                ("Main timer refresh rate", FormatTimerPaintSummary(debugSnapshot, owner))
            ]);

        string menuScale = owner.Localize("Unknown");
        string logicalMenuSize = owner.Localize("Unknown");
        string autoCreateSequence = owner.Localize("Unavailable because client size is unknown.");
        if (TryCreateGeometry(window.ClientSize, out TerrariaMenuGeometry geometry))
        {
            menuScale = FormatScale(geometry.Scale);
            logicalMenuSize = FormatLogicalSize(geometry);
            autoCreateSequence = BuildAutoCreateSequenceText(autoCreate, geometry, inventory.FavoritePlayers, owner);
        }

        AppendReportSection(
            lines,
            owner,
            "Window & Coordinates",
            [
                ("PID", FormatProcessId(window.ProcessId, owner)),
                ("Start time", FormatDateTime(window.ProcessStartTime, owner)),
                ("Process path", FormatText(diagnostics.ProcessPath, owner)),
                ("Process architecture", FormatText(diagnostics.ProcessArchitecture, owner)),
                ("Process version", FormatText(diagnostics.ProcessVersion, owner)),
                ("Window handle", window.HasWindow ? $"0x{window.WindowHandle.ToInt64():X}" : owner.Localize("Unknown")),
                ("Window title", string.IsNullOrWhiteSpace(window.WindowTitle) ? owner.Localize("Unknown") : window.WindowTitle),
                ("Responding", window.HasProcess ? FormatBool(window.IsResponding, owner) : owner.Localize("Unknown")),
                ("Visible", window.HasWindow ? FormatBool(window.IsVisible, owner) : owner.Localize("Unknown")),
                ("Minimized", window.HasWindow ? FormatBool(window.IsMinimized, owner) : owner.Localize("Unknown")),
                ("Maximized", window.HasWindow ? FormatBool(window.IsMaximized, owner) : owner.Localize("Unknown")),
                ("Foreground", window.HasWindow ? FormatBool(window.IsForeground, owner) : owner.Localize("Unknown")),
                ("Window bounds", FormatBounds(window.WindowBounds, owner)),
                ("Client size", FormatSize(window.ClientSize, owner)),
                ("Menu scale", menuScale),
                ("Logical menu size", logicalMenuSize)
            ]);

        AppendReportSection(
            lines,
            owner,
            "Auto Create Route",
            [
                ("Player files", inventory.PlayerFiles.ToString(CultureInfo.InvariantCulture)),
                ("World files", inventory.WorldFiles.ToString(CultureInfo.InvariantCulture)),
                ("Favorite players", inventory.FavoritePlayers.ToString(CultureInfo.InvariantCulture)),
                ("Favorite worlds", inventory.FavoriteWorlds.ToString(CultureInfo.InvariantCulture)),
                ("Player name", FormatPlayerName(autoCreate.PlayerName)),
                ("Player difficulty", owner.Localize(AutoCreatePlayerDifficulty.Normalize(autoCreate.PlayerDifficulty))),
                ("World size", owner.Localize(AutoCreateWorldSize.Normalize(autoCreate.WorldSize))),
                ("World difficulty", owner.Localize(AutoCreateWorldDifficulty.Normalize(autoCreate.WorldDifficulty))),
                ("World evil", owner.Localize(AutoCreateWorldEvil.Normalize(autoCreate.WorldEvil))),
                ("Catch stars", FormatBool(autoCreate.EnableZenithStarCatch, owner)),
                ("Catch stars through", owner.Localize(AutoCreateZenithStarCatchStage.Normalize(autoCreate.ZenithStarCatchStopStage))),
                ("Catch speed", AutoCreateZenithStarCatchSpeed.FormatMultiplier(autoCreate.ZenithStarCatchSpeedSliderValue)),
                ("Filter pyramid", FormatBool(autoCreate.EnablePyramidFilter, owner)),
                ("Short action delay ms", autoCreate.ShortActionDelayMilliseconds.ToString(CultureInfo.InvariantCulture)),
                ("Menu action delay ms", autoCreate.MenuActionDelayMilliseconds.ToString(CultureInfo.InvariantCulture)),
                ("Window activation wait ms", autoCreate.WindowActivationDelayMilliseconds.ToString(CultureInfo.InvariantCulture)),
                ("Click focus wait ms", autoCreate.ClickFocusDelayMilliseconds.ToString(CultureInfo.InvariantCulture)),
                ("Mouse / key press ms", autoCreate.InputPressDurationMilliseconds.ToString(CultureInfo.InvariantCulture))
            ]);

        AppendMultilineSection(lines, owner, "Click sequence", autoCreateSequence);

        AppendReportSection(
            lines,
            owner,
            "Boss Progress",
            [
                ("Skeletron", FormatOptionalBool(snapshot.BossStates.Skeletron, owner)),
                ("Wall of Flesh", FormatOptionalBool(snapshot.BossStates.WallOfFlesh, owner)),
                ("Destroyer", FormatOptionalBool(snapshot.BossStates.Destroyer, owner)),
                ("The Twins", FormatOptionalBool(snapshot.BossStates.Twins, owner)),
                ("Skeletron Prime", FormatOptionalBool(snapshot.BossStates.SkeletronPrime, owner)),
                ("Plantera", FormatOptionalBool(snapshot.BossStates.Plantera, owner)),
                ("Golem", FormatOptionalBool(snapshot.BossStates.Golem, owner)),
                ("Lunatic Cultist", FormatOptionalBool(snapshot.BossStates.LunaticCultist, owner)),
                ("Moon Lord", FormatOptionalBool(snapshot.BossStates.MoonLord, owner))
            ]);

        AppendReportSection(
            lines,
            owner,
            "World Generation",
            [
                ("Current pass", FormatWorldGenerationText(snapshot.WorldGeneration.CurrentPassName, diagnostics.CurrentControllerAddress, owner)),
                ("Progress message", FormatWorldGenerationText(snapshot.WorldGeneration.ProgressMessage, diagnostics.CurrentGenerationProgressAddress, owner)),
                ("Current progress", FormatWorldGenerationPercent(snapshot.WorldGeneration.CurrentProgress, diagnostics.CurrentGenerationProgressAddress, owner)),
                ("Total progress", FormatWorldGenerationPercent(snapshot.WorldGeneration.TotalProgress, diagnostics.CurrentGenerationProgressAddress, owner))
            ]);

        AppendReportSection(
            lines,
            owner,
            "Memory & Signatures",
            [
                ("Scan attempts", diagnostics.SignatureScanAttempts.ToString(CultureInfo.InvariantCulture)),
                ("Last scan", FormatTimestamp(diagnostics.LastSignatureScanUtc, owner)),
                ("Scan page stats", FormatScanStats(diagnostics.LastSignatureScan, owner)),
                ("Scan failures", FormatScanFailures(diagnostics.LastSignatureScan, owner)),
                ("Main module base", FormatAddress(diagnostics.MainModuleBaseAddress, owner)),
                ("Main module size", FormatByteCount(diagnostics.MainModuleSize, owner)),
                ("UpdateTime address", FormatAddress(diagnostics.UpdateTimeAddress, owner)),
                ("Boss flags address", FormatAddress(diagnostics.BossFlagsBaseAddress, owner)),
                ("Hardmode address", FormatAddress(diagnostics.HardmodeAddress, owner)),
                ("Generation progress address", FormatAddress(diagnostics.CurrentGenerationProgressAddress, owner)),
                ("Generation controller address", FormatAddress(diagnostics.CurrentControllerAddress, owner)),
                ("Failure stage", LocalizeStage(diagnostics.Stage, owner))
            ]);

        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendReportSection(
        List<string> lines,
        SettingsForm owner,
        string title,
        params (string Label, string Value)[] rows)
    {
        if (lines.Count > 0)
        {
            lines.Add(string.Empty);
        }

        lines.Add(owner.Localize(title));
        foreach ((string label, string value) in rows)
        {
            lines.Add($"{owner.Localize(label)}: {value}");
        }
    }

    private static void AppendMultilineSection(List<string> lines, SettingsForm owner, string title, string content)
    {
        if (lines.Count > 0)
        {
            lines.Add(string.Empty);
        }

        lines.Add(owner.Localize(title));
        lines.AddRange(content.Split([Environment.NewLine], StringSplitOptions.None));
    }

    private static string BuildAutoCreateSequenceText(
        AutoCreateWorldSettings autoCreate,
        TerrariaMenuGeometry geometry,
        int favoritePlayers,
        SettingsForm owner)
    {
        var lines = new List<string>();
        int step = 1;

        AppendSequenceStep(lines, owner, ref step, "Single Player", geometry.MainMenuSinglePlayer());
        AppendSequenceStep(lines, owner, ref step, "New Player", geometry.SelectMenuNewButton());

        if (!string.IsNullOrWhiteSpace(autoCreate.PlayerTemplateCode))
        {
            AppendSequenceStep(lines, owner, ref step, "Character Clothing Tab", geometry.CharacterClothingCategoryButton());
            AppendSequenceStep(lines, owner, ref step, "Paste Player Template", geometry.CharacterTemplatePasteButton());
        }

        string normalizedPlayerDifficulty = AutoCreatePlayerDifficulty.Normalize(autoCreate.PlayerDifficulty);
        if (!string.Equals(normalizedPlayerDifficulty, AutoCreatePlayerDifficulty.Softcore, StringComparison.OrdinalIgnoreCase))
        {
            AppendSequenceStep(lines, owner, ref step, "Character Info Tab", geometry.CharacterInfoCategoryButton());
            AppendSequenceStep(
                lines,
                owner,
                ref step,
                "Player difficulty",
                geometry.PlayerDifficultyButton(normalizedPlayerDifficulty),
                owner.Localize(normalizedPlayerDifficulty));
        }

        AppendSequenceStep(lines, owner, ref step, "Create Player", geometry.CreatePlayerButton());
        AppendSequenceStep(lines, owner, ref step, "Select Created Player", geometry.PlayerPlayButton(favoritePlayers));
        AppendSequenceStep(lines, owner, ref step, "New World", geometry.SelectMenuNewButton());

        bool usesPooledSeed = UsesPooledSeedPath(autoCreate, owner);
        if (!usesPooledSeed)
        {
            string normalizedWorldSize = AutoCreateWorldSize.Normalize(autoCreate.WorldSize);
            AppendSequenceStep(
                lines,
                owner,
                ref step,
                "World size",
                geometry.WorldSizeButton(normalizedWorldSize),
                owner.Localize(normalizedWorldSize));

            string normalizedWorldDifficulty = AutoCreateWorldDifficulty.Normalize(autoCreate.WorldDifficulty);
            AppendSequenceStep(
                lines,
                owner,
                ref step,
                "World difficulty",
                geometry.WorldDifficultyButton(normalizedWorldDifficulty),
                owner.Localize(normalizedWorldDifficulty));

            string normalizedWorldEvil = AutoCreateWorldEvil.Normalize(autoCreate.WorldEvil);
            AppendSequenceStep(
                lines,
                owner,
                ref step,
                "World evil",
                geometry.WorldEvilButton(normalizedWorldEvil),
                owner.Localize(normalizedWorldEvil));
        }

        AppendSequenceStep(lines, owner, ref step, "Advanced Seed", geometry.WorldAdvancedSeedButton());

        if (usesPooledSeed)
        {
            AppendSequenceStep(lines, owner, ref step, "Pooled seed", geometry.AdvancedSeedTextButton());
            AppendSequenceStep(lines, owner, ref step, "Submit World Seed", geometry.VirtualKeyboardSubmitButton());
            AppendSequenceStep(
                lines,
                owner,
                ref step,
                "Apply pooled seed",
                geometry.WorldAdvancedApplyButton());
        }
        else
        {
            foreach (string specialSeed in AutoCreateSpecialWorldSeed.ParseList(autoCreate.SpecialSeeds))
            {
                AppendSequenceStep(
                    lines,
                    owner,
                    ref step,
                    "Special seeds",
                    geometry.AdvancedSpecialSeedButton(specialSeed),
                    owner.Localize(specialSeed));
            }

            string secretSeeds = autoCreate.SecretSeeds?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(secretSeeds))
            {
                AppendSequenceStep(lines, owner, ref step, "Secret seeds", geometry.AdvancedSeedTextButton(), secretSeeds);
                AppendSequenceStep(lines, owner, ref step, "Submit World Seed", geometry.VirtualKeyboardSubmitButton());
            }

            AppendSequenceStep(lines, owner, ref step, "Randomize Visible Seed", geometry.AdvancedSeedRandomizeButton());
            AppendSequenceStep(lines, owner, ref step, "Apply visible seed", geometry.WorldAdvancedApplyButton());
        }

        AppendSequenceStep(lines, owner, ref step, "Create World", geometry.CreateWorldButton());

        if (AutoCreateSpecialWorldSeed.ParseList(autoCreate.SpecialSeeds)
                .Contains(AutoCreateSpecialWorldSeed.Zenith, StringComparer.OrdinalIgnoreCase) &&
            autoCreate.EnableZenithStarCatch)
        {
            lines.Add(
                $"{step++}. {owner.Localize("Catch stars through")}: " +
                owner.Localize(AutoCreateZenithStarCatchStage.Normalize(autoCreate.ZenithStarCatchStopStage)));
            lines.Add(
                $"{step++}. {owner.Localize("Catch speed")}: " +
                AutoCreateZenithStarCatchSpeed.FormatMultiplier(autoCreate.ZenithStarCatchSpeedSliderValue));
        }

        if (autoCreate.EnablePyramidFilter && !usesPooledSeed)
        {
            lines.Add($"{step++}. {owner.Localize("Filter pyramid")}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static bool UsesPooledSeedPath(AutoCreateWorldSettings autoCreate, SettingsForm owner)
    {
        return autoCreate.EnablePyramidFilter &&
            autoCreate.EnableSeedPool &&
            SeedPoolSupport.IsSupported(autoCreate) &&
            owner.GetSeedPoolCount(autoCreate) > 0;
    }

    private static void AppendSequenceStep(
        List<string> lines,
        SettingsForm owner,
        ref int step,
        string label,
        Point point,
        string? detail = null)
    {
        string title = owner.Localize(label);
        if (!string.IsNullOrWhiteSpace(detail))
        {
            title += $" ({detail})";
        }

        lines.Add($"{step.ToString(CultureInfo.InvariantCulture)}. {title} -> {FormatPoint(point)}");
        step++;
    }

    private static bool TryCreateGeometry(Size? clientSize, out TerrariaMenuGeometry geometry)
    {
        if (clientSize is not Size size || size.Width <= 0 || size.Height <= 0)
        {
            geometry = default;
            return false;
        }

        geometry = TerrariaMenuGeometry.From(size);
        return true;
    }

    private static bool HasAnyBossState(TerrariaBossStates states)
    {
        return states.Skeletron.HasValue ||
            states.WallOfFlesh.HasValue ||
            states.Destroyer.HasValue ||
            states.Twins.HasValue ||
            states.SkeletronPrime.HasValue ||
            states.Plantera.HasValue ||
            states.Golem.HasValue ||
            states.LunaticCultist.HasValue ||
            states.MoonLord.HasValue;
    }

    private static string FormatGameState(bool? isGameMenu)
    {
        return isGameMenu switch
        {
            true => "In menu",
            false => "In world",
            null => "Unknown"
        };
    }

    private static string FormatBounds(Rectangle? bounds, SettingsForm owner)
    {
        if (bounds is not Rectangle rect)
        {
            return owner.Localize("Unknown");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{rect.X}, {rect.Y}, {rect.Width} x {rect.Height}");
    }

    private static string FormatSize(Size? size, SettingsForm owner)
    {
        if (size is not Size value)
        {
            return owner.Localize("Unknown");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value.Width} x {value.Height}");
    }

    private static string FormatScale(float scale)
    {
        return scale.ToString("0.###", CultureInfo.InvariantCulture) + "x";
    }

    private static string FormatLogicalSize(TerrariaMenuGeometry geometry)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{geometry.LogicalWidth:0.##} x {geometry.LogicalHeight:0.##}");
    }

    private static string FormatProcessId(int? processId, SettingsForm owner)
    {
        return processId?.ToString(CultureInfo.InvariantCulture) ?? owner.Localize("Unknown");
    }

    private static string FormatDateTime(DateTime? value, SettingsForm owner)
    {
        return value?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? owner.Localize("Unknown");
    }

    private static string FormatTimestamp(DateTime? value, SettingsForm owner)
    {
        return value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) ?? owner.Localize("Unknown");
    }

    private static string FormatAddress(IntPtr address, SettingsForm owner)
    {
        return address == IntPtr.Zero
            ? owner.Localize("Unknown")
            : $"0x{address.ToInt64():X}";
    }

    private static string FormatByteCount(int? bytes, SettingsForm owner)
    {
        return bytes.HasValue
            ? FormatBytes(bytes.Value)
            : owner.Localize("Unknown");
    }

    private static string FormatRefreshRateSummary(
        double configuredIntervalMilliseconds,
        double actualIntervalMilliseconds,
        double maxIntervalMilliseconds,
        int sampleCount,
        SettingsForm owner)
    {
        string configured = FormatFrequency(configuredIntervalMilliseconds, owner);
        bool hasSamples = sampleCount >= 2 && actualIntervalMilliseconds > 0;
        string actual = hasSamples
            ? FormatFrequency(actualIntervalMilliseconds, owner)
            : owner.Localize("Waiting for samples");
        string average = hasSamples
            ? FormatMilliseconds(actualIntervalMilliseconds)
            : owner.Localize("Waiting for samples");
        string maximum = hasSamples && maxIntervalMilliseconds > 0
            ? FormatMilliseconds(maxIntervalMilliseconds)
            : owner.Localize("Waiting for samples");
        return string.Format(
            CultureInfo.InvariantCulture,
            owner.Localize("configured {0}, actual {1}, avg {2}, max {3}"),
            configured,
            actual,
            average,
            maximum);
    }

    private static string FormatConfiguredWaitingSummary(
        double configuredIntervalMilliseconds,
        string waitingText,
        SettingsForm owner)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            owner.Localize("configured {0}, waiting {1}"),
            FormatFrequency(configuredIntervalMilliseconds, owner),
            owner.Localize(waitingText));
    }

    private static string FormatConfiguredHzWaitingSummary(
        int configuredHz,
        string waitingText,
        SettingsForm owner)
    {
        return FormatConfiguredWaitingSummary(
            RefreshRateSettings.ToInterval(configuredHz).TotalMilliseconds,
            waitingText,
            owner);
    }

    private static string FormatWatcherPollSummary(RuntimeDebugSnapshot debugSnapshot, SettingsForm owner)
    {
        if (!debugSnapshot.WatchSnapshot.IsAttached || !debugSnapshot.WatchSnapshot.IsReady)
        {
            return FormatConfiguredHzWaitingSummary(
                RefreshRateSettings.NormalizeReadyWatcherPollHz(
                    owner.Result.Advanced?.ReadyWatcherPollHz ?? AppSettingsDefaults.Advanced.ReadyWatcherPollHz),
                "Waiting for attached memory",
                owner);
        }

        return FormatRefreshRateSummary(
            debugSnapshot.Performance.WatcherPollIntervalMilliseconds,
            debugSnapshot.Performance.ActualWatcherPollIntervalMilliseconds,
            debugSnapshot.Performance.MaxWatcherPollIntervalMilliseconds,
            debugSnapshot.Performance.WatcherPollCount,
            owner);
    }

    private static string FormatControlTickSummary(RuntimeDebugSnapshot debugSnapshot, SettingsForm owner)
    {
        if (!debugSnapshot.WatchSnapshot.IsAttached || !debugSnapshot.WatchSnapshot.IsReady)
        {
            return FormatConfiguredHzWaitingSummary(
                RefreshRateSettings.NormalizeReadyUiControlHz(
                    owner.Result.Advanced?.ReadyUiControlHz ?? AppSettingsDefaults.Advanced.ReadyUiControlHz),
                "Waiting for attached memory",
                owner);
        }

        return FormatRefreshRateSummary(
            debugSnapshot.Performance.ControlTickIntervalMilliseconds,
            debugSnapshot.Performance.ActualControlTickIntervalMilliseconds,
            debugSnapshot.Performance.MaxControlTickIntervalMilliseconds,
            debugSnapshot.Performance.ControlTickCount,
            owner);
    }

    private static string FormatStatusPaintSummary(RuntimeDebugSnapshot debugSnapshot, SettingsForm owner)
    {
        if (!debugSnapshot.WatchSnapshot.IsAttached || !debugSnapshot.WatchSnapshot.IsReady)
        {
            return FormatConfiguredWaitingSummary(
                debugSnapshot.Performance.StatusPaintIntervalMilliseconds,
                "Waiting for attached memory",
                owner);
        }

        if (debugSnapshot.TimerPhase != SplitTimerPhase.Running)
        {
            return FormatConfiguredWaitingSummary(
                debugSnapshot.Performance.StatusPaintIntervalMilliseconds,
                "Waiting for timer start",
                owner);
        }

        return FormatRefreshRateSummary(
            debugSnapshot.Performance.StatusPaintIntervalMilliseconds,
            debugSnapshot.Performance.ActualStatusPaintIntervalMilliseconds,
            debugSnapshot.Performance.MaxStatusPaintIntervalMilliseconds,
            debugSnapshot.Performance.StatusPaintCount,
            owner);
    }

    private static string FormatTimerPaintSummary(RuntimeDebugSnapshot debugSnapshot, SettingsForm owner)
    {
        if (!debugSnapshot.WatchSnapshot.IsAttached || !debugSnapshot.WatchSnapshot.IsReady)
        {
            return FormatConfiguredWaitingSummary(
                debugSnapshot.Performance.TimerOverlayPaintIntervalMilliseconds,
                "Waiting for attached memory",
                owner);
        }

        if (debugSnapshot.TimerPhase != SplitTimerPhase.Running)
        {
            return FormatConfiguredWaitingSummary(
                debugSnapshot.Performance.TimerOverlayPaintIntervalMilliseconds,
                "Waiting for timer start",
                owner);
        }

        return FormatRefreshRateSummary(
            debugSnapshot.Performance.TimerOverlayPaintIntervalMilliseconds,
            debugSnapshot.Performance.ActualTimerOverlayPaintIntervalMilliseconds,
            debugSnapshot.Performance.MaxTimerOverlayPaintIntervalMilliseconds,
            debugSnapshot.Performance.TimerOverlayPaintCount,
            owner);
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###", CultureInfo.InvariantCulture) + " ms";
    }

    private static string FormatFrequency(double intervalMilliseconds, SettingsForm owner)
    {
        if (intervalMilliseconds <= 0)
        {
            return owner.Localize("Unknown");
        }

        double hertz = 1000d / intervalMilliseconds;
        return hertz.ToString("0.0", CultureInfo.InvariantCulture) + " Hz";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int unitIndex = 0;
        while (value >= 1024d && unitIndex < units.Length - 1)
        {
            value /= 1024d;
            unitIndex++;
        }

        string number = unitIndex == 0
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.0", CultureInfo.InvariantCulture);
        return $"{number} {units[unitIndex]}";
    }

    private static string FormatScanStats(SignatureScanDiagnostics? diagnostics, SettingsForm owner)
    {
        if (diagnostics is not SignatureScanDiagnostics value)
        {
            return owner.Localize("Unknown");
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            owner.Localize("private {0}/{1} scanned, {2} read; image {3}/{4} scanned, {5} read; total {6}; {7}"),
            value.PrivateExecutablePagesScanned,
            value.PrivateExecutablePagesSeen,
            FormatBytes(value.PrivateExecutableBytesScanned),
            value.ImageExecutablePagesScanned,
            value.ImageExecutablePagesSeen,
            FormatBytes(value.ImageExecutableBytesScanned),
            FormatBytes(value.TotalExecutableBytesScanned),
            FormatMilliseconds(value.ElapsedMilliseconds));
    }

    private static string FormatScanFailures(SignatureScanDiagnostics? diagnostics, SettingsForm owner)
    {
        if (diagnostics is not SignatureScanDiagnostics value)
        {
            return owner.Localize("Unknown");
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            owner.Localize("read failures {0}, oversized skipped {1}"),
            value.ReadFailures,
            value.OversizedPagesSkipped);
    }

    private static string FormatPlayerName(string? playerName)
    {
        string trimmed = playerName?.Trim() ?? string.Empty;
        return trimmed.Length == 0 ? "1" : trimmed;
    }

    private static string FormatPoint(Point point)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{point.X}, {point.Y}");
    }

    private static string FormatText(string? value, SettingsForm owner)
    {
        return string.IsNullOrWhiteSpace(value) ? owner.Localize("Unknown") : value;
    }

    private static string FormatOptionalBool(bool? value, SettingsForm owner)
    {
        return value.HasValue ? FormatBool(value.Value, owner) : owner.Localize("Unknown");
    }

    private static string FormatPercent(double? value, SettingsForm owner)
    {
        return value.HasValue
            ? value.Value.ToString("P1", CultureInfo.InvariantCulture)
            : owner.Localize("Unknown");
    }

    private static string FormatWorldGenerationText(string? value, IntPtr slotAddress, SettingsForm owner)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return slotAddress != IntPtr.Zero
            ? owner.Localize("World generation idle")
            : owner.Localize("Unknown");
    }

    private static string FormatWorldGenerationPercent(double? value, IntPtr slotAddress, SettingsForm owner)
    {
        if (value.HasValue)
        {
            return value.Value.ToString("P1", CultureInfo.InvariantCulture);
        }

        return slotAddress != IntPtr.Zero
            ? owner.Localize("World generation idle")
            : owner.Localize("Unknown");
    }

    private static string LocalizeStage(string stage, SettingsForm owner)
    {
        if (string.IsNullOrWhiteSpace(stage))
        {
            return owner.Localize("Unknown");
        }

        const string startPendingSuffix = "; start pending";
        if (stage.EndsWith(startPendingSuffix, StringComparison.Ordinal))
        {
            string prefix = stage[..^startPendingSuffix.Length];
            return $"{owner.Localize(prefix)}\uFF1B{owner.Localize("start pending")}";
        }

        return owner.Localize(stage);
    }

    private static string LocalizeStatus(string status, SettingsForm owner)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return owner.Localize("Unknown");
        }

        const string processChangedPrefix = "Terraria process changed while reading window state: ";
        const string cannotReadPrefix = "cannot read Terraria process: ";
        const string cannotAttachPrefix = "cannot attach to Terraria process: ";
        const string attachedPidPrefix = "attached to Terraria PID ";
        const string attachedProcessPrefix = "attached to Terraria process";

        if (string.Equals(status, "waiting for Terraria.exe", StringComparison.OrdinalIgnoreCase))
        {
            return owner.Localize("waiting for Terraria.exe");
        }

        if (status.StartsWith(processChangedPrefix, StringComparison.Ordinal))
        {
            return string.Format(owner.Localize("Terraria process changed while reading window state: {0}"), status[processChangedPrefix.Length..]);
        }

        if (status.StartsWith(cannotReadPrefix, StringComparison.Ordinal))
        {
            return string.Format(owner.Localize("cannot read Terraria process: {0}"), status[cannotReadPrefix.Length..]);
        }

        if (status.StartsWith(cannotAttachPrefix, StringComparison.Ordinal))
        {
            return string.Format(owner.Localize("cannot attach to Terraria process: {0}"), status[cannotAttachPrefix.Length..]);
        }

        if (status.StartsWith(attachedPidPrefix, StringComparison.Ordinal))
        {
            string remainder = status[attachedPidPrefix.Length..];
            int separatorIndex = remainder.IndexOf(',');
            if (separatorIndex < 0)
            {
                return string.Format(owner.Localize("attached to Terraria PID {0}"), remainder.Trim());
            }

            string processId = remainder[..separatorIndex].Trim();
            string detail = remainder[(separatorIndex + 1)..].Trim();
            return string.Format(owner.Localize("attached to Terraria PID {0}, {1}"), processId, LocalizeStatusDetail(detail, owner));
        }

        if (status.StartsWith(attachedProcessPrefix, StringComparison.Ordinal))
        {
            string remainder = status[attachedProcessPrefix.Length..].Trim();
            if (string.IsNullOrEmpty(remainder))
            {
                return owner.Localize("attached to Terraria process");
            }

            if (remainder.StartsWith(",", StringComparison.Ordinal))
            {
                remainder = remainder[1..].TrimStart();
            }

            return string.Format(owner.Localize("attached to Terraria process, {0}"), LocalizeStatusDetail(remainder, owner));
        }

        return owner.Localize(status);
    }

    private static string LocalizeStatusDetail(string detail, SettingsForm owner)
    {
        const string armTimerSuffix = "; return to menu once to arm timer start";
        const string scanMemoryPrefix = "scanning for ";
        const string scanMemorySuffix = " memory";
        const string windowHandleUnavailablePrefix = "window handle 0x";
        const string windowHandleUnavailableSuffix = ", client rect unavailable";
        const string windowHandlePrefix = "window handle 0x";

        string localizedSuffix = string.Empty;
        if (detail.EndsWith(armTimerSuffix, StringComparison.Ordinal))
        {
            detail = detail[..^armTimerSuffix.Length];
            localizedSuffix = "\uFF1B" + owner.Localize("return to menu once to arm timer start");
        }

        if (detail.StartsWith(scanMemoryPrefix, StringComparison.Ordinal) &&
            detail.EndsWith(scanMemorySuffix, StringComparison.Ordinal))
        {
            string version = detail.Substring(scanMemoryPrefix.Length, detail.Length - scanMemoryPrefix.Length - scanMemorySuffix.Length);
            return string.Format(owner.Localize("scanning for {0} memory"), version) + localizedSuffix;
        }

        if (detail.StartsWith(windowHandleUnavailablePrefix, StringComparison.Ordinal) &&
            detail.EndsWith(windowHandleUnavailableSuffix, StringComparison.Ordinal))
        {
            string handle = detail.Substring(windowHandleUnavailablePrefix.Length, detail.Length - windowHandleUnavailablePrefix.Length - windowHandleUnavailableSuffix.Length);
            return string.Format(owner.Localize("window handle 0x{0}, client rect unavailable"), handle) + localizedSuffix;
        }

        if (detail.StartsWith(windowHandlePrefix, StringComparison.Ordinal))
        {
            string handle = detail[windowHandlePrefix.Length..];
            return string.Format(owner.Localize("window handle 0x{0}"), handle) + localizedSuffix;
        }

        return owner.Localize(detail) + localizedSuffix;
    }

    private static string FormatBool(bool value, SettingsForm owner)
    {
        return owner.Localize(value ? "Yes" : "No");
    }

    private static void SetQuickBool(Label label, SettingsForm owner, bool value)
    {
        SetValue(label, FormatBool(value, owner), value ? QuickStatusNormalColor : QuickStatusProblemColor);
    }

    private static void SetQuickGameState(Label label, SettingsForm owner, bool? isGameMenu)
    {
        Color color = isGameMenu switch
        {
            false => QuickStatusNormalColor,
            true => QuickStatusMenuColor,
            null => QuickStatusProblemColor
        };
        SetValue(label, owner.Localize(FormatGameState(isGameMenu)), color);
    }

    private static void SetQuickStatus(Label label, string status, SettingsForm owner)
    {
        SetValue(
            label,
            LocalizeStatus(status, owner),
            IsNormalStatus(status) ? QuickStatusNormalColor : QuickStatusProblemColor);
    }

    private static void SetBool(Label label, SettingsForm owner, bool value)
    {
        SetValue(label, FormatBool(value, owner));
    }

    private static void SetBossState(Label label, bool? value, SettingsForm owner)
    {
        if (!value.HasValue)
        {
            SetValue(label, owner.Localize("Unknown"));
            return;
        }

        SetBool(label, owner, value.Value);
    }

    private static void SetOptionalBool(Label label, SettingsForm owner, bool? value)
    {
        if (!value.HasValue)
        {
            SetValue(label, owner.Localize("Unknown"));
            return;
        }

        SetBool(label, owner, value.Value);
    }

    private static void SetValue(Label label, string text)
    {
        SetValue(label, text, UiTheme.Text);
    }

    private static void SetValue(Label label, string text, Color color)
    {
        if (!string.Equals(label.Text, text, StringComparison.Ordinal))
        {
            label.Text = text;
        }

        if (label.ForeColor != color)
        {
            label.ForeColor = color;
        }
    }

    private static bool IsNormalStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        if (ContainsStatusText(status, "cannot") ||
            ContainsStatusText(status, "changed while") ||
            ContainsStatusText(status, "unreadable") ||
            ContainsStatusText(status, "lost") ||
            ContainsStatusText(status, "missing") ||
            ContainsStatusText(status, "unavailable"))
        {
            return false;
        }

        if (ContainsStatusText(status, "not ready") ||
            ContainsStatusText(status, "pending") ||
            ContainsStatusText(status, "scanning") ||
            ContainsStatusText(status, "found signature but not"))
        {
            return false;
        }

        return status.StartsWith("attached to Terraria", StringComparison.OrdinalIgnoreCase) ||
            status.StartsWith("ready", StringComparison.OrdinalIgnoreCase) ||
            status.StartsWith("timer ready", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsStatusText(string status, string value)
    {
        return status.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private static void SetSequenceText(TextBox textBox, string text)
    {
        if (!string.Equals(textBox.Text, text, StringComparison.Ordinal))
        {
            textBox.Text = text;
        }
    }

    private static TableLayoutPanel CreateSection(SettingsForm owner, string title)
    {
        return SettingsUiFactory.For(owner).CreateSection(title);
    }

    private static FlowLayoutPanel CreateActionBar(SettingsForm owner)
    {
        return SettingsUiFactory.For(owner).CreateActionBar();
    }

    private static TableLayoutPanel CreateGrid(SettingsForm owner)
    {
        SettingsUiFactory factory = SettingsUiFactory.For(owner);
        return factory.CreateGrid(
            SettingsUiFactory.ColumnStyleAbsolute(290f),
            SettingsUiFactory.ColumnStylePercent(100f));
    }

    private static void AddValueRow(TableLayoutPanel grid, SettingsForm owner, string label, Label valueLabel)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));
        grid.Controls.Add(CreateRowLabel(owner, label), 0, row);
        grid.Controls.Add(valueLabel, 1, row);
    }

    private static Label CreateRowLabel(SettingsForm owner, string text)
    {
        return SettingsUiFactory.For(owner).CreateRowLabel(text);
    }

    private static Label CreateValueLabel()
    {
        return new SettingsUiFactory(static key => key).CreateValueLabel();
    }

    private static Label CreateMutedLabel(SettingsForm owner, string text)
    {
        return SettingsUiFactory.For(owner).CreateMutedLabel(text);
    }

    private static TextBox CreateMultilineValueBox(int height)
    {
        return new SettingsUiFactory(static key => key).CreateMultilineValueBox(height);
    }

    private static Button CreateActionButton(SettingsForm owner, string text)
    {
        return SettingsUiFactory.For(owner).CreateActionButton(text);
    }

    private static void AddSection(TableLayoutPanel parent, Control section)
    {
        SettingsUiFactory.AddSection(parent, section);
    }

    private static void AddSectionControl(TableLayoutPanel section, Control control)
    {
        SettingsUiFactory.AddSectionControl(section, control);
    }
}
