using System.Drawing;
using System.Diagnostics;
using System.Threading;
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
    private readonly HighPrecisionScheduler controlScheduler;
    private readonly HighPrecisionScheduler statusPaintScheduler;
    private readonly HotkeyShell hotkeyShell;
    private readonly OverlayRenderResources renderResources;
    private readonly OverlayAnimationController overlayAnimations;
    private readonly ContextMenuStrip contextMenu;
    private readonly RuntimePerformanceTracker performance;
    private readonly ApplicationController applicationController;
    private readonly ApplicationShellEffectExecutor effectExecutor;
    private readonly SettingsShell settingsShell;
    private readonly TerrariaMonitorCoordinator monitorCoordinator;
    private readonly RuntimeShell runtimeShell = new(
        DefaultControlTickInterval,
        RefreshRateSettings.ToInterval(AppSettingsDefaults.Advanced.RunningStatusPaintHz));
    private readonly OverlayShell overlayShell = new();
    private readonly OverlayWindowController overlayWindowController;
    private readonly OverlayBoundsController overlayBoundsController;
    private readonly TimerOverlayWindowHost timerOverlayHost;
    private readonly ProgramModalWindowCoordinator modalWindows;
    private readonly MainWindowModalInputRouter mainWindowModalInputRouter;
    private readonly WindowShell windowShell = new();
    private bool runtimeResourcesDisposed;
    private long timerOverlaySettingsRevision;

    private AppSettings timerOverlaySettingsSnapshot = new();
    private UiPalette palette;
    private readonly Action dispatchedControlTick;
    private readonly Action dispatchedStatusPaintTick;
    private bool statusOverlayContentDirty = true;
    private StatusOverlayDynamicKey? lastStatusOverlayDynamicKey;
    private Rectangle? statusOverlayPartialClipBounds;
    private AppSettings settings => applicationController.Settings;

    private ApplicationViewState viewState => applicationController.ViewState;

    private RuntimeRunSnapshot runtimeSnapshot => viewState.RuntimeSnapshot;

    private IReadOnlyList<SplitStatusSnapshot> splitStatuses => viewState.DisplayStatuses;

    private int currentSplitIndex => viewState.CurrentSplitIndex;

    private SplitTimerPhase timerPhase => viewState.TimerPhase;

    private TimeSpan timerElapsed => viewState.ElapsedNow();

    public MainForm(bool registerGlobalHotkeys = true)
    {
        dispatchedControlTick = DispatchedControlTick;
        dispatchedStatusPaintTick = DispatchedStatusPaintTick;
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
        renderResources = services.RenderResources;
        overlayAnimations = services.OverlayAnimations;
        contextMenu = services.ContextMenu;
        performance = services.Performance;
        applicationController = services.ApplicationController;
        RefreshTimerOverlaySettingsSnapshot();
        palette = UiPalette.From(settings.Overlay.Colors);
        monitorCoordinator = MainShellCompositionRoot.CreateMonitorCoordinator(
            callback => BeginInvoke(callback),
            appLogger,
            performance);
        monitorCoordinator.WatcherPollCompleted += HandleWatcherPollCompleted;
        overlayWindowController = MainShellCompositionRoot.CreateOverlayWindowController(
            this,
            graphics =>
            {
                DrawStatusOverlay(graphics);
                return true;
            },
            elapsed => performance.RecordStatusPaint(elapsed));
        int initialReservedRowCount = GetCurrentReservedLayoutRowCount();
        int initialVisibleRowCount = GetCurrentLayoutRowCount();
        overlayBoundsController = new OverlayBoundsController(
            RowGap,
            settings,
            initialReservedRowCount,
            initialVisibleRowCount);
        overlayShell.ApplyLayoutRowCounts(initialReservedRowCount, initialVisibleRowCount, force: true);
        overlayBoundsController.LayoutChanged += ApplyOverlayLayout;
        timerOverlayHost = MainShellCompositionRoot.CreateTimerOverlayWindowHost(
            callback => BeginInvoke(callback),
            elapsed => performance.RecordTimerOverlayPaint(elapsed),
            tick => performance.RecordTimerOverlayPaintTick(tick),
            performance.RecordTimerOverlayPaintDispatchSkipped,
            performance.RecordTimerOverlayPaintInputSkipped);
        modalWindows = MainShellCompositionRoot.CreateModalWindowCoordinator(this, timerOverlayHost);
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
            settingsSnapshots,
            callback => BeginInvoke(callback),
            ApplySettings,
            () => AcceptRuntimeCommandSequence(monitorCoordinator.ClearPendingMenuActions()),
            hotkeyShell.Unregister,
            hotkeyShell.Register,
            () => IsHandleCreated,
            modalWindows,
            () => Bounds);
        timerOverlayHost.DragDeltaRequested += delta => overlayBoundsController.MoveBy(delta);
        timerOverlayHost.UserResizeBoundsChanged += bounds => overlayBoundsController.HandleTimerResize(bounds);
        timerOverlayHost.RightClickRequested += HandleTimerOverlayRightClickRequested;
        timerOverlayHost.Activated += QueueMainWindowForegroundGroupSync;
        timerOverlayHost.ModalActivationRequested += () => modalWindows.ActivateCurrentModal();
        effectExecutor = MainShellCompositionRoot.CreateEffectExecutor(
            SubmitRuntimeCommand,
            soundPlayer,
            overlayAnimations,
            ToggleMouseClickThrough,
            ClearSplitCompletionAnimation,
            TrackSegmentBestDeltaHighlight,
            StartSplitCompletionAnimation,
            monitorCoordinator.ResetUiScalePatchState,
            RefreshTimerOverlaySettingsSnapshot,
            RefreshRuntimeUi,
            ShowSettingsSaveFailure,
            ApplyLoadedSettings,
            automationShell);
        overlayBoundsController.UpdateContext(
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

        controlScheduler = MainShellCompositionRoot.CreateControlScheduler(QueueControlTick);
        statusPaintScheduler = MainShellCompositionRoot.CreateStatusPaintScheduler(QueueStatusPaintTick);

        runtimeShell.UpdateControlTickInterval(ResolveControlTickInterval());
        controlScheduler.Start(runtimeShell.ControlTickInterval);

        runtimeShell.UpdateStatusPaintInterval(ResolveRunningStatusPaintInterval());

        performance.ControlTickInterval = runtimeShell.ControlTickInterval;
        performance.StatusPaintInterval = runtimeShell.StatusPaintInterval;
        performance.WatcherPollInterval = monitorCoordinator.WatcherPollInterval;
        performance.ProcessLookupInterval = monitorCoordinator.ProcessLookupInterval;
        monitorCoordinator.UpdateReadyWatcherPollInterval(ResolveReadyWatcherPollInterval());
    }

}



