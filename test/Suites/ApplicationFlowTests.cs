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
        yield return TestCase.Sync("cumulative personal best uses the final enabled main split and preserves eligibility and snapshot commit rules", TestSuite.Flow, CumulativePersonalBestEndpointJourney);
    }

    private static void CommandJourney()
    {
        AppSettings settings = AppSettingsDefaults.Create();
        var settingsSnapshots = new SettingsSnapshotFactory();
        var controller = new ApplicationController(settings, settingsSnapshots);
        Check.False(controller.SystemState.Race.IsModeEnabled);

        ApplicationUpdate penaltyOutsideRace = controller.HandleSystemEvent(
            new RaceTimePenaltySystemEvent(TimeSpan.FromMinutes(1)));
        Check.Equal(0, penaltyOutsideRace.Effects.Count);

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
        ApplicationUpdate penaltyOutsideRoom = controller.HandleSystemEvent(
            new RaceTimePenaltySystemEvent(TimeSpan.FromMinutes(1)));
        Check.Equal(0, penaltyOutsideRoom.Effects.Count);

        ApplicationUpdate settingsOutsideRoom = controller.HandleSystemEvent(new ControlCommandSystemEvent(
            AppCommand.ApplyTemporarySettings(changedRaceSettings)));
        Check.True(settingsOutsideRoom.Effects.Count > 0);

        ApplicationUpdate package = controller.HandleSystemEvent(new RacePackageSystemEvent("ROOM", "7"));
        Check.True(controller.SystemState.Race.IsInRoom);
        Check.True(controller.SystemState.Race.IsModeEnabled);
        Check.Equal("ROOM", controller.SystemState.Race.RoomCode);
        Check.True(package.Effects.OfType<CancelCreateWorldAutomationEffect>().Any());
        Check.True(package.DisplayInvalidations.Single().Targets.HasFlag(DisplayInvalidationTarget.All));
        ApplicationUpdate racePenalty = controller.HandleSystemEvent(
            new RaceTimePenaltySystemEvent(TimeSpan.FromMinutes(1)));
        Check.True(racePenalty.Effects
            .OfType<SubmitRuntimeCommandEffect>()
            .Any(effect =>
                effect.Command.Kind == RuntimeCommandKind.AddElapsedPenalty &&
                effect.Command.Time == TimeSpan.FromMinutes(1)));
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
        Check.Equal(
            0,
            controller.HandleSystemEvent(new RaceTimePenaltySystemEvent(TimeSpan.FromMinutes(1))).Effects.Count);

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

        IReadOnlyList<RunEvent> penaltyEvents = processor.ApplyCommand(
            RuntimeCommand.AddElapsedPenalty(TimeSpan.FromSeconds(30)),
            manualTimestamp + TestTiming.Timestamp(TimeSpan.FromMilliseconds(250)));
        Check.Equal(0, penaltyEvents.Count);

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
        Check.Equal(TimeSpan.FromSeconds(33), nextTick.Snapshot.Statuses[1].Time);
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
        Check.True(failedPersistence.Plan.TimeSnapshot is not null);
        string segmentKey = failedPersistence.Plan.SegmentBestValues!.Keys.Single();
        string originalSegmentValue = controller.BaseSettings.Comparison.PersonalBestSegmentTimes[segmentKey];
        string timeKey = failedPersistence.Plan.PersonalBestTimes!.Keys.Single();
        string originalTimeValue = controller.BaseSettings.Comparison.PersonalBestTimes[timeKey];

        OperationResult snapshotFailure = OperationResult.Failure("Injected personal best snapshot failure.");
        ApplicationUpdate failed = controller.HandleSystemEvent(new PersonalBestFinalizationSystemEvent(
            new PersonalBestFinalizationResult(
                failedPersistence.Plan.PlanId,
                Approved: true,
                SegmentSnapshot: null,
                TimeSnapshot: null,
                Failures: [snapshotFailure])));
        Check.Equal(originalSegmentValue, controller.BaseSettings.Comparison.PersonalBestSegmentTimes[segmentKey]);
        Check.Equal(originalTimeValue, controller.BaseSettings.Comparison.PersonalBestTimes[timeKey]);
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
        var timeSnapshot = new ReferenceSplitSet
        {
            Name = "Finalization_Test_Time_Snapshot",
            Splits = new Dictionary<string, string>(successfulPersistence.Plan.TimeSnapshot!.Splits, StringComparer.OrdinalIgnoreCase)
        };
        ApplicationUpdate succeeded = controller.HandleSystemEvent(new PersonalBestFinalizationSystemEvent(
            new PersonalBestFinalizationResult(
                successfulPersistence.Plan.PlanId,
                Approved: true,
                snapshot,
                TimeSnapshot: timeSnapshot,
                Failures: [])));

        Check.Equal(
            successfulPersistence.Plan.SegmentBestValues![segmentKey],
            controller.BaseSettings.Comparison.PersonalBestSegmentTimes[segmentKey]);
        Check.Equal(snapshot.Name, controller.BaseSettings.Comparison.ActivePersonalBestSegmentSet);
        Check.Equal(successfulPersistence.Plan.PersonalBestTimes![timeKey], controller.BaseSettings.Comparison.PersonalBestTimes[timeKey]);
        Check.Equal(timeSnapshot.Name, controller.BaseSettings.Comparison.ActivePersonalBestTimeSet);
        Check.Equal(1, succeeded.Effects.OfType<SaveSettingsEffect>().Count());
        Check.True(succeeded.DisplayInvalidations.Any(
            item => item.Level == DisplayRefreshLevel.SplitProgress));
    }

    private static void CumulativePersonalBestEndpointJourney()
    {
        (string Name, bool MoonLordFirst, int First, int Finish, int? PreviousFinish, bool Update)[] cases =
        [
            ("route without Moon Lord", false, 10, 20, 30, true),
            ("first cumulative record", false, 10, 20, null, true),
            ("Moon Lord improved but route finish did not", true, 8, 35, 30, false),
            ("Moon Lord slowed but route finish improved", true, 12, 25, 30, true),
            ("equal route finish", false, 10, 30, 30, false),
            ("slower route finish", false, 10, 35, 30, false)
        ];
        foreach (var sample in cases)
        {
            AppSettings settings = CreateFinalMainRouteSettings(sample.MoonLordFirst);
            foreach (SplitConditionDataRow row in SplitConditionDataRows.ForSplit(settings, "pb-finish"))
                settings.Comparison.PersonalBestTimes[row.Key] = sample.PreviousFinish is int previous
                    ? TimeText.FormatRecord(TimeSpan.FromSeconds(previous)) : string.Empty;
            SplitStatusSnapshot[] statuses = CreateFinalMainStatuses(settings, sample.First, sample.Finish);
            Check.Equal(3, statuses.Length);
            var finalizer = new RunFinalizer();
            PersonalBestFinalizationPlan? plan = finalizer.CreatePersonalBestPlan(1, settings, settings, statuses);
            if ((plan?.TimeSnapshot is not null) != sample.Update)
                throw new InvalidOperationException($"{sample.Name}: cumulative PB update did not match the final main split comparison.");
            if (!sample.Update) continue;

            PersonalBestSnapshotRequest request = plan!.TimeSnapshot!;
            Check.Equal("Route Finish", request.BossName);
            Check.Equal(TimeText.FormatRecord(TimeSpan.FromSeconds(sample.Finish)), request.NewTime);
            Check.Equal(sample.PreviousFinish is int oldFinish ? TimeText.FormatRecord(TimeSpan.FromSeconds(oldFinish)) : string.Empty, request.PreviousTime);
            SplitDefinition finish = statuses.Single(status => status.Definition.Id == "pb-finish").Definition;
            Check.True(SplitConditionDataRows.TryGetSplitTime(settings, request.Splits, finish, out TimeSpan savedFinish));
            Check.Equal(TimeSpan.FromSeconds(sample.Finish), savedFinish);
            Check.Equal(2, request.Splits.Count);

            var segmentSnapshot = plan.SegmentSnapshot is { } segment ? new ReferenceSplitSet
            {
                Name = "Segment Snapshot", Splits = new(segment.Splits, StringComparer.OrdinalIgnoreCase)
            } : null;
            var oldValues = new Dictionary<string, string>(settings.Comparison.PersonalBestTimes);
            Check.False(RunFinalizer.TryApplySuccessfulPlan(settings, plan,
                new(plan.PlanId, Approved: true, segmentSnapshot, TimeSnapshot: null, Failures: []), out var failures));
            Check.True(failures.Count > 0);
            Check.Sequence(oldValues.OrderBy(pair => pair.Key), settings.Comparison.PersonalBestTimes.OrderBy(pair => pair.Key));
            var timeSnapshot = new ReferenceSplitSet { Name = "Time Snapshot", Splits = new(request.Splits, StringComparer.OrdinalIgnoreCase) };
            Check.True(RunFinalizer.TryApplySuccessfulPlan(settings, plan,
                new(plan.PlanId, Approved: true, segmentSnapshot, timeSnapshot, Failures: []), out failures));
            Check.Equal(0, failures.Count);
            Check.Sequence(request.Splits.OrderBy(pair => pair.Key), settings.Comparison.PersonalBestTimes.OrderBy(pair => pair.Key));
        }

        AppSettings moonLordFinish = CreateFinalMainRouteSettings(moonLordFirst: true);
        (moonLordFinish.Route.SplitRoute[0], moonLordFinish.Route.SplitRoute[2]) =
            (moonLordFinish.Route.SplitRoute[2], moonLordFinish.Route.SplitRoute[0]);
        SettingsNormalizer.Normalize(moonLordFinish);
        foreach (SplitConditionDataRow row in SplitConditionDataRows.ForSplit(moonLordFinish, "pb-first"))
            moonLordFinish.Comparison.PersonalBestTimes[row.Key] = TimeText.FormatRecord(TimeSpan.FromSeconds(10));
        PersonalBestSnapshotRequest moonLordRequest = new RunFinalizer().CreatePersonalBestPlan(1, moonLordFinish, moonLordFinish,
            CreateFinalMainStatuses(moonLordFinish, 8, 5))!.TimeSnapshot!;
        Check.Equal(TimeText.FormatRecord(TimeSpan.FromSeconds(8)), moonLordRequest.NewTime);

        AppSettings eligible = CreateFinalMainRouteSettings(moonLordFirst: false);
        SplitStatusSnapshot[] complete = CreateFinalMainStatuses(eligible, 10, 20);
        var eligibilityFinalizer = new RunFinalizer();
        foreach (int index in Enumerable.Range(0, complete.Length).Where(index => !complete[index].Definition.IsAttached))
        {
            foreach (bool skipped in new[] { false, true })
            {
                SplitStatusSnapshot[] incomplete = complete.ToArray();
                incomplete[index] = incomplete[index] with { Time = skipped ? incomplete[index].Time : null, IsSkipped = skipped };
                Check.True(eligibilityFinalizer.CreatePersonalBestPlan(1, eligible, eligible, incomplete)?.TimeSnapshot is null);
            }
        }
        Check.True(eligibilityFinalizer.CreatePersonalBestPlan(1, eligible, eligible, [])?.TimeSnapshot is null);
        Check.True(eligibilityFinalizer.CreatePersonalBestPlan(1, eligible, eligible,
            complete.Where(status => status.Definition.IsAttached).ToArray())?.TimeSnapshot is null);
        eligible.Comparison.AutoUpdatePersonalBestData = false;
        Check.True(eligibilityFinalizer.CreatePersonalBestPlan(1, eligible, eligible, complete) is null);

        AppSettings multiple = CreateFinalMainRouteSettings(moonLordFirst: false);
        multiple.Route.SplitRoute.Single(entry => entry.Id == "pb-finish").Condition = SplitCondition.All([
            SplitCondition.Fact("event:pb-finish-a"), SplitCondition.Fact("event:pb-finish-b")]);
        SettingsNormalizer.Normalize(multiple);
        var previousTimes = new[] { 25, 30 };
        SplitConditionDataRow[] rows = SplitConditionDataRows.ForSplit(multiple, "pb-finish").ToArray();
        for (int index = 0; index < rows.Length; index++)
            multiple.Comparison.PersonalBestTimes[rows[index].Key] = TimeText.FormatRecord(TimeSpan.FromSeconds(previousTimes[index]));
        SplitStatusSnapshot[] multiStatuses = CreateFinalMainStatuses(multiple, 10, 28);
        int finishIndex = Array.FindIndex(multiStatuses, status => status.Definition.Id == "pb-finish");
        multiStatuses[finishIndex] = multiStatuses[finishIndex] with
        {
            FactCompletionTimes = new Dictionary<string, TimeSpan>
            {
                ["event:pb-finish-a"] = TimeSpan.FromSeconds(20), ["event:pb-finish-b"] = TimeSpan.FromSeconds(28)
            }
        };
        PersonalBestSnapshotRequest multiRequest = eligibilityFinalizer.CreatePersonalBestPlan(1, multiple, multiple, multiStatuses)!.TimeSnapshot!;
        Check.Equal(TimeText.FormatRecord(TimeSpan.FromSeconds(28)), multiRequest.NewTime);
        Check.Equal(TimeText.FormatRecord(TimeSpan.FromSeconds(30)), multiRequest.PreviousTime);
        Check.Equal(TimeText.FormatRecord(TimeSpan.FromSeconds(20)), multiRequest.Splits[rows[0].Key]);
        Check.Equal(TimeText.FormatRecord(TimeSpan.FromSeconds(28)), multiRequest.Splits[rows[1].Key]);
    }

    internal static AppSettings CreateFinalMainRouteSettings(bool moonLordFirst)
    {
        AppSettings settings = AppSettingsDefaults.Create();
        settings.Route.SplitRoute =
        [
            new() { Id = "pb-first", DisplayName = "First Main", Condition = SplitCondition.Fact(
                moonLordFirst ? "boss:moon-lord:defeated" : "event:pb-first") },
            new() { Id = "pb-attached", IsAttached = true, Condition = SplitCondition.Fact("event:pb-attached") },
            new() { Id = "pb-finish", DisplayName = "Route Finish", Condition = SplitCondition.Fact("event:pb-finish") },
            new() { Id = "pb-disabled", Enabled = false, Condition = SplitCondition.Fact("event:pb-disabled") }
        ];
        settings.Comparison.AutoUpdatePersonalBestData = true;
        settings.Comparison.PersonalBestTimes.Clear();
        SettingsNormalizer.Normalize(settings);
        foreach (SplitConditionDataRow row in SplitConditionDataRows.Build(settings))
            settings.Comparison.PersonalBestTimes[row.Key] = TimeText.FormatRecord(TimeSpan.FromSeconds(row.SplitId == "pb-first" ? 10 : 30));
        return settings;
    }

    internal static SplitStatusSnapshot[] CreateFinalMainStatuses(AppSettings settings, int firstSeconds, int? finishSeconds)
    {
        return SplitCatalog.Build(settings).Select(definition =>
        {
            TimeSpan? time = definition.Id == "pb-first" ? TimeSpan.FromSeconds(firstSeconds)
                : definition.Id == "pb-finish" && finishSeconds.HasValue ? TimeSpan.FromSeconds(finishSeconds.Value) : null;
            return new SplitStatusSnapshot(definition, time, IsSkipped: false,
                CompletedFactKeys: time.HasValue ? definition.Condition.GetFactConditions().Select(condition => condition.FactKey).ToArray() : []);
        }).ToArray();
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

