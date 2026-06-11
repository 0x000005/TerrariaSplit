using TerrariaSplit;
using System.Diagnostics;

namespace TerrariaSplit.Tests;

internal static class HotkeyTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("Hotkey command mapper routes hotkeys into app commands", HotkeyCommandMapperRoutesHotkeysIntoAppCommands);
        yield return ("ApplicationController maps input commands to effects", ApplicationControllerMapsInputCommandsToEffects);
        yield return ("TimerController consumes queued menu actions only on menu", TimerControllerConsumesQueuedMenuActionsOnlyOnMenu);
        yield return ("TimerController records automatic events at observed timestamps", TimerControllerRecordsAutomaticEventsAtObservedTimestamps);
        yield return ("BossSplitTracker skips initially defeated bosses after delayed state resolution", BossSplitTrackerSkipsInitiallyDefeatedBossesAfterDelayedStateResolution);
        yield return ("BossSplitTracker completes bosses after initial incomplete state", BossSplitTrackerCompletesBossesAfterInitialIncompleteState);
    }

    private static void HotkeyCommandMapperRoutesHotkeysIntoAppCommands()
    {
        DateTime requestedAtUtc = DateTime.UtcNow;

        TestAssert.Equal(true, HotkeyCommandMapper.TryMap(
            HotkeyAction.PauseResume,
            requestedAtUtc,
            createWorldRunning: false,
            enterWorldRunning: false,
            out AppCommand pauseCommand));
        TestAssert.Equal(AppCommandKind.TogglePause, pauseCommand.Kind);

        TestAssert.Equal(true, HotkeyCommandMapper.TryMap(
            HotkeyAction.MouseClickThrough,
            requestedAtUtc,
            createWorldRunning: false,
            enterWorldRunning: false,
            out AppCommand clickThroughCommand));
        TestAssert.Equal(AppCommandKind.ToggleMouseClickThrough, clickThroughCommand.Kind);

        TestAssert.Equal(true, HotkeyCommandMapper.TryMap(
            HotkeyAction.Reset,
            requestedAtUtc,
            createWorldRunning: false,
            enterWorldRunning: false,
            out AppCommand resetCommand));
        TestAssert.Equal(AppCommandKind.QueueMenuAction, resetCommand.Kind);
        TestAssert.Equal(MenuActionKind.Reset, resetCommand.MenuAction);

        TestAssert.Equal(true, HotkeyCommandMapper.TryMap(
            HotkeyAction.CreateWorld,
            requestedAtUtc,
            createWorldRunning: false,
            enterWorldRunning: false,
            out AppCommand createWorldCommand));
        TestAssert.Equal(AppCommandKind.QueueMenuAction, createWorldCommand.Kind);
        TestAssert.Equal(MenuActionKind.CreateWorld, createWorldCommand.MenuAction);

        TestAssert.Equal(true, HotkeyCommandMapper.TryMap(
            HotkeyAction.PracticeWorld,
            requestedAtUtc,
            createWorldRunning: false,
            enterWorldRunning: false,
            out AppCommand practiceWorldCommand));
        TestAssert.Equal(AppCommandKind.QueueMenuAction, practiceWorldCommand.Kind);
        TestAssert.Equal(MenuActionKind.PracticeWorld, practiceWorldCommand.MenuAction);

        TestAssert.Equal(true, HotkeyCommandMapper.TryMap(
            HotkeyAction.CreateWorld,
            requestedAtUtc,
            createWorldRunning: true,
            enterWorldRunning: false,
            out AppCommand cancelCreateCommand));
        TestAssert.Equal(AppCommandKind.CancelCreateWorld, cancelCreateCommand.Kind);

        TestAssert.Equal(false, HotkeyCommandMapper.TryMap(
            HotkeyAction.Reset,
            requestedAtUtc,
            createWorldRunning: true,
            enterWorldRunning: false,
            out _));

        TestAssert.Equal(true, HotkeyCommandMapper.TryMap(
            HotkeyAction.PracticeWorld,
            requestedAtUtc,
            createWorldRunning: false,
            enterWorldRunning: true,
            out AppCommand cancelEnterCommand));
        TestAssert.Equal(AppCommandKind.CancelEnterWorld, cancelEnterCommand.Kind);
    }

    private static void ApplicationControllerMapsInputCommandsToEffects()
    {
        DateTime requestedAtUtc = DateTime.UtcNow;
        var controller = new ApplicationController(new AppSettings(), _ => true);

        ApplicationUpdate resetUpdate = controller.HandleCommand(
            AppCommand.QueueMenuAction(MenuActionKind.Reset, requestedAtUtc));
        ApplicationEffect resetEffect = resetUpdate.Effects.Single();
        TestAssert.Equal(ApplicationEffectKind.SubmitRuntimeCommand, resetEffect.Kind);
        TestAssert.Equal(RuntimeCommandKind.QueueMenuAction, resetEffect.RuntimeCommand?.Kind);
        TestAssert.Equal(MenuActionKind.Reset, resetEffect.RuntimeCommand?.MenuAction);

        ApplicationUpdate clickThroughUpdate = controller.HandleCommand(AppCommand.ToggleMouseClickThrough());
        TestAssert.Equal(ApplicationEffectKind.ToggleMouseClickThrough, clickThroughUpdate.Effects.Single().Kind);

        ApplicationUpdate pyramidFilterUpdate = controller.HandleCommand(AppCommand.TogglePyramidFilter());
        TestAssert.Equal(true, controller.Settings.AutoCreate.EnablePyramidFilter);
        TestAssert.Equal(2, pyramidFilterUpdate.Effects.Count);
        TestAssert.Equal(ApplicationEffectKind.SaveSettings, pyramidFilterUpdate.Effects[0].Kind);
        TestAssert.Equal(ApplicationEffectKind.ApplySettingsToShell, pyramidFilterUpdate.Effects[1].Kind);
        TestAssert.Equal(false, pyramidFilterUpdate.Effects.Any(effect =>
            effect.Kind == ApplicationEffectKind.SubmitRuntimeCommand));

        ApplicationUpdate cancelCreateUpdate = controller.HandleCommand(AppCommand.CancelCreateWorld());
        TestAssert.Equal(ApplicationEffectKind.CancelCreateWorldAutomation, cancelCreateUpdate.Effects.Single().Kind);

        ApplicationUpdate cancelEnterUpdate = controller.HandleCommand(AppCommand.CancelEnterWorld());
        TestAssert.Equal(ApplicationEffectKind.CancelEnterWorldAutomation, cancelEnterUpdate.Effects.Single().Kind);
    }

    private static void TimerControllerConsumesQueuedMenuActionsOnlyOnMenu()
    {
        var controller = new TimerController(
            new SplitTimer(),
            new BossSplitTracker(),
            new PendingMenuActionScheduler(),
            TimeSpan.FromSeconds(1));
        DateTime requestedAtUtc = DateTime.UtcNow;

        controller.QueuePendingMenuAction(MenuActionKind.CreateWorld, requestedAtUtc);

        IReadOnlyList<RunEvent> inWorldEvents = controller.Tick(TestSnapshots.Terraria(isGameMenu: false));
        TestAssert.Equal(null, GetMenuAction(inWorldEvents));

        IReadOnlyList<RunEvent> menuEvents = controller.Tick(TestSnapshots.Terraria(isGameMenu: true));
        TestAssert.Equal(MenuActionKind.CreateWorld, GetMenuAction(menuEvents));

        controller.QueuePendingMenuAction(MenuActionKind.Reset, DateTime.UtcNow);
        IReadOnlyList<RunEvent> resetEvents = controller.Tick(TestSnapshots.Terraria(isGameMenu: true));
        TestAssert.Equal(MenuActionKind.Reset, GetMenuAction(resetEvents));

        controller.QueuePendingMenuAction(MenuActionKind.PracticeWorld, DateTime.UtcNow);
        IReadOnlyList<RunEvent> enterWorldEvents = controller.Tick(TestSnapshots.Terraria(isGameMenu: true));
        TestAssert.Equal(MenuActionKind.PracticeWorld, GetMenuAction(enterWorldEvents));
    }

    private static void BossSplitTrackerSkipsInitiallyDefeatedBossesAfterDelayedStateResolution()
    {
        BossSplitTracker tracker = CreateSingleBossTracker();
        var controller = new TimerController(
            new SplitTimer(),
            tracker,
            new PendingMenuActionScheduler(),
            TimeSpan.FromSeconds(1));

        IReadOnlyList<RunEvent> startEvents = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: TerrariaBossStates.Unknown,
                enteredWorld: true));
        TestAssert.Equal(true, HasEvent(startEvents, RunEventKind.RunStarted));
        TestAssert.Equal(null, GetCompletedSplitIndex(startEvents));

        IReadOnlyList<RunEvent> resolvedEvents = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateSkeletronState(true)));
        TestAssert.Equal(null, GetCompletedSplitIndex(resolvedEvents));
        TestAssert.Equal(true, tracker.Statuses[0].IsSkipped);
        TestAssert.Equal(1, tracker.CurrentIndex);
    }

    private static void BossSplitTrackerCompletesBossesAfterInitialIncompleteState()
    {
        BossSplitTracker tracker = CreateSingleBossTracker();
        var controller = new TimerController(
            new SplitTimer(),
            tracker,
            new PendingMenuActionScheduler(),
            TimeSpan.FromSeconds(1));

        _ = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: TerrariaBossStates.Unknown,
                enteredWorld: true));
        IReadOnlyList<RunEvent> initialStateEvents = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateSkeletronState(false)));
        TestAssert.Equal(null, GetCompletedSplitIndex(initialStateEvents));

        IReadOnlyList<RunEvent> completedEvents = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateSkeletronState(true)));
        TestAssert.Equal(0, GetCompletedSplitIndex(completedEvents));
        TestAssert.Equal(false, tracker.Statuses[0].IsSkipped);
    }

    private static void TimerControllerRecordsAutomaticEventsAtObservedTimestamps()
    {
        BossSplitTracker tracker = CreateSingleBossTracker();
        var timer = new SplitTimer();
        var controller = new TimerController(
            timer,
            tracker,
            new PendingMenuActionScheduler(),
            TimeSpan.FromSeconds(1));

        long startTimestamp = 1_000_000;
        long initialIncompleteTimestamp = startTimestamp + Stopwatch.Frequency / 10;
        long completionTimestamp = startTimestamp + Stopwatch.Frequency / 4;

        IReadOnlyList<RunEvent> startEvents = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: TerrariaBossStates.Unknown,
                enteredWorld: true),
            startTimestamp);
        TestAssert.Equal(true, HasEvent(startEvents, RunEventKind.RunStarted));

        _ = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateSkeletronState(false)),
            initialIncompleteTimestamp);

        IReadOnlyList<RunEvent> completionEvents = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateSkeletronState(true)),
            completionTimestamp);

        TestAssert.Equal(0, GetCompletedSplitIndex(completionEvents));
        TimeSpan expected = TimeSpan.FromSeconds((completionTimestamp - startTimestamp) / (double)Stopwatch.Frequency);
        TestAssert.Equal(expected, tracker.Statuses[0].Time);
    }

    private static bool HasEvent(IReadOnlyList<RunEvent> events, RunEventKind kind)
    {
        return events.Any(runEvent => runEvent.Kind == kind);
    }

    private static MenuActionKind? GetMenuAction(IReadOnlyList<RunEvent> events)
    {
        foreach (RunEvent runEvent in events)
        {
            if (runEvent.Kind == RunEventKind.MenuActionRequested)
            {
                return runEvent.MenuAction;
            }
        }

        return null;
    }

    private static int? GetCompletedSplitIndex(IReadOnlyList<RunEvent> events)
    {
        foreach (RunEvent runEvent in events)
        {
            if (runEvent.Kind == RunEventKind.SplitCompleted)
            {
                return runEvent.SplitIndex;
            }
        }

        return null;
    }

    private static BossSplitTracker CreateSingleBossTracker()
    {
        var tracker = new BossSplitTracker();
        tracker.SetDefinitions([
            new BossSplitDefinition(
                BossSplitDefinitions.Skeletron,
                "Skeletron",
                [BossFlag.Skeletron],
                Array.Empty<string>(),
                Array.Empty<string>(),
                [BossSplitDefinitions.Skeletron])
        ]);
        return tracker;
    }

    private static TerrariaBossStates CreateSkeletronState(bool defeated)
    {
        return new TerrariaBossStates(
            defeated,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }
}
