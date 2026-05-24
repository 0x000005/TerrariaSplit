namespace TerrariaSplit;

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

internal sealed record ApplicationEffect
{
    private ApplicationEffect(ApplicationEffectKind kind)
    {
        Kind = kind;
    }

    public ApplicationEffectKind Kind { get; }

    public RuntimeCommand? RuntimeCommand { get; private init; }

    public string? SoundPath { get; private init; }

    public int SplitIndex { get; private init; } = -1;

    public AppSettings? PreviousSettings { get; private init; }

    public AppSettings? SettingsToSave { get; private init; }

    public int SplitCount { get; private init; }

    public static ApplicationEffect SubmitRuntimeCommand(RuntimeCommand command)
    {
        return new ApplicationEffect(ApplicationEffectKind.SubmitRuntimeCommand)
        {
            RuntimeCommand = command
        };
    }

    public static ApplicationEffect Simple(ApplicationEffectKind kind) => new(kind);

    public static ApplicationEffect PlaySound(string path)
    {
        return new ApplicationEffect(ApplicationEffectKind.PlaySound)
        {
            SoundPath = path
        };
    }

    public static ApplicationEffect Split(ApplicationEffectKind kind, int splitIndex)
    {
        return new ApplicationEffect(kind)
        {
            SplitIndex = splitIndex
        };
    }

    public static ApplicationEffect SaveSettings(AppSettings settings)
    {
        return new ApplicationEffect(ApplicationEffectKind.SaveSettings)
        {
            SettingsToSave = AppSettingsStore.Clone(settings)
        };
    }

    public static ApplicationEffect ApplySettingsToShell(AppSettings previousSettings, int splitCount)
    {
        return new ApplicationEffect(ApplicationEffectKind.ApplySettingsToShell)
        {
            PreviousSettings = previousSettings,
            SplitCount = splitCount
        };
    }
}

internal sealed record ApplicationUpdate(
    IReadOnlyList<ApplicationEffect> Effects,
    bool InvalidateAll = false);

internal sealed class ApplicationController
{
    private readonly RunLifecycleController runLifecycle = new();
    private readonly Func<string, bool> confirmPersonalBestUpdate;
    private long minimumAcceptedRuntimeCommandSequence;

    public ApplicationController(AppSettings settings, Func<string, bool> confirmPersonalBestUpdate)
    {
        this.confirmPersonalBestUpdate = confirmPersonalBestUpdate;
        Settings = settings;
        Definitions = BossSplitDefinitions.Build(settings);
        ViewState = ApplicationViewState.FromDefinitions(settings, Definitions);
    }

    public AppSettings Settings { get; private set; }

    public IReadOnlyList<BossSplitDefinition> Definitions { get; private set; }

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
        runLifecycle.Reset(Settings, ViewState.DisplayStatuses, recordStats, confirmPersonalBestUpdate);
        ViewState = ApplicationViewState.FromDefinitions(Settings, Definitions);
        effects.Add(ApplicationEffect.Simple(ApplicationEffectKind.ClearOverlayAnimation));
        effects.Add(ApplicationEffect.Simple(ApplicationEffectKind.RefreshTimerOverlaySettings));
        if (playResetSound)
        {
            effects.Add(ApplicationEffect.Simple(ApplicationEffectKind.StopAllSounds));
            effects.Add(ApplicationEffect.PlaySound(Settings.Sounds.Reset));
        }

        effects.Add(ApplicationEffect.SubmitRuntimeCommand(RuntimeCommand.Reset()));
        effects.Add(ApplicationEffect.Simple(ApplicationEffectKind.RefreshRuntimeUi));
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
        AppSettings previousSettings = AppSettingsStore.Clone(Settings);
        AppSettings nextSettings = AppSettingsStore.Clone(appliedSettings);
        runLifecycle.Reset(nextSettings, ViewState.DisplayStatuses, recordStats: true, confirmPersonalBestUpdate);
        Settings = nextSettings;
        Definitions = BossSplitDefinitions.Build(Settings);
        ViewState = ApplicationViewState.FromDefinitions(Settings, Definitions);

        effects.Add(ApplicationEffect.SaveSettings(Settings));
        effects.Add(ApplicationEffect.Simple(ApplicationEffectKind.ClearOverlayAnimation));
        effects.Add(ApplicationEffect.Simple(ApplicationEffectKind.RefreshTimerOverlaySettings));
        effects.Add(ApplicationEffect.SubmitRuntimeCommand(RuntimeCommand.Reset()));
        effects.Add(ApplicationEffect.SubmitRuntimeCommand(RuntimeCommand.SetDefinitions(Definitions)));
        effects.Add(ApplicationEffect.Simple(ApplicationEffectKind.ResetUiScalePatchState));
        effects.Add(ApplicationEffect.ApplySettingsToShell(previousSettings, Definitions.Count));
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
                        effects.Add(ApplicationEffect.PlaySound(settings.Sounds.Pause));
                    }
                    else if (runEvent.CurrentPhase == SplitTimerPhase.Running)
                    {
                        effects.Add(ApplicationEffect.PlaySound(settings.Sounds.Resume));
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
                    effects.Add(ApplicationEffect.PlaySound(settings.Sounds.EnterWorld));
                    break;
                case RunEventKind.SplitCompleted:
                    effects.Add(ApplicationEffect.Split(
                        ApplicationEffectKind.TrackSegmentBestDeltaHighlight,
                        runEvent.SplitIndex));
                    effects.Add(settings.ShowSplitCompletionAnimation
                        ? ApplicationEffect.Split(
                            ApplicationEffectKind.StartSplitCompletionAnimation,
                            runEvent.SplitIndex)
                        : ApplicationEffect.Simple(ApplicationEffectKind.ClearSplitCompletionAnimation));
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
}
