namespace TerrariaSplit.UI;

internal sealed class ApplicationShellEffectExecutor
{
    private readonly IRuntimeCommandPort runtimeCommands;
    private readonly ISoundPort sounds;
    private readonly IOverlayPort overlay;
    private readonly ISettingsPort settings;
    private readonly IAutomationPort automation;

    public ApplicationShellEffectExecutor(
        IRuntimeCommandPort runtimeCommands,
        ISoundPort sounds,
        IOverlayPort overlay,
        ISettingsPort settings,
        IAutomationPort automation)
    {
        this.runtimeCommands = runtimeCommands;
        this.sounds = sounds;
        this.overlay = overlay;
        this.settings = settings;
        this.automation = automation;
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
                settings.Save(save.Settings);
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
        }
    }
}
