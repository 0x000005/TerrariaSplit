using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal static class MainShellCompositionRoot
{
    public static StartupCore CreateStartupCore(Func<string, bool> confirmPersonalBestUpdate)
    {
        IRuntimeDataPaths runtimeDataPaths = AppContextRuntimeDataPaths.Default;
        var splitTimeSets = new SplitTimeSetRepository(runtimeDataPaths);
        var runStatsRepository = new RunStatsRepository(splitTimeSets);
        var settingsRepository = new AppSettingsRepository(runtimeDataPaths, splitTimeSets);
        StartupDiagnostics.RecordTrace("StartupRepositoriesCreated");
        ISettingsSnapshotFactory settingsSnapshots = new StoredSettingsSnapshotFactory(settingsRepository);
        IAppLogger logger = StaticAppLogger.Instance;
        var runStatisticsRecorder = new DelegateRunStatisticsRecorder(runStatsRepository.RecordRun);
        var personalBestSnapshotStore = new DelegatePersonalBestSnapshotStore(
            (splits, bossName, previousTime, newTime) =>
            {
                OperationResult result = splitTimeSets.TrySavePersonalBestTimeSnapshot(
                    splits,
                    bossName,
                    previousTime,
                    newTime,
                    out ReferenceSplitSet? snapshot);
                return PersonalBestSnapshotSaveResult.FromResult(result, snapshot);
            },
            (splits, bossName, previousTime, newTime) =>
            {
                OperationResult result = splitTimeSets.TrySavePersonalBestSegmentSnapshot(
                    splits,
                    bossName,
                    previousTime,
                    newTime,
                    out ReferenceSplitSet? snapshot);
                return PersonalBestSnapshotSaveResult.FromResult(result, snapshot);
            });
        AppSettings settings = settingsRepository.Load();
        StartupDiagnostics.RecordTrace("StartupSettingsLoaded");
        var applicationController = new ApplicationController(
            settings,
            confirmPersonalBestUpdate,
            settingsSnapshots,
            runStatisticsRecorder,
            personalBestSnapshotStore);
        StartupDiagnostics.RecordTrace("StartupApplicationCreated");
        var renderResources = new OverlayRenderResources();
        StartupDiagnostics.RecordTrace("StartupRenderResourcesCreated");
        Task statusIconPreloadTask = Task.Run(() =>
            renderResources.BossIcons.PreloadInitialFrame(
                applicationController.ViewState.DisplayStatuses,
                applicationController.ViewState.CurrentSplitIndex,
                applicationController.Settings));
        StartupDiagnostics.RecordTrace("StartupIconPreloadQueued");

        return new StartupCore(
            runtimeDataPaths,
            settingsRepository,
            settingsRepository.Save,
            settingsSnapshots,
            logger,
            new GlobalHotkeyManager(logger),
            renderResources,
            statusIconPreloadTask,
            new OverlayAnimationController(),
            applicationController);
    }

    public static Task<RuntimeServicePreparation> CreateRuntimeServicesAsync(
        StartupCore startupCore,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var worldPoolStore = new WorldPoolStore(startupCore.RuntimeDataPaths);
            cancellationToken.ThrowIfCancellationRequested();
            var preparation = new RuntimeServicePreparation(
                worldPoolStore,
                new WorldPoolFillService(
                    worldPoolStore,
                    startupCore.SettingsSnapshots,
                    startupCore.AppLogger,
                    startupCore.RuntimeDataPaths),
                new MainFormContextMenuBuilder(startupCore.SettingsRepository),
                new SoundPlayerService());
            if (cancellationToken.IsCancellationRequested)
            {
                preparation.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
            }

            return preparation;
        }, cancellationToken);
    }

    public static TerrariaMonitorCoordinator CreateMonitorCoordinator(
        Action<Action> dispatch,
        IAppLogger logger)
    {
        return new TerrariaMonitorCoordinator(
            new TerrariaWorldWatcher(),
            new TerrariaUiScalePatchApplierAdapter(),
            dispatch,
            logger,
            shouldYieldDispatch: UiInputMessageProbe.HasPendingInputMessage);
    }

    public static OverlayWindowController CreateOverlayWindowController(
        Form owner,
        Func<Graphics, bool> render)
    {
        return new OverlayWindowController(owner, render);
    }

    public static TimerOverlayWindowHost CreateTimerOverlayWindowHost(
        Action<Action> dispatch)
    {
        return new TimerOverlayWindowHost(dispatch);
    }

    public static ProgramModalWindowCoordinator CreateModalWindowCoordinator(
        Form owner,
        TimerOverlayWindowHost timerOverlayHost)
    {
        return new ProgramModalWindowCoordinator(
            owner,
            timerOverlayHost.ApplyInteractionBlocked,
            () => timerOverlayHost.WindowHandle);
    }

    public static MainWindowModalInputRouter CreateModalInputRouter(
        ProgramModalWindowCoordinator modalWindows,
        Func<ContextMenuStrip?> getContextMenu,
        Action cancelDragging)
    {
        return new MainWindowModalInputRouter(modalWindows, getContextMenu, cancelDragging);
    }

    public static AutomationShell CreateAutomationShell(
        WorldPoolStore worldPoolStore,
        Func<AppSettings> getSettings,
        ISettingsSnapshotFactory settingsSnapshots,
        ProgramModalWindowCoordinator modalWindows,
        Control owner,
        Action clearPendingMenuActions,
        IAppLogger logger)
    {
        return new AutomationShell(
            worldPoolStore,
            getSettings,
            settingsSnapshots,
            modalWindows,
            owner,
            clearPendingMenuActions,
            logger);
    }

    public static SettingsShell CreateSettingsShell(
        Func<AppSettings> getSettings,
        Func<bool> isRaceRoomActive,
        ISettingsRepository settingsRepository,
        ISettingsSnapshotFactory settingsSnapshots,
        Action<Action> dispatch,
        Action<AppSettings> applySettings,
        Action clearPendingMenuActions,
        Action disposeHotkeys,
        Action registerHotkeys,
        Func<bool> isMainHandleCreated,
        Func<Rectangle> getOwnerBounds,
        Action<PreparedApplicationUpdate> restartForUpdate)
    {
        return new SettingsShell(
            getSettings,
            isRaceRoomActive,
            settingsRepository,
            settingsSnapshots,
            dispatch,
            applySettings,
            clearPendingMenuActions,
            disposeHotkeys,
            registerHotkeys,
            isMainHandleCreated,
            getOwnerBounds,
            restartForUpdate);
    }

    public static ApplicationShellEffectExecutor CreateEffectExecutor(
        Action<RuntimeCommand> submitRuntimeCommand,
        SoundPlayerService soundPlayer,
        OverlayAnimationController overlayAnimations,
        Action toggleMouseClickThrough,
        Action clearSplitCompletionAnimation,
        Action<int> trackSegmentBestDeltaHighlight,
        Action<int> startSplitCompletionAnimation,
        Action resetUiScalePatchState,
        Action refreshTimerOverlaySettings,
        Action refreshRuntimeUi,
        Action<OperationResult> showSettingsSaveFailure,
        Action<AppSettings, int> applyLoadedSettings,
        Func<AppSettings, OperationResult> saveSettings,
        AutomationShell automationShell,
        Action resetRaceProgressReports,
        Action<bool, bool> queueRaceProgressReports)
    {
        return new ApplicationShellEffectExecutor(
            new DelegateRuntimeCommandPort(submitRuntimeCommand),
            new DelegateSoundPort(soundPlayer.StopAll, soundPlayer.Play),
            new DelegateOverlayPort(
                toggleMouseClickThrough,
                overlayAnimations.Clear,
                clearSplitCompletionAnimation,
                trackSegmentBestDeltaHighlight,
                startSplitCompletionAnimation,
                resetUiScalePatchState,
                refreshTimerOverlaySettings,
                refreshRuntimeUi),
            new DelegateSettingsPort(saveSettings, showSettingsSaveFailure, applyLoadedSettings),
            new DelegateAutomationPort(
                automationShell.StartCreateWorld,
                automationShell.ShowPracticeWorldSelector,
                () => automationShell.CancelCreateWorld(),
                () => automationShell.CancelEnterWorld()),
            new DelegateRaceProgressPort(resetRaceProgressReports, queueRaceProgressReports));
    }

    public static HighPrecisionScheduler CreateControlScheduler(Action queueControlTick)
    {
        return new HighPrecisionScheduler("TerrariaSplit UI control", _ => queueControlTick());
    }

    public static HighPrecisionScheduler CreateStatusPaintScheduler(Action<HighPrecisionSchedulerTick> queueStatusPaintTick)
    {
        return new HighPrecisionScheduler("TerrariaSplit status paint", queueStatusPaintTick);
    }
}
