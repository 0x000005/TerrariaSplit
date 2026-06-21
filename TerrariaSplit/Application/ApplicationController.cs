namespace TerrariaSplit.Application;

internal enum ApplicationEffectKind
{
    SubmitRuntimeCommand,
    StopAllSounds,
    PlaySound,
    ToggleMouseClickThrough,
    ClearOverlayAnimation,
    ClearSplitCompletionAnimation,
    TrackSegmentBestDeltaHighlight,
    StartSplitCompletionAnimation,
    SaveSettings,
    StartCreateWorldAutomation,
    ShowPracticeWorldSelector,
    CancelCreateWorldAutomation,
    CancelEnterWorldAutomation,
    ResetUiScalePatchState,
    ApplySettingsToShell,
    RefreshTimerOverlaySettings,
    RefreshRuntimeUi
}

internal abstract record ApplicationEffect(ApplicationEffectKind Kind)
{
    public static ApplicationEffect SubmitRuntimeCommand(RuntimeCommand command)
    {
        return new SubmitRuntimeCommandEffect(command);
    }

    public static ApplicationEffect Simple(ApplicationEffectKind kind)
    {
        return kind switch
        {
            ApplicationEffectKind.StopAllSounds => new StopAllSoundsEffect(),
            ApplicationEffectKind.ToggleMouseClickThrough => new ToggleMouseClickThroughEffect(),
            ApplicationEffectKind.ClearOverlayAnimation => new ClearOverlayAnimationEffect(),
            ApplicationEffectKind.ClearSplitCompletionAnimation => new ClearSplitCompletionAnimationEffect(),
            ApplicationEffectKind.StartCreateWorldAutomation => new StartCreateWorldAutomationEffect(),
            ApplicationEffectKind.ShowPracticeWorldSelector => new ShowPracticeWorldSelectorEffect(),
            ApplicationEffectKind.CancelCreateWorldAutomation => new CancelCreateWorldAutomationEffect(),
            ApplicationEffectKind.CancelEnterWorldAutomation => new CancelEnterWorldAutomationEffect(),
            ApplicationEffectKind.ResetUiScalePatchState => new ResetUiScalePatchStateEffect(),
            ApplicationEffectKind.RefreshTimerOverlaySettings => new RefreshTimerOverlaySettingsEffect(),
            ApplicationEffectKind.RefreshRuntimeUi => new RefreshRuntimeUiEffect(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Effect requires a payload.")
        };
    }

    public static ApplicationEffect PlaySound(string path)
    {
        return new PlaySoundEffect(path);
    }

    public static ApplicationEffect Split(ApplicationEffectKind kind, int splitIndex)
    {
        return kind switch
        {
            ApplicationEffectKind.TrackSegmentBestDeltaHighlight => new TrackSegmentBestDeltaHighlightEffect(splitIndex),
            ApplicationEffectKind.StartSplitCompletionAnimation => new StartSplitCompletionAnimationEffect(splitIndex),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Effect is not split-index based.")
        };
    }

    public static ApplicationEffect SaveSettings(AppSettings settings)
    {
        return new SaveSettingsEffect(settings);
    }

    public static ApplicationEffect ApplySettingsToShell(AppSettings previousSettings, int splitCount)
    {
        return new ApplySettingsToShellEffect(previousSettings, splitCount);
    }
}

internal sealed record SubmitRuntimeCommandEffect(RuntimeCommand Command)
    : ApplicationEffect(ApplicationEffectKind.SubmitRuntimeCommand);

internal sealed record StopAllSoundsEffect()
    : ApplicationEffect(ApplicationEffectKind.StopAllSounds);

internal sealed record PlaySoundEffect(string Path)
    : ApplicationEffect(ApplicationEffectKind.PlaySound);

internal sealed record ToggleMouseClickThroughEffect()
    : ApplicationEffect(ApplicationEffectKind.ToggleMouseClickThrough);

internal sealed record ClearOverlayAnimationEffect()
    : ApplicationEffect(ApplicationEffectKind.ClearOverlayAnimation);

internal sealed record ClearSplitCompletionAnimationEffect()
    : ApplicationEffect(ApplicationEffectKind.ClearSplitCompletionAnimation);

internal sealed record TrackSegmentBestDeltaHighlightEffect(int SplitIndex)
    : ApplicationEffect(ApplicationEffectKind.TrackSegmentBestDeltaHighlight);

internal sealed record StartSplitCompletionAnimationEffect(int SplitIndex)
    : ApplicationEffect(ApplicationEffectKind.StartSplitCompletionAnimation);

internal sealed record SaveSettingsEffect(AppSettings Settings)
    : ApplicationEffect(ApplicationEffectKind.SaveSettings);

internal sealed record StartCreateWorldAutomationEffect()
    : ApplicationEffect(ApplicationEffectKind.StartCreateWorldAutomation);

internal sealed record ShowPracticeWorldSelectorEffect()
    : ApplicationEffect(ApplicationEffectKind.ShowPracticeWorldSelector);

internal sealed record CancelCreateWorldAutomationEffect()
    : ApplicationEffect(ApplicationEffectKind.CancelCreateWorldAutomation);

internal sealed record CancelEnterWorldAutomationEffect()
    : ApplicationEffect(ApplicationEffectKind.CancelEnterWorldAutomation);

internal sealed record ResetUiScalePatchStateEffect()
    : ApplicationEffect(ApplicationEffectKind.ResetUiScalePatchState);

internal sealed record ApplySettingsToShellEffect(AppSettings PreviousSettings, int SplitCount)
    : ApplicationEffect(ApplicationEffectKind.ApplySettingsToShell);

internal sealed record RefreshTimerOverlaySettingsEffect()
    : ApplicationEffect(ApplicationEffectKind.RefreshTimerOverlaySettings);

internal sealed record RefreshRuntimeUiEffect()
    : ApplicationEffect(ApplicationEffectKind.RefreshRuntimeUi);

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
                    effects.Add(ApplicationEffect.SubmitRuntimeCommand(RuntimeCommand.TogglePause()));
                }
                break;
            case AppCommandKind.ResetRun:
                AddResetEffects(effects, command.RecordStats, command.PlayResetSound);
                invalidateAll = true;
                break;
            case AppCommandKind.ToggleMouseClickThrough:
                effects.Add(ApplicationEffect.Simple(ApplicationEffectKind.ToggleMouseClickThrough));
                invalidateAll = true;
                break;
            case AppCommandKind.TogglePyramidFilter:
                TogglePyramidFilter(effects);
                invalidateAll = true;
                break;
            case AppCommandKind.QueueMenuAction:
                effects.Add(ApplicationEffect.SubmitRuntimeCommand(
                    RuntimeCommand.QueueMenuAction(command.MenuAction, command.RequestedAtUtc)));
                break;
            case AppCommandKind.CancelCreateWorld:
                effects.Add(ApplicationEffect.Simple(ApplicationEffectKind.CancelCreateWorldAutomation));
                break;
            case AppCommandKind.CancelEnterWorld:
                effects.Add(ApplicationEffect.Simple(ApplicationEffectKind.CancelEnterWorldAutomation));
                break;
            case AppCommandKind.EditPracticeSplitTime:
                effects.Add(ApplicationEffect.SubmitRuntimeCommand(
                    RuntimeCommand.SetPracticeSplitTime(command.SplitIndex, command.Time)));
                break;
            case AppCommandKind.EditPracticeTotalTime:
                if (command.Time is TimeSpan time)
                {
                    effects.Add(ApplicationEffect.SubmitRuntimeCommand(RuntimeCommand.SetPracticeTotalTime(time)));
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

        effects.Add(ApplicationEffect.Simple(ApplicationEffectKind.ClearOverlayAnimation));
        effects.Add(ApplicationEffect.Simple(ApplicationEffectKind.RefreshTimerOverlaySettings));
        if (playResetSound)
        {
            effects.Add(ApplicationEffect.Simple(ApplicationEffectKind.StopAllSounds));
            effects.Add(ApplicationEffect.PlaySound(Settings.Overlay.Sounds.Reset));
        }

        effects.Add(ApplicationEffect.SubmitRuntimeCommand(RuntimeCommand.Reset()));
        effects.Add(ApplicationEffect.Simple(ApplicationEffectKind.RefreshRuntimeUi));
    }

    private void TogglePyramidFilter(List<ApplicationEffect> effects)
    {
        AppSettings previousSettings = settingsSnapshots.CreateSnapshot(Settings);
        AppSettings nextSettings = settingsSnapshots.CreateSnapshot(Settings);
        nextSettings.Automation.AutoCreate.EnablePyramidFilter = !nextSettings.Automation.AutoCreate.EnablePyramidFilter;
        Settings = nextSettings;

        effects.Add(CreateSaveSettingsEffect(Settings));
        effects.Add(ApplicationEffect.ApplySettingsToShell(previousSettings, Definitions.Count));
    }

    private void AddStartCreateWorldEffects(List<ApplicationEffect> effects)
    {
        AddResetEffects(effects, recordStats: true, playResetSound: true);
        effects.Add(ApplicationEffect.Simple(ApplicationEffectKind.StartCreateWorldAutomation));
    }

    private void AddStartPracticeWorldEffects(List<ApplicationEffect> effects)
    {
        AddResetEffects(effects, recordStats: true, playResetSound: true);
        effects.Add(ApplicationEffect.Simple(ApplicationEffectKind.ShowPracticeWorldSelector));
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
        effects.Add(ApplicationEffect.Simple(ApplicationEffectKind.ClearOverlayAnimation));
        effects.Add(ApplicationEffect.Simple(ApplicationEffectKind.RefreshTimerOverlaySettings));
        effects.Add(ApplicationEffect.SubmitRuntimeCommand(RuntimeCommand.Reset()));
        effects.Add(ApplicationEffect.SubmitRuntimeCommand(RuntimeCommand.SetDefinitions(Definitions)));
        effects.Add(ApplicationEffect.Simple(ApplicationEffectKind.ResetUiScalePatchState));
        effects.Add(ApplicationEffect.ApplySettingsToShell(previousSettings, Definitions.Count));
    }

    private ApplicationEffect CreateSaveSettingsEffect(AppSettings settings)
    {
        return ApplicationEffect.SaveSettings(settingsSnapshots.CreateSnapshot(settings));
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
                        effects.Add(ApplicationEffect.PlaySound(settings.Overlay.Sounds.Pause));
                    }
                    else if (runEvent.CurrentPhase == SplitTimerPhase.Running)
                    {
                        effects.Add(ApplicationEffect.PlaySound(settings.Overlay.Sounds.Resume));
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
                    effects.Add(ApplicationEffect.PlaySound(settings.Overlay.Sounds.EnterWorld));
                    break;
                case RunEventKind.SplitCompleted:
                    effects.Add(ApplicationEffect.Split(
                        ApplicationEffectKind.TrackSegmentBestDeltaHighlight,
                        runEvent.SplitIndex));
                    if (!IsAttachedSplit(viewState, runEvent.SplitIndex))
                    {
                        effects.Add(settings.Overlay.ShowSplitCompletionAnimation
                            ? ApplicationEffect.Split(
                                ApplicationEffectKind.StartSplitCompletionAnimation,
                                runEvent.SplitIndex)
                            : ApplicationEffect.Simple(ApplicationEffectKind.ClearSplitCompletionAnimation));
                    }

                    effects.Add(ApplicationEffect.PlaySound(
                        SoundFeedbackService.GetSplitSoundPath(settings, viewState.DisplayStatuses, runEvent.SplitIndex)));
                    break;
                case RunEventKind.RunCompleted:
                    runLifecycle.RecordRunStatsOnce(viewState.DisplayStatuses);
                    break;
                case RunEventKind.PracticeSplitTimeEdited:
                    effects.Add(ApplicationEffect.Split(
                        ApplicationEffectKind.TrackSegmentBestDeltaHighlight,
                        runEvent.SplitIndex));
                    break;
                case RunEventKind.PracticeTotalTimeEdited:
                    effects.Add(ApplicationEffect.Simple(ApplicationEffectKind.RefreshRuntimeUi));
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
