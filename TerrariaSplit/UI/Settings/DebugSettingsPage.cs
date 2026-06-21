using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed partial class DebugSettingsPage : SettingsPageBase
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
        Label pyramidFilterValue = CreateValueLabel();
        Label pyramidItemsValue = CreateValueLabel();
        Label returnToMainMenuOnFilterFailureValue = CreateValueLabel();
        Label shortActionDelayValue = CreateValueLabel();
        Label menuActionDelayValue = CreateValueLabel();
        Label pyramidFilterPostDelayValue = CreateValueLabel();
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
        Label currentSeedValue = CreateValueLabel();
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
            AddValueRow(automationGrid, owner, "Filter pyramid", pyramidFilterValue);
            AddValueRow(automationGrid, owner, "Required pyramid items", pyramidItemsValue);
            AddValueRow(automationGrid, owner, "Return to main menu on filter failure", returnToMainMenuOnFilterFailureValue);
            AddValueRow(automationGrid, owner, "Initial wait ms", windowActivationDelayValue);
            AddValueRow(automationGrid, owner, "Pre-click wait ms", clickFocusDelayValue);
            AddValueRow(automationGrid, owner, "Mouse / key duration ms", inputPressDurationValue);
            AddValueRow(automationGrid, owner, "Adjacent operation delay ms", shortActionDelayValue);
            AddValueRow(automationGrid, owner, "Cross-menu operation delay ms", menuActionDelayValue);
            AddValueRow(automationGrid, owner, "Pyramid filter post wait ms", pyramidFilterPostDelayValue);
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
            AddValueRow(worldGenerationGrid, owner, "Current seed", currentSeedValue);
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
            AutoCreateWorldSettings autoCreate = owner.Result.Automation.AutoCreate;
            latestWindow = window;
            latestSnapshot = snapshot;
            latestDiagnostics = diagnostics;
            latestInventory = inventory;

            page.SuspendLayout();
            try
            {
                bool bossFlagsReady = HasAnyBossState(snapshot.Facts);

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
                SetValue(pyramidFilterValue, FormatBool(autoCreate.EnablePyramidFilter, owner));
                SetValue(pyramidItemsValue, FormatPyramidFilterItems(autoCreate, owner));
                SetValue(returnToMainMenuOnFilterFailureValue, FormatBool(autoCreate.ReturnToMainMenuOnFilterFailure, owner));
                SetValue(shortActionDelayValue, autoCreate.ShortActionDelayMilliseconds.ToString(CultureInfo.InvariantCulture));
                SetValue(menuActionDelayValue, autoCreate.MenuActionDelayMilliseconds.ToString(CultureInfo.InvariantCulture));
                SetValue(pyramidFilterPostDelayValue, autoCreate.PyramidFilterPostDelayMilliseconds.ToString(CultureInfo.InvariantCulture));
                SetValue(windowActivationDelayValue, autoCreate.WindowActivationDelayMilliseconds.ToString(CultureInfo.InvariantCulture));
                SetValue(clickFocusDelayValue, autoCreate.ClickFocusDelayMilliseconds.ToString(CultureInfo.InvariantCulture));
                SetValue(inputPressDurationValue, autoCreate.InputPressDurationMilliseconds.ToString(CultureInfo.InvariantCulture));

                SetBossState(skeletronValue, GetBossFact(snapshot.Facts, SplitCatalog.Skeletron), owner);
                SetBossState(wallOfFleshValue, GetBossFact(snapshot.Facts, SplitCatalog.WallOfFlesh), owner);
                SetBossState(destroyerValue, GetBossFact(snapshot.Facts, SplitCatalog.Destroyer), owner);
                SetBossState(twinsValue, GetBossFact(snapshot.Facts, SplitCatalog.Twins), owner);
                SetBossState(skeletronPrimeValue, GetBossFact(snapshot.Facts, SplitCatalog.SkeletronPrime), owner);
                SetBossState(planteraValue, GetBossFact(snapshot.Facts, SplitCatalog.Plantera), owner);
                SetBossState(golemValue, GetBossFact(snapshot.Facts, SplitCatalog.Golem), owner);
                SetBossState(lunaticCultistValue, GetBossFact(snapshot.Facts, SplitCatalog.LunaticCultist), owner);
                SetBossState(moonLordValue, GetBossFact(snapshot.Facts, SplitCatalog.MoonLord), owner);

                SetValue(
                    currentPassValue,
                    FormatWorldGenerationText(
                        snapshot.WorldGeneration.CurrentPassName,
                        diagnostics.CurrentControllerAddress,
                        owner));
                SetValue(
                    currentSeedValue,
                    FormatWorldCreationSeed(diagnostics.WorldCreationSeed, owner));
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
                Clipboard.SetText(BuildDiagnosticReport(latestWindow, debugSnapshot, latestDiagnostics, latestInventory, owner.Result.Automation.AutoCreate, owner));
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
        bool bossFlagsReady = HasAnyBossState(snapshot.Facts);

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
                ("Required pyramid items", FormatPyramidFilterItems(autoCreate, owner)),
                ("Return to main menu on filter failure", FormatBool(autoCreate.ReturnToMainMenuOnFilterFailure, owner)),
                ("Initial wait ms", autoCreate.WindowActivationDelayMilliseconds.ToString(CultureInfo.InvariantCulture)),
                ("Pre-click wait ms", autoCreate.ClickFocusDelayMilliseconds.ToString(CultureInfo.InvariantCulture)),
                ("Mouse / key duration ms", autoCreate.InputPressDurationMilliseconds.ToString(CultureInfo.InvariantCulture)),
                ("Adjacent operation delay ms", autoCreate.ShortActionDelayMilliseconds.ToString(CultureInfo.InvariantCulture)),
                ("Cross-menu operation delay ms", autoCreate.MenuActionDelayMilliseconds.ToString(CultureInfo.InvariantCulture)),
                ("Pyramid filter post wait ms", autoCreate.PyramidFilterPostDelayMilliseconds.ToString(CultureInfo.InvariantCulture))
            ]);

        AppendMultilineSection(lines, owner, "Click sequence", autoCreateSequence);

        AppendReportSection(
            lines,
            owner,
            "Boss Progress",
            [
                ("Skeletron", FormatOptionalBool(GetBossFact(snapshot.Facts, SplitCatalog.Skeletron), owner)),
                ("Wall of Flesh", FormatOptionalBool(GetBossFact(snapshot.Facts, SplitCatalog.WallOfFlesh), owner)),
                ("Destroyer", FormatOptionalBool(GetBossFact(snapshot.Facts, SplitCatalog.Destroyer), owner)),
                ("The Twins", FormatOptionalBool(GetBossFact(snapshot.Facts, SplitCatalog.Twins), owner)),
                ("Skeletron Prime", FormatOptionalBool(GetBossFact(snapshot.Facts, SplitCatalog.SkeletronPrime), owner)),
                ("Plantera", FormatOptionalBool(GetBossFact(snapshot.Facts, SplitCatalog.Plantera), owner)),
                ("Golem", FormatOptionalBool(GetBossFact(snapshot.Facts, SplitCatalog.Golem), owner)),
                ("Lunatic Cultist", FormatOptionalBool(GetBossFact(snapshot.Facts, SplitCatalog.LunaticCultist), owner)),
                ("Moon Lord", FormatOptionalBool(GetBossFact(snapshot.Facts, SplitCatalog.MoonLord), owner))
            ]);

        AppendReportSection(
            lines,
            owner,
            "World Generation",
            [
                ("Current pass", FormatWorldGenerationText(snapshot.WorldGeneration.CurrentPassName, diagnostics.CurrentControllerAddress, owner)),
                ("Current seed", FormatWorldCreationSeed(diagnostics.WorldCreationSeed, owner)),
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

}
