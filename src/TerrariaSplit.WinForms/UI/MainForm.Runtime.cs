using System.Diagnostics;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed partial class MainForm : Form
{
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

        if (!runtimeShell.TryMarkControlTickDispatchPending())
        {
            return;
        }

        try
        {
            BeginInvoke(dispatchedControlTick);
        }
        catch (ObjectDisposedException)
        {
            runtimeShell.ClearControlTickDispatchPending();
        }
        catch (InvalidOperationException)
        {
            runtimeShell.ClearControlTickDispatchPending();
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
            runtimeShell.ClearControlTickDispatchPending();
        }
    }

    private void QueueStatusPaintTick(HighPrecisionSchedulerTick tick)
    {
        performance.RecordStatusPaintTick(tick);

        if (!CanDispatchToUiThread())
        {
            return;
        }

        if (!runtimeShell.TryMarkStatusPaintDispatchPending())
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
            runtimeShell.ClearStatusPaintDispatchPending();
        }
        catch (InvalidOperationException)
        {
            runtimeShell.ClearStatusPaintDispatchPending();
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
            runtimeShell.ClearStatusPaintDispatchPending();
        }
    }

    private bool CanDispatchToUiThread()
    {
        return !windowShell.IsClosing && IsHandleCreated && !IsDisposed && !Disposing;
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
        if (overlayShell.StatusOverlayContentDirty || overlayShell.LastStatusOverlayDynamicKey is null)
        {
            overlayWindowController.RenderImmediately();
            return;
        }

        if (overlayShell.CanSkipRunningStatusOverlayFrame(
                StatusOverlayHighlightsActive,
                ComputeStatusOverlayDynamicKey(timerElapsed)))
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
        bool shouldRun = !windowShell.IsClosing &&
            !runtimeShell.IsOverlayPaintSuspended &&
            (timerPhase == SplitTimerPhase.Running || overlayAnimations.SplitCompletionAnimation is not null);
        if (shouldRun && !statusPaintScheduler.IsRunning)
        {
            statusPaintScheduler.Start(runtimeShell.StatusPaintInterval);
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

        runtimeShell.ApplyWatcherNotification(notification);
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
        if (command is ApplySettingsCommand)
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
        if (settings.Overlay.ShowEarlyDeltaTime &&
            currentSplitIndex >= 0 &&
            currentSplitIndex < splitStatuses.Count &&
            SplitDisplayRows.TryGetRowIndex(
                settings,
                splitStatuses,
                currentSplitIndex,
                currentSplitIndex,
                GetCurrentVisibleStatusRowCount(),
                ShouldIgnoreVisibleGroupLimitForCompletedRun(),
                out int visualRowIndex) &&
            TryGetLayout(out SplitLayout layout))
        {
            Rectangle rowRect = overlayShell.WindowsInitialized
                ? overlayBoundsController.CurrentLayout.ToStatusLocal(layout.GetRowRect(visualRowIndex))
                : layout.GetRowRect(visualRowIndex);
            Invalidate(Rectangle.Inflate(rowRect, ScaleInt(6), ScaleInt(6)));
            return;
        }

        Invalidate();
    }

    private void UpdateWindowTitle()
    {
        windowShell.SyncTitle(this, SegmentTimerWindowTitle);
    }

    internal RuntimePerformanceDiagnostics GetRuntimeDiagnostics()
    {
        return performance.Snapshot();
    }

    internal RuntimeDebugSnapshot GetRuntimeDebugSnapshot()
    {
        return runtimeShell.CreateDebugSnapshot(performance.Snapshot(), timerPhase);
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

        bool wasClickThrough = overlayShell.MouseClickThrough;
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
        overlayShell.SetMouseClickThrough(enabled);
        overlayWindowController.ApplyWindowStyle(overlayShell.MouseClickThrough);
        timerOverlayHost.ApplyMouseClickThrough(overlayShell.MouseClickThrough);
        modalWindows.ApplyWindowState();
        PublishTimerOverlaySnapshot();
        UpdateWindowTitle();
    }

    private void ToggleMouseClickThrough()
    {
        SetMouseClickThrough(!overlayShell.MouseClickThrough);
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

    private T RunWithSuspendedRuntimeOverlayPaint<T>(Func<T> action)
    {
        RuntimeOverlayPaintSuspension suspension =
            runtimeShell.BeginOverlayPaintSuspension(controlScheduler.IsRunning);
        if (suspension.Started)
        {
            if (suspension.ShouldStopControlScheduler)
            {
                controlScheduler.Stop();
            }

            monitorCoordinator.ApplyUiDispatchSuspended(true);
        }

        UpdateStatusPaintSchedulerState();
        if (overlayShell.WindowsInitialized)
        {
            timerOverlayHost.ApplyPaintSuspended(true);
        }

        try
        {
            return action();
        }
        finally
        {
            RuntimeOverlayPaintResume resume =
                runtimeShell.EndOverlayPaintSuspension(!windowShell.IsClosing);
            if (overlayShell.WindowsInitialized && resume.Completed)
            {
                timerOverlayHost.ApplyPaintSuspended(false);
                timerOverlayHost.RequestRender();
            }

            if (resume.Completed)
            {
                monitorCoordinator.ApplyUiDispatchSuspended(false);
                if (resume.ShouldRestartControlScheduler)
                {
                    controlScheduler.Start(runtimeShell.ControlTickInterval);
                }
            }

            UpdateStatusPaintSchedulerState();
            QueueStatusOverlayRender();
        }
    }
}
