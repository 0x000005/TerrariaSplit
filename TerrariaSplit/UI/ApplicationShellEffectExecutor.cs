namespace TerrariaSplit.UI;

internal sealed class ApplicationShellEffectExecutor
{
    private readonly Action<RuntimeCommand> submitRuntimeCommand;
    private readonly Action stopAllSounds;
    private readonly Action<string> playSound;
    private readonly Action toggleMouseClickThrough;
    private readonly Action clearOverlayAnimation;
    private readonly Action clearSplitCompletionAnimation;
    private readonly Action<int> trackSegmentBestDeltaHighlight;
    private readonly Action<int> startSplitCompletionAnimation;
    private readonly Action<AppSettings> saveSettings;
    private readonly Action startCreateWorldAutomation;
    private readonly Action showPracticeWorldSelector;
    private readonly Action cancelCreateWorldAutomation;
    private readonly Action cancelEnterWorldAutomation;
    private readonly Action resetUiScalePatchState;
    private readonly Action<AppSettings, int> applySettingsToShell;
    private readonly Action refreshTimerOverlaySettings;
    private readonly Action refreshRuntimeUi;

    public ApplicationShellEffectExecutor(
        Action<RuntimeCommand> submitRuntimeCommand,
        Action stopAllSounds,
        Action<string> playSound,
        Action toggleMouseClickThrough,
        Action clearOverlayAnimation,
        Action clearSplitCompletionAnimation,
        Action<int> trackSegmentBestDeltaHighlight,
        Action<int> startSplitCompletionAnimation,
        Action<AppSettings> saveSettings,
        Action startCreateWorldAutomation,
        Action showPracticeWorldSelector,
        Action cancelCreateWorldAutomation,
        Action cancelEnterWorldAutomation,
        Action resetUiScalePatchState,
        Action<AppSettings, int> applySettingsToShell,
        Action refreshTimerOverlaySettings,
        Action refreshRuntimeUi)
    {
        this.submitRuntimeCommand = submitRuntimeCommand;
        this.stopAllSounds = stopAllSounds;
        this.playSound = playSound;
        this.toggleMouseClickThrough = toggleMouseClickThrough;
        this.clearOverlayAnimation = clearOverlayAnimation;
        this.clearSplitCompletionAnimation = clearSplitCompletionAnimation;
        this.trackSegmentBestDeltaHighlight = trackSegmentBestDeltaHighlight;
        this.startSplitCompletionAnimation = startSplitCompletionAnimation;
        this.saveSettings = saveSettings;
        this.startCreateWorldAutomation = startCreateWorldAutomation;
        this.showPracticeWorldSelector = showPracticeWorldSelector;
        this.cancelCreateWorldAutomation = cancelCreateWorldAutomation;
        this.cancelEnterWorldAutomation = cancelEnterWorldAutomation;
        this.resetUiScalePatchState = resetUiScalePatchState;
        this.applySettingsToShell = applySettingsToShell;
        this.refreshTimerOverlaySettings = refreshTimerOverlaySettings;
        this.refreshRuntimeUi = refreshRuntimeUi;
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
                stopAllSounds();
                break;
            case PlaySoundEffect play:
                if (!string.IsNullOrWhiteSpace(play.Path))
                {
                    playSound(play.Path);
                }
                break;
            case ToggleMouseClickThroughEffect:
                toggleMouseClickThrough();
                break;
            case ClearOverlayAnimationEffect:
                clearOverlayAnimation();
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
                saveSettings(save.Settings);
                break;
            case StartCreateWorldAutomationEffect:
                startCreateWorldAutomation();
                break;
            case ShowPracticeWorldSelectorEffect:
                showPracticeWorldSelector();
                break;
            case CancelCreateWorldAutomationEffect:
                cancelCreateWorldAutomation();
                break;
            case CancelEnterWorldAutomationEffect:
                cancelEnterWorldAutomation();
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
        }
    }
}
