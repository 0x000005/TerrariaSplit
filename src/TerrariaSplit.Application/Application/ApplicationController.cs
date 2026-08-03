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

public sealed record ResetRaceProgressReportsEffect()
    : ApplicationEffect;

public sealed record QueueRaceProgressReportsEffect(bool RunStarted, bool RunCompleted)
    : ApplicationEffect;

public sealed record RecordRunStatisticsEffect(IReadOnlyList<SplitStatusSnapshot> Statuses)
    : ApplicationEffect;

public sealed record FinalizePersonalBestEffect(PersonalBestFinalizationPlan Plan)
    : ApplicationEffect;

public sealed class ApplicationController
{
    private readonly RunLifecycleController runLifecycle = new();
    private readonly ISettingsSnapshotFactory settingsSnapshots;
    private readonly Dictionary<long, PendingPersonalBestCommit> pendingPersonalBestCommits = [];
    private AppSettings baseSettings;
    private SettingsRouteOverridePackage? activeRouteOverride;
    private long minimumAcceptedRuntimeCommandSequence;
    private RaceSystemState raceState = new();

    public ApplicationController(
        AppSettings settings,
        ISettingsSnapshotFactory settingsSnapshots)
    {
        this.settingsSnapshots = settingsSnapshots;
        baseSettings = settingsSnapshots.CreateSnapshot(settings);
        SettingsNormalizer.Normalize(baseSettings);
        Settings = settingsSnapshots.CreateSnapshot(baseSettings);
        Definitions = SplitCatalog.Build(Settings);
        ViewState = ApplicationViewState.FromDefinitions(Settings, Definitions);
    }

    public AppSettings BaseSettings => baseSettings;

    public AppSettings Settings { get; private set; }

    public IReadOnlyList<SplitDefinition> Definitions { get; private set; }

    public ApplicationViewState ViewState { get; private set; }

    public long MinimumAcceptedRuntimeCommandSequence => minimumAcceptedRuntimeCommandSequence;

    public SystemState SystemState => new(raceState);

    public void AcceptRuntimeCommandSequence(long sequence)
    {
        minimumAcceptedRuntimeCommandSequence = Math.Max(minimumAcceptedRuntimeCommandSequence, sequence);
    }

    public ApplicationUpdate HandleSystemEvent(SystemEvent systemEvent)
    {
        return systemEvent switch
        {
            ControlCommandSystemEvent control => HandleCommand(control.Command),
            RuntimeWatcherSystemEvent runtime => HandleWatcherNotification(runtime.Notification),
            RacePackageSystemEvent racePackage => HandleRacePackageEvent(racePackage),
            RaceProgressSystemEvent raceProgress => HandleRaceProgressEvent(raceProgress),
            RaceRosterSystemEvent raceRoster => HandleRaceRosterEvent(raceRoster),
            RaceModeSystemEvent raceMode => HandleRaceModeEvent(raceMode),
            RaceTimePenaltySystemEvent racePenalty => HandleRaceTimePenaltyEvent(racePenalty),
            PersonalBestFinalizationSystemEvent finalization => HandlePersonalBestFinalization(finalization.Result),
            _ => throw new NotSupportedException($"Unsupported system event {systemEvent.GetType().Name}.")
        };
    }

    private ApplicationUpdate HandleRacePackageEvent(RacePackageSystemEvent racePackage)
    {
        bool enteredRoom = racePackage.IsInRoom && !raceState.IsInRoom;
        raceState = new RaceSystemState(
            racePackage.IsInRoom,
            racePackage.IsInRoom ? racePackage.RoomCode : string.Empty,
            racePackage.IsInRoom ? racePackage.PackageRevision : string.Empty,
            raceState.IsModeEnabled);
        return new ApplicationUpdate(
            enteredRoom ? CreateRaceRoomEntryEffects() : [],
            [DisplayInvalidation.For(DisplayRefreshLevel.RoutePackage, DisplayInvalidationTarget.All)]);
    }

    private ApplicationUpdate HandleRaceProgressEvent(RaceProgressSystemEvent raceProgress)
    {
        if (!raceState.IsInRoom ||
            !string.Equals(raceState.RoomCode, raceProgress.RoomCode, StringComparison.OrdinalIgnoreCase))
        {
            return ApplicationUpdate.Empty;
        }

        return new ApplicationUpdate(
            [],
            [DisplayInvalidation.For(DisplayRefreshLevel.SplitProgress, DisplayInvalidationTarget.RaceLeaderboard)]);
    }

    private ApplicationUpdate HandleRaceRosterEvent(RaceRosterSystemEvent raceRoster)
    {
        bool enteredRoom = raceRoster.IsInRoom && !raceState.IsInRoom;
        raceState = raceRoster.IsInRoom
            ? raceState with { IsInRoom = true, RoomCode = raceRoster.RoomCode }
            : raceState with { IsInRoom = false, RoomCode = string.Empty, PackageRevision = string.Empty };
        return new ApplicationUpdate(
            enteredRoom ? CreateRaceRoomEntryEffects() : [],
            [DisplayInvalidation.For(DisplayRefreshLevel.RuntimeFacts, DisplayInvalidationTarget.RaceLeaderboard)]);
    }

    private ApplicationUpdate HandleRaceModeEvent(RaceModeSystemEvent raceMode)
    {
        bool enteredMode = raceMode.Enabled && !raceState.IsModeEnabled;
        raceState = raceState with { IsModeEnabled = raceMode.Enabled };
        return new ApplicationUpdate(
            enteredMode ? CreateRaceRoomEntryEffects() : [],
            [DisplayInvalidation.For(DisplayRefreshLevel.RuntimeFacts, DisplayInvalidationTarget.All)]);
    }

    private ApplicationUpdate HandleRaceTimePenaltyEvent(RaceTimePenaltySystemEvent racePenalty)
    {
        if (!raceState.IsModeEnabled ||
            !raceState.IsInRoom ||
            racePenalty.Penalty <= TimeSpan.Zero ||
            racePenalty.Penalty > TimeSpan.FromMinutes(7.5))
        {
            return ApplicationUpdate.Empty;
        }

        return new ApplicationUpdate(
            [new SubmitRuntimeCommandEffect(RuntimeCommand.AddElapsedPenalty(racePenalty.Penalty))],
            []);
    }

    private ApplicationUpdate HandlePersonalBestFinalization(PersonalBestFinalizationResult result)
    {
        if (!pendingPersonalBestCommits.Remove(result.PlanId, out PendingPersonalBestCommit? pending))
        {
            return ApplicationUpdate.Empty;
        }

        if (!result.Approved)
        {
            return ApplicationUpdate.Empty;
        }

        if (result.Failures.Count > 0)
        {
            var failureEffects = new List<ApplicationEffect>();
            AddPersistenceFailureEffects(failureEffects, result.Failures);
            return new ApplicationUpdate(failureEffects, []);
        }

        AppSettings nextBaseSettings = settingsSnapshots.CreateSnapshot(pending.TargetBaseSettings);
        if (!RunFinalizer.TryApplySuccessfulPlan(
                nextBaseSettings,
                pending.Plan,
                result,
                out IReadOnlyList<OperationResult> failures))
        {
            var failureEffects = new List<ApplicationEffect>();
            AddPersistenceFailureEffects(failureEffects, failures);
            return new ApplicationUpdate(failureEffects, []);
        }

        if (!ReferenceEquals(baseSettings, pending.TargetBaseSettings))
        {
            return new ApplicationUpdate(
                [
                    new ShowPersistenceFailureEffect(OperationResult.Failure(
                        "Personal best snapshots were saved, but settings changed before they could be applied."))
                ],
                []);
        }

        baseSettings = nextBaseSettings;
        Settings = CreateEffectiveSettings(baseSettings);
        ViewState = ViewState.WithSettings(Settings);
        return new ApplicationUpdate(
            [CreateSaveSettingsEffect(baseSettings)],
            [DisplayInvalidation.For(DisplayRefreshLevel.SplitProgress, DisplayInvalidationTarget.All)]);
    }

    private ApplicationUpdate HandleCommand(AppCommand command)
    {
        if (!RaceInteractionPolicy.Allows(command, raceState.IsModeEnabled, raceState.IsInRoom))
        {
            return ApplicationUpdate.Empty;
        }

        var effects = new List<ApplicationEffect>();
        var invalidations = new List<DisplayInvalidation>();

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
                invalidations.Add(DisplayInvalidation.For(DisplayRefreshLevel.RunReset, DisplayInvalidationTarget.All));
                break;
            case ToggleMouseClickThroughCommand:
                effects.Add(new ToggleMouseClickThroughEffect());
                invalidations.Add(DisplayInvalidation.For(DisplayRefreshLevel.DisplaySettings, DisplayInvalidationTarget.All));
                break;
            case ToggleCheatsCommand:
                ToggleCheats(effects);
                invalidations.Add(DisplayInvalidation.For(DisplayRefreshLevel.FullRebuild, DisplayInvalidationTarget.All));
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
            case CompleteNextSplitManuallyCommand:
                effects.Add(new SubmitRuntimeCommandEffect(RuntimeCommand.CompleteNextSplitManually()));
                break;
            case ApplySettingsCommand applySettings:
                ApplySettings(applySettings.Settings, effects, saveSettings: true);
                invalidations.Add(DisplayInvalidation.For(DisplayRefreshLevel.FullRebuild, DisplayInvalidationTarget.All));
                break;
            case ApplyTemporarySettingsCommand applySettings:
                ApplySettings(applySettings.Settings, effects, saveSettings: false);
                invalidations.Add(DisplayInvalidation.For(DisplayRefreshLevel.RoutePackage, DisplayInvalidationTarget.All));
                break;
            case UpdateRaceSettingsCommand updateRaceSettings:
                UpdateRaceSettings(updateRaceSettings.Settings, effects);
                invalidations.Add(DisplayInvalidation.For(
                    DisplayRefreshLevel.DisplaySettings,
                    DisplayInvalidationTarget.RaceLeaderboard | DisplayInvalidationTarget.TimerOverlay));
                break;
            case ApplyRouteOverrideCommand applyOverride:
                ApplyRouteOverride(applyOverride.Package, effects);
                invalidations.Add(DisplayInvalidation.For(DisplayRefreshLevel.RoutePackage, DisplayInvalidationTarget.All));
                invalidations.Add(DisplayInvalidation.For(DisplayRefreshLevel.RunReset, DisplayInvalidationTarget.All));
                break;
            case ClearRouteOverrideCommand:
                ClearRouteOverride(effects);
                invalidations.Add(DisplayInvalidation.For(DisplayRefreshLevel.RoutePackage, DisplayInvalidationTarget.All));
                invalidations.Add(DisplayInvalidation.For(DisplayRefreshLevel.RunReset, DisplayInvalidationTarget.All));
                break;
            default:
                throw new NotSupportedException($"Unsupported application command {command.GetType().Name}.");
        }

        return new ApplicationUpdate(effects, invalidations);
    }

    private ApplicationUpdate HandleWatcherNotification(WatcherPollNotification notification)
    {
        if (notification.RuntimeCommandSequence < minimumAcceptedRuntimeCommandSequence)
        {
            return ApplicationUpdate.Empty;
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
        IReadOnlyList<DisplayInvalidation> invalidations = ResolveRuntimeInvalidations(notification.RunEvents);
        return new ApplicationUpdate(effects, invalidations);
    }

    private static IReadOnlyList<DisplayInvalidation> ResolveRuntimeInvalidations(IReadOnlyList<RunEvent> events)
    {
        if (events.Count == 0)
        {
            return
            [
                DisplayInvalidation.For(
                    DisplayRefreshLevel.RuntimeFacts,
                    DisplayInvalidationTarget.SplitOverlay | DisplayInvalidationTarget.TimerOverlay)
            ];
        }

        var invalidations = new List<DisplayInvalidation>();
        if (events.Any(static item => item.Kind == RunEventKind.SplitCompleted ||
                item.Kind == RunEventKind.PracticeSplitTimeEdited ||
                item.Kind == RunEventKind.PracticeTotalTimeEdited ||
                item.Kind == RunEventKind.RunCompleted))
        {
            invalidations.Add(DisplayInvalidation.For(DisplayRefreshLevel.SplitProgress, DisplayInvalidationTarget.All));
        }

        if (events.Any(static item => item.Kind == RunEventKind.RunStarted ||
                item.Kind == RunEventKind.PauseChanged ||
                item.Kind == RunEventKind.MenuActionRequested))
        {
            invalidations.Add(DisplayInvalidation.For(
                DisplayRefreshLevel.RuntimeFacts,
                DisplayInvalidationTarget.SplitOverlay | DisplayInvalidationTarget.TimerOverlay));
        }

        return invalidations.Count == 0
            ? [DisplayInvalidation.For(DisplayRefreshLevel.RuntimeFacts, DisplayInvalidationTarget.SplitOverlay | DisplayInvalidationTarget.TimerOverlay)]
            : invalidations;
    }

    private void AddResetEffects(List<ApplicationEffect> effects, bool recordStats, bool playResetSound)
    {
        RunFinalizationRequest finalization = runLifecycle.Reset(
            Settings,
            baseSettings,
            ViewState.DisplayStatuses,
            recordStats);
        AddRunStatisticsEffect(effects, finalization);
        AddPersonalBestEffect(effects, finalization, baseSettings);
        ViewState = ApplicationViewState.FromDefinitions(Settings, Definitions);

        effects.Add(new ClearOverlayAnimationEffect());
        effects.Add(new RefreshTimerOverlaySettingsEffect());
        if (playResetSound)
        {
            effects.Add(new StopAllSoundsEffect());
            effects.Add(new PlaySoundEffect(Settings.Overlay.Sounds.Reset));
        }

        effects.Add(new SubmitRuntimeCommandEffect(RuntimeCommand.Reset()));
        effects.Add(new ResetRaceProgressReportsEffect());
        effects.Add(new RefreshRuntimeUiEffect());
    }

    private void ToggleCheats(List<ApplicationEffect> effects)
    {
        AppSettings previousSettings = settingsSnapshots.CreateSnapshot(Settings);
        AppSettings nextBaseSettings = settingsSnapshots.CreateSnapshot(baseSettings);
        nextBaseSettings.Automation.AutoCreate.EnableCheats = !nextBaseSettings.Automation.AutoCreate.EnableCheats;
        SettingsNormalizer.Normalize(nextBaseSettings);
        baseSettings = nextBaseSettings;
        Settings = CreateEffectiveSettings(baseSettings);
        Definitions = SplitCatalog.Build(Settings);
        ViewState = ApplicationViewState.FromDefinitions(Settings, Definitions);

        effects.Add(CreateSaveSettingsEffect(baseSettings));
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
        if (!RaceInteractionPolicy.Allows(action, raceState.IsModeEnabled))
        {
            return [];
        }

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

    private static IReadOnlyList<ApplicationEffect> CreateRaceRoomEntryEffects()
    {
        return
        [
            new CancelCreateWorldAutomationEffect(),
            new CancelEnterWorldAutomationEffect(),
            new SubmitRuntimeCommandEffect(RuntimeCommand.ClearPendingMenuActions())
        ];
    }

    private void ApplySettings(
        AppSettings appliedSettings,
        List<ApplicationEffect> effects,
        bool saveSettings)
    {
        AppSettings previousSettings = settingsSnapshots.CreateSnapshot(Settings);
        AppSettings nextBaseSettings = settingsSnapshots.CreateSnapshot(appliedSettings);
        SettingsNormalizer.Normalize(nextBaseSettings);
        RunFinalizationRequest finalization = runLifecycle.Reset(
            Settings,
            nextBaseSettings,
            ViewState.DisplayStatuses,
            recordStats: true);
        AddRunStatisticsEffect(effects, finalization);
        baseSettings = nextBaseSettings;
        Settings = CreateEffectiveSettings(baseSettings);
        Definitions = SplitCatalog.Build(Settings);
        ViewState = ApplicationViewState.FromDefinitions(Settings, Definitions);

        if (saveSettings)
        {
            effects.Add(CreateSaveSettingsEffect(baseSettings));
        }

        AddPersonalBestEffect(effects, finalization, baseSettings);
        effects.Add(new ClearOverlayAnimationEffect());
        effects.Add(new RefreshTimerOverlaySettingsEffect());
        effects.Add(new SubmitRuntimeCommandEffect(RuntimeCommand.Reset()));
        effects.Add(new SubmitRuntimeCommandEffect(RuntimeCommand.SetDefinitions(Definitions)));
        effects.Add(new ResetUiScalePatchStateEffect());
        effects.Add(new ResetRaceProgressReportsEffect());
        effects.Add(new ApplySettingsToShellEffect(previousSettings, Definitions.Count));
    }

    private void ApplyRouteOverride(
        SettingsRouteOverridePackage package,
        List<ApplicationEffect> effects)
    {
        activeRouteOverride = SettingsRouteOverrideService.Clone(package);
        RebuildEffectiveSettings(effects, resetRaceProgress: true);
    }

    private void ClearRouteOverride(List<ApplicationEffect> effects)
    {
        if (activeRouteOverride is null)
        {
            return;
        }

        activeRouteOverride = null;
        RebuildEffectiveSettings(effects, resetRaceProgress: false);
    }

    private void RebuildEffectiveSettings(List<ApplicationEffect> effects, bool resetRaceProgress)
    {
        AppSettings previousSettings = settingsSnapshots.CreateSnapshot(Settings);
        RunFinalizationRequest finalization = runLifecycle.Reset(
            Settings,
            baseSettings,
            ViewState.DisplayStatuses,
            recordStats: true);
        AddRunStatisticsEffect(effects, finalization);
        AddPersonalBestEffect(effects, finalization, baseSettings);
        Settings = CreateEffectiveSettings(baseSettings);
        Definitions = SplitCatalog.Build(Settings);
        ViewState = ApplicationViewState.FromDefinitions(Settings, Definitions);

        effects.Add(new ClearOverlayAnimationEffect());
        effects.Add(new RefreshTimerOverlaySettingsEffect());
        effects.Add(new SubmitRuntimeCommandEffect(RuntimeCommand.Reset()));
        effects.Add(new SubmitRuntimeCommandEffect(RuntimeCommand.SetDefinitions(Definitions)));
        effects.Add(new ResetUiScalePatchStateEffect());
        if (resetRaceProgress)
        {
            effects.Add(new ResetRaceProgressReportsEffect());
        }

        effects.Add(new ApplySettingsToShellEffect(previousSettings, Definitions.Count));
    }

    private AppSettings CreateEffectiveSettings(AppSettings sourceBaseSettings)
    {
        AppSettings normalizedBase = settingsSnapshots.CreateSnapshot(sourceBaseSettings);
        SettingsNormalizer.Normalize(normalizedBase);
        return activeRouteOverride is null
            ? normalizedBase
            : SettingsRouteOverrideService.Apply(normalizedBase, activeRouteOverride, settingsSnapshots);
    }

    private ApplicationEffect CreateSaveSettingsEffect(AppSettings settings)
    {
        return new SaveSettingsEffect(settingsSnapshots.CreateSnapshot(settings));
    }

    private void UpdateRaceSettings(
        RaceSettings raceSettings,
        List<ApplicationEffect> effects)
    {
        AppSettings nextBaseSettings = settingsSnapshots.CreateSnapshot(baseSettings);
        nextBaseSettings.Race = AppSettingsCloner.CloneRaceSettings(raceSettings);
        SettingsNormalizer.Normalize(nextBaseSettings);
        baseSettings = nextBaseSettings;
        Settings = CreateEffectiveSettings(baseSettings);
        ViewState = ViewState.WithSettings(Settings);

        effects.Add(CreateSaveSettingsEffect(baseSettings));
        effects.Add(new RefreshTimerOverlaySettingsEffect());
    }

    private static void AddRunStatisticsEffect(
        List<ApplicationEffect> effects,
        RunFinalizationRequest finalization)
    {
        if (finalization.Statistics.Count > 0)
        {
            effects.Add(new RecordRunStatisticsEffect(finalization.Statistics));
        }
    }

    private void AddPersonalBestEffect(
        List<ApplicationEffect> effects,
        RunFinalizationRequest finalization,
        AppSettings targetBaseSettings)
    {
        if (finalization.PersonalBestPlan is not PersonalBestFinalizationPlan plan)
        {
            return;
        }

        pendingPersonalBestCommits[plan.PlanId] = new PendingPersonalBestCommit(plan, targetBaseSettings);
        effects.Add(new FinalizePersonalBestEffect(plan));
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

    private sealed record PendingPersonalBestCommit(
        PersonalBestFinalizationPlan Plan,
        AppSettings TargetBaseSettings);
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
        bool queueRaceProgress = false;
        bool raceRunStarted = false;
        bool raceRunCompleted = false;
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
                    queueRaceProgress = true;
                    raceRunStarted = true;
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
                    queueRaceProgress = true;
                    break;
                case RunEventKind.RunCompleted:
                    IReadOnlyList<SplitStatusSnapshot>? runStatistics =
                        runLifecycle.CaptureRunStatisticsOnce(viewState.DisplayStatuses);
                    if (runStatistics is not null)
                    {
                        effects.Add(new RecordRunStatisticsEffect(runStatistics));
                    }
                    queueRaceProgress = true;
                    raceRunCompleted = true;
                    break;
                case RunEventKind.PracticeSplitTimeEdited:
                    effects.Add(new TrackSegmentBestDeltaHighlightEffect(runEvent.SplitIndex));
                    break;
                case RunEventKind.PracticeTotalTimeEdited:
                    effects.Add(new RefreshRuntimeUiEffect());
                    break;
            }
        }

        if (queueRaceProgress)
        {
            effects.Add(new QueueRaceProgressReportsEffect(raceRunStarted, raceRunCompleted));
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
