namespace TerrariaSplit.Application;

internal abstract record ApplicationEffect;

internal sealed record SubmitRuntimeCommandEffect(RuntimeCommand Command)
    : ApplicationEffect;

internal sealed record StopAllSoundsEffect()
    : ApplicationEffect;

internal sealed record PlaySoundEffect(string Path)
    : ApplicationEffect;

internal sealed record ToggleMouseClickThroughEffect()
    : ApplicationEffect;

internal sealed record ClearOverlayAnimationEffect()
    : ApplicationEffect;

internal sealed record ClearSplitCompletionAnimationEffect()
    : ApplicationEffect;

internal sealed record TrackSegmentBestDeltaHighlightEffect(int SplitIndex)
    : ApplicationEffect;

internal sealed record StartSplitCompletionAnimationEffect(int SplitIndex)
    : ApplicationEffect;

internal sealed record SaveSettingsEffect(AppSettings Settings)
    : ApplicationEffect;

internal sealed record StartCreateWorldAutomationEffect()
    : ApplicationEffect;

internal sealed record ShowPracticeWorldSelectorEffect()
    : ApplicationEffect;

internal sealed record CancelCreateWorldAutomationEffect()
    : ApplicationEffect;

internal sealed record CancelEnterWorldAutomationEffect()
    : ApplicationEffect;

internal sealed record ResetUiScalePatchStateEffect()
    : ApplicationEffect;

internal sealed record ApplySettingsToShellEffect(AppSettings PreviousSettings, int SplitCount)
    : ApplicationEffect;

internal sealed record RefreshTimerOverlaySettingsEffect()
    : ApplicationEffect;

internal sealed record RefreshRuntimeUiEffect()
    : ApplicationEffect;

internal sealed record ApplicationUpdate(
    IReadOnlyList<ApplicationEffect> Effects,
    bool InvalidateAll = false);

internal sealed class ApplicationController
{
    private readonly RunLifecycleController runLifecycle = new();
    private readonly Func<string, bool> confirmPersonalBestUpdate;
    private readonly ISettingsSnapshotFactory settingsSnapshots;
    private long minimumAcceptedRuntimeCommandSequence;

    public ApplicationController(
        AppSettings settings,
        Func<string, bool> confirmPersonalBestUpdate,
        ISettingsSnapshotFactory settingsSnapshots)
    {
        this.confirmPersonalBestUpdate = confirmPersonalBestUpdate;
        this.settingsSnapshots = settingsSnapshots;
        Settings = settings;
        Definitions = SplitCatalog.Build(settings);
        ViewState = ApplicationViewState.FromDefinitions(settings, Definitions);
    }

    public AppSettings Settings { get; private set; }

    public IReadOnlyList<SplitDefinition> Definitions { get; private set; }

    public ApplicationViewState ViewState { get; private set; }

    public long MinimumAcceptedRuntimeCommandSequence => minimumAcceptedRuntimeCommandSequence;

    public void AcceptRuntimeCommandSequence(long sequence)
    {
        minimumAcceptedRuntimeCommandSequence = Math.Max(minimumAcceptedRuntimeCommandSequence, sequence);
    }

    public ApplicationUpdate HandleCommand(AppCommand command)
    {
        var effects = new List<ApplicationEffect>();
        bool invalidateAll = false;

        switch (command.Kind)
        {
            case AppCommandKind.TogglePause:
                if (ViewState.TimerPhase != SplitTimerPhase.NotStarted)
                {
                    effects.Add(new SubmitRuntimeCommandEffect(RuntimeCommand.TogglePause()));
                }
                break;
            case AppCommandKind.ResetRun:
                AddResetEffects(effects, command.RecordStats, command.PlayResetSound);
                invalidateAll = true;
                break;
            case AppCommandKind.ToggleMouseClickThrough:
                effects.Add(new ToggleMouseClickThroughEffect());
                invalidateAll = true;
                break;
            case AppCommandKind.TogglePyramidFilter:
                TogglePyramidFilter(effects);
                invalidateAll = true;
                break;
            case AppCommandKind.QueueMenuAction:
                effects.Add(new SubmitRuntimeCommandEffect(
                    RuntimeCommand.QueueMenuAction(command.MenuAction, command.RequestedAtUtc)));
                break;
            case AppCommandKind.CancelCreateWorld:
                effects.Add(new CancelCreateWorldAutomationEffect());
                break;
            case AppCommandKind.CancelEnterWorld:
                effects.Add(new CancelEnterWorldAutomationEffect());
                break;
            case AppCommandKind.EditPracticeSplitTime:
                effects.Add(new SubmitRuntimeCommandEffect(
                    RuntimeCommand.SetPracticeSplitTime(command.SplitIndex, command.Time)));
                break;
            case AppCommandKind.EditPracticeTotalTime:
                if (command.Time is TimeSpan time)
                {
                    effects.Add(new SubmitRuntimeCommandEffect(RuntimeCommand.SetPracticeTotalTime(time)));
                }
                break;
            case AppCommandKind.ApplySettings:
                if (command.Settings is AppSettings nextSettings)
                {
                    ApplySettings(nextSettings, effects);
                    invalidateAll = true;
                }
                break;
        }

        return new ApplicationUpdate(effects, invalidateAll);
    }

    public ApplicationUpdate HandleWatcherNotification(WatcherPollNotification notification)
    {
        if (notification.RuntimeCommandSequence < minimumAcceptedRuntimeCommandSequence)
        {
            return new ApplicationUpdate([], InvalidateAll: false);
        }

        ViewState = ApplicationViewState.FromRuntimeSnapshot(Settings, notification.RuntimeSnapshot);
        minimumAcceptedRuntimeCommandSequence = Math.Max(
            minimumAcceptedRuntimeCommandSequence,
            notification.RuntimeCommandSequence);

        IReadOnlyList<ApplicationEffect> effects = RunEventProcessor.Process(
            notification.RunEvents,
            Settings,
            ViewState,
            runLifecycle,
            ResolveMenuActionEffects);
        return new ApplicationUpdate(effects, effects.Count > 0);
    }

    private void AddResetEffects(List<ApplicationEffect> effects, bool recordStats, bool playResetSound)
    {
        bool settingsUpdated = runLifecycle.Reset(Settings, ViewState.DisplayStatuses, recordStats, confirmPersonalBestUpdate);
        ViewState = ApplicationViewState.FromDefinitions(Settings, Definitions);
        if (settingsUpdated)
        {
            effects.Add(CreateSaveSettingsEffect(Settings));
        }

        effects.Add(new ClearOverlayAnimationEffect());
        effects.Add(new RefreshTimerOverlaySettingsEffect());
        if (playResetSound)
        {
            effects.Add(new StopAllSoundsEffect());
            effects.Add(new PlaySoundEffect(Settings.Overlay.Sounds.Reset));
        }

        effects.Add(new SubmitRuntimeCommandEffect(RuntimeCommand.Reset()));
        effects.Add(new RefreshRuntimeUiEffect());
    }

    private void TogglePyramidFilter(List<ApplicationEffect> effects)
    {
        AppSettings previousSettings = settingsSnapshots.CreateSnapshot(Settings);
        AppSettings nextSettings = settingsSnapshots.CreateSnapshot(Settings);
        nextSettings.Automation.AutoCreate.EnablePyramidFilter = !nextSettings.Automation.AutoCreate.EnablePyramidFilter;
        Settings = nextSettings;

        effects.Add(CreateSaveSettingsEffect(Settings));
        effects.Add(new ApplySettingsToShellEffect(previousSettings, Definitions.Count));
    }

    private void AddStartCreateWorldEffects(List<ApplicationEffect> effects)
    {
        AddResetEffects(effects, recordStats: true, playResetSound: true);
        effects.Add(new StartCreateWorldAutomationEffect());
    }

    private void AddStartPracticeWorldEffects(List<ApplicationEffect> effects)
    {
        AddResetEffects(effects, recordStats: true, playResetSound: true);
        effects.Add(new ShowPracticeWorldSelectorEffect());
    }

    private IReadOnlyList<ApplicationEffect> ResolveMenuActionEffects(MenuActionKind action)
    {
        var effects = new List<ApplicationEffect>();
        switch (action)
        {
            case MenuActionKind.Reset:
                AddResetEffects(effects, recordStats: true, playResetSound: true);
                break;
            case MenuActionKind.CreateWorld:
                AddStartCreateWorldEffects(effects);
                break;
            case MenuActionKind.PracticeWorld:
                AddStartPracticeWorldEffects(effects);
                break;
        }

        return effects;
    }

    private void ApplySettings(AppSettings appliedSettings, List<ApplicationEffect> effects)
    {
        AppSettings previousSettings = settingsSnapshots.CreateSnapshot(Settings);
        AppSettings nextSettings = settingsSnapshots.CreateSnapshot(appliedSettings);
        runLifecycle.Reset(
            Settings,
            nextSettings,
            ViewState.DisplayStatuses,
            recordStats: true,
            confirmPersonalBestUpdate);
        Settings = nextSettings;
        Definitions = SplitCatalog.Build(Settings);
        ViewState = ApplicationViewState.FromDefinitions(Settings, Definitions);

        effects.Add(CreateSaveSettingsEffect(Settings));
        effects.Add(new ClearOverlayAnimationEffect());
        effects.Add(new RefreshTimerOverlaySettingsEffect());
        effects.Add(new SubmitRuntimeCommandEffect(RuntimeCommand.Reset()));
        effects.Add(new SubmitRuntimeCommandEffect(RuntimeCommand.SetDefinitions(Definitions)));
        effects.Add(new ResetUiScalePatchStateEffect());
        effects.Add(new ApplySettingsToShellEffect(previousSettings, Definitions.Count));
    }

    private ApplicationEffect CreateSaveSettingsEffect(AppSettings settings)
    {
        return new SaveSettingsEffect(settingsSnapshots.CreateSnapshot(settings));
    }
}

internal static class RunEventProcessor
{
    public static IReadOnlyList<ApplicationEffect> Process(
        IReadOnlyList<RunEvent> events,
        AppSettings settings,
        ApplicationViewState viewState,
        RunLifecycleController runLifecycle,
        Func<MenuActionKind, IReadOnlyList<ApplicationEffect>> resolveMenuActionEffects)
    {
        if (events.Count == 0)
        {
            return [];
        }

        var effects = new List<ApplicationEffect>();
        foreach (RunEvent runEvent in events)
        {
            switch (runEvent.Kind)
            {
                case RunEventKind.PauseChanged:
                    if (runEvent.CurrentPhase == SplitTimerPhase.Paused)
                    {
                        effects.Add(new PlaySoundEffect(settings.Overlay.Sounds.Pause));
                    }
                    else if (runEvent.CurrentPhase == SplitTimerPhase.Running)
                    {
                        effects.Add(new PlaySoundEffect(settings.Overlay.Sounds.Resume));
                    }
                    break;
                case RunEventKind.MenuActionRequested:
                    if (runEvent.MenuAction is MenuActionKind menuAction)
                    {
                        effects.AddRange(resolveMenuActionEffects(menuAction));
                    }
                    break;
                case RunEventKind.RunStarted:
                    runLifecycle.MarkRunStarted();
                    effects.Add(new PlaySoundEffect(settings.Overlay.Sounds.EnterWorld));
                    break;
                case RunEventKind.SplitCompleted:
                    effects.Add(new TrackSegmentBestDeltaHighlightEffect(runEvent.SplitIndex));
                    if (!IsAttachedSplit(viewState, runEvent.SplitIndex))
                    {
                        effects.Add(settings.Overlay.ShowSplitCompletionAnimation
                            ? new StartSplitCompletionAnimationEffect(runEvent.SplitIndex)
                            : new ClearSplitCompletionAnimationEffect());
                    }

                    effects.Add(new PlaySoundEffect(
                        SoundFeedbackService.GetSplitSoundPath(settings, viewState.DisplayStatuses, runEvent.SplitIndex)));
                    break;
                case RunEventKind.RunCompleted:
                    runLifecycle.RecordRunStatsOnce(viewState.DisplayStatuses);
                    break;
                case RunEventKind.PracticeSplitTimeEdited:
                    effects.Add(new TrackSegmentBestDeltaHighlightEffect(runEvent.SplitIndex));
                    break;
                case RunEventKind.PracticeTotalTimeEdited:
                    effects.Add(new RefreshRuntimeUiEffect());
                    break;
            }
        }

        return effects;
    }

    private static bool IsAttachedSplit(ApplicationViewState viewState, int splitIndex)
    {
        return splitIndex >= 0 &&
            splitIndex < viewState.DisplayStatuses.Count &&
            viewState.DisplayStatuses[splitIndex].Definition.IsAttached;
    }
}
