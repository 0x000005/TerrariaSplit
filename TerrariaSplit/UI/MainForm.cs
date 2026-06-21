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

    private readonly WorldPoolStore worldPoolStore = new();
    private readonly ISettingsSnapshotFactory settingsSnapshots = new StoredSettingsSnapshotFactory();
    private readonly IAppLogger appLogger = StaticAppLogger.Instance;
    private readonly AutomationShell automationShell;
    private readonly WorldPoolFillService worldPoolFillService;
    private readonly MainFormContextMenuBuilder contextMenuBuilder = new();
    private readonly SoundPlayerService soundPlayer = new();
    private readonly HighPrecisionScheduler controlScheduler;
    private readonly HighPrecisionScheduler statusPaintScheduler;
    private readonly GlobalHotkeyManager hotkeyManager = new();
    private readonly OverlayRenderResources renderResources = new();
    private readonly OverlayAnimationController overlayAnimations = new();
    private readonly ContextMenuStrip contextMenu = new();
    private readonly RuntimePerformanceTracker performance = new();
    private readonly ApplicationController applicationController;
    private readonly ApplicationShellEffectExecutor effectExecutor;
    private readonly SettingsShell settingsShell;
    private readonly TerrariaMonitorCoordinator monitorCoordinator;
    private readonly OverlayWindowController overlayWindowController;
    private readonly OverlayBoundsController overlayBoundsController;
    private readonly TimerOverlayWindowHost timerOverlayHost;
    private readonly ProgramModalWindowCoordinator modalWindows;
    private readonly MainWindowModalInputRouter mainWindowModalInputRouter;
    private readonly object runtimeDebugSnapshotLock = new();
    private bool mouseClickThrough;
    private bool dragging;
    private Point dragStartCursor;
    private bool closeFinalizationPending;
    private bool closeFinalizationComplete;
    private int runtimeOverlayPaintSuspensionCount;
    private bool runtimeControlSchedulerSuspended;
    private string? lastHotkeyWarningText;
    private bool closing;
    private bool runtimeResourcesDisposed;
    private string currentWindowText = string.Empty;
    private bool overlayWindowsInitialized;
    private bool overlayWindowInitializationInProgress;
    private bool statusBoundsFeedbackEnabled;
    private bool suppressStatusBoundsFeedback;
    private Rectangle? pendingInitialCompositeBounds;
    private TimeSpan controlTickInterval = DefaultControlTickInterval;
    private TimeSpan statusPaintInterval = RefreshRateSettings.ToInterval(AppSettingsDefaults.Advanced.RunningStatusPaintHz);
    private long timerOverlaySettingsRevision;
    private int controlTickDispatchPending;
    private int statusPaintDispatchPending;
    private int appliedOverlayReservedRowCount = -1;
    private int appliedOverlayVisibleRowCount = -1;

    private AppSettings timerOverlaySettingsSnapshot = new();
    private UiPalette palette;
    private readonly Action dispatchedControlTick;
    private readonly Action dispatchedStatusPaintTick;
    private readonly bool registerGlobalHotkeys;
    private bool statusOverlayContentDirty = true;
    private StatusOverlayDynamicKey? lastStatusOverlayDynamicKey;
    private Rectangle? statusOverlayPartialClipBounds;
    private TerrariaWatcherDiagnostics watcherDiagnostics = TerrariaWatcherDiagnosticsDefaults.Empty;
    private TerrariaWatchSnapshot snapshot =
        new(false, null, false, null, TerrariaGameFacts.Unknown, TerrariaWorldGenerationState.Unknown, false, "waiting for Terraria.exe");
    private AppSettings settings => applicationController.Settings;

    private ApplicationViewState viewState => applicationController.ViewState;

    private RuntimeRunSnapshot runtimeSnapshot => viewState.RuntimeSnapshot;

    private IReadOnlyList<SplitStatusSnapshot> splitStatuses => viewState.DisplayStatuses;

    private int currentSplitIndex => viewState.CurrentSplitIndex;

    private SplitTimerPhase timerPhase => viewState.TimerPhase;

    private TimeSpan timerElapsed => viewState.ElapsedNow();

    public MainForm(bool registerGlobalHotkeys = true)
    {
        this.registerGlobalHotkeys = registerGlobalHotkeys;
        dispatchedControlTick = DispatchedControlTick;
        dispatchedStatusPaintTick = DispatchedStatusPaintTick;
        applicationController = new ApplicationController(
            AppSettingsStore.Load(),
            ShowPersonalBestUpdateConfirmation,
            settingsSnapshots);
        worldPoolFillService = new WorldPoolFillService(worldPoolStore, settingsSnapshots, appLogger);
        RefreshTimerOverlaySettingsSnapshot();
        palette = UiPalette.From(settings.Overlay.Colors);
        monitorCoordinator = new TerrariaMonitorCoordinator(
            new TerrariaWorldWatcher(),
            new TerrariaUiScalePatchApplierAdapter(),
            callback => BeginInvoke(callback),
            appLogger,
            shouldYieldDispatch: UiInputMessageProbe.HasPendingInputMessage,
            recordPoll: performance.RecordWatcherPoll);
        monitorCoordinator.WatcherPollCompleted += HandleWatcherPollCompleted;
        overlayWindowController = new OverlayWindowController(
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
        appliedOverlayReservedRowCount = initialReservedRowCount;
        appliedOverlayVisibleRowCount = initialVisibleRowCount;
        overlayBoundsController.LayoutChanged += ApplyOverlayLayout;
        timerOverlayHost = new TimerOverlayWindowHost(
            callback => BeginInvoke(callback),
            elapsed => performance.RecordTimerOverlayPaint(elapsed),
            tick => performance.RecordTimerOverlayPaintTick(tick),
            performance.RecordTimerOverlayPaintDispatchSkipped,
            performance.RecordTimerOverlayPaintInputSkipped);
        modalWindows = new ProgramModalWindowCoordinator(
            this,
            timerOverlayHost.ApplyInteractionBlocked,
            () => timerOverlayHost.WindowHandle);
        mainWindowModalInputRouter = new MainWindowModalInputRouter(
            modalWindows,
            contextMenu,
            () => dragging = false);
        automationShell = new AutomationShell(
            worldPoolStore,
            () => settings,
            settingsSnapshots,
            modalWindows,
            this,
            () => AcceptRuntimeCommandSequence(monitorCoordinator.ClearPendingMenuActions()),
            appLogger);
        settingsShell = new SettingsShell(
            () => settings,
            GetRuntimeDiagnostics,
            GetRuntimeDebugSnapshot,
            GetWorldPoolCount,
            callback => BeginInvoke(callback),
            ApplySettings,
            () => AcceptRuntimeCommandSequence(monitorCoordinator.ClearPendingMenuActions()),
            hotkeyManager.Dispose,
            RegisterConfiguredHotkeys,
            () => IsHandleCreated,
            modalWindows,
            () => Bounds);
        timerOverlayHost.DragDeltaRequested += delta => overlayBoundsController.MoveBy(delta);
        timerOverlayHost.UserResizeBoundsChanged += bounds => overlayBoundsController.HandleTimerResize(bounds);
        timerOverlayHost.RightClickRequested += HandleTimerOverlayRightClickRequested;
        timerOverlayHost.Activated += QueueMainWindowForegroundGroupSync;
        timerOverlayHost.ModalActivationRequested += () => modalWindows.ActivateCurrentModal();
        effectExecutor = new ApplicationShellEffectExecutor(
            new DelegateRuntimeCommandPort(SubmitRuntimeCommand),
            new DelegateSoundPort(soundPlayer.StopAll, soundPlayer.Play),
            new DelegateOverlayPort(
                ToggleMouseClickThrough,
                overlayAnimations.Clear,
                ClearSplitCompletionAnimation,
                TrackSegmentBestDeltaHighlight,
                StartSplitCompletionAnimation,
                monitorCoordinator.ResetUiScalePatchState,
                RefreshTimerOverlaySettingsSnapshot,
                RefreshRuntimeUi),
            new DelegateSettingsPort(AppSettingsStore.Save, ApplyLoadedSettings),
            new DelegateAutomationPort(
                automationShell.StartCreateWorld,
                automationShell.ShowPracticeWorldSelector,
                () => automationShell.CancelCreateWorld(),
                () => automationShell.CancelEnterWorld()));
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

        controlScheduler = new HighPrecisionScheduler("TerrariaSplit UI control", _ => QueueControlTick());
        statusPaintScheduler = new HighPrecisionScheduler("TerrariaSplit status paint", QueueStatusPaintTick);

        controlTickInterval = ResolveControlTickInterval();
        controlScheduler.Start(controlTickInterval);

        statusPaintInterval = ResolveRunningStatusPaintInterval();

        performance.ControlTickInterval = controlTickInterval;
        performance.StatusPaintInterval = statusPaintInterval;
        performance.WatcherPollInterval = monitorCoordinator.WatcherPollInterval;
        performance.ProcessLookupInterval = monitorCoordinator.ProcessLookupInterval;
        monitorCoordinator.UpdateReadyWatcherPollInterval(ResolveReadyWatcherPollInterval());
    }

}



