namespace TerrariaSplit.Application;

public abstract record ApplicationEffect;

public sealed record SubmitRuntimeCommandEffect(RuntimeCommand Command)
    : ApplicationEffect;

public sealed record StopAllSoundsEffect()
    : ApplicationEffect;

public sealed record PlaySoundEffect(string Path)
    : ApplicationEffect;

public sealed record ToggleMouseClickThroughEffect()
    : ApplicationEffect;

public sealed record ClearOverlayAnimationEffect()
    : ApplicationEffect;

public sealed record ClearSplitCompletionAnimationEffect()
    : ApplicationEffect;

public sealed record TrackSegmentBestDeltaHighlightEffect(int SplitIndex)
    : ApplicationEffect;

public sealed record StartSplitCompletionAnimationEffect(int SplitIndex)
    : ApplicationEffect;

public sealed record SaveSettingsEffect(AppSettings Settings)
    : ApplicationEffect;

public sealed record ShowPersistenceFailureEffect(OperationResult Result)
    : ApplicationEffect;

public sealed record StartCreateWorldAutomationEffect()
    : ApplicationEffect;

public sealed record ShowPracticeWorldSelectorEffect()
    : ApplicationEffect;

public sealed record CancelCreateWorldAutomationEffect()
    : ApplicationEffect;

public sealed record CancelEnterWorldAutomationEffect()
    : ApplicationEffect;

public sealed record ResetUiScalePatchStateEffect()
    : ApplicationEffect;

public sealed record ApplySettingsToShellEffect(AppSettings PreviousSettings, int SplitCount)
    : ApplicationEffect;

public sealed record RefreshTimerOverlaySettingsEffect()
    : ApplicationEffect;

public sealed record RefreshRuntimeUiEffect()
    : ApplicationEffect;

public sealed record ApplicationUpdate(
    IReadOnlyList<ApplicationEffect> Effects,
    bool InvalidateAll = false);

public sealed class ApplicationController
{
    private readonly RunLifecycleController runLifecycle;
    private readonly Func<string, bool> confirmPersonalBestUpdate;
    private readonly ISettingsSnapshotFactory settingsSnapshots;
    private long minimumAcceptedRuntimeCommandSequence;

    public ApplicationController(
        AppSettings settings,
        Func<string, bool> confirmPersonalBestUpdate,
        ISettingsSnapshotFactory settingsSnapshots,
        IRunStatisticsRecorder? runStatisticsRecorder = null,
        IPersonalBestSnapshotStore? personalBestSnapshotStore = null)
    {
        this.confirmPersonalBestUpdate = confirmPersonalBestUpdate;
        this.settingsSnapshots = settingsSnapshots;
        runLifecycle = new RunLifecycleController(runStatisticsRecorder, personalBestSnapshotStore);
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

        switch (command)
        {
            case TogglePauseCommand:
                if (ViewState.TimerPhase != SplitTimerPhase.NotStarted)
                {
                    effects.Add(new SubmitRuntimeCommandEffect(RuntimeCommand.TogglePause()));
                }
                break;
            case ResetRunCommand reset:
                AddResetEffects(effects, reset.RecordStats, reset.PlayResetSound);
                invalidateAll = true;
                break;
            case ToggleMouseClickThroughCommand:
                effects.Add(new ToggleMouseClickThroughEffect());
                invalidateAll = true;
                break;
            case TogglePyramidFilterCommand:
                TogglePyramidFilter(effects);
                invalidateAll = true;
                break;
            case QueueMenuActionCommand queueMenuAction:
                effects.Add(new SubmitRuntimeCommandEffect(
                    RuntimeCommand.QueueMenuAction(queueMenuAction.Action, queueMenuAction.RequestedAtUtc)));
                break;
            case CancelCreateWorldCommand:
                effects.Add(new CancelCreateWorldAutomationEffect());
                break;
            case CancelEnterWorldCommand:
                effects.Add(new CancelEnterWorldAutomationEffect());
                break;
            case EditPracticeSplitTimeCommand editSplitTime:
                effects.Add(new SubmitRuntimeCommandEffect(
                    RuntimeCommand.SetPracticeSplitTime(editSplitTime.SplitIndex, editSplitTime.Time)));
                break;
            case EditPracticeTotalTimeCommand editTotalTime:
                effects.Add(new SubmitRuntimeCommandEffect(RuntimeCommand.SetPracticeTotalTime(editTotalTime.Time)));
                break;
            case ApplySettingsCommand applySettings:
                ApplySettings(applySettings.Settings, effects);
                invalidateAll = true;
                break;
            default:
                throw new NotSupportedException($"Unsupported application command {command.GetType().Name}.");
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
        RunFinalizationResult finalization = runLifecycle.Reset(
            Settings,
            ViewState.DisplayStatuses,
            recordStats,
            confirmPersonalBestUpdate);
        ViewState = ApplicationViewState.FromDefinitions(Settings, Definitions);
        if (finalization.SettingsUpdated)
        {
            effects.Add(CreateSaveSettingsEffect(Settings));
        }

        AddPersistenceFailureEffects(effects, finalization.PersistenceFailures);
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
        RunFinalizationResult finalization = runLifecycle.Reset(
            Settings,
            nextSettings,
            ViewState.DisplayStatuses,
            recordStats: true,
            confirmPersonalBestUpdate);
        Settings = nextSettings;
        Definitions = SplitCatalog.Build(Settings);
        ViewState = ApplicationViewState.FromDefinitions(Settings, Definitions);

        effects.Add(CreateSaveSettingsEffect(Settings));
        AddPersistenceFailureEffects(effects, finalization.PersistenceFailures);
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

    private static void AddPersistenceFailureEffects(
        List<ApplicationEffect> effects,
        IReadOnlyList<OperationResult> failures)
    {
        foreach (OperationResult failure in failures)
        {
            effects.Add(new ShowPersistenceFailureEffect(failure));
        }
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
