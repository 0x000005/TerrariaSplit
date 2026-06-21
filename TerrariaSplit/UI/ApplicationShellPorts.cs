namespace TerrariaSplit.UI;

internal interface IRuntimeCommandPort
{
    void Submit(RuntimeCommand command);
}

internal interface ISoundPort
{
    void StopAll();

    void Play(string path);
}

internal interface IOverlayPort
{
    void ToggleMouseClickThrough();

    void ClearOverlayAnimation();

    void ClearSplitCompletionAnimation();

    void TrackSegmentBestDeltaHighlight(int splitIndex);

    void StartSplitCompletionAnimation(int splitIndex);

    void ResetUiScalePatchState();

    void RefreshTimerOverlaySettings();

    void RefreshRuntimeUi();
}

internal interface ISettingsPort
{
    void Save(AppSettings settings);

    void ApplyToShell(AppSettings previousSettings, int splitCount);
}

internal interface IAutomationPort
{
    void StartCreateWorld();

    void ShowPracticeWorldSelector();

    void CancelCreateWorld();

    void CancelEnterWorld();
}

internal sealed class DelegateRuntimeCommandPort : IRuntimeCommandPort
{
    private readonly Action<RuntimeCommand> submit;

    public DelegateRuntimeCommandPort(Action<RuntimeCommand> submit)
    {
        this.submit = submit;
    }

    public void Submit(RuntimeCommand command)
    {
        submit(command);
    }
}

internal sealed class DelegateSoundPort : ISoundPort
{
    private readonly Action stopAll;
    private readonly Action<string> play;

    public DelegateSoundPort(Action stopAll, Action<string> play)
    {
        this.stopAll = stopAll;
        this.play = play;
    }

    public void StopAll()
    {
        stopAll();
    }

    public void Play(string path)
    {
        play(path);
    }
}

internal sealed class DelegateOverlayPort : IOverlayPort
{
    private readonly Action toggleMouseClickThrough;
    private readonly Action clearOverlayAnimation;
    private readonly Action clearSplitCompletionAnimation;
    private readonly Action<int> trackSegmentBestDeltaHighlight;
    private readonly Action<int> startSplitCompletionAnimation;
    private readonly Action resetUiScalePatchState;
    private readonly Action refreshTimerOverlaySettings;
    private readonly Action refreshRuntimeUi;

    public DelegateOverlayPort(
        Action toggleMouseClickThrough,
        Action clearOverlayAnimation,
        Action clearSplitCompletionAnimation,
        Action<int> trackSegmentBestDeltaHighlight,
        Action<int> startSplitCompletionAnimation,
        Action resetUiScalePatchState,
        Action refreshTimerOverlaySettings,
        Action refreshRuntimeUi)
    {
        this.toggleMouseClickThrough = toggleMouseClickThrough;
        this.clearOverlayAnimation = clearOverlayAnimation;
        this.clearSplitCompletionAnimation = clearSplitCompletionAnimation;
        this.trackSegmentBestDeltaHighlight = trackSegmentBestDeltaHighlight;
        this.startSplitCompletionAnimation = startSplitCompletionAnimation;
        this.resetUiScalePatchState = resetUiScalePatchState;
        this.refreshTimerOverlaySettings = refreshTimerOverlaySettings;
        this.refreshRuntimeUi = refreshRuntimeUi;
    }

    public void ToggleMouseClickThrough()
    {
        toggleMouseClickThrough();
    }

    public void ClearOverlayAnimation()
    {
        clearOverlayAnimation();
    }

    public void ClearSplitCompletionAnimation()
    {
        clearSplitCompletionAnimation();
    }

    public void TrackSegmentBestDeltaHighlight(int splitIndex)
    {
        trackSegmentBestDeltaHighlight(splitIndex);
    }

    public void StartSplitCompletionAnimation(int splitIndex)
    {
        startSplitCompletionAnimation(splitIndex);
    }

    public void ResetUiScalePatchState()
    {
        resetUiScalePatchState();
    }

    public void RefreshTimerOverlaySettings()
    {
        refreshTimerOverlaySettings();
    }

    public void RefreshRuntimeUi()
    {
        refreshRuntimeUi();
    }
}

internal sealed class DelegateSettingsPort : ISettingsPort
{
    private readonly Action<AppSettings> save;
    private readonly Action<AppSettings, int> applyToShell;

    public DelegateSettingsPort(Action<AppSettings> save, Action<AppSettings, int> applyToShell)
    {
        this.save = save;
        this.applyToShell = applyToShell;
    }

    public void Save(AppSettings settings)
    {
        save(settings);
    }

    public void ApplyToShell(AppSettings previousSettings, int splitCount)
    {
        applyToShell(previousSettings, splitCount);
    }
}

internal sealed class DelegateAutomationPort : IAutomationPort
{
    private readonly Action startCreateWorld;
    private readonly Action showPracticeWorldSelector;
    private readonly Action cancelCreateWorld;
    private readonly Action cancelEnterWorld;

    public DelegateAutomationPort(
        Action startCreateWorld,
        Action showPracticeWorldSelector,
        Action cancelCreateWorld,
        Action cancelEnterWorld)
    {
        this.startCreateWorld = startCreateWorld;
        this.showPracticeWorldSelector = showPracticeWorldSelector;
        this.cancelCreateWorld = cancelCreateWorld;
        this.cancelEnterWorld = cancelEnterWorld;
    }

    public void StartCreateWorld()
    {
        startCreateWorld();
    }

    public void ShowPracticeWorldSelector()
    {
        showPracticeWorldSelector();
    }

    public void CancelCreateWorld()
    {
        cancelCreateWorld();
    }

    public void CancelEnterWorld()
    {
        cancelEnterWorld();
    }
}
