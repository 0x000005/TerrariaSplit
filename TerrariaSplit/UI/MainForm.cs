using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed partial class MainForm : Form
{
    private static readonly TimeSpan SplitCompletionFadeDuration = TimeSpan.FromSeconds(0.45);
    private static readonly TimeSpan ResetMenuGraceDuration = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan SplitCompletionDeltaIntroGap = TimeSpan.FromSeconds(0.06);
    private static readonly TimeSpan ControlTickInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan TimerRenderInterval = TimeSpan.FromMilliseconds(10);
    private const int ResizeBorder = 8;
    private const int RowGap = 9;
    private const int WsExTransparent = 0x20;
    private const int WsExLayered = 0x80000;

    private readonly RunSessionController runSession = new();
    private readonly TerrariaWorldAutomation worldAutomation = new();
    private readonly MainFormContextMenuBuilder contextMenuBuilder = new();
    private readonly SoundPlayerService soundPlayer = new();
    private readonly System.Windows.Forms.Timer controlTimer = new();
    private readonly System.Windows.Forms.Timer renderTimer = new();
    private readonly GlobalHotkeyManager hotkeyManager = new();
    private readonly Queue<TimerHotkeyRequest> pendingHotkeyRequests = new();
    private readonly OverlayRenderResources renderResources = new();
    private readonly Dictionary<int, SegmentBestDeltaHighlight> segmentBestDeltaHighlights = new();
    private readonly ContextMenuStrip contextMenu = new();
    private readonly RuntimePerformanceTracker performance = new();
    private readonly TerrariaMonitorCoordinator monitorCoordinator;
    private readonly OverlayWindowController overlayWindowController;
    private bool mouseClickThrough;
    private bool dragging;
    private Point dragStartCursor;
    private Point dragStartLocation;
    private SplitCompletionAnimation? splitCompletionAnimation;
    private bool closeFinalizationPending;
    private bool closeFinalizationComplete;
    private bool settingsFormOpen;
    private string? lastHotkeyWarningText;
    private bool closing;
    private string currentWindowText = string.Empty;
    private bool hasCachedLayout;
    private SplitLayout cachedLayout;
    private Rectangle cachedLayoutBounds;
    private int cachedLayoutStatusCount = -1;
    private int cachedLayoutScalePercent;
    private readonly PendingMenuHotkeyScheduler pendingMenuHotkeys = new();
    private readonly TimerController timerController;

    private AppSettings settings = AppSettingsStore.Load();
    private UiPalette palette;
    private TerrariaWatchSnapshot snapshot =
        new(false, null, false, null, TerrariaBossStates.Unknown, false, "waiting for Terraria.exe");

    private SplitTimer runTimer => runSession.Timer;

    private BossSplitTracker splitTracker => runSession.SplitTracker;

    public MainForm()
    {
        palette = UiPalette.From(settings.Colors);
        timerController = new TimerController(
            runTimer,
            splitTracker,
            pendingMenuHotkeys,
            ResetMenuGraceDuration);
        monitorCoordinator = new TerrariaMonitorCoordinator(
            new TerrariaWorldWatcher(),
            new TerrariaUiScalePatchApplierAdapter(),
            callback => BeginInvoke(callback));
        monitorCoordinator.WatcherPollCompleted += HandleWatcherPollCompleted;
        overlayWindowController = new OverlayWindowController(
            this,
            graphics =>
            {
                DrawOverlay(graphics);
                return true;
            },
            elapsed => performance.RecordPaint(elapsed));
        runSession.SetDefinitions(BossSplitDefinitions.Build(settings));
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
        performance.WatcherPollInterval = monitorCoordinator.WatcherPollInterval;
        performance.ProcessLookupInterval = monitorCoordinator.ProcessLookupInterval;
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
        overlayWindowController.ApplyWindowStyle(mouseClickThrough);
        if (!settingsFormOpen)
        {
            RegisterConfiguredHotkeys();
        }

        Invalidate();
        overlayWindowController.QueueRender();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        overlayWindowController.QueueRender();
    }

    protected override void OnInvalidated(InvalidateEventArgs e)
    {
        base.OnInvalidated(e);
        overlayWindowController.QueueRender();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        overlayWindowController.QueueRender();
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        overlayWindowController.QueueRender();
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
        monitorCoordinator.Dispose();
        worldAutomation.Dispose();
        overlayWindowController.Dispose();
        renderResources.Dispose();
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
        overlayWindowController.QueueRender();
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
            monitorCoordinator.Tick(runTimer.Phase, settings.Advanced?.EnableTerrariaUiScalePatch == true);
            bool consumedEnteredWorld = snapshot.EnteredWorld;
            TimerHotkeyRequest[] hotkeyRequests = DrainPendingHotkeyRequests();
            hotkeyRequests = CancelRequestedAutomations(hotkeyRequests);
            TimerControllerTickResult tickResult = timerController.Tick(snapshot, hotkeyRequests);
            if (consumedEnteredWorld)
            {
                snapshot = snapshot with { EnteredWorld = false };
            }

            bool invalidateAll = false;

            if (tickResult.PauseSoundRequested)
            {
                soundPlayer.Play(settings.Sounds.Pause);
            }

            if (tickResult.ResumeSoundRequested)
            {
                soundPlayer.Play(settings.Sounds.Resume);
            }

            if (tickResult.ToggleMouseClickThroughRequested)
            {
                SetMouseClickThrough(!mouseClickThrough);
                InvalidateRuntimeRenderRegion();
            }

            if (tickResult.RequestedMenuAction is MenuHotkeyActionKind menuAction)
            {
                ExecuteMenuHotkeyAction(menuAction);
                return;
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

    private void HandleWatcherPollCompleted(WatcherPollNotification notification)
    {
        performance.RecordWatcherPoll(notification.Elapsed);
        performance.WatcherPollInterval = notification.NextPollInterval;
        performance.ProcessLookupInterval = notification.ProcessLookupInterval;

        snapshot = notification.Snapshot;
        UpdateWindowTitle();
        if (!notification.Snapshot.Equals(notification.PreviousSnapshot))
        {
            Invalidate();
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
        settingsFormOpen = true;
        hotkeyManager.Dispose();
        pendingHotkeyRequests.Clear();
        pendingMenuHotkeys.Clear();
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
        runSession.SetDefinitions(BossSplitDefinitions.Build(settings));
        ResetRun();
        monitorCoordinator.ResetUiScalePatchState();
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

    private void ApplyLayeredOverlayWindowStyle()
    {
        BackColor = Color.Black;
        TransparencyKey = Color.Empty;
        overlayWindowController.ApplyWindowStyle(mouseClickThrough);
        overlayWindowController.QueueRender();
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
        runSession.Reset(settings, recordStats, ShowPersonalBestUpdateConfirmation);
        splitCompletionAnimation = null;
        pendingMenuHotkeys.Clear();
        segmentBestDeltaHighlights.Clear();
        UpdateRenderTimerState();
        Invalidate();
    }

    private void RecordRunStatsOnce()
    {
        runSession.RecordRunStatsOnce();
    }

    private void SetMouseClickThrough(bool enabled)
    {
        mouseClickThrough = enabled;
        overlayWindowController.ApplyWindowStyle(mouseClickThrough);
        UpdateWindowTitle();
    }

    private async void StartCreateWorldAutomation()
    {
        pendingHotkeyRequests.Clear();
        pendingMenuHotkeys.Clear();
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

        if (form.ShowDialog(this) == DialogResult.OK && form.SelectedSlot is PracticeWorldSlot selectedSlot)
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
        pendingMenuHotkeys.Clear();

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

    private void ClearIconCache()
    {
        renderResources.BossIcons.Clear();
    }
}



