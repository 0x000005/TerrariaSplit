namespace TerrariaSplit.UI;

internal sealed class ApplicationShellEffectExecutor
{
    private readonly IRuntimeCommandPort runtimeCommands;
    private readonly ISoundPort sounds;
    private readonly IOverlayPort overlay;
    private readonly ISettingsPort settings;
    private readonly IAutomationPort automation;
    private readonly IRaceProgressPort raceProgress;

    public ApplicationShellEffectExecutor(
        IRuntimeCommandPort runtimeCommands,
        ISoundPort sounds,
        IOverlayPort overlay,
        ISettingsPort settings,
        IAutomationPort automation,
        IRaceProgressPort raceProgress)
    {
        this.runtimeCommands = runtimeCommands;
        this.sounds = sounds;
        this.overlay = overlay;
        this.settings = settings;
        this.automation = automation;
        this.raceProgress = raceProgress;
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
                runtimeCommands.Submit(submit.Command);
                break;
            case StopAllSoundsEffect:
                sounds.StopAll();
                break;
            case PlaySoundEffect play:
                if (!string.IsNullOrWhiteSpace(play.Path))
                {
                    sounds.Play(play.Path);
                }
                break;
            case ToggleMouseClickThroughEffect:
                overlay.ToggleMouseClickThrough();
                break;
            case ClearOverlayAnimationEffect:
                overlay.ClearOverlayAnimation();
                break;
            case ClearSplitCompletionAnimationEffect:
                overlay.ClearSplitCompletionAnimation();
                break;
            case TrackSegmentBestDeltaHighlightEffect track:
                overlay.TrackSegmentBestDeltaHighlight(track.SplitIndex);
                break;
            case StartSplitCompletionAnimationEffect startSplit:
                overlay.StartSplitCompletionAnimation(startSplit.SplitIndex);
                break;
            case SaveSettingsEffect save:
                OperationResult saveResult = settings.Save(save.Settings);
                if (saveResult.Failed)
                {
                    settings.ShowSaveFailure(saveResult);
                }
                break;
            case ShowPersistenceFailureEffect failure:
                settings.ShowSaveFailure(failure.Result);
                break;
            case StartCreateWorldAutomationEffect:
                automation.StartCreateWorld();
                break;
            case ShowPracticeWorldSelectorEffect:
                automation.ShowPracticeWorldSelector();
                break;
            case CancelCreateWorldAutomationEffect:
                automation.CancelCreateWorld();
                break;
            case CancelEnterWorldAutomationEffect:
                automation.CancelEnterWorld();
                break;
            case ResetUiScalePatchStateEffect:
                overlay.ResetUiScalePatchState();
                break;
            case ApplySettingsToShellEffect applySettings:
                settings.ApplyToShell(applySettings.PreviousSettings, applySettings.SplitCount);
                break;
            case RefreshTimerOverlaySettingsEffect:
                overlay.RefreshTimerOverlaySettings();
                break;
            case RefreshRuntimeUiEffect:
                overlay.RefreshRuntimeUi();
                break;
            case ResetRaceProgressReportsEffect:
                raceProgress.ResetReportedProgress();
                break;
            case QueueRaceProgressReportsEffect queueRaceProgress:
                raceProgress.QueueProgressReports(queueRaceProgress.RunStarted, queueRaceProgress.RunCompleted);
                break;
            default:
                throw new NotSupportedException($"Unsupported application effect {effect.GetType().Name}.");
        }
    }
}
