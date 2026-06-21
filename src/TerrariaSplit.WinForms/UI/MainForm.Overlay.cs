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
        Rectangle? referenceCompositeBounds = overlayShell.WindowsInitialized
            ? overlayBoundsController.CompositeBounds
            : overlayShell.PendingInitialCompositeBounds;
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
            hotkeyShell.Register();
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
            if (!overlayShell.WindowsInitialized &&
                !IsHandleCreated &&
                TryGetInitialOverlayLayout(defaultCompositeSize, out targetCompositeBounds, out OverlayCompositeLayout initialLayout))
            {
                overlayShell.PendingInitialCompositeBounds = targetCompositeBounds;
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
            Rectangle referenceBounds = referenceCompositeBoundsOverride ?? (overlayShell.WindowsInitialized
                ? overlayBoundsController.CompositeBounds
                : overlayShell.PendingInitialCompositeBounds ?? Bounds);
            targetCompositeBounds = new Rectangle(referenceBounds.Left, referenceBounds.Top, width, height);
        }

        MinimumSize = minimumStatusSize;
        if (overlayShell.WindowsInitialized)
        {
            overlayBoundsController.ApplyCompositeBounds(targetCompositeBounds);
            return;
        }

        if (targetCompositeBounds.Width != Width || targetCompositeBounds.Height != Height)
        {
            Size = targetCompositeBounds.Size;
        }

        overlayShell.PendingInitialCompositeBounds = targetCompositeBounds;
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
        Size currentSize = currentCompositeSizeOverride ?? (overlayShell.WindowsInitialized
            ? overlayBoundsController.CompositeBounds.Size
            : overlayShell.PendingInitialCompositeBounds?.Size ?? Size);
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
        overlayWindowController.ApplyWindowStyle(overlayShell.MouseClickThrough);
        timerOverlayHost.ApplyMouseClickThrough(overlayShell.MouseClickThrough);
        QueueStatusOverlayRender();
    }

    private void InitializeOverlayWindows()
    {
        if (!overlayShell.BeginWindowInitialization())
        {
            return;
        }

        try
        {
            Rectangle initialCompositeBounds = overlayShell.CompleteWindowInitialization(Bounds);
            overlayBoundsController.Initialize(initialCompositeBounds);
            timerOverlayHost.Start();
            UpdateEffectiveOverlayTopMost();
            timerOverlayHost.ApplyMouseClickThrough(overlayShell.MouseClickThrough);
            UpdateTimerOverlayRefreshInterval();
            PublishTimerOverlaySnapshot(true);
        }
        finally
        {
            overlayShell.EndWindowInitialization();
        }

        RenderInitialStatusOverlay();
        BeginInvoke(new Action(overlayShell.EnableStatusBoundsFeedback));
    }

    private void ApplyOverlayLayout(OverlayCompositeLayout layout)
    {
        overlayShell.BeginSuppressStatusBoundsFeedback();
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
            overlayShell.EndSuppressStatusBoundsFeedback();
        }

        timerOverlayHost.ApplyOverlayLayout(layout);
        UpdateEffectiveOverlayTopMost();
        timerOverlayHost.ApplyMouseClickThrough(overlayShell.MouseClickThrough);
        UpdateTimerOverlayRefreshInterval();
        QueueStatusOverlayRender();
    }

    private void QueueStatusOverlayRender()
    {
        if (!overlayShell.WindowsInitialized || overlayShell.WindowInitializationInProgress)
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
        if (!overlayShell.ApplyLayoutRowCounts(reservedRowCount, visibleRowCount, force))
        {
            return;
        }

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
        if (!overlayShell.WindowsInitialized || overlayShell.WindowInitializationInProgress)
        {
            return;
        }

        overlayWindowController.RenderImmediately();
    }

    private void NotifyStatusBoundsChanged()
    {
        if (!overlayShell.WindowsInitialized ||
            !overlayShell.StatusBoundsFeedbackEnabled ||
            overlayShell.SuppressStatusBoundsFeedback ||
            windowShell.IsDragging)
        {
            return;
        }

        overlayBoundsController.HandleStatusResize(new Rectangle(Location, ClientSize));
    }

    private void PublishTimerOverlaySnapshot(bool force = false)
    {
        if (!overlayShell.WindowsInitialized)
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
        if (runtimeShell.UpdateControlTickInterval(nextControlInterval))
        {
            controlScheduler.UpdateInterval(runtimeShell.ControlTickInterval);
        }

        performance.ControlTickInterval = runtimeShell.ControlTickInterval;

        TimeSpan nextStatusPaintInterval = ResolveRunningStatusPaintInterval();
        if (runtimeShell.UpdateStatusPaintInterval(nextStatusPaintInterval))
        {
            statusPaintScheduler.UpdateInterval(runtimeShell.StatusPaintInterval);
        }

        performance.StatusPaintInterval = runtimeShell.StatusPaintInterval;
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
        if (!runtimeShell.CurrentSnapshot.IsReady)
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
            overlayShell.MouseClickThrough);
    }

    private TimerOverlayStateKey BuildTimerOverlaySnapshotKey()
    {
        return new TimerOverlayStateKey(
            runtimeSnapshot.TimerState,
            currentSplitIndex,
            overlayShell.MouseClickThrough,
            viewState.StatusHash,
            timerOverlaySettingsRevision);
    }

    private void UpdateTimerOverlayRefreshInterval()
    {
        if (!overlayShell.WindowsInitialized)
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
            overlayShell.WindowsInitialized &&
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
        timerOverlaySettingsSnapshot = settingsSnapshots.CreateSnapshot(settings);
    }

    private void UpdateEffectiveOverlayTopMost()
    {
        modalWindows.SetAlwaysOnTop(settings.General.AlwaysOnTop);
    }
}
