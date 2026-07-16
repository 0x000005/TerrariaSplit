using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed partial class MainForm : Form
{
    private void EnsureRuntimeInitializationStarted()
    {
        runtimeInitializationTask ??= InitializeRuntimeAfterFirstFrameAsync();
    }

    private async Task InitializeRuntimeAfterFirstFrameAsync()
    {
        RuntimeServicePreparation? preparation = null;
        ContextMenuStrip? nextContextMenu = null;
        try
        {
            CancellationToken cancellationToken = runtimeBootstrapper.CancellationToken;
            await Task.WhenAll(
                    statusFirstFramePresented.Task,
                    overlayShell.TimerOverlayHost.FirstFramePresented)
                .WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!runtimeBootstrapper.TryMarkFirstFramePresented())
            {
                return;
            }

            StartupDiagnostics.RecordTrace("FirstFrameContinuation");

            preparation = await runtimeBootstrapper.InitializeAsync(
                token => MainShellCompositionRoot.CreateRuntimeServicesAsync(startupCore, token));
            StartupDiagnostics.RecordTrace("RuntimePreparationReady");
            cancellationToken.ThrowIfCancellationRequested();

            TerrariaMonitorCoordinator monitorCoordinator = MainShellCompositionRoot.CreateMonitorCoordinator(
                callback => BeginInvoke(callback),
                appLogger);
            monitorCoordinator.WatcherPollCompleted += HandleWatcherPollCompleted;
            AutomationShell nextAutomationShell = MainShellCompositionRoot.CreateAutomationShell(
                preparation.WorldPoolStore,
                () => settings,
                settingsSnapshots,
                modalWindows,
                this,
                () => AcceptRuntimeCommandSequence(monitorCoordinator.ClearPendingMenuActions()),
                appLogger);
            SettingsShell nextSettingsShell = MainShellCompositionRoot.CreateSettingsShell(
                () => editableSettings,
                () => IsRaceRoomActive,
                startupCore.SettingsRepository,
                settingsSnapshots,
                callback => BeginInvoke(callback),
                ApplySettings,
                () => AcceptRuntimeCommandSequence(monitorCoordinator.ClearPendingMenuActions()),
                hotkeyShell.Unregister,
                hotkeyShell.Register,
                () => IsHandleCreated,
                () => Bounds,
                RestartForApplicationUpdate);

            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            nextContextMenu = new ContextMenuStrip();
            nextContextMenu.Opening += (_, e) =>
            {
                UpdateContextMenu();
                if (mainWindowModalInputRouter.TryRedirectFromMainInput())
                {
                    e.Cancel = true;
                    return;
                }

                if (CanEditPracticeTimes && IsEditablePracticePoint(PointToClient(Cursor.Position)))
                {
                    e.Cancel = true;
                }
            };

            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            RaceShell nextRaceShell = new(
                settingsSnapshots,
                appLogger,
                () => settings,
                () => editableSettings,
                () => viewState,
                () => runtimeShell.CurrentWatcherDiagnostics.ProcessVersion,
                ApplyRouteOverride,
                ClearRouteOverride,
                startupCore.SaveSettings,
                PublishExternalSystemEvent,
                this,
                RefreshRaceMainTimerColor,
                () => ResetRun(recordStats: false, allowDuringRace: true));
            ApplicationShellEffectExecutor nextEffectExecutor = MainShellCompositionRoot.CreateEffectExecutor(
                SubmitRuntimeCommand,
                preparation.SoundPlayer,
                overlayShell.Animations,
                ToggleMouseClickThrough,
                ClearSplitCompletionAnimation,
                TrackSegmentBestDeltaHighlight,
                StartSplitCompletionAnimation,
                monitorCoordinator.ResetUiScalePatchState,
                RefreshTimerOverlaySettingsSnapshot,
                RefreshRuntimeUi,
                ShowSettingsSaveFailure,
                ApplyLoadedSettings,
                startupCore.SaveSettings,
                nextAutomationShell,
                nextRaceShell.ResetReportedProgress,
                nextRaceShell.QueueProgressReports);
            HighPrecisionScheduler controlScheduler = MainShellCompositionRoot.CreateControlScheduler(QueueControlTick);
            HighPrecisionScheduler statusPaintScheduler = MainShellCompositionRoot.CreateStatusPaintScheduler(QueueStatusPaintTick);

            runtimeServices = new RuntimeServices(
                preparation,
                monitorCoordinator,
                nextAutomationShell,
                nextSettingsShell,
                nextRaceShell,
                nextEffectExecutor,
                controlScheduler,
                statusPaintScheduler,
                nextContextMenu);
            preparation = null;
            contextMenu = nextContextMenu;
            nextContextMenu = null;
            ContextMenuStrip = contextMenu;
            runtimeShell.AttachRuntimeComponents(
                monitorCoordinator,
                controlScheduler,
                statusPaintScheduler);

            AcceptRuntimeCommandSequence(monitorCoordinator.SetRuntimeDefinitions(applicationController.Definitions));
            runtimeShell.UpdateControlTickInterval(ResolveControlTickInterval());
            runtimeShell.UpdateStatusPaintInterval(ResolveRunningStatusPaintInterval());
            monitorCoordinator.UpdateReadyWatcherPollInterval(ResolveReadyWatcherPollInterval());
            controlScheduler.Start(runtimeShell.ControlTickInterval);
            worldPoolFillService.UpdateSettings(settings);
            UpdateContextMenu();
            UpdateRtssOverlaySchedulerState();

            runtimeBootstrapper.MarkFullyReady();
            StartupDiagnostics.RecordTrace("FullyReady");
            MarkStatusOverlayStaticContentDirty();
            QueueStatusOverlayRender();
            StartupDiagnostics.FlushTrace();
            StartupDiagnostics.SignalFullyReady();
            startupCommandGate.Open(ExecuteAppCommandNow);
        }
        catch (OperationCanceledException) when (
            runtimeBootstrapper.Phase == StartupPhase.Stopping ||
            windowShell.IsClosing ||
            IsDisposed ||
            Disposing)
        {
            nextContextMenu?.Dispose();
            preparation?.Dispose();
        }
        catch (Exception ex)
        {
            nextContextMenu?.Dispose();
            preparation?.Dispose();
            runtimeBootstrapper.MarkFailed();
            startupCommandGate.Cancel();
            ShowStartupFailure(ex);
        }
    }

    private void ShowStartupFailure(Exception exception)
    {
        appLogger.Error(exception, "TerrariaSplit runtime initialization failed.");
        if (windowShell.IsClosing || IsDisposed || Disposing)
        {
            return;
        }

        string message = Localizer.Get(
            "TerrariaSplit could not finish initialization and must close.",
            settings);
        string title = Localizer.Get("Startup failed", settings);
        MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        Close();
    }
}
