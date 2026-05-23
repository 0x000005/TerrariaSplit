using System.Drawing;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace TerrariaSplit;

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

    private readonly RunSessionController runSession = new();
    private readonly TerrariaWorldAutomation worldAutomation = new();
    private readonly MainFormContextMenuBuilder contextMenuBuilder = new();
    private readonly SoundPlayerService soundPlayer = new();
    private readonly HighPrecisionScheduler controlScheduler;
    private readonly HighPrecisionScheduler statusPaintScheduler;
    private readonly GlobalHotkeyManager hotkeyManager = new();
    private readonly Queue<TimerHotkeyRequest> pendingHotkeyRequests = new();
    private readonly OverlayRenderResources renderResources = new();
    private readonly Dictionary<int, SegmentBestDeltaHighlight> segmentBestDeltaHighlights = new();
    private readonly ContextMenuStrip contextMenu = new();
    private readonly RuntimePerformanceTracker performance = new();
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
    private SplitCompletionAnimation? splitCompletionAnimation;
    private bool closeFinalizationPending;
    private bool closeFinalizationComplete;
    private bool settingsFormOpen;
    private int runtimeOverlayPaintSuspensionCount;
    private bool runtimeControlSchedulerSuspended;
    private string? lastHotkeyWarningText;
    private bool closing;
    private bool runtimeResourcesDisposed;
    private string currentWindowText = string.Empty;
    private long minimumAcceptedRuntimeCommandSequence;
    private IDisposable? settingsModalWindowRegistration;
    private bool overlayWindowsInitialized;
    private bool overlayWindowInitializationInProgress;
    private bool statusBoundsFeedbackEnabled;
    private bool suppressStatusBoundsFeedback;
    private Rectangle? pendingInitialCompositeBounds;
    private TimeSpan controlTickInterval = DefaultControlTickInterval;
    private TimeSpan statusPaintInterval = RefreshRateSettings.ToInterval(AppSettingsDefaults.Advanced.RunningStatusPaintHz);
    private SettingsDialogHost? settingsDialogHost;
    private long timerOverlaySettingsRevision;
    private int controlTickDispatchPending;
    private int statusPaintDispatchPending;

    private AppSettings settings = AppSettingsStore.Load();
    private AppSettings timerOverlaySettingsSnapshot = new();
    private UiPalette palette;
    private TerrariaWatcherDiagnostics watcherDiagnostics = TerrariaWatcherDiagnosticsDefaults.Empty;
    private TerrariaWatchSnapshot snapshot =
        new(false, null, false, null, TerrariaBossStates.Unknown, TerrariaWorldGenerationState.Unknown, false, "waiting for Terraria.exe");

    private SplitTimer runTimer => runSession.Timer;

    private BossSplitTracker splitTracker => runSession.SplitTracker;

    public MainForm()
    {
        RefreshTimerOverlaySettingsSnapshot();
        palette = UiPalette.From(settings.Colors);
        monitorCoordinator = new TerrariaMonitorCoordinator(
            new TerrariaWorldWatcher(),
            new TerrariaUiScalePatchApplierAdapter(),
            callback => BeginInvoke(callback),
            shouldYieldDispatch: UiInputMessageProbe.HasPendingInputMessage);
        monitorCoordinator.WatcherPollCompleted += HandleWatcherPollCompleted;
        overlayWindowController = new OverlayWindowController(
            this,
            graphics =>
            {
                DrawStatusOverlay(graphics);
                return true;
            },
            elapsed => performance.RecordStatusPaint(elapsed));
        overlayBoundsController = new OverlayBoundsController(RowGap, settings, runSession.SplitTracker.Statuses.Count);
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
        timerOverlayHost.DragDeltaRequested += delta => overlayBoundsController.MoveBy(delta);
        timerOverlayHost.UserResizeBoundsChanged += bounds => overlayBoundsController.HandleTimerResize(bounds);
        timerOverlayHost.RightClickRequested += HandleTimerOverlayRightClickRequested;
        timerOverlayHost.Activated += QueueMainWindowForegroundGroupSync;
        timerOverlayHost.ModalActivationRequested += () => modalWindows.ActivateCurrentModal();
        IReadOnlyList<BossSplitDefinition> initialDefinitions = BossSplitDefinitions.Build(settings);
        runSession.SetDefinitions(initialDefinitions);
        overlayBoundsController.UpdateContext(settings, runSession.SplitTracker.Statuses.Count);
        minimumAcceptedRuntimeCommandSequence = monitorCoordinator.SetRuntimeDefinitions(initialDefinitions);
        Text = SegmentTimerWindowTitle;
        modalWindows.SetAlwaysOnTop(settings.AlwaysOnTop);
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

            if (settings.PracticeMode && IsEditablePracticePoint(PointToClient(Cursor.Position)))
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

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.Style = OverlayWindowController.ComposeBorderlessStyle(parameters.Style);
            parameters.ExStyle |= WsExLayered;
            if (mouseClickThrough)
            {
                parameters.ExStyle |= WsExTransparent;
            }

            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        overlayWindowController.ApplyWindowStyle(mouseClickThrough);
        InitializeOverlayWindows();
        modalWindows.ApplyWindowState();
        if (!settingsFormOpen)
        {
            RegisterConfiguredHotkeys();
        }

        Invalidate();
        QueueStatusOverlayRender();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        QueueStatusOverlayRender();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        if (IsHandleCreated)
        {
            QueueMainWindowForegroundGroupSync(Handle);
        }
    }

    private void QueueMainWindowForegroundGroupSync(IntPtr activatedHandle)
    {
        if (activatedHandle == IntPtr.Zero || !CanDispatchToUiThread())
        {
            return;
        }

        try
        {
            BeginInvoke(new Action(() =>
            {
                if (CanDispatchToUiThread())
                {
                    modalWindows.SyncMainWindowGroup(activatedHandle);
                }
            }));
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    protected override void OnInvalidated(InvalidateEventArgs e)
    {
        base.OnInvalidated(e);
        QueueStatusOverlayRender();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        QueueStatusOverlayRender();
        NotifyStatusBoundsChanged();
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        QueueStatusOverlayRender();
        NotifyStatusBoundsChanged();
    }

    private void UpdateContextMenu()
    {
        contextMenuBuilder.Rebuild(
            contextMenu,
            settings,
            OpenStatistics,
            OpenSettings,
            SwitchSettingsFile,
            Close);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        closing = true;
        DisposeRuntimeResources();
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            closing = true;
            DisposeRuntimeResources();
        }

        base.Dispose(disposing);
    }

    private void DisposeRuntimeResources()
    {
        if (runtimeResourcesDisposed)
        {
            return;
        }

        runtimeResourcesDisposed = true;
        controlScheduler.Dispose();
        statusPaintScheduler.Dispose();
        hotkeyManager.Dispose();
        monitorCoordinator.Dispose();
        worldAutomation.Dispose();
        settingsDialogHost?.Dispose();
        settingsDialogHost = null;
        settingsModalWindowRegistration?.Dispose();
        settingsModalWindowRegistration = null;
        timerOverlayHost.Dispose();
        overlayWindowController.Dispose();
        renderResources.Dispose();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        hotkeyManager.Dispose();
        base.OnHandleDestroyed(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (closeFinalizationComplete)
        {
            base.OnFormClosing(e);
            return;
        }

        if (closeFinalizationPending)
        {
            e.Cancel = true;
            return;
        }

        closeFinalizationPending = true;
        e.Cancel = true;
        BeginInvoke(new Action(() =>
        {
            try
            {
                FinalizeRunBeforeExit();
            }
            finally
            {
                closeFinalizationPending = false;
                closeFinalizationComplete = true;
                Close();
            }
        }));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (mainWindowModalInputRouter.TryRedirectFromMainInput())
        {
            return;
        }

        base.OnMouseDown(e);
        if (IsHandleCreated)
        {
            modalWindows.SyncMainWindowGroup(Handle);
        }

        if (e.Button == MouseButtons.Left &&
            !OverlayResizeHitTest.IsResizeZone(
                e.Location,
                ClientSize,
                ResizeBorder,
                OverlayResizeEdges.Left | OverlayResizeEdges.Top | OverlayResizeEdges.Right | OverlayResizeEdges.Bottom))
        {
            dragging = true;
            dragStartCursor = Cursor.Position;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (mainWindowModalInputRouter.HasModalWindow)
        {
            dragging = false;
            return;
        }

        base.OnMouseMove(e);
        if (!dragging)
        {
            return;
        }

        Point currentCursor = Cursor.Position;
        Point delta = new(currentCursor.X - dragStartCursor.X, currentCursor.Y - dragStartCursor.Y);
        if (delta.X == 0 && delta.Y == 0)
        {
            return;
        }

        dragStartCursor = currentCursor;
        overlayBoundsController.MoveBy(delta);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (mainWindowModalInputRouter.TryRedirectFromMainInput())
        {
            dragging = false;
            return;
        }

        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            dragging = false;
        }

        if (e.Button == MouseButtons.Right && settings.PracticeMode)
        {
            TryOpenPracticeEdit(e.Location);
        }
    }

    private bool IsEditablePracticePoint(Point point)
    {
        if (!TryGetSplitRowAt(point, out int rowIndex, out Rectangle rowRect))
        {
            return false;
        }

        ColumnRects columns = SplitListRenderer.GetColumnRects(settings, rowRect);
        if (columns.Time is Rectangle timeRect && timeRect.Contains(point))
        {
            BossSplitStatus status = splitTracker.Statuses[rowIndex];
            return status.IsCompleted;
        }

        return false;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        QueueStatusOverlayRender();
    }

    protected override void WndProc(ref Message m)
    {
        const int wmNcHitTest = 0x84;
        const int htTransparent = -1;
        const int htClient = 1;

        if (mainWindowModalInputRouter.TryHandleWindowMessage(ref m))
        {
            return;
        }

        if (hotkeyManager.TryGetAction(m, out TimerHotkeyAction action))
        {
            if (worldAutomation.IsCreateWorldRunning && action != TimerHotkeyAction.CreateWorld)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            if (worldAutomation.IsEnterWorldRunning && action != TimerHotkeyAction.PracticeWorld)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            if (TryHandleImmediateHotkey(action))
            {
                m.Result = IntPtr.Zero;
                return;
            }

            pendingHotkeyRequests.Enqueue(new TimerHotkeyRequest(action, DateTime.UtcNow));
            m.Result = IntPtr.Zero;
            return;
        }

        base.WndProc(ref m);

        if (mouseClickThrough && m.Msg == wmNcHitTest)
        {
            m.Result = (IntPtr)htTransparent;
            return;
        }

        if (m.Msg != wmNcHitTest || m.Result != (IntPtr)htClient)
        {
            return;
        }

        long lParam = m.LParam.ToInt64();
        int x = unchecked((short)(lParam & 0xFFFF));
        int y = unchecked((short)((lParam >> 16) & 0xFFFF));
        Point point = PointToClient(new Point(x, y));
        IntPtr? hit = OverlayResizeHitTest.Resolve(
            point,
            ClientSize,
            ResizeBorder,
            OverlayResizeEdges.Left | OverlayResizeEdges.Top | OverlayResizeEdges.Right | OverlayResizeEdges.Bottom);
        if (hit.HasValue)
        {
            m.Result = hit.Value;
        }
    }

    private void ControlTick()
    {
        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            TimerHotkeyRequest[] hotkeyRequests = DrainPendingHotkeyRequests();
            hotkeyRequests = CancelRequestedAutomations(hotkeyRequests);
            hotkeyRequests = ProcessLocalHotkeyRequests(hotkeyRequests);
            monitorCoordinator.Tick(
                runTimer.Phase,
                settings.Advanced?.EnableTerrariaUiScalePatch == true,
                hotkeyRequests);
            ProcessUiTick();
        }
        finally
        {
            UpdateStatusPaintSchedulerState();
            performance.RecordControlTick(Stopwatch.GetElapsedTime(startTimestamp));
        }
    }

    private void QueueControlTick()
    {
        if (!CanDispatchToUiThread())
        {
            return;
        }

        if (Interlocked.Exchange(ref controlTickDispatchPending, 1) == 1)
        {
            return;
        }

        try
        {
            BeginInvoke(new Action(() =>
            {
                try
                {
                    if (CanDispatchToUiThread())
                    {
                        ControlTick();
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref controlTickDispatchPending, 0);
                }
            }));
        }
        catch (ObjectDisposedException)
        {
            Interlocked.Exchange(ref controlTickDispatchPending, 0);
        }
        catch (InvalidOperationException)
        {
            Interlocked.Exchange(ref controlTickDispatchPending, 0);
        }
    }

    private void QueueStatusPaintTick(HighPrecisionSchedulerTick tick)
    {
        performance.RecordStatusPaintTick(tick);

        if (!CanDispatchToUiThread())
        {
            return;
        }

        if (Interlocked.Exchange(ref statusPaintDispatchPending, 1) == 1)
        {
            performance.RecordStatusPaintDispatchSkipped();
            return;
        }

        try
        {
            BeginInvoke(new Action(() =>
            {
                try
                {
                    if (!CanDispatchToUiThread())
                    {
                        return;
                    }

                    if (!UiInputMessageProbe.HasPendingInputMessage())
                    {
                        RenderStatusOverlayTick();
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref statusPaintDispatchPending, 0);
                }
            }));
        }
        catch (ObjectDisposedException)
        {
            Interlocked.Exchange(ref statusPaintDispatchPending, 0);
        }
        catch (InvalidOperationException)
        {
            Interlocked.Exchange(ref statusPaintDispatchPending, 0);
        }
    }

    private bool CanDispatchToUiThread()
    {
        return !closing && IsHandleCreated && !IsDisposed && !Disposing;
    }

    private void ProcessUiTick()
    {
        monitorCoordinator.UpdateRunPhase(runTimer.Phase);
        UpdateWindowTitle();
    }

    private TimerHotkeyRequest[] ProcessLocalHotkeyRequests(IReadOnlyCollection<TimerHotkeyRequest> hotkeyRequests)
    {
        if (hotkeyRequests.Count == 0)
        {
            return [];
        }

        var forwardedRequests = new List<TimerHotkeyRequest>(hotkeyRequests.Count);
        foreach (TimerHotkeyRequest request in hotkeyRequests)
        {
            if (request.Action == TimerHotkeyAction.MouseClickThrough)
            {
                SetMouseClickThrough(!mouseClickThrough);
                InvalidateRuntimeRenderRegion();
            }
            else
            {
                forwardedRequests.Add(request);
            }
        }

        return forwardedRequests.ToArray();
    }

    private bool TryHandleImmediateHotkey(TimerHotkeyAction action)
    {
        if (action == TimerHotkeyAction.MouseClickThrough)
        {
            SetMouseClickThrough(!mouseClickThrough);
            InvalidateRuntimeRenderRegion();
            return true;
        }

        if (action != TimerHotkeyAction.PauseResume)
        {
            return false;
        }

        TogglePauseImmediately();
        return true;
    }

    private void TogglePauseImmediately()
    {
        SplitTimerPhase previousPhase = runTimer.Phase;
        if (previousPhase == SplitTimerPhase.NotStarted)
        {
            return;
        }

        runTimer.TogglePause();
        if (previousPhase == SplitTimerPhase.Running && runTimer.Phase == SplitTimerPhase.Paused)
        {
            soundPlayer.Play(settings.Sounds.Pause);
        }
        else if (previousPhase == SplitTimerPhase.Paused && runTimer.Phase == SplitTimerPhase.Running)
        {
            soundPlayer.Play(settings.Sounds.Resume);
        }

        SyncBackgroundRuntimeState();
        UpdateStatusPaintSchedulerState();
        UpdateWindowTitle();
        Invalidate();
    }

    private void RenderStatusOverlayTick()
    {
        if (splitCompletionAnimation is not null)
        {
            overlayWindowController.RenderImmediately();
            return;
        }

        if (runTimer.Phase == SplitTimerPhase.Running)
        {
            overlayWindowController.RenderImmediately();
            return;
        }

        UpdateStatusPaintSchedulerState();
    }

    private void UpdateStatusPaintSchedulerState()
    {
        bool shouldRun = !closing &&
            runtimeOverlayPaintSuspensionCount <= 0 &&
            (runTimer.Phase == SplitTimerPhase.Running || splitCompletionAnimation is not null);
        if (shouldRun && !statusPaintScheduler.IsRunning)
        {
            statusPaintScheduler.Start(statusPaintInterval);
        }
        else if (!shouldRun && statusPaintScheduler.IsRunning)
        {
            statusPaintScheduler.Stop();
        }
    }

    private void HandleWatcherPollCompleted(WatcherPollNotification notification)
    {
        performance.RecordWatcherPoll(notification.Elapsed, notification.CompletedTimestamp);
        performance.WatcherPollInterval = notification.NextPollInterval;
        performance.ProcessLookupInterval = notification.ProcessLookupInterval;

        lock (runtimeDebugSnapshotLock)
        {
            snapshot = notification.Snapshot;
            watcherDiagnostics = notification.Diagnostics;
        }
        UpdateConfiguredRefreshIntervals();
        bool invalidateAll = false;
        if (notification.RuntimeCommandSequence >= minimumAcceptedRuntimeCommandSequence)
        {
            runTimer.ApplyState(notification.RuntimeState.TimerState);
            splitTracker.ApplyState(notification.RuntimeState.SplitTrackerState);
            invalidateAll = ApplyRuntimeTickResult(notification.RuntimeTickResult);
            minimumAcceptedRuntimeCommandSequence = Math.Max(
                minimumAcceptedRuntimeCommandSequence,
                notification.RuntimeCommandSequence);
        }

        ProcessUiTick();
        UpdateStatusPaintSchedulerState();
        PublishTimerOverlaySnapshot();
        if (invalidateAll || !notification.Snapshot.Equals(notification.PreviousSnapshot))
        {
            Invalidate();
        }
    }

    private bool ApplyRuntimeTickResult(TimerControllerTickResult tickResult)
    {
        bool invalidateAll = false;

        if (tickResult.PauseSoundRequested)
        {
            soundPlayer.Play(settings.Sounds.Pause);
            invalidateAll = true;
        }

        if (tickResult.ResumeSoundRequested)
        {
            soundPlayer.Play(settings.Sounds.Resume);
            invalidateAll = true;
        }

        if (tickResult.RequestedMenuAction is MenuHotkeyActionKind menuAction)
        {
            ExecuteMenuHotkeyAction(menuAction);
            return true;
        }

        if (tickResult.RunStarted)
        {
            soundPlayer.Play(settings.Sounds.EnterWorld);
            runSession.MarkRunStarted();
            invalidateAll = true;
        }

        if (tickResult.CompletedSplitIndex is int completedIndex)
        {
            TrackSegmentBestDeltaHighlight(completedIndex);
            PlaySplitSound(completedIndex);

            if (settings.ShowSplitCompletionAnimation)
            {
                StartSplitCompletionAnimation(completedIndex);
            }
            else
            {
                splitCompletionAnimation = null;
            }

            if (tickResult.RunCompleted)
            {
                RecordRunStatsOnce();
            }

            invalidateAll = true;
        }

        return invalidateAll;
    }

    private void InvalidateRuntimeRenderRegion()
    {
        if (settings.ShowEarlyDeltaTime &&
            splitTracker.CurrentIndex >= 0 &&
            splitTracker.CurrentIndex < splitTracker.Statuses.Count &&
            TryGetLayout(out SplitLayout layout))
        {
            Rectangle rowRect = overlayWindowsInitialized
                ? overlayBoundsController.CurrentLayout.ToStatusLocal(layout.GetRowRect(splitTracker.CurrentIndex))
                : layout.GetRowRect(splitTracker.CurrentIndex);
            Invalidate(Rectangle.Inflate(rowRect, ScaleInt(6), ScaleInt(6)));
            return;
        }

        Invalidate();
    }

    private void UpdateWindowTitle()
    {
        string title = SegmentTimerWindowTitle;
        if (string.Equals(title, currentWindowText, StringComparison.Ordinal))
        {
            return;
        }

        currentWindowText = title;
        Text = title;
    }

    internal RuntimePerformanceDiagnostics GetRuntimeDiagnostics()
    {
        return performance.Snapshot();
    }

    internal RuntimeDebugSnapshot GetRuntimeDebugSnapshot()
    {
        lock (runtimeDebugSnapshotLock)
        {
            return new RuntimeDebugSnapshot(snapshot, watcherDiagnostics, performance.Snapshot(), runTimer.Phase);
        }
    }

    private TimerHotkeyRequest[] DrainPendingHotkeyRequests()
    {
        if (pendingHotkeyRequests.Count == 0)
        {
            return [];
        }

        TimerHotkeyRequest[] requests = pendingHotkeyRequests.ToArray();
        pendingHotkeyRequests.Clear();
        return requests;
    }

    private TimerHotkeyRequest[] CancelRequestedAutomations(IReadOnlyCollection<TimerHotkeyRequest> hotkeyRequests)
    {
        if (hotkeyRequests.Count == 0)
        {
            return [];
        }

        var remainingRequests = new List<TimerHotkeyRequest>(hotkeyRequests.Count);
        foreach (TimerHotkeyRequest request in hotkeyRequests)
        {
            if (request.Action == TimerHotkeyAction.CreateWorld && worldAutomation.IsCreateWorldRunning)
            {
                worldAutomation.CancelCreateWorld();
                continue;
            }
            else if (request.Action == TimerHotkeyAction.PracticeWorld && worldAutomation.IsEnterWorldRunning)
            {
                worldAutomation.CancelEnterWorld();
                continue;
            }

            remainingRequests.Add(request);
        }

        return remainingRequests.ToArray();
    }

    private bool ShowPersonalBestUpdateConfirmation(string promptText)
    {
        bool wasClickThrough = mouseClickThrough;
        if (wasClickThrough)
        {
            SetMouseClickThrough(false);
        }

        try
        {
            return RunWithSuspendedRuntimeOverlayPaint(() =>
            {
                using var form = new PersonalBestUpdatePromptForm(
                    promptText,
                    timeoutSeconds: 10,
                    settings);
                return modalWindows.ShowDialog(form) != DialogResult.No;
            });
        }
        finally
        {
            if (wasClickThrough)
            {
                SetMouseClickThrough(true);
            }
        }
    }

    private void ExecuteReset()
    {
        ResetRunWithSound(recordStats: true);
    }

    private void ExecuteMenuHotkeyAction(MenuHotkeyActionKind action)
    {
        switch (action)
        {
            case MenuHotkeyActionKind.Reset:
                ExecuteReset();
                break;
            case MenuHotkeyActionKind.CreateWorld:
                StartCreateWorldAutomation();
                break;
            case MenuHotkeyActionKind.PracticeWorld:
                ResetRunWithSound(recordStats: true);
                ShowPracticeWorldSelector();
                break;
        }
    }

    private string FormatTimerPhase()
    {
        return runTimer.Phase switch
        {
            SplitTimerPhase.NotStarted => "READY",
            SplitTimerPhase.Running => "RUNNING",
            SplitTimerPhase.Paused => "PAUSED",
            _ => "UNKNOWN"
        };
    }

    private string FormatWorldState()
    {
        return snapshot.IsGameMenu switch
        {
            true => "menu",
            false => FormatBossSummary(),
            null => "unknown"
        };
    }

    private string FormatBossSummary()
    {
        return $"Skl:{FormatFlag(snapshot.BossStates.Skeletron)} " +
            $"WoF:{FormatFlag(snapshot.BossStates.WallOfFlesh)} " +
            $"ML:{FormatFlag(snapshot.BossStates.MoonLord)}";
    }

    private static string FormatFlag(bool? value)
    {
        return value switch
        {
            true => "down",
            false => "up",
            null => "?"
        };
    }

    private void OpenSettings()
    {
        if (settingsFormOpen)
        {
            modalWindows.ActivateCurrentModal();
            return;
        }

        settingsFormOpen = true;
        settingsModalWindowRegistration?.Dispose();
        hotkeyManager.Dispose();
        pendingHotkeyRequests.Clear();
        minimumAcceptedRuntimeCommandSequence = Math.Max(
            minimumAcceptedRuntimeCommandSequence,
            monitorCoordinator.ClearPendingHotkeys());
        settingsDialogHost = new SettingsDialogHost(
            settings,
            GetRuntimeDiagnostics,
            GetRuntimeDebugSnapshot,
            callback => BeginInvoke(callback),
            ApplySettings,
            result =>
            {
                if (result.DialogResult == DialogResult.OK)
                {
                    ApplySettings(result.Result);
                }

                settingsModalWindowRegistration?.Dispose();
                settingsModalWindowRegistration = null;
                settingsDialogHost = null;
                settingsFormOpen = false;
                if (IsHandleCreated)
                {
                    RegisterConfiguredHotkeys();
                }
            },
            () =>
            {
                modalWindows.ApplyWindowState();
            },
            Bounds);
        settingsModalWindowRegistration = modalWindows.RegisterModalWindow(
            () => settingsDialogHost?.WindowHandle ?? IntPtr.Zero);
        settingsDialogHost.Show();
    }

    private void ApplySettings(AppSettings appliedSettings)
    {
        AppSettings previousSettings = AppSettingsStore.Clone(settings);
        AppSettings nextSettings = AppSettingsStore.Clone(appliedSettings);
        FinalizeCurrentRunForSettingsChange(nextSettings);
        settings = nextSettings;
        AppSettingsStore.Save(settings);
        ApplyLoadedSettings(previousSettings);
    }

    private void SwitchSettingsFile(string path)
    {
        if (string.Equals(
                Path.GetFullPath(path),
                Path.GetFullPath(AppSettingsStore.SettingsPath),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AppSettings previousSettings = AppSettingsStore.Clone(settings);
        AppSettings nextSettings = AppSettingsStore.Load(path);
        FinalizeCurrentRunForSettingsChange(nextSettings);
        settings = nextSettings;
        ApplyLoadedSettings(previousSettings);
    }

    private void FinalizeCurrentRunForSettingsChange(AppSettings nextSettings)
    {
        runSession.Reset(nextSettings, recordStats: true, ShowPersonalBestUpdateConfirmation);
        splitCompletionAnimation = null;
        segmentBestDeltaHighlights.Clear();
        minimumAcceptedRuntimeCommandSequence = Math.Max(
            minimumAcceptedRuntimeCommandSequence,
            monitorCoordinator.ResetRuntimeState());
    }

    private void ApplyLoadedSettings(AppSettings? previousSettings = null)
    {
        Rectangle? referenceCompositeBounds = overlayWindowsInitialized
            ? overlayBoundsController.CompositeBounds
            : pendingInitialCompositeBounds;
        palette = UiPalette.From(settings.Colors);
        RefreshTimerOverlaySettingsSnapshot();
        IReadOnlyList<BossSplitDefinition> definitions = BossSplitDefinitions.Build(settings);
        runSession.SetDefinitions(definitions);
        overlayBoundsController.UpdateContext(settings, runSession.SplitTracker.Statuses.Count);
        minimumAcceptedRuntimeCommandSequence = Math.Max(
            minimumAcceptedRuntimeCommandSequence,
            monitorCoordinator.SetRuntimeDefinitions(definitions));
        ResetRun();
        monitorCoordinator.ResetUiScalePatchState();
        UpdateEffectiveOverlayTopMost();
        if (IsHandleCreated && !settingsFormOpen)
        {
            RegisterConfiguredHotkeys();
        }

        ApplyLayeredOverlayWindowStyle();
        ApplyLayoutBounds(useDefaultSize: false, previousSettings, referenceCompositeBounds);
        UpdateContextMenu();
        ClearIconCache();
        UpdateConfiguredRefreshIntervals();
        UpdateTimerOverlayRefreshInterval();
        PublishTimerOverlaySnapshot(true);
        Invalidate();
    }

    private void ApplyLayoutBounds(
        bool useDefaultSize,
        AppSettings? previousSettings = null,
        Rectangle? referenceCompositeBoundsOverride = null)
    {
        Size minimumCompositeSize = SplitLayoutCalculator.GetMinimumWindowSize(settings);
        Size minimumStatusSize = GetStatusWindowMinimumSize(minimumCompositeSize);
        Rectangle targetCompositeBounds;
        if (useDefaultSize)
        {
            Size defaultCompositeSize = new(
                Math.Max(minimumCompositeSize.Width, SplitLayoutCalculator.GetDefaultWindowWidth(settings)),
                Math.Max(minimumCompositeSize.Height, SplitLayoutCalculator.GetDefaultWindowHeight(settings)));
            if (!overlayWindowsInitialized &&
                !IsHandleCreated &&
                TryGetInitialOverlayLayout(defaultCompositeSize, out targetCompositeBounds, out OverlayCompositeLayout initialLayout))
            {
                pendingInitialCompositeBounds = targetCompositeBounds;
                MinimumSize = minimumStatusSize;
                StartPosition = FormStartPosition.Manual;
                Bounds = initialLayout.StatusScreenBounds;
                return;
            }

            targetCompositeBounds = new Rectangle(Left, Top, defaultCompositeSize.Width, defaultCompositeSize.Height);
        }
        else
        {
            Size targetSize = GetRuntimeLayoutSize(previousSettings, referenceCompositeBoundsOverride?.Size);
            int width = Math.Max(targetSize.Width, minimumCompositeSize.Width);
            int height = Math.Max(targetSize.Height, minimumCompositeSize.Height);
            Rectangle referenceBounds = referenceCompositeBoundsOverride ?? (overlayWindowsInitialized
                ? overlayBoundsController.CompositeBounds
                : pendingInitialCompositeBounds ?? Bounds);
            targetCompositeBounds = new Rectangle(referenceBounds.Left, referenceBounds.Top, width, height);
        }

        MinimumSize = minimumStatusSize;
        if (overlayWindowsInitialized)
        {
            overlayBoundsController.ApplyCompositeBounds(targetCompositeBounds);
            return;
        }

        if (targetCompositeBounds.Width != Width || targetCompositeBounds.Height != Height)
        {
            Size = targetCompositeBounds.Size;
        }

        pendingInitialCompositeBounds = targetCompositeBounds;
    }

    private Size GetStatusWindowMinimumSize(Size minimumCompositeSize)
    {
        if (OverlayCompositeLayoutCalculator.TryCreate(
                new Rectangle(Point.Empty, minimumCompositeSize),
                settings,
                runSession.SplitTracker.Statuses.Count,
                RowGap,
                out OverlayCompositeLayout minimumLayout))
        {
            return minimumLayout.StatusLocalBounds.Size;
        }

        return minimumCompositeSize;
    }

    private bool TryGetInitialOverlayLayout(
        Size compositeSize,
        out Rectangle compositeBounds,
        out OverlayCompositeLayout layout)
    {
        Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        int left = workingArea.Left + Math.Max(0, (workingArea.Width - compositeSize.Width) / 2);
        int top = workingArea.Top + Math.Max(0, (workingArea.Height - compositeSize.Height) / 2);
        compositeBounds = new Rectangle(left, top, compositeSize.Width, compositeSize.Height);
        return OverlayCompositeLayoutCalculator.TryCreate(
            compositeBounds,
            settings,
            runSession.SplitTracker.Statuses.Count,
            RowGap,
            out layout);
    }

    private Size GetRuntimeLayoutSize(AppSettings? previousSettings, Size? currentCompositeSizeOverride = null)
    {
        Size currentSize = currentCompositeSizeOverride ?? (overlayWindowsInitialized
            ? overlayBoundsController.CompositeBounds.Size
            : pendingInitialCompositeBounds?.Size ?? Size);
        if (previousSettings is null)
        {
            return currentSize;
        }

        int oldScale = Math.Clamp(previousSettings.Columns.ScalePercent, 25, 300);
        int newScale = Math.Clamp(settings.Columns.ScalePercent, 25, 300);
        float ratio = newScale / (float)oldScale;
        int width = currentSize.Width;
        int height = currentSize.Height;
        if (oldScale != newScale)
        {
            width = ScaleRuntimeDimension(width, ratio);
            height = ScaleRuntimeDimension(height, ratio);
        }

        int scaledPreviousDefaultWidth = ScaleRuntimeDimension(
            SplitLayoutCalculator.GetDefaultWindowWidth(previousSettings),
            ratio);
        int currentDefaultWidth = SplitLayoutCalculator.GetDefaultWindowWidth(settings);
        width += currentDefaultWidth - scaledPreviousDefaultWidth;

        return new Size(Math.Max(1, width), Math.Max(1, height));
    }

    private static int ScaleRuntimeDimension(int value, float ratio)
    {
        return Math.Max(1, (int)Math.Round(value * ratio, MidpointRounding.AwayFromZero));
    }

    private void ApplyLayeredOverlayWindowStyle()
    {
        BackColor = Color.Black;
        TransparencyKey = Color.Empty;
        overlayWindowController.ApplyWindowStyle(mouseClickThrough);
        timerOverlayHost.ApplyMouseClickThrough(mouseClickThrough);
        QueueStatusOverlayRender();
    }

    private void InitializeOverlayWindows()
    {
        if (overlayWindowsInitialized)
        {
            return;
        }

        overlayWindowInitializationInProgress = true;
        try
        {
            overlayWindowsInitialized = true;
            Rectangle initialCompositeBounds = pendingInitialCompositeBounds ?? Bounds;
            pendingInitialCompositeBounds = null;
            overlayBoundsController.Initialize(initialCompositeBounds);
            timerOverlayHost.Start();
            UpdateEffectiveOverlayTopMost();
            timerOverlayHost.ApplyMouseClickThrough(mouseClickThrough);
            UpdateTimerOverlayRefreshInterval();
            PublishTimerOverlaySnapshot(true);
        }
        finally
        {
            overlayWindowInitializationInProgress = false;
        }

        RenderInitialStatusOverlay();
        BeginInvoke(new Action(() => statusBoundsFeedbackEnabled = true));
    }

    private void ApplyOverlayLayout(OverlayCompositeLayout layout)
    {
        suppressStatusBoundsFeedback = true;
        try
        {
            if (Bounds != layout.StatusScreenBounds)
            {
                Bounds = layout.StatusScreenBounds;
            }
        }
        finally
        {
            suppressStatusBoundsFeedback = false;
        }

        timerOverlayHost.ApplyOverlayLayout(layout);
        UpdateEffectiveOverlayTopMost();
        timerOverlayHost.ApplyMouseClickThrough(mouseClickThrough);
        UpdateTimerOverlayRefreshInterval();
        QueueStatusOverlayRender();
    }

    private void QueueStatusOverlayRender()
    {
        if (!overlayWindowsInitialized || overlayWindowInitializationInProgress)
        {
            return;
        }

        overlayWindowController.QueueRender();
    }

    private void RenderInitialStatusOverlay()
    {
        if (!overlayWindowsInitialized || overlayWindowInitializationInProgress)
        {
            return;
        }

        overlayWindowController.RenderImmediately();
    }

    private void NotifyStatusBoundsChanged()
    {
        if (!overlayWindowsInitialized ||
            !statusBoundsFeedbackEnabled ||
            suppressStatusBoundsFeedback ||
            dragging)
        {
            return;
        }

        overlayBoundsController.HandleStatusResize(Bounds);
    }

    private void PublishTimerOverlaySnapshot(bool force = false)
    {
        if (!overlayWindowsInitialized)
        {
            return;
        }

        // One-way boundary: the main UI publishes state changes, while the timer
        // overlay thread owns high-frequency elapsed-time painting.
        timerOverlayHost.ApplyRenderState(
            BuildTimerOverlaySnapshot(),
            BuildTimerOverlaySnapshotKey(),
            force);
    }

    private void UpdateConfiguredRefreshIntervals()
    {
        TimeSpan nextControlInterval = ResolveControlTickInterval();
        if (controlTickInterval != nextControlInterval)
        {
            controlTickInterval = nextControlInterval;
            controlScheduler.UpdateInterval(controlTickInterval);
        }

        performance.ControlTickInterval = controlTickInterval;

        TimeSpan nextStatusPaintInterval = ResolveRunningStatusPaintInterval();
        if (statusPaintInterval != nextStatusPaintInterval)
        {
            statusPaintInterval = nextStatusPaintInterval;
            statusPaintScheduler.UpdateInterval(statusPaintInterval);
        }

        performance.StatusPaintInterval = statusPaintInterval;
        monitorCoordinator.UpdateReadyWatcherPollInterval(ResolveReadyWatcherPollInterval());
    }

    private TimeSpan ResolveReadyWatcherPollInterval()
    {
        int hz = RefreshRateSettings.NormalizeReadyWatcherPollHz(
            settings.Advanced?.ReadyWatcherPollHz ?? AppSettingsDefaults.Advanced.ReadyWatcherPollHz);
        return RefreshRateSettings.ToInterval(hz);
    }

    private TimeSpan ResolveControlTickInterval()
    {
        if (!snapshot.IsReady)
        {
            return DefaultControlTickInterval;
        }

        int hz = RefreshRateSettings.NormalizeReadyUiControlHz(
            settings.Advanced?.ReadyUiControlHz ?? AppSettingsDefaults.Advanced.ReadyUiControlHz);
        return RefreshRateSettings.ToInterval(hz);
    }

    private TimeSpan ResolveRunningStatusPaintInterval()
    {
        int hz = RefreshRateSettings.NormalizeRunningStatusPaintHz(
            settings.Advanced?.RunningStatusPaintHz ?? AppSettingsDefaults.Advanced.RunningStatusPaintHz);
        return RefreshRateSettings.ToInterval(hz);
    }

    private TimerOverlayRenderState BuildTimerOverlaySnapshot()
    {
        BossSplitStatus[] statusCopies = splitTracker.Statuses
            .Select(status => status.CreateRenderCopy())
            .ToArray();
        return new TimerOverlayRenderState(
            timerOverlaySettingsSnapshot,
            palette,
            statusCopies,
            splitTracker.CurrentIndex,
            runTimer.CaptureState(),
            mouseClickThrough);
    }

    private TimerOverlayStateKey BuildTimerOverlaySnapshotKey()
    {
        SplitTimerState timerState = runTimer.CaptureState();
        var hash = new HashCode();
        foreach (BossSplitStatus status in splitTracker.Statuses)
        {
            hash.Add(status.Time);
            hash.Add(status.IsSkipped);
        }

        return new TimerOverlayStateKey(
            timerState,
            splitTracker.CurrentIndex,
            mouseClickThrough,
            hash.ToHashCode(),
            timerOverlaySettingsRevision);
    }

    private void UpdateTimerOverlayRefreshInterval()
    {
        if (!overlayWindowsInitialized)
        {
            return;
        }

        int timerRefreshHz = RefreshRateSettings.NormalizeTimerOverlayRefreshHz(
            settings.Advanced?.TimerOverlayRefreshHz ?? AppSettingsDefaults.Advanced.TimerOverlayRefreshHz);
        TimeSpan interval = RefreshRateSettings.ToInterval(timerRefreshHz);
        performance.TimerOverlayPaintInterval = interval;
        timerOverlayHost.ApplyRefreshInterval(interval);
    }

    private void HandleTimerOverlayRightClickRequested(TimerOverlayRightClickRequest request)
    {
        if (settings.PracticeMode &&
            overlayWindowsInitialized &&
            TryGetLayout(out SplitLayout layout))
        {
            Point compositePoint = overlayBoundsController.CurrentLayout.MapTimerPointToComposite(request.LocalPoint);
            if (layout.TimerRect.Contains(compositePoint))
            {
                EditPracticeTotalTime();
                return;
            }
        }

        ShowContextMenuAtScreen(request.ScreenPoint);
    }

    private void ShowContextMenuAtScreen(Point screenPoint)
    {
        contextMenu.Show(screenPoint);
    }

    private void OpenStatistics()
    {
        using var form = new StatisticsForm(settings);
        modalWindows.ShowDialog(form);
    }

    private void FinalizeRunBeforeExit()
    {
        ResetRun(recordStats: true);
    }

    private void ResetRun(bool recordStats = false)
    {
        runSession.Reset(settings, recordStats, ShowPersonalBestUpdateConfirmation);
        RefreshTimerOverlaySettingsSnapshot();
        splitCompletionAnimation = null;
        segmentBestDeltaHighlights.Clear();
        minimumAcceptedRuntimeCommandSequence = Math.Max(
            minimumAcceptedRuntimeCommandSequence,
            monitorCoordinator.ResetRuntimeState());
        UpdateStatusPaintSchedulerState();
        PublishTimerOverlaySnapshot();
        Invalidate();
    }

    private void RecordRunStatsOnce()
    {
        runSession.RecordRunStatsOnce();
    }

    private void SyncBackgroundRuntimeState()
    {
        minimumAcceptedRuntimeCommandSequence = Math.Max(
            minimumAcceptedRuntimeCommandSequence,
            monitorCoordinator.ReplaceRuntimeState(
                runTimer.CaptureState(),
                splitTracker.CaptureState()));
        PublishTimerOverlaySnapshot();
    }

    private void SetMouseClickThrough(bool enabled)
    {
        mouseClickThrough = enabled;
        overlayWindowController.ApplyWindowStyle(mouseClickThrough);
        timerOverlayHost.ApplyMouseClickThrough(mouseClickThrough);
        modalWindows.ApplyWindowState();
        PublishTimerOverlaySnapshot();
        UpdateWindowTitle();
    }

    private async void StartCreateWorldAutomation()
    {
        pendingHotkeyRequests.Clear();
        ResetRunWithSound(recordStats: true);

        try
        {
            await worldAutomation.StartCreateWorldAsync(AppSettingsStore.Clone(settings));
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Unhandled create world automation error.");
        }
    }

    private void ShowPracticeWorldSelector()
    {
        using var form = new PracticeWorldSelectorForm(settings);
        var window = new TerrariaWindowController();
        if (window.TryGetClientScreenBounds(out Rectangle terrariaBounds))
        {
            form.Location = new Point(
                terrariaBounds.Left + Math.Max(0, (terrariaBounds.Width - form.Width) / 2),
                terrariaBounds.Top + Math.Max(0, (terrariaBounds.Height - form.Height) / 2));
        }
        else
        {
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            form.Location = new Point(
                workingArea.Left + Math.Max(0, (workingArea.Width - form.Width) / 2),
                workingArea.Top + Math.Max(0, (workingArea.Height - form.Height) / 2));
        }

        if (modalWindows.ShowDialog(form, ModalWindowOptions.ForceTopMostForeground) == DialogResult.OK &&
            form.SelectedSlot is PracticeWorldSlot selectedSlot)
        {
            StartPracticeWorldAutomation(selectedSlot);
        }
    }

    private async void StartPracticeWorldAutomation(PracticeWorldSlot selectedSlot)
    {
        if (!EnterWorldSaveInstaller.TryValidate(selectedSlot, out string validationMessage))
        {
            AppLogger.Info(validationMessage);
            return;
        }

        pendingHotkeyRequests.Clear();
        minimumAcceptedRuntimeCommandSequence = Math.Max(
            minimumAcceptedRuntimeCommandSequence,
            monitorCoordinator.ClearPendingHotkeys());

        try
        {
            await worldAutomation.StartEnterWorldAsync(AppSettingsStore.Clone(settings), selectedSlot);
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Unhandled practice world automation error.");
        }
    }

    private void ResetRunWithSound(bool recordStats = false)
    {
        soundPlayer.StopAll();
        soundPlayer.Play(settings.Sounds.Reset);
        ResetRun(recordStats);
    }

    private void RegisterConfiguredHotkeys()
    {
        IReadOnlyList<HotkeyRegistrationWarning> warnings = hotkeyManager.RegisterConfiguredHotkeys(Handle, settings);
        ShowHotkeyRegistrationWarnings(warnings);
    }

    private void ShowHotkeyRegistrationWarnings(IReadOnlyList<HotkeyRegistrationWarning> warnings)
    {
        if (warnings.Count == 0)
        {
            lastHotkeyWarningText = null;
            return;
        }

        string warningText = string.Join(Environment.NewLine, warnings.Select(FormatHotkeyRegistrationWarning));
        if (string.Equals(warningText, lastHotkeyWarningText, StringComparison.Ordinal))
        {
            return;
        }

        lastHotkeyWarningText = warningText;
        string message = Localizer.Get("Some hotkeys could not be registered:", settings) +
            Environment.NewLine +
            warningText;
        ShowHotkeyWarning(message);
    }

    private void ShowHotkeyWarning(string message)
    {
        using var dialog = new HotkeyWarningDialog(
            Localizer.Get("Hotkey warning", settings),
            message);
        modalWindows.ShowDialog(dialog);
    }

    private string FormatHotkeyRegistrationWarning(HotkeyRegistrationWarning warning)
    {
        string actionName = Localizer.Get(GetHotkeyActionDisplayName(warning.Action), settings);
        return warning.Kind switch
        {
            HotkeyRegistrationWarningKind.Duplicate => string.Format(
                Localizer.Get("{0}: {1} is duplicated; only the first action using this key is active.", settings),
                actionName,
                HotkeyKeyValidator.Format(warning.Keys)),
            HotkeyRegistrationWarningKind.Invalid => string.Format(
                Localizer.Get("{0}: {1} is not allowed as a hotkey.", settings),
                actionName,
                HotkeyKeyValidator.Format(warning.Keys)),
            HotkeyRegistrationWarningKind.SystemRegistrationFailed => string.Format(
                Localizer.Get("{0}: {1} registration failed. It may be used by another program. ({2})", settings),
                actionName,
                HotkeyKeyValidator.Format(warning.Keys),
                warning.Detail),
            _ => $"{actionName}: {warning.Keys}"
        };
    }

    private static string GetHotkeyActionDisplayName(TimerHotkeyAction action)
    {
        return action switch
        {
            TimerHotkeyAction.PauseResume => "Pause / Resume",
            TimerHotkeyAction.Reset => "Reset (Disabled in world)",
            TimerHotkeyAction.MouseClickThrough => "Mouse passthrough",
            TimerHotkeyAction.CreateWorld => "Create world (Disabled in world)",
            TimerHotkeyAction.PracticeWorld => "Load world (Disabled in world)",
            _ => action.ToString()
        };
    }

    private void ClearIconCache()
    {
        renderResources.BossIcons.Clear();
    }

    private void RefreshTimerOverlaySettingsSnapshot()
    {
        timerOverlaySettingsRevision++;
        timerOverlaySettingsSnapshot = AppSettingsStore.Clone(settings);
    }

    private void UpdateEffectiveOverlayTopMost()
    {
        modalWindows.SetAlwaysOnTop(settings.AlwaysOnTop);
    }

    private T RunWithSuspendedRuntimeOverlayPaint<T>(Func<T> action)
    {
        bool firstSuspension = runtimeOverlayPaintSuspensionCount == 0;
        runtimeOverlayPaintSuspensionCount++;
        if (firstSuspension)
        {
            runtimeControlSchedulerSuspended = controlScheduler.IsRunning;
            if (runtimeControlSchedulerSuspended)
            {
                controlScheduler.Stop();
            }

            monitorCoordinator.ApplyUiDispatchSuspended(true);
        }

        UpdateStatusPaintSchedulerState();
        if (overlayWindowsInitialized)
        {
            timerOverlayHost.ApplyPaintSuspended(true);
        }

        try
        {
            return action();
        }
        finally
        {
            runtimeOverlayPaintSuspensionCount = Math.Max(0, runtimeOverlayPaintSuspensionCount - 1);
            if (overlayWindowsInitialized && runtimeOverlayPaintSuspensionCount == 0)
            {
                timerOverlayHost.ApplyPaintSuspended(false);
                timerOverlayHost.RequestRender();
            }

            if (runtimeOverlayPaintSuspensionCount == 0)
            {
                monitorCoordinator.ApplyUiDispatchSuspended(false);
                if (runtimeControlSchedulerSuspended && !closing)
                {
                    controlScheduler.Start(controlTickInterval);
                }

                runtimeControlSchedulerSuspended = false;
            }

            UpdateStatusPaintSchedulerState();
            QueueStatusOverlayRender();
        }
    }
}



