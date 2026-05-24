namespace TerrariaSplit;

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
        switch (effect.Kind)
        {
            case ApplicationEffectKind.SubmitRuntimeCommand:
                if (effect.RuntimeCommand is not null)
                {
                    submitRuntimeCommand(effect.RuntimeCommand);
                }
                break;
            case ApplicationEffectKind.StopAllSounds:
                stopAllSounds();
                break;
            case ApplicationEffectKind.PlaySound:
                if (!string.IsNullOrWhiteSpace(effect.SoundPath))
                {
                    playSound(effect.SoundPath);
                }
                break;
            case ApplicationEffectKind.ToggleMouseClickThrough:
                toggleMouseClickThrough();
                break;
            case ApplicationEffectKind.ClearOverlayAnimation:
                clearOverlayAnimation();
                break;
            case ApplicationEffectKind.ClearSplitCompletionAnimation:
                clearSplitCompletionAnimation();
                break;
            case ApplicationEffectKind.TrackSegmentBestDeltaHighlight:
                trackSegmentBestDeltaHighlight(effect.SplitIndex);
                break;
            case ApplicationEffectKind.StartSplitCompletionAnimation:
                startSplitCompletionAnimation(effect.SplitIndex);
                break;
            case ApplicationEffectKind.SaveSettings:
                if (effect.SettingsToSave is AppSettings settingsToSave)
                {
                    saveSettings(settingsToSave);
                }
                break;
            case ApplicationEffectKind.StartCreateWorldAutomation:
                startCreateWorldAutomation();
                break;
            case ApplicationEffectKind.ShowPracticeWorldSelector:
                showPracticeWorldSelector();
                break;
            case ApplicationEffectKind.CancelCreateWorldAutomation:
                cancelCreateWorldAutomation();
                break;
            case ApplicationEffectKind.CancelEnterWorldAutomation:
                cancelEnterWorldAutomation();
                break;
            case ApplicationEffectKind.ResetUiScalePatchState:
                resetUiScalePatchState();
                break;
            case ApplicationEffectKind.ApplySettingsToShell:
                if (effect.PreviousSettings is AppSettings previousSettings)
                {
                    applySettingsToShell(previousSettings, effect.SplitCount);
                }
                break;
            case ApplicationEffectKind.RefreshTimerOverlaySettings:
                refreshTimerOverlaySettings();
                break;
            case ApplicationEffectKind.RefreshRuntimeUi:
                refreshRuntimeUi();
                break;
        }
    }
}
