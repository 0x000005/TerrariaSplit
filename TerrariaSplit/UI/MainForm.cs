using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed partial class MainForm : Form
{
    private static readonly TimeSpan SplitCompletionFadeDuration = TimeSpan.FromSeconds(0.45);
    private static readonly TimeSpan ResetMenuGraceDuration = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan CreateWorldHotkeyPendingDuration = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan SplitCompletionDeltaIntroGap = TimeSpan.FromSeconds(0.06);
    private static readonly TimeSpan ControlTickInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan TimerRenderInterval = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan WatcherRunningPollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan WatcherIdlePollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan WatcherScanPollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan WatcherProcessLookupInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan UiScalePatchRetryInterval = TimeSpan.FromSeconds(2);
    private const int ResizeBorder = 8;
    private const int RowGap = 9;
    private const int WsExTransparent = 0x20;
    private const int WsExLayered = 0x80000;
    private const byte AcSrcOver = 0x00;
    private const byte AcSrcAlpha = 0x01;
    private const int UlwAlpha = 0x00000002;
    private const uint BiRgb = 0;
    private const uint DibRgbColors = 0;
    private const int TextEffectSupersampleScale = 3;
    private const float SplitCompletionLabelFontRatio = 0.58f;
    private const float SplitCompletionDeltaFontRatio = 0.85f;
    private const float SplitCompletionDeltaOutroLeadRatio = 0.55f;
    private const float SplitCompletionDeltaIntroDurationRatio = 0.85f;
    private const float SplitCompletionDeltaSlideDistanceRatio = 0.75f;
    private const float SplitCompletionDeltaMinSlideDistance = 10f;
    private const float SplitCompletionDeltaMaxSlideDistance = 28f;

    private readonly SplitTimer runTimer = new();
    private readonly BossSplitTracker splitTracker = new();
    private readonly TerrariaWorldWatcher watcher = new();
    private readonly TerrariaCreateWorldAutomation createWorldAutomation = new();
    private readonly TerrariaPracticeWorldAutomation practiceWorldAutomation = new();
    private readonly MainFormContextMenuBuilder contextMenuBuilder = new();
    private readonly RunFinalizer runFinalizer = new();
    private readonly SoundPlayerService soundPlayer = new();
    private readonly TerrariaUiScalePatch uiScalePatch = new();
    private readonly System.Windows.Forms.Timer controlTimer = new();
    private readonly System.Windows.Forms.Timer renderTimer = new();
    private readonly GlobalHotkeyManager hotkeyManager = new();
    private readonly Queue<TimerHotkeyRequest> pendingHotkeyRequests = new();
    private readonly Dictionary<string, IconPair> iconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<FontKey, Font> fontCache = new();
    private readonly Dictionary<int, SegmentBestDeltaHighlight> segmentBestDeltaHighlights = new();
    private readonly ContextMenuStrip contextMenu = new();
    private readonly RuntimePerformanceTracker performance = new();
    private bool mouseClickThrough;
    private bool dragging;
    private Point dragStartCursor;
    private Point dragStartLocation;
    private SplitCompletionAnimation? splitCompletionAnimation;
    private bool runStatsRecorded;
    private bool closeFinalizationPending;
    private bool closeFinalizationComplete;
    private bool settingsFormOpen;
    private string? lastHotkeyWarningText;
    private DateTime? pendingCreateWorldDeadlineUtc;
    private DateTime? pendingPracticeWorldDeadlineUtc;
    private DateTime nextWatcherPollUtc = DateTime.MinValue;
    private DateTime nextUiScalePatchAttemptUtc = DateTime.MinValue;
    private bool watcherPollInFlight;
    private bool uiScalePatchInFlight;
    private bool closing;
    private bool layeredRenderPending;
    private bool layeredRenderInProgress;
    private string currentWindowText = string.Empty;
    private bool hasCachedLayout;
    private SplitLayout cachedLayout;
    private Rectangle cachedLayoutBounds;
    private int cachedLayoutStatusCount = -1;
    private int cachedLayoutScalePercent;
    private readonly PendingMenuHotkeyScheduler pendingMenuHotkeys = new();
    private int? uiScalePatchAppliedProcessId;
    private string? lastUiScalePatchLogKey;
    private readonly TimerController timerController;

    private AppSettings settings = AppSettingsStore.Load();
    private UiPalette palette;
    private TerrariaWatchSnapshot snapshot =
        new(false, null, false, null, TerrariaBossStates.Unknown, false, "waiting for Terraria.exe");

    public MainForm()
    {
        palette = UiPalette.From(settings.Colors);
        timerController = new TimerController(
            runTimer,
            splitTracker,
            pendingMenuHotkeys,
            ResetMenuGraceDuration);
        splitTracker.SetDefinitions(BossSplitDefinitions.Build(settings));
        Text = "TerrariaSplit";
        TopMost = settings.AlwaysOnTop;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        ApplyLayoutBounds(useDefaultSize: true);
        DoubleBuffered = true;
        ResizeRedraw = true;
        ApplyLayeredOverlayWindowStyle();
        Padding = Padding.Empty;

        UpdateContextMenu();
        contextMenu.Opening += (_, e) =>
        {
            if (settings.PracticeMode && IsEditablePracticePoint(PointToClient(Cursor.Position)))
            {
                e.Cancel = true;
            }
        };
        ContextMenuStrip = contextMenu;

        controlTimer.Interval = (int)ControlTickInterval.TotalMilliseconds;
        controlTimer.Tick += (_, _) => ControlTick();
        controlTimer.Start();

        renderTimer.Interval = (int)TimerRenderInterval.TotalMilliseconds;
        renderTimer.Tick += (_, _) => RenderTick();

        performance.TimerRenderInterval = TimerRenderInterval;
        performance.WatcherPollInterval = WatcherProcessLookupInterval;
        performance.ProcessLookupInterval = WatcherProcessLookupInterval;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
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
        UpdateMouseClickThroughStyle();
        if (!settingsFormOpen)
        {
            RegisterConfiguredHotkeys();
        }

        Invalidate();
        QueueLayeredOverlayRender();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        QueueLayeredOverlayRender();
    }

    protected override void OnInvalidated(InvalidateEventArgs e)
    {
        base.OnInvalidated(e);
        QueueLayeredOverlayRender();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        QueueLayeredOverlayRender();
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        QueueLayeredOverlayRender();
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
        controlTimer.Stop();
        renderTimer.Stop();
        controlTimer.Dispose();
        renderTimer.Dispose();
        hotkeyManager.Dispose();
        watcher.Dispose();
        createWorldAutomation.Dispose();
        practiceWorldAutomation.Dispose();

        foreach (IconPair iconPair in iconCache.Values)
        {
            iconPair.Lit.Dispose();
            iconPair.Undefeated.Dispose();
            iconPair.Current.Dispose();
        }

        foreach (Font font in fontCache.Values)
        {
            font.Dispose();
        }

        base.OnFormClosed(e);
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
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            dragging = true;
            dragStartCursor = Cursor.Position;
            dragStartLocation = Location;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!dragging)
        {
            return;
        }

        Point delta = new(Cursor.Position.X - dragStartCursor.X, Cursor.Position.Y - dragStartCursor.Y);
        Location = new Point(dragStartLocation.X + delta.X, dragStartLocation.Y + delta.Y);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
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
        if (TryGetTimerRect(out Rectangle timerRect) && timerRect.Contains(point))
        {
            return true;
        }

        if (!TryGetSplitRowAt(point, out int rowIndex, out Rectangle rowRect))
        {
            return false;
        }

        ColumnRects columns = GetColumnRects(rowRect);
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
        QueueLayeredOverlayRender();
    }

    protected override void WndProc(ref Message m)
    {
        const int wmNcHitTest = 0x84;
        const int htTransparent = -1;
        const int htClient = 1;
        const int htLeft = 10;
        const int htRight = 11;
        const int htTop = 12;
        const int htTopLeft = 13;
        const int htTopRight = 14;
        const int htBottom = 15;
        const int htBottomLeft = 16;
        const int htBottomRight = 17;

        if (hotkeyManager.TryGetAction(m, out TimerHotkeyAction action))
        {
            if (createWorldAutomation.IsRunning && action != TimerHotkeyAction.CreateWorld)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            if (practiceWorldAutomation.IsRunning && action != TimerHotkeyAction.PracticeWorld)
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

        bool left = point.X <= ResizeBorder;
        bool right = point.X >= ClientSize.Width - ResizeBorder;
        bool top = point.Y <= ResizeBorder;
        bool bottom = point.Y >= ClientSize.Height - ResizeBorder;

        if (left && top)
        {
            m.Result = (IntPtr)htTopLeft;
        }
        else if (right && top)
        {
            m.Result = (IntPtr)htTopRight;
        }
        else if (left && bottom)
        {
            m.Result = (IntPtr)htBottomLeft;
        }
        else if (right && bottom)
        {
            m.Result = (IntPtr)htBottomRight;
        }
        else if (left)
        {
            m.Result = (IntPtr)htLeft;
        }
        else if (right)
        {
            m.Result = (IntPtr)htRight;
        }
        else if (top)
        {
            m.Result = (IntPtr)htTop;
        }
        else if (bottom)
        {
            m.Result = (IntPtr)htBottom;
        }
    }

    private void ControlTick()
    {
        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            ScheduleWatcherPoll();
            ScheduleTerrariaUiScalePatch();
            bool consumedEnteredWorld = snapshot.EnteredWorld;
            TimerControllerTickResult tickResult = timerController.Tick(snapshot, DrainPendingHotkeyRequests());
            if (consumedEnteredWorld)
            {
                snapshot = snapshot with { EnteredWorld = false };
            }

            bool invalidateAll = false;

            if (tickResult.PauseSoundRequested)
            {
                soundPlayer.Play(settings.Sounds.Pause);
            }

            if (tickResult.ToggleMouseClickThroughRequested)
            {
                SetMouseClickThrough(!mouseClickThrough);
                InvalidateRuntimeRenderRegion();
            }

            if (tickResult.CreateWorldRequestedAtUtc is DateTime createWorldRequestedAtUtc)
            {
                QueuePendingCreateWorldAutomationRequest(createWorldRequestedAtUtc);
            }

            if (tickResult.PracticeWorldRequestedAtUtc is DateTime practiceWorldRequestedAtUtc)
            {
                QueuePendingPracticeWorldSelectorRequest(practiceWorldRequestedAtUtc);
            }

            if (tickResult.RequestedMenuAction == MenuHotkeyActionKind.Reset)
            {
                ExecuteReset();
                return;
            }

            if (TryStartPendingCreateWorldAutomation())
            {
                return;
            }

            if (TryShowPendingPracticeWorldSelector())
            {
                return;
            }

            if (tickResult.RunStarted)
            {
                runStatsRecorded = false;
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

            UpdateWindowTitle();
            if (invalidateAll)
            {
                Invalidate();
            }
        }
        finally
        {
            UpdateRenderTimerState();
            performance.RecordControlTick(Stopwatch.GetElapsedTime(startTimestamp));
        }
    }

    private void RenderTick()
    {
        if (splitCompletionAnimation is not null)
        {
            Invalidate();
            return;
        }

        if (runTimer.Phase == SplitTimerPhase.Running)
        {
            // Tight dirty rectangles can leave stale glyph pixels in capture clients when
            // the transparent overlay is repainted continuously.
            Invalidate();
            return;
        }

        UpdateRenderTimerState();
    }

    private void UpdateRenderTimerState()
    {
        bool shouldRun = !closing &&
            (runTimer.Phase == SplitTimerPhase.Running || splitCompletionAnimation is not null);
        if (shouldRun && !renderTimer.Enabled)
        {
            renderTimer.Start();
        }
        else if (!shouldRun && renderTimer.Enabled)
        {
            renderTimer.Stop();
        }
    }

    private void ScheduleWatcherPoll()
    {
        if (closing || watcherPollInFlight || DateTime.UtcNow < nextWatcherPollUtc)
        {
            return;
        }

        watcherPollInFlight = true;
        long startTimestamp = Stopwatch.GetTimestamp();
        _ = Task.Run(() =>
        {
            try
            {
                TerrariaWatchSnapshot polledSnapshot = watcher.Poll();
                return new WatcherPollCompletion(polledSnapshot, Stopwatch.GetElapsedTime(startTimestamp), null);
            }
            catch (Exception ex)
            {
                return new WatcherPollCompletion(
                    new TerrariaWatchSnapshot(
                        false,
                        null,
                        false,
                        null,
                        TerrariaBossStates.Unknown,
                        false,
                        $"watcher poll failed: {ex.Message}"),
                    Stopwatch.GetElapsedTime(startTimestamp),
                    ex);
            }
        }).ContinueWith(task =>
        {
            if (closing)
            {
                return;
            }

            try
            {
                BeginInvoke(new Action(() => CompleteWatcherPoll(task.Result)));
            }
            catch (ObjectDisposedException)
            {
                watcherPollInFlight = false;
            }
            catch (InvalidOperationException)
            {
                watcherPollInFlight = false;
            }
        }, TaskScheduler.Default);
    }

    private void CompleteWatcherPoll(WatcherPollCompletion completion)
    {
        watcherPollInFlight = false;
        performance.RecordWatcherPoll(completion.Elapsed);

        if (completion.Error is not null)
        {
            AppLogger.Error(completion.Error, "Unhandled watcher poll error.");
        }

        TerrariaWatchSnapshot previousSnapshot = snapshot;
        snapshot = completion.Snapshot;
        TimeSpan nextPollInterval = GetNextWatcherPollInterval(snapshot);
        nextWatcherPollUtc = DateTime.UtcNow + nextPollInterval;
        performance.WatcherPollInterval = nextPollInterval;
        performance.ProcessLookupInterval = snapshot.IsAttached ? TimeSpan.Zero : nextPollInterval;

        UpdateWindowTitle();
        if (!snapshot.Equals(previousSnapshot))
        {
            Invalidate();
        }
    }

    private TimeSpan GetNextWatcherPollInterval(TerrariaWatchSnapshot currentSnapshot)
    {
        if (!currentSnapshot.IsAttached)
        {
            return WatcherProcessLookupInterval;
        }

        if (!currentSnapshot.IsReady)
        {
            return WatcherScanPollInterval;
        }

        return runTimer.Phase == SplitTimerPhase.Running
            ? WatcherRunningPollInterval
            : WatcherIdlePollInterval;
    }

    private void ScheduleTerrariaUiScalePatch()
    {
        if (closing || settings.Advanced?.EnableTerrariaUiScalePatch != true)
        {
            uiScalePatchAppliedProcessId = null;
            return;
        }

        if (uiScalePatchAppliedProcessId is int appliedProcessId)
        {
            if (snapshot.ProcessId == appliedProcessId ||
                (!snapshot.ProcessId.HasValue && IsProcessStillRunning(appliedProcessId)))
            {
                return;
            }

            uiScalePatchAppliedProcessId = null;
        }

        if (uiScalePatchInFlight || DateTime.UtcNow < nextUiScalePatchAttemptUtc)
        {
            return;
        }

        uiScalePatchInFlight = true;
        int? fallbackProcessId = snapshot.ProcessId;
        _ = Task.Run(uiScalePatch.TryApply).ContinueWith(task =>
        {
            TerrariaUiScalePatchResult result = task.Status == TaskStatus.RanToCompletion
                ? task.Result
                : new TerrariaUiScalePatchResult(
                    TerrariaUiScalePatchStatus.Failed,
                    fallbackProcessId,
                    task.Exception?.GetBaseException().Message ?? "Unexpected Terraria UI scale patch failure.");

            if (closing)
            {
                return;
            }

            try
            {
                BeginInvoke(new Action(() => CompleteTerrariaUiScalePatch(result)));
            }
            catch (ObjectDisposedException)
            {
                uiScalePatchInFlight = false;
            }
            catch (InvalidOperationException)
            {
                uiScalePatchInFlight = false;
            }
        }, TaskScheduler.Default);
    }

    private void CompleteTerrariaUiScalePatch(TerrariaUiScalePatchResult result)
    {
        uiScalePatchInFlight = false;
        nextUiScalePatchAttemptUtc = DateTime.UtcNow + UiScalePatchRetryInterval;

        if (result.Status == TerrariaUiScalePatchStatus.NoProcess)
        {
            uiScalePatchAppliedProcessId = null;
            return;
        }

        if (result.IsSuccess && result.ProcessId.HasValue)
        {
            uiScalePatchAppliedProcessId = result.ProcessId.Value;
        }

        LogTerrariaUiScalePatchResult(result);
    }

    private void LogTerrariaUiScalePatchResult(TerrariaUiScalePatchResult result)
    {
        string logKey = string.Create(
            CultureInfo.InvariantCulture,
            $"{result.Status}:{result.ProcessId}:{result.Message}");
        if (string.Equals(logKey, lastUiScalePatchLogKey, StringComparison.Ordinal))
        {
            return;
        }

        lastUiScalePatchLogKey = logKey;
        string pid = result.ProcessId.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $"PID {result.ProcessId.Value}")
            : "no PID";
        AppLogger.Info($"Terraria UI scale enhancement {result.Status} for {pid}: {result.Message}");
    }

    private static bool IsProcessStillRunning(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void InvalidateRuntimeRenderRegion()
    {
        bool invalidated = false;
        if (TryGetTimerRect(out Rectangle timerRect))
        {
            Invalidate(Rectangle.Inflate(timerRect, ScaleInt(6), ScaleInt(6)));
            invalidated = true;
        }

        if (settings.ShowEarlyDeltaTime &&
            splitTracker.CurrentIndex >= 0 &&
            splitTracker.CurrentIndex < splitTracker.Statuses.Count &&
            TryGetLayout(out SplitLayout layout))
        {
            Rectangle rowRect = layout.GetRowRect(splitTracker.CurrentIndex);
            Invalidate(Rectangle.Inflate(rowRect, ScaleInt(6), ScaleInt(6)));
            invalidated = true;
        }

        if (!invalidated)
        {
            Invalidate();
        }
    }

    private void UpdateWindowTitle()
    {
        string title = $"TerrariaSplit - {FormatTimerPhase()} - {FormatWorldState()}";
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

    private bool ShowPersonalBestUpdateConfirmation(string promptText)
    {
        bool wasClickThrough = mouseClickThrough;
        if (wasClickThrough)
        {
            SetMouseClickThrough(false);
        }

        controlTimer.Stop();
        renderTimer.Stop();
        try
        {
            using var form = new PersonalBestUpdatePromptForm(
                promptText,
                timeoutSeconds: 10,
                settings);
            form.TopMost = true;
            return form.ShowDialog(this) != DialogResult.No;
        }
        finally
        {
            controlTimer.Start();
            UpdateRenderTimerState();
            if (wasClickThrough)
            {
                SetMouseClickThrough(true);
            }
        }
    }

    private void ExecuteReset()
    {
        pendingMenuHotkeys.Clear();
        ClearPendingCreateWorldAutomationRequest();
        soundPlayer.Play(settings.Sounds.Reset);
        ResetRun(recordStats: true);
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
        settingsFormOpen = true;
        hotkeyManager.Dispose();
        pendingHotkeyRequests.Clear();
        ClearPendingCreateWorldAutomationRequest();
        ClearPendingPracticeWorldSelectorRequest();
        try
        {
            using var form = new SettingsForm(settings, GetRuntimeDiagnostics);
            form.TopMost = TopMost;
            form.Applied += (_, _) => ApplySettings(form.Result);
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            ApplySettings(form.Result);
        }
        finally
        {
            settingsFormOpen = false;
            if (IsHandleCreated)
            {
                RegisterConfiguredHotkeys();
            }
        }
    }

    private void ApplySettings(AppSettings appliedSettings)
    {
        AppSettings previousSettings = AppSettingsStore.Clone(settings);
        settings = AppSettingsStore.Clone(appliedSettings);
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
        settings = AppSettingsStore.Load(path);
        ApplyLoadedSettings(previousSettings);
    }

    private void ApplyLoadedSettings(AppSettings? previousSettings = null)
    {
        palette = UiPalette.From(settings.Colors);
        splitTracker.SetDefinitions(BossSplitDefinitions.Build(settings));
        ResetRun();
        ResetTerrariaUiScalePatchState();
        TopMost = settings.AlwaysOnTop;
        if (IsHandleCreated && !settingsFormOpen)
        {
            RegisterConfiguredHotkeys();
        }

        ApplyLayeredOverlayWindowStyle();
        ApplyLayoutBounds(useDefaultSize: false, previousSettings);
        UpdateContextMenu();
        ClearIconCache();
        Invalidate();
    }

    private void ApplyLayoutBounds(bool useDefaultSize, AppSettings? previousSettings = null)
    {
        Size minimumSize = SplitLayoutCalculator.GetMinimumWindowSize(settings);
        if (useDefaultSize)
        {
            MinimumSize = minimumSize;
            Size = new Size(
                Math.Max(minimumSize.Width, SplitLayoutCalculator.GetDefaultWindowWidth(settings)),
                Math.Max(minimumSize.Height, SplitLayoutCalculator.GetDefaultWindowHeight(settings)));
            return;
        }

        Size targetSize = GetRuntimeLayoutSize(previousSettings);
        MinimumSize = minimumSize;
        int width = Math.Max(targetSize.Width, minimumSize.Width);
        int height = Math.Max(targetSize.Height, minimumSize.Height);
        if (width != Width || height != Height)
        {
            Size = new Size(width, height);
        }
    }

    private Size GetRuntimeLayoutSize(AppSettings? previousSettings)
    {
        if (previousSettings is null)
        {
            return Size;
        }

        int oldScale = Math.Clamp(previousSettings.Columns.ScalePercent, 25, 300);
        int newScale = Math.Clamp(settings.Columns.ScalePercent, 25, 300);
        float ratio = newScale / (float)oldScale;
        int width = Width;
        int height = Height;
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

    private void ResetTerrariaUiScalePatchState()
    {
        nextUiScalePatchAttemptUtc = DateTime.MinValue;
        uiScalePatchAppliedProcessId = null;
        lastUiScalePatchLogKey = null;
    }

    private void ApplyLayeredOverlayWindowStyle()
    {
        BackColor = Color.Black;
        TransparencyKey = Color.Empty;
        UpdateMouseClickThroughStyle();
        QueueLayeredOverlayRender();
    }

    private void OpenStatistics()
    {
        using var form = new StatisticsForm(settings);
        form.TopMost = TopMost;
        form.ShowDialog(this);
    }

    private void FinalizeRunBeforeExit()
    {
        ResetRun(recordStats: true);
    }

    private void ResetRun(bool recordStats = false)
    {
        if (recordStats)
        {
            runFinalizer.Finalize(settings, splitTracker.Statuses, runStatsRecorded, ShowPersonalBestUpdateConfirmation);
            runStatsRecorded = true;
        }

        runTimer.Reset();
        splitTracker.Reset();
        splitCompletionAnimation = null;
        pendingMenuHotkeys.Clear();
        ClearPendingCreateWorldAutomationRequest();
        ClearPendingPracticeWorldSelectorRequest();
        segmentBestDeltaHighlights.Clear();
        runStatsRecorded = false;
        UpdateRenderTimerState();
        Invalidate();
    }

    private void RecordRunStatsOnce()
    {
        if (runStatsRecorded)
        {
            return;
        }

        RunStatsStore.RecordRun(splitTracker.Statuses);
        runStatsRecorded = true;
    }

    private void SetMouseClickThrough(bool enabled)
    {
        mouseClickThrough = enabled;
        UpdateMouseClickThroughStyle();
        UpdateWindowTitle();
    }

    private async void StartCreateWorldAutomation()
    {
        pendingHotkeyRequests.Clear();
        ClearPendingCreateWorldAutomationRequest();
        ClearPendingPracticeWorldSelectorRequest();
        ResetRun(recordStats: true);

        try
        {
            await createWorldAutomation.RunAsync(AppSettingsStore.Clone(settings));
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Unhandled create world automation error.");
        }
    }

    private void QueuePendingCreateWorldAutomationRequest(DateTime requestedAtUtc)
    {
        pendingCreateWorldDeadlineUtc = requestedAtUtc + CreateWorldHotkeyPendingDuration;

        if (createWorldAutomation.IsRunning)
        {
            createWorldAutomation.Cancel();
            return;
        }
    }

    private void QueuePendingPracticeWorldSelectorRequest(DateTime requestedAtUtc)
    {
        pendingPracticeWorldDeadlineUtc = requestedAtUtc + CreateWorldHotkeyPendingDuration;

        if (practiceWorldAutomation.IsRunning)
        {
            practiceWorldAutomation.Cancel();
            return;
        }
    }

    private bool TryStartPendingCreateWorldAutomation()
    {
        if (pendingCreateWorldDeadlineUtc is not DateTime deadlineUtc || createWorldAutomation.IsRunning)
        {
            return false;
        }

        if (DateTime.UtcNow > deadlineUtc)
        {
            ClearPendingCreateWorldAutomationRequest();
            return false;
        }

        if (snapshot.IsGameMenu != true)
        {
            return false;
        }

        StartCreateWorldAutomation();
        return true;
    }

    private void ClearPendingCreateWorldAutomationRequest()
    {
        pendingCreateWorldDeadlineUtc = null;
    }

    private bool TryShowPendingPracticeWorldSelector()
    {
        if (pendingPracticeWorldDeadlineUtc is not DateTime deadlineUtc || practiceWorldAutomation.IsRunning)
        {
            return false;
        }

        if (DateTime.UtcNow > deadlineUtc)
        {
            ClearPendingPracticeWorldSelectorRequest();
            return false;
        }

        if (snapshot.IsGameMenu != true)
        {
            return false;
        }

        ClearPendingPracticeWorldSelectorRequest();
        ResetRun(recordStats: true);
        ShowPracticeWorldSelector();
        return true;
    }

    private void ClearPendingPracticeWorldSelectorRequest()
    {
        pendingPracticeWorldDeadlineUtc = null;
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

        if (form.ShowDialog(this) == DialogResult.OK && form.SelectedSlot is PracticeWorldSlot selectedSlot)
        {
            StartPracticeWorldAutomation(selectedSlot);
        }
    }

    private async void StartPracticeWorldAutomation(PracticeWorldSlot selectedSlot)
    {
        if (!PracticeWorldSaveInstaller.TryValidate(selectedSlot, out string validationMessage))
        {
            AppLogger.Info(validationMessage);
            return;
        }

        pendingHotkeyRequests.Clear();
        ClearPendingCreateWorldAutomationRequest();
        ClearPendingPracticeWorldSelectorRequest();

        try
        {
            await practiceWorldAutomation.RunAsync(AppSettingsStore.Clone(settings), selectedSlot);
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Unhandled practice world automation error.");
        }
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
        ShowTopMostHotkeyWarning(message);
    }

    private void ShowTopMostHotkeyWarning(string message)
    {
        const uint mbOk = 0x00000000;
        const uint mbIconWarning = 0x00000030;
        const uint mbSetForeground = 0x00010000;
        const uint mbTopMost = 0x00040000;

        int result = NativeMethods.MessageBox(
            IsHandleCreated ? Handle : IntPtr.Zero,
            message,
            Localizer.Get("Hotkey warning", settings),
            mbOk | mbIconWarning | mbSetForeground | mbTopMost);
        if (result == 0)
        {
            MessageBox.Show(
                this,
                message,
                Localizer.Get("Hotkey warning", settings),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private string FormatHotkeyRegistrationWarning(HotkeyRegistrationWarning warning)
    {
        string actionName = Localizer.Get(GetHotkeyActionDisplayName(warning.Action), settings);
        return warning.Kind switch
        {
            HotkeyRegistrationWarningKind.Duplicate => string.Format(
                Localizer.Get("{0}: {1} is duplicated; only the first action using this key is active.", settings),
                actionName,
                warning.Keys),
            HotkeyRegistrationWarningKind.Invalid => string.Format(
                Localizer.Get("{0}: {1} is not allowed as a hotkey.", settings),
                actionName,
                warning.Keys),
            HotkeyRegistrationWarningKind.SystemRegistrationFailed => string.Format(
                Localizer.Get("{0}: {1} registration failed. It may be used by another program. ({2})", settings),
                actionName,
                warning.Keys,
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
            TimerHotkeyAction.PracticeWorld => "Quick enter world (Disabled in world)",
            _ => action.ToString()
        };
    }

    private void UpdateMouseClickThroughStyle()
    {
        const int gwlExStyle = -20;

        if (!IsHandleCreated)
        {
            return;
        }

        IntPtr handle = Handle;
        int style = GetWindowLong(handle, gwlExStyle);
        style |= WsExLayered;
        if (mouseClickThrough)
        {
            style |= WsExTransparent;
        }
        else
        {
            style &= ~WsExTransparent;
        }

        SetWindowLong(handle, gwlExStyle, style);
    }

    private void QueueLayeredOverlayRender()
    {
        if (!IsHandleCreated || IsDisposed || Disposing || closing || layeredRenderPending)
        {
            return;
        }

        layeredRenderPending = true;
        try
        {
            BeginInvoke(new Action(RenderLayeredOverlayNow));
        }
        catch (InvalidOperationException)
        {
            layeredRenderPending = false;
        }
    }

    private void RenderLayeredOverlayNow()
    {
        if (!IsHandleCreated || IsDisposed || Disposing || closing)
        {
            layeredRenderPending = false;
            return;
        }

        if (layeredRenderInProgress)
        {
            return;
        }

        layeredRenderPending = false;
        layeredRenderInProgress = true;
        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            if (!RenderLayeredOverlay())
            {
                AppLogger.Info($"Layered overlay update failed. Win32Error={Marshal.GetLastWin32Error()}.");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Layered overlay render failed.");
        }
        finally
        {
            layeredRenderInProgress = false;
            performance.RecordPaint(Stopwatch.GetElapsedTime(startTimestamp));
        }
    }

    private bool RenderLayeredOverlay()
    {
        if (!IsHandleCreated || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return false;
        }

        using var bitmap = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppPArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            ConfigureOverlayGraphics(graphics);
            graphics.Clear(Color.Transparent);
            DrawOverlay(graphics);
        }

        return UpdateLayeredBitmap(bitmap);
    }

    private static void ConfigureOverlayGraphics(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.CompositingMode = CompositingMode.SourceOver;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
    }

    private bool UpdateLayeredBitmap(Bitmap bitmap)
    {
        IntPtr screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            return false;
        }

        IntPtr memoryDc = IntPtr.Zero;
        IntPtr bitmapHandle = IntPtr.Zero;
        IntPtr oldBitmap = IntPtr.Zero;
        try
        {
            memoryDc = CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero)
            {
                return false;
            }

            bitmapHandle = CreateLayeredBitmapHandle(bitmap, screenDc);
            if (bitmapHandle == IntPtr.Zero)
            {
                return false;
            }

            oldBitmap = SelectObject(memoryDc, bitmapHandle);
            if (oldBitmap == IntPtr.Zero)
            {
                return false;
            }

            var destination = new NativePoint(Left, Top);
            var size = new NativeSize(bitmap.Width, bitmap.Height);
            var source = new NativePoint(0, 0);
            var blend = new BlendFunction
            {
                BlendOp = AcSrcOver,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = AcSrcAlpha
            };

            return UpdateLayeredWindow(
                Handle,
                screenDc,
                ref destination,
                ref size,
                memoryDc,
                ref source,
                0,
                ref blend,
                UlwAlpha);
        }
        finally
        {
            if (oldBitmap != IntPtr.Zero)
            {
                SelectObject(memoryDc, oldBitmap);
            }

            if (bitmapHandle != IntPtr.Zero)
            {
                DeleteObject(bitmapHandle);
            }

            if (memoryDc != IntPtr.Zero)
            {
                DeleteDC(memoryDc);
            }

            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static IntPtr CreateLayeredBitmapHandle(Bitmap bitmap, IntPtr deviceContext)
    {
        var bitmapInfo = new BitmapInfo
        {
            Header = new BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                Width = bitmap.Width,
                Height = -bitmap.Height,
                Planes = 1,
                BitCount = 32,
                Compression = BiRgb,
                SizeImage = (uint)(bitmap.Width * bitmap.Height * 4)
            }
        };

        IntPtr bitmapHandle = CreateDIBSection(
            deviceContext,
            ref bitmapInfo,
            DibRgbColors,
            out IntPtr bits,
            IntPtr.Zero,
            0);
        if (bitmapHandle == IntPtr.Zero || bits == IntPtr.Zero)
        {
            if (bitmapHandle != IntPtr.Zero)
            {
                DeleteObject(bitmapHandle);
            }

            return IntPtr.Zero;
        }

        CopyBitmapPixels(bitmap, bits);
        return bitmapHandle;
    }

    private static void CopyBitmapPixels(Bitmap bitmap, IntPtr destination)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            int rowBytes = bitmap.Width * 4;
            byte[] buffer = new byte[rowBytes];
            int sourceStride = data.Stride;
            for (int y = 0; y < bitmap.Height; y++)
            {
                IntPtr sourceRow = sourceStride >= 0
                    ? IntPtr.Add(data.Scan0, y * sourceStride)
                    : IntPtr.Add(data.Scan0, (bitmap.Height - 1 - y) * -sourceStride);
                IntPtr destinationRow = IntPtr.Add(destination, y * rowBytes);
                Marshal.Copy(sourceRow, buffer, 0, rowBytes);
                Marshal.Copy(buffer, 0, destinationRow, rowBytes);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private void ClearIconCache()
    {
        foreach (IconPair iconPair in iconCache.Values)
        {
            iconPair.Lit.Dispose();
            iconPair.Undefeated.Dispose();
            iconPair.Current.Dispose();
        }

        iconCache.Clear();
    }

    private IconPair LoadIconPair(BossSplitDefinition definition, string fileName)
    {
        string iconKey = GetIconKey(definition, fileName);
        string customPath = settings.GetBossIconPath(iconKey);
        string cacheKey = string.IsNullOrWhiteSpace(customPath)
            ? $"asset:{fileName}"
            : $"file:{customPath}";

        if (iconCache.TryGetValue(cacheKey, out IconPair? iconPair))
        {
            return iconPair;
        }

        string path = !string.IsNullOrWhiteSpace(customPath)
            ? customPath
            : Path.Combine(AppContext.BaseDirectory, "Assets", "BossIcons", fileName);
        Bitmap lit = File.Exists(path) ? new Bitmap(path) : CreatePlaceholderIcon();
        Bitmap undefeated = CreateBossChecklistUndefeatedIcon(
            lit,
            settings.UndefeatedIconGrayscalePercent,
            settings.UndefeatedIconBrightnessPercent);
        Bitmap current = CreateBossChecklistUndefeatedIcon(
            lit,
            Math.Max(0, settings.UndefeatedIconGrayscalePercent - settings.CurrentBossIconGrayscaleWeakenPercent),
            Math.Min(100, settings.UndefeatedIconBrightnessPercent + settings.CurrentBossIconBrightnessBoostPercent));
        iconPair = new IconPair(lit, undefeated, current);
        iconCache[cacheKey] = iconPair;
        return iconPair;
    }

    private static string GetIconKey(BossSplitDefinition definition, string fileName)
    {
        int index = definition.IconFileNames
            .Select((value, itemIndex) => new { value, itemIndex })
            .FirstOrDefault(item => string.Equals(item.value, fileName, StringComparison.OrdinalIgnoreCase))
            ?.itemIndex ?? -1;
        return index >= 0 && index < definition.IconKeys.Count
            ? definition.IconKeys[index]
            : definition.Name;
    }

    private static Bitmap CreateBossChecklistUndefeatedIcon(
        Bitmap source,
        int grayscalePercent,
        int brightnessPercent)
    {
        var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        float grayscale = Math.Clamp(grayscalePercent, 0, 100) / 100f;
        float brightness = Math.Clamp(brightnessPercent, 0, 100) / 100f;

        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                Color pixel = source.GetPixel(x, y);
                if (pixel.A == 0)
                {
                    continue;
                }

                int gray = (int)Math.Round(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                int red = Darken(Lerp(pixel.R, gray, grayscale), brightness);
                int green = Darken(Lerp(pixel.G, gray, grayscale), brightness);
                int blue = Darken(Lerp(pixel.B, gray, grayscale), brightness);
                bitmap.SetPixel(x, y, Color.FromArgb(pixel.A, red, green, blue));
            }
        }

        return bitmap;
    }

    private static int Lerp(int from, int to, float amount)
    {
        return Math.Clamp((int)Math.Round(from + (to - from) * amount), 0, 255);
    }

    private static int Darken(int value, float amount)
    {
        return Math.Clamp((int)Math.Round(value * amount), 0, 255);
    }

    private static Bitmap CreatePlaceholderIcon()
    {
        var bitmap = new Bitmap(32, 32);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        using var brush = new SolidBrush(Color.FromArgb(100, 100, 100));
        graphics.FillEllipse(brush, 2, 2, 28, 28);
        return bitmap;
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteDC(IntPtr hDc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr SelectObject(IntPtr hDc, IntPtr hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateDIBSection(
        IntPtr hdc,
        ref BitmapInfo pbmi,
        uint usage,
        out IntPtr bits,
        IntPtr section,
        uint offset);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindow(
        IntPtr hWnd,
        IntPtr hdcDst,
        ref NativePoint pptDst,
        ref NativeSize psize,
        IntPtr hdcSrc,
        ref NativePoint pptSrc,
        int crKey,
        ref BlendFunction pblend,
        int dwFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;

        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Width;
        public int Height;

        public NativeSize(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    private sealed class PersonalBestUpdatePromptForm : Form
    {
        private readonly System.Windows.Forms.Timer timer = new();
        private readonly Label countdownLabel = new();
        private int remainingSeconds;

        private readonly AppSettings settings;

        public PersonalBestUpdatePromptForm(string updateText, int timeoutSeconds, AppSettings settings)
        {
            this.settings = settings;
            remainingSeconds = Math.Max(1, timeoutSeconds);
            int lineCount = Math.Max(1, updateText.Split(Environment.NewLine).Length);
            int height = Math.Clamp(210 + lineCount * 28, 260, 760);
            UiTheme.ConfigureForm(this, new Size(1040, 260));
            ClientSize = new Size(1040, height);
            Text = Localizer.Get("Update personal data?", settings);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(22, 18, 22, 20),
                ColumnCount = 1,
                RowCount = 4
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var titleLabel = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                Font = UiTheme.FormFont(12.5f, FontStyle.Bold),
                ForeColor = UiTheme.Text,
                Text = Localizer.Get("Update personal data?", settings)
            };

            var detailLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = UiTheme.Text,
                Font = UiTheme.FormFont(10f),
                Text = updateText,
                TextAlign = ContentAlignment.TopLeft,
                UseMnemonic = false
            };

            countdownLabel.AutoSize = true;
            countdownLabel.Dock = DockStyle.Fill;
            countdownLabel.ForeColor = UiTheme.MutedText;

            var buttonPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            var yesButton = new Button { Text = Localizer.Get("Update", settings) };
            UiTheme.StyleButton(yesButton, accent: true, minimumWidth: 118);
            yesButton.Click += (_, _) =>
            {
                DialogResult = DialogResult.Yes;
                Close();
            };

            var noButton = new Button { Text = Localizer.Get("Skip", settings) };
            UiTheme.StyleButton(noButton, accent: false, minimumWidth: 118);
            noButton.Click += (_, _) =>
            {
                DialogResult = DialogResult.No;
                Close();
            };

            buttonPanel.Controls.Add(yesButton);
            buttonPanel.Controls.Add(noButton);

            layout.Controls.Add(titleLabel, 0, 0);
            layout.Controls.Add(detailLabel, 0, 1);
            layout.Controls.Add(countdownLabel, 0, 2);
            layout.Controls.Add(buttonPanel, 0, 3);
            Controls.Add(layout);

            AcceptButton = yesButton;
            CancelButton = noButton;
            DialogResult = DialogResult.Yes;
            UpdateCountdownText();

            timer.Interval = 1000;
            timer.Tick += (_, _) =>
            {
                remainingSeconds--;
                if (remainingSeconds <= 0)
                {
                    DialogResult = DialogResult.Yes;
                    Close();
                    return;
                }

                UpdateCountdownText();
            };
            timer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
            }

            base.Dispose(disposing);
        }

        private void UpdateCountdownText()
        {
            countdownLabel.Text = string.Format(
                Localizer.Get("No response updates automatically in {0}s.", settings),
                remainingSeconds);
        }
    }

    private sealed record IconPair(Image Lit, Image Undefeated, Image Current);

    private readonly record struct FontKey(float Size, bool Bold);

    private readonly record struct FontMetrics(float Ascent, float Descent);

    private readonly record struct TimerTextLayout(float Right, float Top, float Height)
    {
        public static TimerTextLayout Empty => new(0f, 0f, 0f);
    }

    private readonly record struct SplitCompletionDeltaMotion(float OffsetX, float Opacity);

    private readonly record struct ColumnWidth(SplitColumn Column, int Width);

    private readonly record struct SegmentBestDeltaHighlight(string Style, DateTime StartedAtUtc);

    private sealed record WatcherPollCompletion(
        TerrariaWatchSnapshot Snapshot,
        TimeSpan Elapsed,
        Exception? Error);

    private sealed record SplitCompletionAnimation(
        BossSplitDefinition Definition,
        TimeSpan SegmentTime,
        TimeSpan SplitTime,
        SplitComparison ReferenceSplitComparison,
        SplitComparison PersonalBestSegmentComparison,
        bool ShowSplitComparison,
        string SplitTimeOutlineStyle,
        bool ShowSegmentComparison,
        string SegmentTimeOutlineStyle,
        string SegmentBestDeltaHighlightStyle,
        DateTime StartedAtUtc);

    private readonly record struct ColumnRects(
        Rectangle? Icon,
        Rectangle? Time,
        Rectangle? Delta);

    private enum SplitColumn
    {
        Icon,
        Time,
        Delta
    }

    private readonly record struct SplitComparison(TimeSpan? Delta, bool ShowDelta)
    {
        public static SplitComparison Empty => new(null, false);
    }

    private readonly record struct TextRenderStyle(
        Color Fill,
        Color Outline,
        Color Shadow,
        int ShadowPercent,
        int OutlineThicknessPercent);

    private readonly record struct UiPalette(
        Color ReferenceText,
        Color ReferenceTextOutline,
        Color ReferenceTextShadow,
        Color ActiveReferenceText,
        Color ActiveReferenceTextOutline,
        Color ActiveReferenceTextShadow,
        Color SplitText,
        Color SplitTextOutline,
        Color SplitTextShadow,
        Color DeltaAheadText,
        Color DeltaAheadTextOutline,
        Color DeltaAheadTextShadow,
        Color DeltaBehindText,
        Color DeltaBehindTextOutline,
        Color DeltaBehindTextShadow,
        Color TimerText,
        Color TimerTextOutline,
        Color TimerTextShadow,
        Color TimerAheadText,
        Color TimerAheadTextOutline,
        Color TimerAheadTextShadow,
        Color TimerBehindText,
        Color TimerBehindTextOutline,
        Color TimerBehindTextShadow,
        Color TimerRecordText,
        Color TimerRecordTextOutline,
        Color TimerRecordTextShadow,
        Color TimerNoRecordText,
        Color TimerNoRecordTextOutline,
        Color TimerNoRecordTextShadow,
        Color TimerPausedText,
        Color TimerPausedTextOutline,
        Color TimerPausedTextShadow,
        Color SplitCompletionLabelText,
        Color SplitCompletionTimeText)
    {
        public static UiPalette From(UiColorSettings settings)
        {
            return new UiPalette(
                ColorText.Parse(settings.ReferenceText, Color.FromArgb(200, 200, 200)),
                ColorText.Parse(settings.ReferenceTextOutline, Color.FromArgb(16, 16, 16)),
                ColorText.Parse(settings.ReferenceTextShadow, Color.Black),
                ColorText.Parse(settings.ActiveReferenceText, Color.FromArgb(255, 211, 90)),
                ColorText.Parse(settings.ActiveReferenceTextOutline, Color.FromArgb(16, 16, 16)),
                ColorText.Parse(settings.ActiveReferenceTextShadow, Color.Black),
                ColorText.Parse(settings.SplitText, Color.FromArgb(240, 160, 64)),
                ColorText.Parse(settings.SplitTextOutline, Color.FromArgb(16, 16, 16)),
                ColorText.Parse(settings.SplitTextShadow, Color.Black),
                ColorText.Parse(settings.DeltaAheadText, Color.LightGreen),
                ColorText.Parse(settings.DeltaAheadTextOutline, Color.FromArgb(16, 16, 16)),
                ColorText.Parse(settings.DeltaAheadTextShadow, Color.Black),
                ColorText.Parse(settings.DeltaBehindText, Color.LightCoral),
                ColorText.Parse(settings.DeltaBehindTextOutline, Color.FromArgb(16, 16, 16)),
                ColorText.Parse(settings.DeltaBehindTextShadow, Color.Black),
                ColorText.Parse(settings.TimerText, Color.FromArgb(242, 242, 242)),
                ColorText.Parse(settings.TimerTextOutline, Color.FromArgb(16, 16, 16)),
                ColorText.Parse(settings.TimerTextShadow, Color.Black),
                ColorText.Parse(settings.TimerAheadText, Color.LightGreen),
                ColorText.Parse(settings.TimerAheadTextOutline, Color.FromArgb(16, 16, 16)),
                ColorText.Parse(settings.TimerAheadTextShadow, Color.Black),
                ColorText.Parse(settings.TimerBehindText, Color.LightCoral),
                ColorText.Parse(settings.TimerBehindTextOutline, Color.FromArgb(16, 16, 16)),
                ColorText.Parse(settings.TimerBehindTextShadow, Color.Black),
                ColorText.Parse(settings.TimerRecordText, Color.FromArgb(105, 167, 255)),
                ColorText.Parse(settings.TimerRecordTextOutline, Color.FromArgb(16, 16, 16)),
                ColorText.Parse(settings.TimerRecordTextShadow, Color.Black),
                ColorText.Parse(settings.TimerNoRecordText, Color.Red),
                ColorText.Parse(settings.TimerNoRecordTextOutline, Color.FromArgb(16, 16, 16)),
                ColorText.Parse(settings.TimerNoRecordTextShadow, Color.Black),
                ColorText.Parse(settings.TimerPausedText, Color.Gainsboro),
                ColorText.Parse(settings.TimerPausedTextOutline, Color.FromArgb(16, 16, 16)),
                ColorText.Parse(settings.TimerPausedTextShadow, Color.Black),
                ColorText.Parse(settings.SplitCompletionLabelText, Color.FromArgb(222, 222, 226)),
                ColorText.Parse(settings.SplitCompletionTimeText, Color.White));
        }
    }
}

