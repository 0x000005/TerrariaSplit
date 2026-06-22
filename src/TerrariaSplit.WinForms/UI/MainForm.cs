using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed partial class MainForm : Form
{
    private static readonly TimeSpan SplitCompletionFadeDuration = TimeSpan.FromSeconds(0.45);
    private static readonly TimeSpan SplitCompletionDeltaIntroGap = TimeSpan.FromSeconds(0.06);
    private static readonly TimeSpan DefaultControlTickInterval = TimeSpan.FromMilliseconds(5);
    private const int ResizeBorder = 8;
    private const int RowGap = 9;
    private const int WsExTransparent = 0x20;
    private const int WsExLayered = 0x80000;
    private const string SegmentTimerWindowTitle = "TerrariaSplit - Segment Timer";

    private readonly WorldPoolStore worldPoolStore;
    private readonly ISettingsSnapshotFactory settingsSnapshots;
    private readonly IAppLogger appLogger;
    private readonly AutomationShell automationShell;
    private readonly WorldPoolFillService worldPoolFillService;
    private readonly MainFormContextMenuBuilder contextMenuBuilder;
    private readonly SoundPlayerService soundPlayer;
    private readonly HotkeyShell hotkeyShell;
    private readonly ContextMenuStrip contextMenu;
    private readonly ApplicationController applicationController;
    private readonly ApplicationShellEffectExecutor effectExecutor;
    private readonly SettingsShell settingsShell;
    private readonly RuntimeShell runtimeShell = new(
        DefaultControlTickInterval,
        RefreshRateSettings.ToInterval(AppSettingsDefaults.Advanced.RunningStatusPaintHz));
    private readonly OverlayShell overlayShell = new();
    private readonly ProgramModalWindowCoordinator modalWindows;
    private readonly MainWindowModalInputRouter mainWindowModalInputRouter;
    private readonly WindowShell windowShell = new();
    private bool runtimeResourcesDisposed;

    private AppSettings settings => applicationController.Settings;

    private ApplicationViewState viewState => applicationController.ViewState;

    private RuntimeRunSnapshot runtimeSnapshot => viewState.RuntimeSnapshot;

    private IReadOnlyList<SplitStatusSnapshot> splitStatuses => viewState.DisplayStatuses;

    private int currentSplitIndex => viewState.CurrentSplitIndex;

    private SplitTimerPhase timerPhase => viewState.TimerPhase;

    private TimeSpan timerElapsed => viewState.ElapsedNow();

    public MainForm(bool registerGlobalHotkeys = true)
    {
        runtimeShell.AttachDispatchActions(DispatchedControlTick, DispatchedStatusPaintTick);
        MainShellServices services = MainShellCompositionRoot.CreateCore(ShowPersonalBestUpdateConfirmation);
        worldPoolStore = services.WorldPoolStore;
        settingsSnapshots = services.SettingsSnapshots;
        appLogger = services.AppLogger;
        worldPoolFillService = services.WorldPoolFillService;
        contextMenuBuilder = services.ContextMenuBuilder;
        soundPlayer = services.SoundPlayer;
        hotkeyShell = new HotkeyShell(
            services.HotkeyManager,
            () => settings,
            () => Handle,
            () => IsHandleCreated,
            registerGlobalHotkeys,
            ShowHotkeyWarning);
        contextMenu = services.ContextMenu;
        applicationController = services.ApplicationController;
        RefreshTimerOverlaySettingsSnapshot();
        overlayShell.RefreshPalette(settings);
        RuntimePerformanceTracker performance = services.Performance;
        TerrariaMonitorCoordinator monitorCoordinator = MainShellCompositionRoot.CreateMonitorCoordinator(
            callback => BeginInvoke(callback),
            appLogger,
            performance);
        monitorCoordinator.WatcherPollCompleted += HandleWatcherPollCompleted;
        OverlayWindowController overlayWindowController = MainShellCompositionRoot.CreateOverlayWindowController(
            this,
            graphics =>
            {
                DrawStatusOverlay(graphics);
                return true;
            },
            elapsed => performance.RecordStatusPaint(elapsed));
        int initialReservedRowCount = GetCurrentReservedLayoutRowCount();
        int initialVisibleRowCount = GetCurrentLayoutRowCount();
        OverlayBoundsController overlayBoundsController = new OverlayBoundsController(
            RowGap,
            settings,
            initialReservedRowCount,
            initialVisibleRowCount);
        overlayShell.ApplyLayoutRowCounts(initialReservedRowCount, initialVisibleRowCount, force: true);
        overlayBoundsController.LayoutChanged += ApplyOverlayLayout;
        TimerOverlayWindowHost timerOverlayHost = MainShellCompositionRoot.CreateTimerOverlayWindowHost(
            callback => BeginInvoke(callback),
            elapsed => performance.RecordTimerOverlayPaint(elapsed),
            tick => performance.RecordTimerOverlayPaintTick(tick),
            performance.RecordTimerOverlayPaintDispatchSkipped,
            performance.RecordTimerOverlayPaintInputSkipped);
        overlayShell.AttachRuntimeComponents(
            overlayWindowController,
            overlayBoundsController,
            timerOverlayHost,
            services.RenderResources,
            services.OverlayAnimations);
        modalWindows = MainShellCompositionRoot.CreateModalWindowCoordinator(this, overlayShell.TimerOverlayHost);
        mainWindowModalInputRouter = MainShellCompositionRoot.CreateModalInputRouter(
            modalWindows,
            contextMenu,
            windowShell.CancelDrag);
        automationShell = MainShellCompositionRoot.CreateAutomationShell(
            worldPoolStore,
            () => settings,
            settingsSnapshots,
            modalWindows,
            this,
            () => AcceptRuntimeCommandSequence(monitorCoordinator.ClearPendingMenuActions()),
            appLogger);
        settingsShell = MainShellCompositionRoot.CreateSettingsShell(
            () => settings,
            GetRuntimeDiagnostics,
            GetRuntimeDebugSnapshot,
            GetWorldPoolCount,
            services.SettingsRepository,
            settingsSnapshots,
            callback => BeginInvoke(callback),
            ApplySettings,
            () => AcceptRuntimeCommandSequence(monitorCoordinator.ClearPendingMenuActions()),
            hotkeyShell.Unregister,
            hotkeyShell.Register,
            () => IsHandleCreated,
            modalWindows,
            () => Bounds);
        overlayShell.TimerOverlayHost.DragDeltaRequested += delta => overlayShell.BoundsController.MoveBy(delta);
        overlayShell.TimerOverlayHost.UserResizeBoundsChanged += bounds => overlayShell.BoundsController.HandleTimerResize(bounds);
        overlayShell.TimerOverlayHost.RightClickRequested += HandleTimerOverlayRightClickRequested;
        overlayShell.TimerOverlayHost.Activated += QueueMainWindowForegroundGroupSync;
        overlayShell.TimerOverlayHost.ModalActivationRequested += () => modalWindows.ActivateCurrentModal();
        effectExecutor = MainShellCompositionRoot.CreateEffectExecutor(
            SubmitRuntimeCommand,
            soundPlayer,
            overlayShell.Animations,
            ToggleMouseClickThrough,
            ClearSplitCompletionAnimation,
            TrackSegmentBestDeltaHighlight,
            StartSplitCompletionAnimation,
            monitorCoordinator.ResetUiScalePatchState,
            RefreshTimerOverlaySettingsSnapshot,
            RefreshRuntimeUi,
            ShowSettingsSaveFailure,
            ApplyLoadedSettings,
            services.SaveSettings,
            automationShell);
        overlayShell.BoundsController.UpdateContext(
            settings,
            GetCurrentReservedLayoutRowCount(),
            GetCurrentLayoutRowCount());
        AcceptRuntimeCommandSequence(monitorCoordinator.SetRuntimeDefinitions(applicationController.Definitions));
        Text = SegmentTimerWindowTitle;
        modalWindows.SetAlwaysOnTop(settings.General.AlwaysOnTop);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        ApplyLayoutBounds(useDefaultSize: true);
        DoubleBuffered = true;
        ResizeRedraw = true;
        ApplyLayeredOverlayWindowStyle();
        Padding = Padding.Empty;

        UpdateContextMenu();
        contextMenu.Opening += (_, e) =>
        {
            if (mainWindowModalInputRouter.TryRedirectFromMainInput())
            {
                e.Cancel = true;
                return;
            }

            if (settings.General.PracticeMode && IsEditablePracticePoint(PointToClient(Cursor.Position)))
            {
                e.Cancel = true;
                return;
            }
        };
        ContextMenuStrip = contextMenu;

        HighPrecisionScheduler controlScheduler = MainShellCompositionRoot.CreateControlScheduler(QueueControlTick);
        HighPrecisionScheduler statusPaintScheduler = MainShellCompositionRoot.CreateStatusPaintScheduler(QueueStatusPaintTick);
        runtimeShell.AttachRuntimeComponents(
            monitorCoordinator,
            controlScheduler,
            statusPaintScheduler,
            performance);

        runtimeShell.UpdateControlTickInterval(ResolveControlTickInterval());
        runtimeShell.ControlScheduler.Start(runtimeShell.ControlTickInterval);

        runtimeShell.UpdateStatusPaintInterval(ResolveRunningStatusPaintInterval());

        runtimeShell.Performance.ControlTickInterval = runtimeShell.ControlTickInterval;
        runtimeShell.Performance.StatusPaintInterval = runtimeShell.StatusPaintInterval;
        runtimeShell.Performance.WatcherPollInterval = runtimeShell.MonitorCoordinator.WatcherPollInterval;
        runtimeShell.Performance.ProcessLookupInterval = runtimeShell.MonitorCoordinator.ProcessLookupInterval;
        runtimeShell.MonitorCoordinator.UpdateReadyWatcherPollInterval(ResolveReadyWatcherPollInterval());
    }

}



