namespace TerrariaSplit.UI;

internal sealed class ApplicationShellEffectExecutor
{
    private readonly Action<RuntimeCommand> submitRuntimeCommand;
    private readonly SoundPlayerService soundPlayer;
    private readonly OverlayAnimationController overlayAnimations;
    private readonly Action toggleMouseClickThrough;
    private readonly Action clearSplitCompletionAnimation;
    private readonly Action<int> trackSegmentBestDeltaHighlight;
    private readonly Action<int> startSplitCompletionAnimation;
    private readonly Action resetUiScalePatchState;
    private readonly Action refreshTimerOverlaySettings;
    private readonly Action refreshRuntimeUi;
    private readonly Func<AppSettings, OperationResult> saveSettings;
    private readonly Action<OperationResult> showSettingsSaveFailure;
    private readonly Action<AppSettings, int> applySettingsToShell;
    private readonly AutomationShell automationShell;
    private readonly Action resetRaceProgressReports;
    private readonly Action<bool, bool> queueRaceProgressReports;
    private readonly RunFinalizationPersistence runFinalization;
    private readonly Action<SystemEvent> publishSystemEvent;

    public ApplicationShellEffectExecutor(
        Action<RuntimeCommand> submitRuntimeCommand,
        SoundPlayerService soundPlayer,
        OverlayAnimationController overlayAnimations,
        Action toggleMouseClickThrough,
        Action clearSplitCompletionAnimation,
        Action<int> trackSegmentBestDeltaHighlight,
        Action<int> startSplitCompletionAnimation,
        Action resetUiScalePatchState,
        Action refreshTimerOverlaySettings,
        Action refreshRuntimeUi,
        Func<AppSettings, OperationResult> saveSettings,
        Action<OperationResult> showSettingsSaveFailure,
        Action<AppSettings, int> applySettingsToShell,
        AutomationShell automationShell,
        Action resetRaceProgressReports,
        Action<bool, bool> queueRaceProgressReports,
        RunFinalizationPersistence runFinalization,
        Action<SystemEvent> publishSystemEvent)
    {
        this.submitRuntimeCommand = submitRuntimeCommand;
        this.soundPlayer = soundPlayer;
        this.overlayAnimations = overlayAnimations;
        this.toggleMouseClickThrough = toggleMouseClickThrough;
        this.clearSplitCompletionAnimation = clearSplitCompletionAnimation;
        this.trackSegmentBestDeltaHighlight = trackSegmentBestDeltaHighlight;
        this.startSplitCompletionAnimation = startSplitCompletionAnimation;
        this.resetUiScalePatchState = resetUiScalePatchState;
        this.refreshTimerOverlaySettings = refreshTimerOverlaySettings;
        this.refreshRuntimeUi = refreshRuntimeUi;
        this.saveSettings = saveSettings;
        this.showSettingsSaveFailure = showSettingsSaveFailure;
        this.applySettingsToShell = applySettingsToShell;
        this.automationShell = automationShell;
        this.resetRaceProgressReports = resetRaceProgressReports;
        this.queueRaceProgressReports = queueRaceProgressReports;
        this.runFinalization = runFinalization;
        this.publishSystemEvent = publishSystemEvent;
    }

    public void Apply(IReadOnlyList<ApplicationEffect> effects)
    {
        foreach (ApplicationEffect effect in effects)
        {
            Apply(effect);
        }
    }

    private void Apply(ApplicationEffect effect)
    {
        switch (effect)
        {
            case SubmitRuntimeCommandEffect submit:
                submitRuntimeCommand(submit.Command);
                break;
            case StopAllSoundsEffect:
                soundPlayer.StopAll();
                break;
            case PlaySoundEffect play:
                if (!string.IsNullOrWhiteSpace(play.Path))
                {
                    soundPlayer.Play(play.Path);
                }
                break;
            case ToggleMouseClickThroughEffect:
                toggleMouseClickThrough();
                break;
            case ClearOverlayAnimationEffect:
                overlayAnimations.Clear();
                break;
            case ClearSplitCompletionAnimationEffect:
                clearSplitCompletionAnimation();
                break;
            case TrackSegmentBestDeltaHighlightEffect track:
                trackSegmentBestDeltaHighlight(track.SplitIndex);
                break;
            case StartSplitCompletionAnimationEffect startSplit:
                startSplitCompletionAnimation(startSplit.SplitIndex);
                break;
            case SaveSettingsEffect save:
                OperationResult saveResult = saveSettings(save.Settings);
                if (saveResult.Failed)
                {
                    showSettingsSaveFailure(saveResult);
                }
                break;
            case ShowPersistenceFailureEffect failure:
                showSettingsSaveFailure(failure.Result);
                break;
            case StartCreateWorldAutomationEffect:
                automationShell.StartCreateWorld();
                break;
            case ShowPracticeWorldSelectorEffect:
                automationShell.ShowPracticeWorldSelector();
                break;
            case CancelCreateWorldAutomationEffect:
                automationShell.CancelCreateWorld();
                break;
            case CancelEnterWorldAutomationEffect:
                automationShell.CancelEnterWorld();
                break;
            case ResetUiScalePatchStateEffect:
                resetUiScalePatchState();
                break;
            case ApplySettingsToShellEffect applySettings:
                applySettingsToShell(applySettings.PreviousSettings, applySettings.SplitCount);
                break;
            case RefreshTimerOverlaySettingsEffect:
                refreshTimerOverlaySettings();
                break;
            case RefreshRuntimeUiEffect:
                refreshRuntimeUi();
                break;
            case ResetRaceProgressReportsEffect:
                resetRaceProgressReports();
                break;
            case QueueRaceProgressReportsEffect queueRaceProgress:
                queueRaceProgressReports(queueRaceProgress.RunStarted, queueRaceProgress.RunCompleted);
                break;
            case RecordRunStatisticsEffect recordRun:
                OperationResult recordResult = runFinalization.RecordStatistics(recordRun.Statuses);
                if (recordResult.Failed)
                {
                    showSettingsSaveFailure(recordResult);
                }
                break;
            case FinalizePersonalBestEffect finalizePersonalBest:
                PersonalBestFinalizationResult finalizationResult =
                    runFinalization.FinalizePersonalBest(finalizePersonalBest.Plan);
                publishSystemEvent(new PersonalBestFinalizationSystemEvent(finalizationResult));
                break;
            default:
                throw new NotSupportedException($"Unsupported application effect {effect.GetType().Name}.");
        }
    }
}
