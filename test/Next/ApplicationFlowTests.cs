namespace TerrariaSplit.Tests;

internal static class ApplicationFlowTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("application commands coordinate settings, runtime effects and full display invalidation", TestSuite.Flow, CommandJourney);
        yield return TestCase.Sync("race events update system state and target only relevant views", TestSuite.Flow, SystemEventJourney);
        yield return TestCase.Sync("world-entry transition facts cannot skip or complete a split", TestSuite.Flow, WorldEntryTransitionFacts);
        yield return TestCase.Sync("manual split starts an idle run without completing a split", TestSuite.Flow, ManualSplitStartsIdleRun);
        yield return TestCase.Sync("manual split completes exactly one advanced split at the timer time without inventing conditions", TestSuite.Flow, ManualSplitJourney);
        yield return TestCase.Sync("run finalization emits persistence effects and commits personal best settings only after every snapshot succeeds", TestSuite.Flow, RunFinalizationJourney);
    }

    private static void CommandJourney()
    {
        AppSettings settings = AppSettingsDefaults.Create();
        var settingsSnapshots = new SettingsSnapshotFactory();
        var controller = new ApplicationController(settings, settingsSnapshots);
        Check.False(controller.SystemState.Race.IsModeEnabled);

        ApplicationUpdate idlePause = controller.HandleSystemEvent(new ControlCommandSystemEvent(AppCommand.TogglePause()));
        Check.Equal(0, idlePause.Effects.Count);
        ApplicationUpdate clickThrough = controller.HandleSystemEvent(new ControlCommandSystemEvent(AppCommand.ToggleMouseClickThrough()));
        Check.True(clickThrough.Effects.OfType<ToggleMouseClickThroughEffect>().Any());
        ApplicationUpdate manualSplit = controller.HandleSystemEvent(new ControlCommandSystemEvent(AppCommand.CompleteNextSplitManually()));
        Check.True(manualSplit.Effects
            .OfType<SubmitRuntimeCommandEffect>()
            .Any(static effect => effect.Command.Kind == RuntimeCommandKind.CompleteNextSplitManually));

        bool previousCheats = controller.Settings.Automation.AutoCreate.EnableCheats;
        ApplicationUpdate cheats = controller.HandleSystemEvent(new ControlCommandSystemEvent(AppCommand.ToggleCheats()));
        Check.Equal(!previousCheats, controller.Settings.Automation.AutoCreate.EnableCheats);
        Check.True(cheats.Effects.OfType<SaveSettingsEffect>().Any());
        Check.True(cheats.Effects.OfType<ApplySettingsToShellEffect>().Any());

        AppSettings changed = settingsSnapshots.CreateSnapshot(controller.Settings);
        changed.General.AlwaysOnTop = !changed.General.AlwaysOnTop;
        ApplicationUpdate applied = controller.HandleSystemEvent(new ControlCommandSystemEvent(AppCommand.ApplySettings(changed)));
        Check.Equal(changed.General.AlwaysOnTop, controller.Settings.General.AlwaysOnTop);
        Check.True(applied.Effects.OfType<SaveSettingsEffect>().Any());
        Check.True(applied.Effects.OfType<SubmitRuntimeCommandEffect>().Count() >= 2);
        Check.True(applied.DisplayInvalidations.Any(item => item.Level == DisplayRefreshLevel.FullRebuild));

        RuntimeRunSnapshot runtimeBeforeRaceUpdate = controller.ViewState.RuntimeSnapshot;
        RaceSettings raceSettings = AppSettingsCloner.CloneRaceSettings(controller.BaseSettings.Race);
        raceSettings.Nickname = "command-owned-race-settings";
        ApplicationUpdate raceUpdate = controller.HandleSystemEvent(new ControlCommandSystemEvent(
            AppCommand.UpdateRaceSettings(raceSettings)));
        raceSettings.Nickname = "mutated-after-dispatch";

        Check.Equal("command-owned-race-settings", controller.BaseSettings.Race.Nickname);
        Check.Equal("command-owned-race-settings", controller.Settings.Race.Nickname);
        Check.True(ReferenceEquals(runtimeBeforeRaceUpdate, controller.ViewState.RuntimeSnapshot));
        Check.Equal(1, raceUpdate.Effects.OfType<SaveSettingsEffect>().Count());
        Check.Equal(0, raceUpdate.Effects.OfType<SubmitRuntimeCommandEffect>().Count());
        DisplayInvalidation raceInvalidation = raceUpdate.DisplayInvalidations.Single();
        Check.Equal(DisplayRefreshLevel.DisplaySettings, raceInvalidation.Level);
        Check.Equal(
            DisplayInvalidationTarget.RaceLeaderboard | DisplayInvalidationTarget.TimerOverlay,
            raceInvalidation.Targets);
    }

    private static void SystemEventJourney()
    {
        AppSettings settings = AppSettingsDefaults.Create();
        var settingsSnapshots = new SettingsSnapshotFactory();
        var controller = new ApplicationController(settings, settingsSnapshots);
        Check.False(controller.SystemState.Race.IsModeEnabled);

        ApplicationUpdate createBeforeRace = controller.HandleSystemEvent(new ControlCommandSystemEvent(
            AppCommand.QueueMenuAction(MenuActionKind.CreateWorld, DateTime.UtcNow)));
        Check.True(createBeforeRace.Effects.OfType<SubmitRuntimeCommandEffect>().Any());

        ApplicationUpdate enteredMode = controller.HandleSystemEvent(new RaceModeSystemEvent(Enabled: true));
        Check.True(controller.SystemState.Race.IsModeEnabled);
        Check.True(enteredMode.Effects.OfType<CancelCreateWorldAutomationEffect>().Any());
        Check.True(enteredMode.Effects.OfType<CancelEnterWorldAutomationEffect>().Any());
        Check.True(enteredMode.Effects
            .OfType<SubmitRuntimeCommandEffect>()
            .Any(effect => effect.Command.Kind == RuntimeCommandKind.ClearPendingMenuActions));

        AppSettings changedRaceSettings = settingsSnapshots.CreateSnapshot(controller.Settings);
        changedRaceSettings.General.AlwaysOnTop = !changedRaceSettings.General.AlwaysOnTop;
        bool raceAlwaysOnTop = controller.Settings.General.AlwaysOnTop;
        AppCommand[] modeRestrictedCommands =
        [
            AppCommand.TogglePause(),
            AppCommand.ResetRun(recordStats: true, playResetSound: true),
            AppCommand.ToggleCheats(),
            AppCommand.QueueMenuAction(MenuActionKind.Reset, DateTime.UtcNow),
            AppCommand.QueueMenuAction(MenuActionKind.CreateWorld, DateTime.UtcNow),
            AppCommand.QueueMenuAction(MenuActionKind.PracticeWorld, DateTime.UtcNow),
            AppCommand.EditPracticeSplitTime(0, TimeSpan.FromSeconds(1)),
            AppCommand.EditPracticeTotalTime(TimeSpan.FromSeconds(1)),
            AppCommand.CompleteNextSplitManually()
        ];
        foreach (AppCommand command in modeRestrictedCommands)
        {
            ApplicationUpdate blocked = controller.HandleSystemEvent(new ControlCommandSystemEvent(command));
            Check.Equal(0, blocked.Effects.Count);
            Check.Equal(0, blocked.DisplayInvalidations.Count);
        }
        Check.Equal(raceAlwaysOnTop, controller.Settings.General.AlwaysOnTop);

        ApplicationUpdate settingsOutsideRoom = controller.HandleSystemEvent(new ControlCommandSystemEvent(
            AppCommand.ApplyTemporarySettings(changedRaceSettings)));
        Check.True(settingsOutsideRoom.Effects.Count > 0);

        ApplicationUpdate package = controller.HandleSystemEvent(new RacePackageSystemEvent("ROOM", "7"));
        Check.True(controller.SystemState.Race.IsInRoom);
        Check.True(controller.SystemState.Race.IsModeEnabled);
        Check.Equal("ROOM", controller.SystemState.Race.RoomCode);
        Check.True(package.Effects.OfType<CancelCreateWorldAutomationEffect>().Any());
        Check.True(package.DisplayInvalidations.Single().Targets.HasFlag(DisplayInvalidationTarget.All));
        foreach (AppCommand command in new[]
        {
            AppCommand.ApplySettings(changedRaceSettings),
            AppCommand.ApplyTemporarySettings(changedRaceSettings)
        })
        {
            ApplicationUpdate blocked = controller.HandleSystemEvent(new ControlCommandSystemEvent(command));
            Check.Equal(0, blocked.Effects.Count);
        }

        ApplicationUpdate raceReset = controller.HandleSystemEvent(new ControlCommandSystemEvent(
            AppCommand.ResetRun(recordStats: false, playResetSound: false, allowDuringRace: true)));
        Check.True(raceReset.Effects.OfType<SubmitRuntimeCommandEffect>().Any());

        ApplicationUpdate otherRoom = controller.HandleSystemEvent(new RaceProgressSystemEvent("OTHER"));
        Check.Equal(0, otherRoom.DisplayInvalidations.Count);
        ApplicationUpdate progress = controller.HandleSystemEvent(new RaceProgressSystemEvent("room"));
        Check.Equal(DisplayInvalidationTarget.RaceLeaderboard, progress.DisplayInvalidations.Single().Targets);

        controller.HandleSystemEvent(new RaceRosterSystemEvent("ROOM", IsInRoom: false));
        Check.False(controller.SystemState.Race.IsInRoom);
        Check.True(controller.SystemState.Race.IsModeEnabled);
        ApplicationUpdate blockedAfterLeavingRoom = controller.HandleSystemEvent(new ControlCommandSystemEvent(
            AppCommand.QueueMenuAction(MenuActionKind.CreateWorld, DateTime.UtcNow)));
        Check.Equal(0, blockedAfterLeavingRoom.Effects.Count);

        controller.HandleSystemEvent(new RaceModeSystemEvent(Enabled: false));
        Check.False(controller.SystemState.Race.IsModeEnabled);
        ApplicationUpdate createWorld = controller.HandleSystemEvent(new ControlCommandSystemEvent(
            AppCommand.QueueMenuAction(MenuActionKind.CreateWorld, DateTime.UtcNow)));
        Check.True(createWorld.Effects.OfType<SubmitRuntimeCommandEffect>().Any());
    }

    private static void WorldEntryTransitionFacts()
    {
        const string underworldFact = "biome:underworld";
        var timer = new SplitTimer();
        var tracker = new SplitTracker();
        tracker.SetDefinitions(
        [
            new SplitDefinition(
                "underworld",
                "Underworld",
                SplitCondition.Fact(underworldFact),
                [],
                [],
                [],
                false)
        ]);
        var controller = new TimerController(
            timer,
            tracker,
            new PendingMenuActionScheduler(),
            TimeSpan.FromSeconds(1));
        long startedAt = 10_000;

        IReadOnlyList<RunEvent> entryEvents = controller.Tick(
            Snapshot(CoreAndRunTests.Facts((underworldFact, true)), enteredWorld: true),
            startedAt);

        Check.Equal(1, entryEvents.Count);
        Check.Equal(RunEventKind.RunStarted, entryEvents[0].Kind);
        Check.Equal(SplitTimerPhase.Running, timer.Phase);
        Check.Equal(0, tracker.CurrentIndex);
        Check.False(tracker.Statuses[0].IsSkipped);
        Check.False(tracker.Statuses[0].IsCompleted);

        IReadOnlyList<RunEvent> stableSpawnEvents = controller.Tick(
            Snapshot(CoreAndRunTests.Facts((underworldFact, false))),
            startedAt + TestTiming.Timestamp(TimeSpan.FromMilliseconds(20)));

        Check.Equal(0, stableSpawnEvents.Count);
        Check.Equal(0, tracker.CurrentIndex);
        Check.False(tracker.Statuses[0].IsSkipped);
        Check.False(tracker.Statuses[0].IsCompleted);

        IReadOnlyList<RunEvent> underworldEvents = controller.Tick(
            Snapshot(CoreAndRunTests.Facts((underworldFact, true))),
            startedAt + TestTiming.Timestamp(TimeSpan.FromSeconds(5)));

        Check.True(underworldEvents.Any(item => item.Kind == RunEventKind.SplitCompleted));
        Check.True(underworldEvents.Any(item => item.Kind == RunEventKind.RunCompleted));
        Check.True(tracker.Statuses[0].IsCompleted);
        Check.Equal(TimeSpan.FromSeconds(5), tracker.Statuses[0].Time);
    }

    private static void ManualSplitJourney()
    {
        const string firstFact = "manual:first";
        const string secondFact = "manual:second";
        const string thirdFact = "manual:third";
        const string nextFact = "manual:next";
        var processor = new WatcherRuntimeProcessor(TimeSpan.FromSeconds(1));
        processor.SetDefinitions(
        [
            new SplitDefinition(
                "advanced",
                "Advanced",
                SplitCondition.AtLeast(
                [
                    SplitCondition.Fact(firstFact),
                    SplitCondition.Fact(secondFact),
                    SplitCondition.Fact(thirdFact)
                ],
                requiredCount: 2),
                [],
                [],
                []),
            new SplitDefinition(
                "next",
                "Next",
                SplitCondition.Fact(nextFact),
                [],
                [],
                [])
        ]);

        long startedAt = 20_000;
        processor.Tick(
            Snapshot(CoreAndRunTests.Facts(), enteredWorld: true),
            startedAt,
            []);
        processor.Tick(
            Snapshot(CoreAndRunTests.Facts((firstFact, true), (secondFact, false), (thirdFact, false))),
            startedAt + TestTiming.Timestamp(TimeSpan.FromSeconds(1)),
            []);

        long manualTimestamp = startedAt + TestTiming.Timestamp(TimeSpan.FromSeconds(2));
        IReadOnlyList<RunEvent> manualEvents = processor.ApplyCommand(
            RuntimeCommand.CompleteNextSplitManually(),
            manualTimestamp);
        Check.Equal(1, manualEvents.Count);
        Check.Equal(RunEventKind.SplitCompleted, manualEvents[0].Kind);

        RuntimeProcessorTickResult manualTick = processor.Tick(
            Snapshot(CoreAndRunTests.Facts((nextFact, true))),
            manualTimestamp,
            manualEvents);
        SplitStatusSnapshot manuallyCompleted = manualTick.Snapshot.Statuses[0];
        Check.Equal(TimeSpan.FromSeconds(2), manuallyCompleted.Time);
        Check.True(manuallyCompleted.IsManuallyCompleted);
        Check.True(manuallyCompleted.CompletedFactKeys.Contains(firstFact, StringComparer.OrdinalIgnoreCase));
        Check.False(manuallyCompleted.CompletedFactKeys.Contains(secondFact, StringComparer.OrdinalIgnoreCase));
        Check.False(manuallyCompleted.CompletedFactKeys.Contains(thirdFact, StringComparer.OrdinalIgnoreCase));
        Check.False(manualTick.Snapshot.Statuses[1].IsCompleted);

        RuntimeProcessorTickResult nextReadyTick = processor.Tick(
            Snapshot(CoreAndRunTests.Facts((nextFact, false))),
            manualTimestamp + TestTiming.Timestamp(TimeSpan.FromMilliseconds(500)),
            []);
        Check.False(nextReadyTick.Snapshot.Statuses[1].IsCompleted);

        RuntimeProcessorTickResult nextTick = processor.Tick(
            Snapshot(CoreAndRunTests.Facts((nextFact, true))),
            manualTimestamp + TestTiming.Timestamp(TimeSpan.FromSeconds(1)),
            []);
        Check.True(nextTick.Snapshot.Statuses[1].IsCompleted);
        Check.True(nextTick.Events.Any(static item => item.Kind == RunEventKind.RunCompleted));
    }

    private static void ManualSplitStartsIdleRun()
    {
        var processor = new WatcherRuntimeProcessor(TimeSpan.FromSeconds(1));
        processor.SetDefinitions(
        [
            new SplitDefinition(
                "first",
                "First",
                SplitCondition.Fact("manual:first"),
                [],
                [],
                []),
            new SplitDefinition(
                "second",
                "Second",
                SplitCondition.Fact("manual:second"),
                [],
                [],
                [])
        ]);

        const long manualTimestamp = 20_000;
        IReadOnlyList<RunEvent> manualEvents = processor.ApplyCommand(
            RuntimeCommand.CompleteNextSplitManually(),
            manualTimestamp);

        Check.Equal(1, manualEvents.Count);
        Check.Equal(RunEventKind.RunStarted, manualEvents[0].Kind);

        RuntimeProcessorTickResult tick = processor.Tick(
            Snapshot(CoreAndRunTests.Facts()),
            manualTimestamp,
            manualEvents);
        Check.Equal(SplitTimerPhase.Running, tick.Snapshot.TimerPhase);
        Check.False(tick.Snapshot.Statuses[0].IsCompleted);
        Check.False(tick.Snapshot.Statuses[0].IsManuallyCompleted);
        Check.False(tick.Snapshot.Statuses[1].IsCompleted);
        Check.Equal(0, tick.Snapshot.CurrentSplitIndex);

        long splitTimestamp = manualTimestamp + TestTiming.Timestamp(TimeSpan.FromSeconds(2));
        IReadOnlyList<RunEvent> splitEvents = processor.ApplyCommand(
            RuntimeCommand.CompleteNextSplitManually(),
            splitTimestamp);
        Check.Equal(1, splitEvents.Count);
        Check.Equal(RunEventKind.SplitCompleted, splitEvents[0].Kind);

        RuntimeProcessorTickResult splitTick = processor.Tick(
            Snapshot(CoreAndRunTests.Facts()),
            splitTimestamp,
            splitEvents);
        Check.Equal(SplitTimerPhase.Running, splitTick.Snapshot.TimerPhase);
        Check.Equal(TimeSpan.FromSeconds(2), splitTick.Snapshot.Statuses[0].Time);
        Check.True(splitTick.Snapshot.Statuses[0].IsManuallyCompleted);
        Check.Equal(1, splitTick.Snapshot.CurrentSplitIndex);
    }

    private static void RunFinalizationJourney()
    {
        AppSettings settings = AppSettingsDefaults.Create();
        settings.Route.SplitRoute =
        [
            new SplitRouteEntry
            {
                Id = "finalization-test",
                DisplayName = "Finalization Test",
                Condition = SplitCondition.Fact("event:finalization-test")
            }
        ];
        settings.Comparison.AutoUpdatePersonalBestData = true;
        settings.Comparison.AskBeforeUpdatingPersonalBestData = true;
        SettingsNormalizer.Normalize(settings);

        var controller = new ApplicationController(settings, new SettingsSnapshotFactory());
        SplitDefinition definition = controller.Definitions.Single();
        var completedStatus = new SplitStatusSnapshot(
            definition,
            TimeSpan.FromSeconds(5),
            IsSkipped: false,
            CompletedFactKeys: ["event:finalization-test"]);
        PublishRuntimeStatuses(controller, [completedStatus]);

        ApplicationUpdate reset = controller.HandleSystemEvent(new ControlCommandSystemEvent(
            AppCommand.ResetRun(recordStats: true, playResetSound: false)));
        Check.Equal(1, reset.Effects.OfType<RecordRunStatisticsEffect>().Count());
        FinalizePersonalBestEffect failedPersistence = reset.Effects.OfType<FinalizePersonalBestEffect>().Single();
        Check.True(failedPersistence.Plan.RequiresConfirmation);
        Check.True(failedPersistence.Plan.SegmentSnapshot is not null);
        string segmentKey = failedPersistence.Plan.SegmentBestValues!.Keys.Single();
        string originalSegmentValue = controller.BaseSettings.Comparison.PersonalBestSegmentTimes[segmentKey];

        OperationResult snapshotFailure = OperationResult.Failure("Injected personal best snapshot failure.");
        ApplicationUpdate failed = controller.HandleSystemEvent(new PersonalBestFinalizationSystemEvent(
            new PersonalBestFinalizationResult(
                failedPersistence.Plan.PlanId,
                Approved: true,
                SegmentSnapshot: null,
                TimeSnapshot: null,
                Failures: [snapshotFailure])));
        Check.Equal(originalSegmentValue, controller.BaseSettings.Comparison.PersonalBestSegmentTimes[segmentKey]);
        Check.Equal(0, failed.Effects.OfType<SaveSettingsEffect>().Count());
        Check.Equal(1, failed.Effects.OfType<ShowPersistenceFailureEffect>().Count());

        PublishRuntimeStatuses(controller, [completedStatus]);
        ApplicationUpdate retryReset = controller.HandleSystemEvent(new ControlCommandSystemEvent(
            AppCommand.ResetRun(recordStats: true, playResetSound: false)));
        FinalizePersonalBestEffect successfulPersistence =
            retryReset.Effects.OfType<FinalizePersonalBestEffect>().Single();
        PersonalBestSnapshotRequest request = successfulPersistence.Plan.SegmentSnapshot!;
        var snapshot = new ReferenceSplitSet
        {
            Name = "Finalization_Test_Snapshot",
            Splits = new Dictionary<string, string>(
                request.Splits,
                StringComparer.OrdinalIgnoreCase)
        };
        ApplicationUpdate succeeded = controller.HandleSystemEvent(new PersonalBestFinalizationSystemEvent(
            new PersonalBestFinalizationResult(
                successfulPersistence.Plan.PlanId,
                Approved: true,
                snapshot,
                TimeSnapshot: null,
                Failures: [])));

        Check.Equal(
            successfulPersistence.Plan.SegmentBestValues![segmentKey],
            controller.BaseSettings.Comparison.PersonalBestSegmentTimes[segmentKey]);
        Check.Equal(snapshot.Name, controller.BaseSettings.Comparison.ActivePersonalBestSegmentSet);
        Check.Equal(1, succeeded.Effects.OfType<SaveSettingsEffect>().Count());
        Check.True(succeeded.DisplayInvalidations.Any(
            item => item.Level == DisplayRefreshLevel.SplitProgress));
    }

    private static void PublishRuntimeStatuses(
        ApplicationController controller,
        IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        var runtimeSnapshot = new RuntimeRunSnapshot(
            new SplitTimerState(SplitTimerPhase.Paused, TimeSpan.FromSeconds(5), 0),
            statuses,
            statuses.Count,
            ObservedTimestamp: 0,
            StatusHash: 1);
        controller.HandleSystemEvent(new RuntimeWatcherSystemEvent(new WatcherPollNotification(
            Snapshot(CoreAndRunTests.Facts(("event:finalization-test", true))),
            Snapshot(CoreAndRunTests.Facts(("event:finalization-test", false))),
            TerrariaSplit.Application.Diagnostics.TerrariaWatcherDiagnosticsDefaults.Empty,
            runtimeSnapshot,
            RunEvents: [],
            RuntimeCommandSequence: controller.MinimumAcceptedRuntimeCommandSequence,
            CompletedTimestamp: 0,
            Error: null)));
    }

    private static TerrariaWatchSnapshot Snapshot(
        TerrariaGameFacts facts,
        bool enteredWorld = false)
    {
        return new TerrariaWatchSnapshot(
            IsAttached: true,
            ProcessId: 1,
            IsReady: true,
            IsGameMenu: false,
            facts,
            TerrariaWorldGenerationState.Unknown,
            enteredWorld,
            Status: "In world");
    }
}

