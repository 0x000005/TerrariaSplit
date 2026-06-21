using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal static class MainShellCompositionRoot
{
    public static MainShellServices CreateCore(Func<string, bool> confirmPersonalBestUpdate)
    {
        IRuntimeDataPaths runtimeDataPaths = AppContextRuntimeDataPaths.Default;
        var worldPoolStore = new WorldPoolStore(runtimeDataPaths);
        ISettingsSnapshotFactory settingsSnapshots = new StoredSettingsSnapshotFactory();
        IAppLogger logger = StaticAppLogger.Instance;
        var runStatisticsRecorder = new DelegateRunStatisticsRecorder(RunStatsStore.RecordRun);
        var personalBestSnapshotStore = new DelegatePersonalBestSnapshotStore(
            SplitTimeSetStore.SavePersonalBestTimeSnapshot,
            SplitTimeSetStore.SavePersonalBestSegmentSnapshot);
        var applicationController = new ApplicationController(
            AppSettingsStore.Load(),
            confirmPersonalBestUpdate,
            settingsSnapshots,
            runStatisticsRecorder,
            personalBestSnapshotStore);

        return new MainShellServices(
            worldPoolStore,
            settingsSnapshots,
            logger,
            new WorldPoolFillService(worldPoolStore, settingsSnapshots, logger, runtimeDataPaths),
            new MainFormContextMenuBuilder(),
            new SoundPlayerService(),
            new GlobalHotkeyManager(),
            new OverlayRenderResources(),
            new OverlayAnimationController(),
            new ContextMenuStrip(),
            new RuntimePerformanceTracker(),
            applicationController);
    }

    public static TerrariaMonitorCoordinator CreateMonitorCoordinator(
        Action<Action> dispatch,
        IAppLogger logger,
        RuntimePerformanceTracker performance)
    {
        return new TerrariaMonitorCoordinator(
            new TerrariaWorldWatcher(),
            new TerrariaUiScalePatchApplierAdapter(),
            dispatch,
            logger,
            shouldYieldDispatch: UiInputMessageProbe.HasPendingInputMessage,
            recordPoll: performance.RecordWatcherPoll);
    }

    public static OverlayWindowController CreateOverlayWindowController(
        Form owner,
        Func<Graphics, bool> render,
        Action<TimeSpan> recordPaint)
    {
        return new OverlayWindowController(owner, render, recordPaint);
    }

    public static TimerOverlayWindowHost CreateTimerOverlayWindowHost(
        Action<Action> dispatch,
        Action<TimeSpan> recordPaint,
        Action<HighPrecisionSchedulerTick> recordPaintTick,
        Action recordDispatchSkipped,
        Action recordInputSkipped)
    {
        return new TimerOverlayWindowHost(
            dispatch,
            recordPaint,
            recordPaintTick,
            recordDispatchSkipped,
            recordInputSkipped);
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
        ContextMenuStrip contextMenu,
        Action cancelDragging)
    {
        return new MainWindowModalInputRouter(modalWindows, contextMenu, cancelDragging);
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
        Func<RuntimePerformanceDiagnostics> getRuntimeDiagnostics,
        Func<RuntimeDebugSnapshot> getRuntimeDebugSnapshot,
        Func<AppSettings, int> getWorldPoolCount,
        ISettingsSnapshotFactory settingsSnapshots,
        Action<Action> dispatch,
        Action<AppSettings> applySettings,
        Action clearPendingMenuActions,
        Action disposeHotkeys,
        Action registerHotkeys,
        Func<bool> isMainHandleCreated,
        ProgramModalWindowCoordinator modalWindows,
        Func<Rectangle> getOwnerBounds)
    {
        return new SettingsShell(
            getSettings,
            getRuntimeDiagnostics,
            getRuntimeDebugSnapshot,
            getWorldPoolCount,
            settingsSnapshots,
            dispatch,
            applySettings,
            clearPendingMenuActions,
            disposeHotkeys,
            registerHotkeys,
            isMainHandleCreated,
            modalWindows,
            getOwnerBounds);
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
        AutomationShell automationShell)
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
            new DelegateSettingsPort(AppSettingsStore.TrySave, showSettingsSaveFailure, applyLoadedSettings),
            new DelegateAutomationPort(
                automationShell.StartCreateWorld,
                automationShell.ShowPracticeWorldSelector,
                () => automationShell.CancelCreateWorld(),
                () => automationShell.CancelEnterWorld()));
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
