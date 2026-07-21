namespace TerrariaSplit.Tests;

internal static class ApplicationFlowTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("application commands coordinate settings, runtime effects and full display invalidation", TestSuite.Flow, CommandJourney);
        yield return TestCase.Sync("race, job and display events update system state and target only relevant views", TestSuite.Flow, SystemEventJourney);
        yield return TestCase.Sync("world-entry transition facts cannot skip or complete a split", TestSuite.Flow, WorldEntryTransitionFacts);
    }

    private static void CommandJourney()
    {
        using var directory = new TestDirectory();
        var repository = new AppSettingsRepository(new AppContextRuntimeDataPaths(directory.Path));
        AppSettings settings = AppSettingsDefaults.Create();
        var controller = new ApplicationController(settings, _ => true, new StoredSettingsSnapshotFactory(repository));
        Check.False(controller.SystemState.Race.IsModeEnabled);

        ApplicationUpdate idlePause = controller.HandleSystemEvent(new ControlCommandSystemEvent(AppCommand.TogglePause()));
        Check.Equal(0, idlePause.Effects.Count);
        ApplicationUpdate clickThrough = controller.HandleSystemEvent(new ControlCommandSystemEvent(AppCommand.ToggleMouseClickThrough()));
        Check.True(clickThrough.Effects.OfType<ToggleMouseClickThroughEffect>().Any());

        bool previousCheats = controller.Settings.Automation.AutoCreate.EnableCheats;
        ApplicationUpdate cheats = controller.HandleSystemEvent(new ControlCommandSystemEvent(AppCommand.ToggleCheats()));
        Check.Equal(!previousCheats, controller.Settings.Automation.AutoCreate.EnableCheats);
        Check.True(cheats.Effects.OfType<SaveSettingsEffect>().Any());
        Check.True(cheats.Effects.OfType<ApplySettingsToShellEffect>().Any());

        AppSettings changed = repository.Clone(controller.Settings);
        changed.General.AlwaysOnTop = !changed.General.AlwaysOnTop;
        ApplicationUpdate applied = controller.HandleSystemEvent(new ControlCommandSystemEvent(AppCommand.ApplySettings(changed)));
        Check.Equal(changed.General.AlwaysOnTop, controller.Settings.General.AlwaysOnTop);
        Check.True(applied.Effects.OfType<SaveSettingsEffect>().Any());
        Check.True(applied.Effects.OfType<SubmitRuntimeCommandEffect>().Count() >= 2);
        Check.True(applied.DisplayInvalidations.Any(item => item.Level == DisplayRefreshLevel.FullRebuild));
    }

    private static void SystemEventJourney()
    {
        using var directory = new TestDirectory();
        var repository = new AppSettingsRepository(new AppContextRuntimeDataPaths(directory.Path));
        AppSettings settings = AppSettingsDefaults.Create();
        var controller = new ApplicationController(settings, _ => true, new StoredSettingsSnapshotFactory(repository));
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

        AppSettings changedRaceSettings = repository.Clone(controller.Settings);
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
            AppCommand.EditPracticeTotalTime(TimeSpan.FromSeconds(1))
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

        controller.HandleSystemEvent(new JobProgressSystemEvent("world", 140));
        Check.Equal(100, controller.SystemState.Jobs.ProgressPercent);
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
        controller.HandleSystemEvent(new DisplaySystemEvent(DisplayInvalidation.For(DisplayRefreshLevel.Frame, DisplayInvalidationTarget.TimerOverlay)));
        Check.Equal(DisplayInvalidationTarget.TimerOverlay, controller.SystemState.Display.ActiveTargets);
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

