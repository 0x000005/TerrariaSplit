using System.Drawing;
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

    private readonly StartupCore startupCore;
    private readonly ISettingsSnapshotFactory settingsSnapshots;
    private readonly IAppLogger appLogger;
    private readonly HotkeyShell hotkeyShell;
    private readonly ApplicationController applicationController;
    private readonly RuntimeShell runtimeShell = new(
        DefaultControlTickInterval,
        RefreshRateSettings.ToInterval(AppSettingsDefaults.Advanced.RunningStatusPaintHz));
    private readonly OverlayShell overlayShell = new();
    private readonly RtssOverlayPublisher rtssOverlayPublisher = new();
    private readonly HighPrecisionScheduler rtssOverlayScheduler;
    private readonly ProgramModalWindowCoordinator modalWindows;
    private readonly MainWindowModalInputRouter mainWindowModalInputRouter;
    private readonly WindowShell windowShell = new();
    private readonly StartupCommandGate startupCommandGate = new();
    private readonly RuntimeBootstrapper runtimeBootstrapper = new();
    private readonly TaskCompletionSource<bool> statusFirstFramePresented =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private RuntimeServices? runtimeServices;
    private ContextMenuStrip? contextMenu;
    private Task? runtimeInitializationTask;
    private int firstFrameComponentCount;
    private int statusRenderCount;
    private StatisticsForm? statisticsForm;
    private int rtssOverlayDispatchPending;
    private bool runtimeResourcesDisposed;
    private RtssOverlayPublishStatus lastRtssOverlayStatus = RtssOverlayPublishStatus.Disabled;
    private string lastRtssOverlayMessage = string.Empty;

    private RuntimeServices RuntimeServices =>
        runtimeServices ?? throw new InvalidOperationException("Runtime services are not fully initialized.");

    private WorldPoolStore worldPoolStore => RuntimeServices.WorldPoolStore;

    private WorldPoolFillService worldPoolFillService => RuntimeServices.WorldPoolFillService;

    private MainFormContextMenuBuilder contextMenuBuilder => RuntimeServices.ContextMenuBuilder;

    private SoundPlayerService soundPlayer => RuntimeServices.SoundPlayer;

    private AutomationShell automationShell => RuntimeServices.AutomationShell;

    private SettingsShell settingsShell => RuntimeServices.SettingsShell;

    private RaceShell raceShell => RuntimeServices.RaceShell;

    private ApplicationShellEffectExecutor effectExecutor => RuntimeServices.EffectExecutor;

    private bool IsRuntimeReady =>
        runtimeServices is not null && runtimeBootstrapper.Phase == StartupPhase.FullyReady;

    private bool IsRaceRoomActive =>
        runtimeServices?.RaceShell.IsInRoom == true || applicationController.SystemState.Race.IsInRoom;

    private bool CanEditPracticeTimes => settings.General.PracticeMode && !IsRaceRoomActive;

    internal StartupPhase CurrentStartupPhase => runtimeBootstrapper.Phase;

    private AppSettings settings => applicationController.Settings;

    private AppSettings editableSettings => applicationController.BaseSettings;

    private ApplicationViewState viewState => applicationController.ViewState;

    private RuntimeRunSnapshot runtimeSnapshot => viewState.RuntimeSnapshot;

    private IReadOnlyList<SplitStatusSnapshot> splitStatuses => viewState.DisplayStatuses;

    private int currentSplitIndex => viewState.CurrentSplitIndex;

    private SplitTimerPhase timerPhase => viewState.TimerPhase;

    private TimeSpan timerElapsed => viewState.ElapsedNow();

    public MainForm(bool registerGlobalHotkeys = true)
    {
        StartupDiagnostics.RecordTrace("MainFormConstructing");
        runtimeShell.AttachDispatchActions(DispatchedControlTick, DispatchedStatusPaintTick);
        rtssOverlayScheduler = new HighPrecisionScheduler("TerrariaSplit RTSS overlay", QueueRtssOverlayTick);
        startupCore = MainShellCompositionRoot.CreateStartupCore(SilentPersonalBestUpdateConfirmation);
        StartupDiagnostics.RecordTrace("StartupCoreReady");
        settingsSnapshots = startupCore.SettingsSnapshots;
        appLogger = startupCore.AppLogger;
        applicationController = startupCore.ApplicationController;
        hotkeyShell = new HotkeyShell(
            startupCore.HotkeyManager,
            () => settings,
            () => Handle,
            () => IsHandleCreated,
            registerGlobalHotkeys);
        RefreshTimerOverlaySettingsSnapshot();
        overlayShell.RefreshPalette(settings);

        OverlayWindowController overlayWindowController = MainShellCompositionRoot.CreateOverlayWindowController(
            this,
            graphics =>
            {
                DrawStatusOverlay(graphics);
                return true;
            });
        overlayWindowController.FirstFrameRendered += () =>
        {
            StartupDiagnostics.RecordTrace("StatusFrame");
            statusFirstFramePresented.TrySetResult(true);
            MarkFirstFrameComponentRendered();
        };
        int initialReservedRowCount = GetCurrentReservedLayoutRowCount();
        int initialVisibleRowCount = GetCurrentLayoutRowCount();
        OverlayBoundsController overlayBoundsController = new(
            RowGap,
            settings,
            initialReservedRowCount,
            initialVisibleRowCount);
        overlayShell.ApplyLayoutRowCounts(initialReservedRowCount, initialVisibleRowCount, force: true);
        overlayBoundsController.LayoutChanged += ApplyOverlayLayout;
        TimerOverlayWindowHost timerOverlayHost = MainShellCompositionRoot.CreateTimerOverlayWindowHost(
            callback => BeginInvoke(callback));
        overlayShell.AttachRuntimeComponents(
            overlayWindowController,
            overlayBoundsController,
            timerOverlayHost,
            startupCore.RenderResources,
            startupCore.OverlayAnimations);
        _ = timerOverlayHost.StartAsync(runtimeBootstrapper.CancellationToken);
        modalWindows = MainShellCompositionRoot.CreateModalWindowCoordinator(this, overlayShell.TimerOverlayHost);
        mainWindowModalInputRouter = MainShellCompositionRoot.CreateModalInputRouter(
            modalWindows,
            () => contextMenu,
            windowShell.CancelDrag);

        overlayShell.TimerOverlayHost.DragDeltaRequested += delta => overlayShell.BoundsController.MoveBy(delta);
        overlayShell.TimerOverlayHost.DragCompleted += PersistOverlayWindowPosition;
        overlayShell.TimerOverlayHost.UserResizeBoundsChanged += bounds => overlayShell.BoundsController.HandleTimerResize(bounds);
        overlayShell.TimerOverlayHost.RightClickRequested += HandleTimerOverlayRightClickRequested;
        overlayShell.TimerOverlayHost.Activated += QueueMainWindowForegroundGroupSync;
        overlayShell.TimerOverlayHost.ModalActivationRequested += () => modalWindows.ActivateCurrentModal();
        overlayShell.TimerOverlayHost.FirstFrameRendered += MarkFirstFrameComponentRendered;
        overlayShell.BoundsController.UpdateContext(
            settings,
            GetCurrentReservedLayoutRowCount(),
            GetCurrentLayoutRowCount());

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

        StartupDiagnostics.RecordTrace("MainFormConstructed");
    }

    private static bool SilentPersonalBestUpdateConfirmation(string promptText)
    {
        StaticAppLogger.Instance.Info(
            "Personal best update confirmation handled silently during runtime: " + promptText);
        return true;
    }

    private void MarkFirstFrameComponentRendered()
    {
        if (Interlocked.Increment(ref firstFrameComponentCount) != 2)
        {
            return;
        }

        StartupDiagnostics.RecordTrace("FirstFrameRendered");
        StartupDiagnostics.SignalFirstFrame();
    }
}
