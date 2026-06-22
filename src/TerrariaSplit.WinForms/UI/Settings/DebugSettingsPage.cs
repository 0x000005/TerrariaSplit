using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed partial class DebugSettingsPage : SettingsPageBase
{
    private const int RefreshIntervalMilliseconds = 500;
    private const int HeavyRefreshIntervalMilliseconds = 2000;
    private const int SequenceBoxHeight = 220;

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
        Label timerLayeredUpdateValue = CreateValueLabel();

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
            AddValueRow(performanceGrid, owner, "Main timer layered update", timerLayeredUpdateValue);
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
            DebugSettingsSnapshot snapshot = BuildDisplaySnapshot();

            page.SuspendLayout();
            try
            {
                ApplySnapshot(snapshot);
            }
            finally
            {
                page.ResumeLayout(false);
            }
        }

        DebugSettingsSnapshot BuildDisplaySnapshot()
        {
            return DebugSettingsSnapshotBuilder.Build(
                latestWindow,
                owner.GetRuntimeDebugSnapshot(),
                latestInventory,
                owner.Result.Automation.AutoCreate,
                owner.Result.Advanced,
                owner.GetWorldPoolCount,
                owner.Localize);
        }

        void ApplySnapshot(DebugSettingsSnapshot snapshot)
        {
            SetValue(lastUpdatedValue, snapshot.QuickStatus.LastUpdated);
            SetValue(processDetectedValue, snapshot.QuickStatus.ProcessDetected);
            SetValue(windowDetectedValue, snapshot.QuickStatus.WindowDetected);
            SetValue(watcherAttachedValue, snapshot.QuickStatus.WatcherAttached);
            SetValue(memoryReadyValue, snapshot.QuickStatus.MemoryReady);
            SetValue(bossFlagsReadyValue, snapshot.QuickStatus.BossFlagsReady);
            SetValue(gameStateValue, snapshot.QuickStatus.GameState);
            SetValue(windowStatusValue, snapshot.QuickStatus.WindowStatus);

            SetValue(watcherPollValue, snapshot.Performance.WatcherPoll);
            SetValue(controlTickValue, snapshot.Performance.ControlTick);
            SetValue(statusPaintValue, snapshot.Performance.StatusPaint);
            SetValue(timerPaintValue, snapshot.Performance.TimerPaint);
            SetValue(timerLayeredUpdateValue, snapshot.Performance.TimerLayeredUpdate);

            SetValue(processIdValue, snapshot.Window.ProcessId);
            SetValue(processStartTimeValue, snapshot.Window.ProcessStartTime);
            SetValue(processPathValue, snapshot.Window.ProcessPath);
            SetValue(processArchitectureValue, snapshot.Window.ProcessArchitecture);
            SetValue(processVersionValue, snapshot.Window.ProcessVersion);
            SetValue(windowHandleValue, snapshot.Window.WindowHandle);
            SetValue(windowTitleValue, snapshot.Window.WindowTitle);
            SetValue(respondingValue, snapshot.Window.Responding);
            SetValue(visibleValue, snapshot.Window.Visible);
            SetValue(minimizedValue, snapshot.Window.Minimized);
            SetValue(maximizedValue, snapshot.Window.Maximized);
            SetValue(foregroundValue, snapshot.Window.Foreground);
            SetValue(windowBoundsValue, snapshot.Window.WindowBounds);
            SetValue(clientSizeValue, snapshot.Window.ClientSize);
            SetValue(menuScaleValue, snapshot.Window.MenuScale);
            SetValue(logicalMenuSizeValue, snapshot.Window.LogicalMenuSize);

            SetValue(playerFilesValue, snapshot.Automation.PlayerFiles);
            SetValue(worldFilesValue, snapshot.Automation.WorldFiles);
            SetValue(favoritePlayersValue, snapshot.Automation.FavoritePlayers);
            SetValue(favoriteWorldsValue, snapshot.Automation.FavoriteWorlds);
            SetValue(playerNameValue, snapshot.Automation.PlayerName);
            SetValue(playerDifficultyValue, snapshot.Automation.PlayerDifficulty);
            SetValue(worldSizeValue, snapshot.Automation.WorldSize);
            SetValue(worldDifficultyValue, snapshot.Automation.WorldDifficulty);
            SetValue(worldEvilValue, snapshot.Automation.WorldEvil);
            SetValue(pyramidFilterValue, snapshot.Automation.PyramidFilter);
            SetValue(pyramidItemsValue, snapshot.Automation.PyramidItems);
            SetValue(returnToMainMenuOnFilterFailureValue, snapshot.Automation.ReturnToMainMenuOnFilterFailure);
            SetValue(windowActivationDelayValue, snapshot.Automation.WindowActivationDelay);
            SetValue(clickFocusDelayValue, snapshot.Automation.ClickFocusDelay);
            SetValue(inputPressDurationValue, snapshot.Automation.InputPressDuration);
            SetValue(shortActionDelayValue, snapshot.Automation.ShortActionDelay);
            SetValue(menuActionDelayValue, snapshot.Automation.MenuActionDelay);
            SetValue(pyramidFilterPostDelayValue, snapshot.Automation.PyramidFilterPostDelay);
            SetSequenceText(autoCreateSequenceValue, snapshot.Automation.AutoCreateSequence);

            SetValue(skeletronValue, snapshot.BossProgress.Skeletron);
            SetValue(wallOfFleshValue, snapshot.BossProgress.WallOfFlesh);
            SetValue(destroyerValue, snapshot.BossProgress.Destroyer);
            SetValue(twinsValue, snapshot.BossProgress.Twins);
            SetValue(skeletronPrimeValue, snapshot.BossProgress.SkeletronPrime);
            SetValue(planteraValue, snapshot.BossProgress.Plantera);
            SetValue(golemValue, snapshot.BossProgress.Golem);
            SetValue(lunaticCultistValue, snapshot.BossProgress.LunaticCultist);
            SetValue(moonLordValue, snapshot.BossProgress.MoonLord);

            SetValue(currentPassValue, snapshot.WorldGeneration.CurrentPass);
            SetValue(currentSeedValue, snapshot.WorldGeneration.CurrentSeed);
            SetValue(progressMessageValue, snapshot.WorldGeneration.ProgressMessage);
            SetValue(currentProgressValue, snapshot.WorldGeneration.CurrentProgress);
            SetValue(totalProgressValue, snapshot.WorldGeneration.TotalProgress);

            SetValue(scanAttemptsValue, snapshot.Memory.ScanAttempts);
            SetValue(lastScanValue, snapshot.Memory.LastScan);
            SetValue(scanPageStatsValue, snapshot.Memory.ScanPageStats);
            SetValue(scanFailuresValue, snapshot.Memory.ScanFailures);
            SetValue(mainModuleBaseValue, snapshot.Memory.MainModuleBase);
            SetValue(mainModuleSizeValue, snapshot.Memory.MainModuleSize);
            SetValue(updateTimeAddressValue, snapshot.Memory.UpdateTimeAddress);
            SetValue(bossFlagsAddressValue, snapshot.Memory.BossFlagsAddress);
            SetValue(hardmodeAddressValue, snapshot.Memory.HardmodeAddress);
            SetValue(generationProgressAddressValue, snapshot.Memory.GenerationProgressAddress);
            SetValue(generationControllerAddressValue, snapshot.Memory.GenerationControllerAddress);
            SetValue(failureStageValue, snapshot.Memory.FailureStage);
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
                                StaticAppLogger.Instance.Error(task.Exception, "Failed to refresh debug page heavy snapshot.");
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
            latestWindow = TerrariaWindowProbe.Read();
            latestInventory = savePreparation.ReadInventorySnapshot();
            DebugSettingsSnapshot snapshot = BuildDisplaySnapshot();

            try
            {
                Clipboard.SetText(snapshot.Report);
            }
            catch (Exception ex)
            {
                StaticAppLogger.Instance.Error(ex, "Failed to copy Terraria debug information.");
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
}
