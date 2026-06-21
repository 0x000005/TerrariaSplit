using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed partial class MainForm : Form
{
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
        palette = UiPalette.From(settings.Overlay.Colors);
        RefreshTimerOverlaySettingsSnapshot();
        UpdateOverlayLayoutContext(resolvedRowCount, visibleRowCount, force: true);
        UpdateEffectiveOverlayTopMost();
        if (IsHandleCreated && !settingsShell.IsOpen)
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

        int oldScale = Math.Clamp(previousSettings.Overlay.Columns.ScalePercent, 25, 300);
        int newScale = Math.Clamp(settings.Overlay.Columns.ScalePercent, 25, 300);
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
        if (settings.General.PracticeMode &&
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
        modalWindows.SetAlwaysOnTop(settings.General.AlwaysOnTop);
    }
}
