using System.Diagnostics;
using System.Threading;
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
