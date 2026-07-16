namespace TerrariaSplit.Tests;

internal static class ApplicationFlowTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("application commands coordinate settings, runtime effects and full display invalidation", TestSuite.Flow, CommandJourney);
        yield return TestCase.Sync("race, job and display events update system state and target only relevant views", TestSuite.Flow, SystemEventJourney);
    }

    private static void CommandJourney()
    {
        using var directory = new TestDirectory();
        var repository = new AppSettingsRepository(new AppContextRuntimeDataPaths(directory.Path));
        AppSettings settings = AppSettingsDefaults.Create();
        var controller = new ApplicationController(settings, _ => true, new StoredSettingsSnapshotFactory(repository));

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
        var controller = new ApplicationController(AppSettingsDefaults.Create(), _ => true, new StoredSettingsSnapshotFactory(repository));

        ApplicationUpdate package = controller.HandleSystemEvent(new RacePackageSystemEvent("ROOM", "7"));
        Check.True(controller.SystemState.Race.IsInRoom);
        Check.Equal("ROOM", controller.SystemState.Race.RoomCode);
        Check.True(package.Effects.OfType<CancelCreateWorldAutomationEffect>().Any());
        Check.True(package.Effects.OfType<CancelEnterWorldAutomationEffect>().Any());
        Check.True(package.Effects
            .OfType<SubmitRuntimeCommandEffect>()
            .Any(effect => effect.Command.Kind == RuntimeCommandKind.ClearPendingMenuActions));
        Check.True(package.DisplayInvalidations.Single().Targets.HasFlag(DisplayInvalidationTarget.All));

        AppSettings changedRaceSettings = repository.Clone(controller.Settings);
        changedRaceSettings.General.AlwaysOnTop = !changedRaceSettings.General.AlwaysOnTop;
        bool raceAlwaysOnTop = controller.Settings.General.AlwaysOnTop;
        AppCommand[] restrictedCommands =
        [
            AppCommand.TogglePause(),
            AppCommand.ResetRun(recordStats: true, playResetSound: true),
            AppCommand.QueueMenuAction(MenuActionKind.Reset, DateTime.UtcNow),
            AppCommand.QueueMenuAction(MenuActionKind.CreateWorld, DateTime.UtcNow),
            AppCommand.QueueMenuAction(MenuActionKind.PracticeWorld, DateTime.UtcNow),
            AppCommand.EditPracticeSplitTime(0, TimeSpan.FromSeconds(1)),
            AppCommand.EditPracticeTotalTime(TimeSpan.FromSeconds(1)),
            AppCommand.ApplySettings(changedRaceSettings),
            AppCommand.ApplyTemporarySettings(changedRaceSettings)
        ];
        foreach (AppCommand command in restrictedCommands)
        {
            ApplicationUpdate blocked = controller.HandleSystemEvent(new ControlCommandSystemEvent(command));
            Check.Equal(0, blocked.Effects.Count);
            Check.Equal(0, blocked.DisplayInvalidations.Count);
        }
        Check.Equal(raceAlwaysOnTop, controller.Settings.General.AlwaysOnTop);

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
        ApplicationUpdate createWorld = controller.HandleSystemEvent(new ControlCommandSystemEvent(
            AppCommand.QueueMenuAction(MenuActionKind.CreateWorld, DateTime.UtcNow)));
        Check.True(createWorld.Effects.OfType<SubmitRuntimeCommandEffect>().Any());
        controller.HandleSystemEvent(new DisplaySystemEvent(DisplayInvalidation.For(DisplayRefreshLevel.Frame, DisplayInvalidationTarget.TimerOverlay)));
        Check.Equal(DisplayInvalidationTarget.TimerOverlay, controller.SystemState.Display.ActiveTargets);
    }
}

