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

    private readonly WorldPoolStore worldPoolStore = new();
    private readonly TerrariaWorldAutomation worldAutomation;
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
    private bool settingsFormOpen;
    private int runtimeOverlayPaintSuspensionCount;
    private bool runtimeControlSchedulerSuspended;
    private string? lastHotkeyWarningText;
    private bool closing;
    private bool runtimeResourcesDisposed;
    private string currentWindowText = string.Empty;
    private IDisposable? settingsModalWindowRegistration;
    private IDisposable? settingsChildModalWindowRegistration;
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
        worldAutomation = new TerrariaWorldAutomation(worldPoolStore);
        applicationController = new ApplicationController(AppSettingsStore.Load(), ShowPersonalBestUpdateConfirmation);
        worldPoolFillService = new WorldPoolFillService(worldPoolStore);
        RefreshTimerOverlaySettingsSnapshot();
        palette = UiPalette.From(settings.Colors);
        monitorCoordinator = new TerrariaMonitorCoordinator(
            new TerrariaWorldWatcher(),
            new TerrariaUiScalePatchApplierAdapter(),
            callback => BeginInvoke(callback),
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
        timerOverlayHost.DragDeltaRequested += delta => overlayBoundsController.MoveBy(delta);
        timerOverlayHost.UserResizeBoundsChanged += bounds => overlayBoundsController.HandleTimerResize(bounds);
        timerOverlayHost.RightClickRequested += HandleTimerOverlayRightClickRequested;
        timerOverlayHost.Activated += QueueMainWindowForegroundGroupSync;
        timerOverlayHost.ModalActivationRequested += () => modalWindows.ActivateCurrentModal();
        effectExecutor = new ApplicationShellEffectExecutor(
            SubmitRuntimeCommand,
            soundPlayer.StopAll,
            soundPlayer.Play,
            ToggleMouseClickThrough,
            overlayAnimations.Clear,
            ClearSplitCompletionAnimation,
            TrackSegmentBestDeltaHighlight,
            StartSplitCompletionAnimation,
            AppSettingsStore.Save,
            StartCreateWorldAutomation,
            ShowPracticeWorldSelector,
            () => worldAutomation.CancelCreateWorld(),
            () => worldAutomation.CancelEnterWorld(),
            monitorCoordinator.ResetUiScalePatchState,
            ApplyLoadedSettings,
            RefreshTimerOverlaySettingsSnapshot,
            RefreshRuntimeUi);
        overlayBoundsController.UpdateContext(
            settings,
            GetCurrentReservedLayoutRowCount(),
            GetCurrentLayoutRowCount());
        AcceptRuntimeCommandSequence(monitorCoordinator.SetRuntimeDefinitions(applicationController.Definitions));
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
        if (overlayWindowsInitialized)
        {
            ApplyOverlayLayout(overlayBoundsController.CurrentLayout);
        }

        worldPoolFillService.UpdateSettings(settings);
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
        statusOverlayContentDirty = true;
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
            TogglePyramidFilter,
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
        worldPoolFillService.Dispose();
        worldAutomation.Dispose();
        settingsDialogHost?.Dispose();
        settingsDialogHost = null;
        settingsChildModalWindowRegistration?.Dispose();
        settingsChildModalWindowRegistration = null;
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

        SplitStatusSnapshot status = splitStatuses[rowIndex];
        ColumnRects columns = SplitListRenderer.GetColumnRects(settings, rowRect, status.Definition.IsAttached);
        if (columns.Time is Rectangle timeRect && timeRect.Contains(point))
        {
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

        if (hotkeyManager.TryGetAction(m, out HotkeyAction action))
        {
            if (HotkeyCommandMapper.TryMap(
                    action,
                    DateTime.UtcNow,
                    worldAutomation.IsCreateWorldRunning,
                    worldAutomation.IsEnterWorldRunning,
                    out AppCommand command))
            {
                ExecuteAppCommand(command);
            }

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
            monitorCoordinator.Tick(
                timerPhase,
                settings.Advanced?.EnableTerrariaUiScalePatch == true);
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
            BeginInvoke(dispatchedControlTick);
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

    private void DispatchedControlTick()
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
            BeginInvoke(dispatchedStatusPaintTick);
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

    private void DispatchedStatusPaintTick()
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
    }

    private bool CanDispatchToUiThread()
    {
        return !closing && IsHandleCreated && !IsDisposed && !Disposing;
    }

    private void ProcessUiTick()
    {
        monitorCoordinator.UpdateRunPhase(timerPhase);
        UpdateWindowTitle();
    }

    private void RenderStatusOverlayTick()
    {
        if (overlayAnimations.SplitCompletionAnimation is not null)
        {
            overlayWindowController.RenderImmediately();
            return;
        }

        if (timerPhase == SplitTimerPhase.Running)
        {
            RenderRunningStatusOverlayFrame();
            return;
        }

        UpdateStatusPaintSchedulerState();
    }

    private void RenderRunningStatusOverlayFrame()
    {
        // Static content (other rows, icons, layout) only changes alongside an
        // Invalidate(); between those, the per-frame dynamics are the current
        // row's early delta and any segment-best highlight colors, so frames
        // can be skipped or limited to redrawing the affected rows.
        if (statusOverlayContentDirty || lastStatusOverlayDynamicKey is null)
        {
            overlayWindowController.RenderImmediately();
            return;
        }

        if (!StatusOverlayHighlightsActive &&
            ComputeStatusOverlayDynamicKey(timerElapsed) == lastStatusOverlayDynamicKey.Value)
        {
            return;
        }

        if (!TryRenderStatusOverlayRegion())
        {
            overlayWindowController.RenderImmediately();
        }
    }

    private void UpdateStatusPaintSchedulerState()
    {
        bool shouldRun = !closing &&
            runtimeOverlayPaintSuspensionCount <= 0 &&
            (timerPhase == SplitTimerPhase.Running || overlayAnimations.SplitCompletionAnimation is not null);
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
        // Poll durations are recorded on the watcher thread via recordPoll; this
        // handler only sees published (changed or heartbeat) completions.
        performance.WatcherPollInterval = notification.NextPollInterval;
        performance.ProcessLookupInterval = notification.ProcessLookupInterval;

        lock (runtimeDebugSnapshotLock)
        {
            snapshot = notification.Snapshot;
            watcherDiagnostics = notification.Diagnostics;
        }
        UpdateConfiguredRefreshIntervals();
        ApplicationUpdate update = applicationController.HandleWatcherNotification(notification);
        ApplyApplicationUpdate(update);

        UpdateOverlayLayoutContextIfChanged();
        ProcessUiTick();
        UpdateStatusPaintSchedulerState();
        PublishTimerOverlaySnapshot();
        if (update.InvalidateAll || !notification.Snapshot.Equals(notification.PreviousSnapshot))
        {
            Invalidate();
        }
    }

    private void ExecuteAppCommand(AppCommand command)
    {
        ApplicationUpdate update = applicationController.HandleCommand(command);
        if (command.Kind == AppCommandKind.ApplySettings)
        {
            ApplySettingsApplicationUpdate(update);
            return;
        }

        ApplyApplicationUpdate(update);
    }

    private void ApplySettingsApplicationUpdate(ApplicationUpdate update)
    {
        using IDisposable windowStateDeferral = modalWindows.DeferWindowStateUpdates();
        RunWithSuspendedRuntimeOverlayPaint(() =>
        {
            ApplyApplicationUpdate(update);
            return true;
        });
    }

    private void ApplyApplicationUpdate(ApplicationUpdate update)
    {
        effectExecutor.Apply(update.Effects);

        if (update.InvalidateAll)
        {
            MarkStatusOverlayStaticContentDirty();
            RefreshRuntimeUi();
            Invalidate();
        }
    }

    private void SubmitRuntimeCommand(RuntimeCommand command)
    {
        AcceptRuntimeCommandSequence(monitorCoordinator.SubmitRuntimeCommand(command));
    }

    private void AcceptRuntimeCommandSequence(long sequence)
    {
        applicationController.AcceptRuntimeCommandSequence(sequence);
    }

    private void InvalidateRuntimeRenderRegion()
    {
        if (settings.ShowEarlyDeltaTime &&
            currentSplitIndex >= 0 &&
            currentSplitIndex < splitStatuses.Count &&
            SplitDisplayRows.TryGetRowIndex(settings, splitStatuses, currentSplitIndex, out int visualRowIndex) &&
            TryGetLayout(out SplitLayout layout))
        {
            Rectangle rowRect = overlayWindowsInitialized
                ? overlayBoundsController.CurrentLayout.ToStatusLocal(layout.GetRowRect(visualRowIndex))
                : layout.GetRowRect(visualRowIndex);
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
            return new RuntimeDebugSnapshot(snapshot, watcherDiagnostics, performance.Snapshot(), timerPhase);
        }
    }

    internal int GetWorldPoolCount(AppSettings settings)
    {
        return worldPoolStore.Count(WorldPoolSignature.From(settings));
    }

    private bool ShowPersonalBestUpdateConfirmation(string promptText)
    {
        if (InvokeRequired)
        {
            try
            {
                object? result = Invoke(new Func<bool>(() => ShowPersonalBestUpdateConfirmation(promptText)));
                return result is bool value && value;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

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

    private void OpenSettings()
    {
        if (settingsFormOpen)
        {
            modalWindows.ActivateCurrentModal();
            return;
        }

        settingsFormOpen = true;
        settingsChildModalWindowRegistration?.Dispose();
        settingsChildModalWindowRegistration = null;
        settingsModalWindowRegistration?.Dispose();
        hotkeyManager.Dispose();
        AcceptRuntimeCommandSequence(monitorCoordinator.ClearPendingMenuActions());
        settingsDialogHost = new SettingsDialogHost(
            settings,
            GetRuntimeDiagnostics,
            GetRuntimeDebugSnapshot,
            GetWorldPoolCount,
            callback => BeginInvoke(callback),
            appliedSettings => ExecuteAppCommand(AppCommand.ApplySettings(appliedSettings)),
            result =>
            {
                if (result.DialogResult == DialogResult.OK)
                {
                    ExecuteAppCommand(AppCommand.ApplySettings(result.Result));
                }

                settingsChildModalWindowRegistration?.Dispose();
                settingsChildModalWindowRegistration = null;
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
        settingsChildModalWindowRegistration = modalWindows.RegisterModalWindow(
            () => settingsDialogHost?.ChildDialogWindowHandle ?? IntPtr.Zero);
        settingsDialogHost.Show();
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

        AppSettings nextSettings = AppSettingsStore.Load(path);
        ExecuteAppCommand(AppCommand.ApplySettings(nextSettings));
    }

    private void ApplySettings(AppSettings appliedSettings)
    {
        ExecuteAppCommand(AppCommand.ApplySettings(appliedSettings));
    }

    private void ApplyLoadedSettings(AppSettings? previousSettings = null, int splitCount = -1)
    {
        MarkStatusOverlayStaticContentDirty();
        Rectangle? referenceCompositeBounds = overlayWindowsInitialized
            ? overlayBoundsController.CompositeBounds
            : pendingInitialCompositeBounds;
        int visibleRowCount = GetCurrentLayoutRowCount();
        int resolvedRowCount = splitCount >= 0
            ? Math.Max(GetCurrentReservedLayoutRowCount(), splitCount)
            : GetCurrentReservedLayoutRowCount();
        palette = UiPalette.From(settings.Colors);
        RefreshTimerOverlaySettingsSnapshot();
        UpdateOverlayLayoutContext(resolvedRowCount, visibleRowCount, force: true);
        UpdateEffectiveOverlayTopMost();
        if (IsHandleCreated && !settingsFormOpen)
        {
            RegisterConfiguredHotkeys();
        }

        ApplyLayeredOverlayWindowStyle();
        ApplyLayoutBounds(useDefaultSize: false, previousSettings, referenceCompositeBounds, resolvedRowCount);
        UpdateContextMenu();
        ClearIconCache();
        UpdateConfiguredRefreshIntervals();
        UpdateTimerOverlayRefreshInterval();
        PublishTimerOverlaySnapshot(true);
        worldPoolFillService.UpdateSettings(settings);
        Invalidate();
    }

    private void ApplyLayoutBounds(
        bool useDefaultSize,
        AppSettings? previousSettings = null,
        Rectangle? referenceCompositeBoundsOverride = null,
        int splitCount = -1)
    {
        int rowCount = splitCount >= 0 ? splitCount : GetCurrentReservedLayoutRowCount();
        int visibleRowCount = GetCurrentLayoutRowCount();
        Size minimumCompositeSize = GetCompositeMinimumSize(rowCount, visibleRowCount);
        Size minimumStatusSize = GetStatusWindowMinimumSize(minimumCompositeSize, rowCount, visibleRowCount);
        Rectangle targetCompositeBounds;
        if (useDefaultSize)
        {
            int defaultWidth = Math.Max(minimumCompositeSize.Width, SplitLayoutCalculator.GetDefaultWindowWidth(settings));
            int defaultHeight = Math.Max(minimumCompositeSize.Height, SplitLayoutCalculator.GetDefaultWindowHeight(settings));
            defaultHeight = AdjustRuntimeHeightForSplitCount(
                defaultHeight,
                GetDefaultLayoutRowCount(),
                rowCount,
                defaultWidth);
            defaultHeight = GetFittingCompositeHeight(defaultWidth, defaultHeight, rowCount, visibleRowCount);
            Size defaultCompositeSize = new(
                defaultWidth,
                defaultHeight);
            if (!overlayWindowsInitialized &&
                !IsHandleCreated &&
                TryGetInitialOverlayLayout(defaultCompositeSize, out targetCompositeBounds, out OverlayCompositeLayout initialLayout))
            {
                pendingInitialCompositeBounds = targetCompositeBounds;
                MinimumSize = minimumStatusSize;
                StartPosition = FormStartPosition.Manual;
                Location = initialLayout.StatusScreenBounds.Location;
                ClientSize = initialLayout.StatusScreenBounds.Size;
                return;
            }

            targetCompositeBounds = new Rectangle(Left, Top, defaultCompositeSize.Width, defaultCompositeSize.Height);
        }
        else
        {
            Size targetSize = GetRuntimeLayoutSize(
                previousSettings,
                rowCount,
                referenceCompositeBoundsOverride?.Size);
            int width = Math.Max(targetSize.Width, minimumCompositeSize.Width);
            int height = Math.Max(targetSize.Height, minimumCompositeSize.Height);
            height = GetFittingCompositeHeight(width, height, rowCount, visibleRowCount);
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

    private Size GetCompositeMinimumSize(int rowCount, int visibleRowCount)
    {
        Size minimum = SplitLayoutCalculator.GetMinimumWindowSize(settings);
        minimum.Height = Math.Max(
            minimum.Height,
            SplitLayoutCalculator.GetMinimumWindowHeightForRows(
                settings,
                Math.Max(rowCount, visibleRowCount),
                RowGap));
        minimum.Height = GetFittingCompositeHeight(minimum.Width, minimum.Height, rowCount, visibleRowCount);
        return minimum;
    }

    private int GetFittingCompositeHeight(int width, int height, int rowCount, int visibleRowCount)
    {
        return OverlayCompositeLayoutCalculator.GetFittingHeight(
            width,
            height,
            settings,
            rowCount,
            visibleRowCount,
            RowGap);
    }

    private Size GetStatusWindowMinimumSize(Size minimumCompositeSize, int rowCount, int visibleRowCount)
    {
        if (OverlayCompositeLayoutCalculator.TryCreate(
                new Rectangle(Point.Empty, minimumCompositeSize),
                settings,
                rowCount,
                visibleRowCount,
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
            GetCurrentReservedLayoutRowCount(),
            GetCurrentLayoutRowCount(),
            RowGap,
            out layout);
    }

    private Size GetRuntimeLayoutSize(
        AppSettings? previousSettings,
        int currentSplitCount,
        Size? currentCompositeSizeOverride = null)
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

        int previousSplitCount = GetLayoutRowCount(previousSettings);
        height = AdjustRuntimeHeightForSplitCount(height, previousSplitCount, currentSplitCount, width);

        return new Size(Math.Max(1, width), Math.Max(1, height));
    }

    private int AdjustRuntimeHeightForSplitCount(
        int currentHeight,
        int previousSplitCount,
        int currentSplitCount,
        int currentWidth)
    {
        int splitDelta = currentSplitCount - previousSplitCount;
        if (splitDelta <= 0)
        {
            return currentHeight;
        }

        if (!SplitLayoutCalculator.TryCreate(
                new Rectangle(0, 0, currentWidth, currentHeight),
                Math.Max(1, previousSplitCount),
                RowGap,
                value => OverlayRenderContext.ScaleInt(settings, value),
                out SplitLayout previousLayout))
        {
            return currentHeight;
        }

        int targetRowHeight = previousLayout.FirstRowRect.Height;
        int rowStep = Math.Max(1, targetRowHeight + previousLayout.RowGap);
        int low = currentHeight;
        int high = Math.Max(low + 1, currentHeight + splitDelta * rowStep);
        while (!CanKeepRuntimeRowHeight(currentWidth, high, currentSplitCount, targetRowHeight) &&
            high < 10000)
        {
            high = Math.Min(10000, high + rowStep);
        }

        while (low + 1 < high)
        {
            int middle = low + (high - low) / 2;
            if (CanKeepRuntimeRowHeight(currentWidth, middle, currentSplitCount, targetRowHeight))
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }

        return CanKeepRuntimeRowHeight(currentWidth, high, currentSplitCount, targetRowHeight)
            ? high
            : currentHeight;
    }

    private bool CanKeepRuntimeRowHeight(int width, int height, int splitCount, int targetRowHeight)
    {
        return SplitLayoutCalculator.TryCreate(
                new Rectangle(0, 0, width, height),
                Math.Max(1, splitCount),
                RowGap,
                value => OverlayRenderContext.ScaleInt(settings, value),
                out SplitLayout layout) &&
            layout.FirstRowRect.Height >= targetRowHeight;
    }

    private static int ScaleRuntimeDimension(int value, float ratio)
    {
        return Math.Max(1, (int)Math.Round(value * ratio, MidpointRounding.AwayFromZero));
    }

    private static int GetDefaultLayoutRowCount()
    {
        return GetLayoutRowCount(AppSettingsDefaults.Create());
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
            if (Location != layout.StatusScreenBounds.Location)
            {
                Location = layout.StatusScreenBounds.Location;
            }

            if (ClientSize != layout.StatusScreenBounds.Size)
            {
                ClientSize = layout.StatusScreenBounds.Size;
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

    private void UpdateOverlayLayoutContextIfChanged()
    {
        UpdateOverlayLayoutContext(GetCurrentReservedLayoutRowCount(), GetCurrentLayoutRowCount(), force: false);
    }

    private void UpdateOverlayLayoutContext(int reservedRowCount, int visibleRowCount, bool force)
    {
        if (!force &&
            reservedRowCount == appliedOverlayReservedRowCount &&
            visibleRowCount == appliedOverlayVisibleRowCount)
        {
            return;
        }

        appliedOverlayReservedRowCount = reservedRowCount;
        appliedOverlayVisibleRowCount = visibleRowCount;
        overlayBoundsController.UpdateContext(settings, reservedRowCount, visibleRowCount);
        MarkStatusOverlayStaticContentDirty();
    }

    private void MarkStatusOverlayStaticContentDirty()
    {
        statusOverlayContentDirty = true;
        lastStatusOverlayDynamicKey = null;
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

        overlayBoundsController.HandleStatusResize(new Rectangle(Location, ClientSize));
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
        return new TimerOverlayRenderState(
            timerOverlaySettingsSnapshot,
            palette,
            splitStatuses,
            currentSplitIndex,
            runtimeSnapshot.TimerState,
            mouseClickThrough);
    }

    private TimerOverlayStateKey BuildTimerOverlaySnapshotKey()
    {
        return new TimerOverlayStateKey(
            runtimeSnapshot.TimerState,
            currentSplitIndex,
            mouseClickThrough,
            viewState.StatusHash,
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
        ExecuteAppCommand(AppCommand.ResetRun(recordStats: true, playResetSound: false));
    }

    private void ResetRun(bool recordStats = false)
    {
        ExecuteAppCommand(AppCommand.ResetRun(recordStats, playResetSound: false));
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

    private void ToggleMouseClickThrough()
    {
        SetMouseClickThrough(!mouseClickThrough);
        InvalidateRuntimeRenderRegion();
    }

    private void ClearSplitCompletionAnimation()
    {
        overlayAnimations.ClearSplitCompletionAnimation();
        UpdateStatusPaintSchedulerState();
        QueueStatusOverlayRender();
    }

    private void TogglePyramidFilter()
    {
        ExecuteAppCommand(AppCommand.TogglePyramidFilter());
    }

    private void RefreshRuntimeUi()
    {
        UpdateOverlayLayoutContextIfChanged();
        UpdateStatusPaintSchedulerState();
        PublishTimerOverlaySnapshot();
    }

    private async void StartCreateWorldAutomation()
    {
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

        AcceptRuntimeCommandSequence(monitorCoordinator.ClearPendingMenuActions());

        try
        {
            await worldAutomation.StartEnterWorldAsync(AppSettingsStore.Clone(settings), selectedSlot);
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Unhandled practice world automation error.");
        }
    }

    private void RegisterConfiguredHotkeys()
    {
        if (!registerGlobalHotkeys)
        {
            hotkeyManager.Dispose();
            lastHotkeyWarningText = null;
            return;
        }

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

    private static string GetHotkeyActionDisplayName(HotkeyAction action)
    {
        return action switch
        {
            HotkeyAction.PauseResume => "Pause / Resume",
            HotkeyAction.Reset => "Reset (Disabled in world)",
            HotkeyAction.MouseClickThrough => "Mouse passthrough",
            HotkeyAction.CreateWorld => "Create world (Disabled in world)",
            HotkeyAction.PracticeWorld => "Load world (Disabled in world)",
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



