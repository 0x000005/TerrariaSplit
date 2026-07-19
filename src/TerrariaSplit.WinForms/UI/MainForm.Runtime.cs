using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed partial class MainForm : Form
{
    private long queuedControlTickTimestamp;

    private void ControlTick()
    {
        try
        {
            runtimeShell.MonitorCoordinator.Tick(
                timerPhase,
                settings.Advanced?.EnableTerrariaUiScalePatch == true);
            ProcessUiTick();
        }
        finally
        {
            UpdateStatusPaintSchedulerState();
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

        System.Threading.Volatile.Write(ref queuedControlTickTimestamp, System.Diagnostics.Stopwatch.GetTimestamp());
        try
        {
            BeginInvoke(runtimeShell.DispatchedControlTick);
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
            long queuedTimestamp = System.Threading.Volatile.Read(ref queuedControlTickTimestamp);
            TimeSpan dispatchDelay = queuedTimestamp == 0
                ? TimeSpan.Zero
                : System.Diagnostics.Stopwatch.GetElapsedTime(queuedTimestamp);
            if (dispatchDelay >= TimeSpan.FromMilliseconds(250))
            {
                appLogger.Info($"UI control tick dispatch was delayed by {dispatchDelay.TotalMilliseconds:F0} ms.");
            }

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

    private void QueueStatusPaintTick(HighPrecisionSchedulerTick _)
    {
        if (!CanDispatchToUiThread())
        {
            return;
        }

        if (!runtimeShell.TryMarkStatusPaintDispatchPending())
        {
            return;
        }

        try
        {
            BeginInvoke(runtimeShell.DispatchedStatusPaintTick);
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

    private void QueueRtssOverlayTick(HighPrecisionSchedulerTick tick)
    {
        if (!CanDispatchToUiThread())
        {
            return;
        }

        if (System.Threading.Interlocked.Exchange(ref rtssOverlayDispatchPending, 1) != 0)
        {
            return;
        }

        try
        {
            BeginInvoke(new Action(DispatchedRtssOverlayTick));
        }
        catch (ObjectDisposedException)
        {
            System.Threading.Interlocked.Exchange(ref rtssOverlayDispatchPending, 0);
        }
        catch (InvalidOperationException)
        {
            System.Threading.Interlocked.Exchange(ref rtssOverlayDispatchPending, 0);
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

    private void DispatchedRtssOverlayTick()
    {
        try
        {
            if (CanDispatchToUiThread())
            {
                PublishRtssOverlay();
            }
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref rtssOverlayDispatchPending, 0);
        }
    }

    private bool CanDispatchToUiThread()
    {
        return !windowShell.IsClosing && IsHandleCreated && !IsDisposed && !Disposing;
    }

    private void ProcessUiTick()
    {
        runtimeShell.MonitorCoordinator.UpdateRunPhase(timerPhase);
        UpdateWindowTitle();
        UpdateRtssOverlaySchedulerState();
        if (!rtssOverlayScheduler.IsRunning)
        {
            PublishRtssOverlay();
        }
    }

    private void RenderStatusOverlayTick()
    {
        if (overlayShell.Animations.SplitCompletionAnimation is not null ||
            overlayShell.AnimatedStatusIconsActive)
        {
            overlayShell.WindowController.RenderImmediately();
            return;
        }

        if (timerPhase == SplitTimerPhase.Running || StatusOverlayHighlightsActive)
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
            overlayShell.WindowController.RenderImmediately();
            return;
        }

        if (overlayShell.AnimatedStatusIconsActive)
        {
            overlayShell.WindowController.RenderImmediately();
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
            overlayShell.WindowController.RenderImmediately();
        }
    }

    private void UpdateStatusPaintSchedulerState()
    {
        if (!runtimeShell.IsRuntimeAttached)
        {
            return;
        }

        bool shouldRun = !windowShell.IsClosing &&
            !runtimeShell.IsOverlayPaintSuspended &&
            (timerPhase == SplitTimerPhase.Running ||
                overlayShell.Animations.SplitCompletionAnimation is not null ||
                StatusOverlayHighlightsActive ||
                overlayShell.AnimatedStatusIconsActive);
        if (shouldRun && !runtimeShell.StatusPaintScheduler.IsRunning)
        {
            runtimeShell.StatusPaintScheduler.Start(runtimeShell.StatusPaintInterval);
        }
        else if (!shouldRun && runtimeShell.StatusPaintScheduler.IsRunning)
        {
            runtimeShell.StatusPaintScheduler.Stop();
        }
    }

    private void UpdateRtssOverlaySchedulerState()
    {
        if (!runtimeShell.IsRuntimeAttached)
        {
            rtssOverlayScheduler.Stop();
            return;
        }

        bool shouldRun = !windowShell.IsClosing &&
            settings.Advanced?.EnableRtssOverlay == true &&
            timerPhase == SplitTimerPhase.Running;
        if (shouldRun)
        {
            rtssOverlayScheduler.Start(ResolveTimerOverlayRefreshInterval());
            return;
        }

        rtssOverlayScheduler.Stop();
    }

    private void HandleWatcherPollCompleted(WatcherPollNotification notification)
    {
        runtimeShell.ApplyWatcherNotification(notification);
        UpdateConfiguredRefreshIntervals();
        ApplicationUpdate update = applicationController.HandleSystemEvent(new RuntimeWatcherSystemEvent(notification));
        ApplyApplicationUpdate(update);

        UpdateOverlayLayoutContextIfChanged();
        ProcessUiTick();
        UpdateStatusPaintSchedulerState();
        PublishTimerOverlaySnapshot();
        if (!notification.Snapshot.Equals(notification.PreviousSnapshot))
        {
            Invalidate();
        }
    }

    private void ExecuteAppCommand(AppCommand command)
    {
        startupCommandGate.Submit(command, ExecuteAppCommandNow);
    }

    private void ExecuteAppCommandNow(AppCommand command)
    {
        if (runtimeServices is not null)
        {
            bool raceModeEnabled = runtimeServices.RaceShell.IsRaceEnabled;
            if (applicationController.SystemState.Race.IsModeEnabled != raceModeEnabled)
            {
                ApplyApplicationUpdate(applicationController.HandleSystemEvent(
                    new RaceModeSystemEvent(raceModeEnabled)));
            }

            bool isInRaceRoom = runtimeServices.RaceShell.IsInRoom;
            if (applicationController.SystemState.Race.IsInRoom != isInRaceRoom)
            {
                ApplyApplicationUpdate(applicationController.HandleSystemEvent(
                    new RaceRosterSystemEvent(
                        isInRaceRoom
                            ? runtimeServices.RaceShell.State?.RoomCode ?? string.Empty
                            : string.Empty,
                        isInRaceRoom)));
            }
        }

        ApplicationUpdate update = applicationController.HandleSystemEvent(new ControlCommandSystemEvent(command));
        if (command is ApplySettingsCommand or ApplyTemporarySettingsCommand or ApplyRouteOverrideCommand or ClearRouteOverrideCommand)
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
        ApplyDisplayInvalidations(update.DisplayInvalidations);
    }

    private void PublishExternalSystemEvent(SystemEvent systemEvent)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => PublishExternalSystemEvent(systemEvent)));
            return;
        }

        ApplyApplicationUpdate(applicationController.HandleSystemEvent(systemEvent));
        bool restoreHotkeys = systemEvent is RaceModeSystemEvent { Enabled: false } or
            RaceRosterSystemEvent { IsInRoom: false };
        if (restoreHotkeys &&
            CanDispatchToUiThread() &&
            runtimeServices?.SettingsShell.IsOpen != true)
        {
            hotkeyShell.Register();
        }
    }

    private void ApplyDisplayInvalidations(IReadOnlyList<DisplayInvalidation> invalidations)
    {
        if (invalidations.Count == 0)
        {
            return;
        }

        bool refreshRuntime = false;
        bool refreshStatic = false;
        bool invalidateStatus = false;
        DisplayRefreshLevel? raceRefreshLevel = null;

        foreach (DisplayInvalidation invalidation in invalidations)
        {
            if ((invalidation.Targets & DisplayInvalidationTarget.RaceLeaderboard) != 0)
            {
                if (raceRefreshLevel is null || invalidation.Level > raceRefreshLevel.Value)
                {
                    raceRefreshLevel = invalidation.Level;
                }
            }

            if ((invalidation.Targets & (DisplayInvalidationTarget.SplitOverlay | DisplayInvalidationTarget.TimerOverlay)) == 0)
            {
                continue;
            }

            switch (invalidation.Level)
            {
                case DisplayRefreshLevel.Frame:
                    QueueStatusOverlayRender();
                    break;
                case DisplayRefreshLevel.RuntimeFacts:
                    refreshRuntime = true;
                    invalidateStatus = true;
                    break;
                case DisplayRefreshLevel.SplitProgress:
                case DisplayRefreshLevel.DisplaySettings:
                case DisplayRefreshLevel.RoutePackage:
                case DisplayRefreshLevel.RunReset:
                case DisplayRefreshLevel.FullRebuild:
                    refreshStatic = true;
                    refreshRuntime = true;
                    invalidateStatus = true;
                    break;
            }
        }

        if (refreshStatic)
        {
            MarkStatusOverlayStaticContentDirty();
        }

        if (refreshRuntime)
        {
            RefreshRuntimeUi();
        }

        if (raceRefreshLevel is DisplayRefreshLevel level)
        {
            raceShell.RefreshDisplay(level);
        }

        if (invalidateStatus)
        {
            Invalidate();
        }
    }

    private void SubmitRuntimeCommand(RuntimeCommand command)
    {
        AcceptRuntimeCommandSequence(runtimeShell.MonitorCoordinator.SubmitRuntimeCommand(command));
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
                ? overlayShell.BoundsController.CurrentLayout.ToStatusLocal(layout.GetRowRect(visualRowIndex))
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

    private void OpenStatistics()
    {
        if (statisticsForm is { IsDisposed: false })
        {
            statisticsForm.Show();
            if (statisticsForm.IsHandleCreated)
            {
                WindowTopMostSync.Apply(false, statisticsForm.Handle);
            }

            statisticsForm.Activate();
            return;
        }

        statisticsForm = new StatisticsForm(settings);
        statisticsForm.FormClosed += (_, _) => statisticsForm = null;
        statisticsForm.Show();
        if (statisticsForm.IsHandleCreated)
        {
            WindowTopMostSync.Apply(false, statisticsForm.Handle);
        }
    }

    private void FinalizeRunBeforeExit()
    {
        ExecuteAppCommand(AppCommand.ResetRun(
            recordStats: true,
            playResetSound: false,
            allowDuringRace: true));
    }

    private void ResetRun(bool recordStats = false, bool allowDuringRace = false)
    {
        ExecuteAppCommand(AppCommand.ResetRun(recordStats, playResetSound: false, allowDuringRace));
    }

    private void SetMouseClickThrough(bool enabled)
    {
        overlayShell.SetMouseClickThrough(enabled);
        overlayShell.WindowController.ApplyWindowStyle(overlayShell.MouseClickThrough);
        overlayShell.TimerOverlayHost.ApplyMouseClickThrough(overlayShell.MouseClickThrough);
        raceShell.ApplyMouseClickThrough(overlayShell.MouseClickThrough);
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
        overlayShell.Animations.ClearSplitCompletionAnimation();
        UpdateStatusPaintSchedulerState();
        QueueStatusOverlayRender();
    }

    private void ToggleCheats()
    {
        ExecuteAppCommand(AppCommand.ToggleCheats());
    }

    private void RefreshRuntimeUi()
    {
        UpdateOverlayLayoutContextIfChanged();
        UpdateStatusPaintSchedulerState();
        UpdateRtssOverlaySchedulerState();
        PublishTimerOverlaySnapshot();
        if (!rtssOverlayScheduler.IsRunning)
        {
            PublishRtssOverlay();
        }
    }

    private T RunWithSuspendedRuntimeOverlayPaint<T>(Func<T> action)
    {
        if (!runtimeShell.IsRuntimeAttached)
        {
            return action();
        }

        RuntimeOverlayPaintSuspension suspension =
            runtimeShell.BeginOverlayPaintSuspension(runtimeShell.ControlScheduler.IsRunning);
        if (suspension.Started)
        {
            if (suspension.ShouldStopControlScheduler)
            {
                runtimeShell.ControlScheduler.Stop();
            }

            runtimeShell.MonitorCoordinator.ApplyUiDispatchSuspended(true);
        }

        UpdateStatusPaintSchedulerState();
        if (overlayShell.WindowsInitialized)
        {
            overlayShell.TimerOverlayHost.ApplyPaintSuspended(true);
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
                overlayShell.TimerOverlayHost.ApplyPaintSuspended(false);
                overlayShell.TimerOverlayHost.RequestRender();
            }

            if (resume.Completed)
            {
                runtimeShell.MonitorCoordinator.ApplyUiDispatchSuspended(false);
                if (resume.ShouldRestartControlScheduler)
                {
                    runtimeShell.ControlScheduler.Start(runtimeShell.ControlTickInterval);
                }
            }

            UpdateStatusPaintSchedulerState();
            UpdateRtssOverlaySchedulerState();
            QueueStatusOverlayRender();
        }
    }
}
