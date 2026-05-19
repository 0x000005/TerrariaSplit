using TerrariaSplit;
using System.Diagnostics;

namespace TerrariaSplit.Tests;

internal static class HotkeyTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("TimerController consumes menu hotkeys only on menu", TimerControllerConsumesMenuHotkeysOnlyOnMenu);
        yield return ("TimerController records automatic events at observed timestamps", TimerControllerRecordsAutomaticEventsAtObservedTimestamps);
        yield return ("BossSplitTracker skips initially defeated bosses after delayed state resolution", BossSplitTrackerSkipsInitiallyDefeatedBossesAfterDelayedStateResolution);
        yield return ("BossSplitTracker completes bosses after initial incomplete state", BossSplitTrackerCompletesBossesAfterInitialIncompleteState);
    }

    private static void TimerControllerConsumesMenuHotkeysOnlyOnMenu()
    {
        var controller = new TimerController(
            new SplitTimer(),
            new BossSplitTracker(),
            new PendingMenuHotkeyScheduler(),
            TimeSpan.FromSeconds(1));
        DateTime requestedAtUtc = DateTime.UtcNow;

        TimerControllerTickResult inWorldResult = controller.Tick(
            TestSnapshots.Terraria(isGameMenu: false),
            [new TimerHotkeyRequest(TimerHotkeyAction.CreateWorld, requestedAtUtc)]);
        TestAssert.Equal(null, inWorldResult.RequestedMenuAction);

        TimerControllerTickResult menuResult = controller.Tick(TestSnapshots.Terraria(isGameMenu: true), []);
        TestAssert.Equal(MenuHotkeyActionKind.CreateWorld, menuResult.RequestedMenuAction);

        TimerControllerTickResult resetResult = controller.Tick(
            TestSnapshots.Terraria(isGameMenu: true),
            [new TimerHotkeyRequest(TimerHotkeyAction.Reset, DateTime.UtcNow)]);
        TestAssert.Equal(MenuHotkeyActionKind.Reset, resetResult.RequestedMenuAction);

        TimerControllerTickResult enterWorldResult = controller.Tick(
            TestSnapshots.Terraria(isGameMenu: true),
            [new TimerHotkeyRequest(TimerHotkeyAction.PracticeWorld, DateTime.UtcNow)]);
        TestAssert.Equal(MenuHotkeyActionKind.PracticeWorld, enterWorldResult.RequestedMenuAction);
    }

    private static void BossSplitTrackerSkipsInitiallyDefeatedBossesAfterDelayedStateResolution()
    {
        BossSplitTracker tracker = CreateSingleBossTracker();
        var controller = new TimerController(
            new SplitTimer(),
            tracker,
            new PendingMenuHotkeyScheduler(),
            TimeSpan.FromSeconds(1));

        TimerControllerTickResult startResult = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: TerrariaBossStates.Unknown,
                enteredWorld: true),
            []);
        TestAssert.Equal(true, startResult.RunStarted);
        TestAssert.Equal(null, startResult.CompletedSplitIndex);

        TimerControllerTickResult resolvedResult = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateSkeletronState(true)),
            []);
        TestAssert.Equal(null, resolvedResult.CompletedSplitIndex);
        TestAssert.Equal(true, tracker.Statuses[0].IsSkipped);
        TestAssert.Equal(1, tracker.CurrentIndex);
    }

    private static void BossSplitTrackerCompletesBossesAfterInitialIncompleteState()
    {
        BossSplitTracker tracker = CreateSingleBossTracker();
        var controller = new TimerController(
            new SplitTimer(),
            tracker,
            new PendingMenuHotkeyScheduler(),
            TimeSpan.FromSeconds(1));

        _ = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: TerrariaBossStates.Unknown,
                enteredWorld: true),
            []);
        TimerControllerTickResult initialStateResult = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateSkeletronState(false)),
            []);
        TestAssert.Equal(null, initialStateResult.CompletedSplitIndex);

        TimerControllerTickResult completedResult = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateSkeletronState(true)),
            []);
        TestAssert.Equal(0, completedResult.CompletedSplitIndex);
        TestAssert.Equal(false, tracker.Statuses[0].IsSkipped);
    }

    private static void TimerControllerRecordsAutomaticEventsAtObservedTimestamps()
    {
        BossSplitTracker tracker = CreateSingleBossTracker();
        var timer = new SplitTimer();
        var controller = new TimerController(
            timer,
            tracker,
            new PendingMenuHotkeyScheduler(),
            TimeSpan.FromSeconds(1));

        long startTimestamp = 1_000_000;
        long initialIncompleteTimestamp = startTimestamp + Stopwatch.Frequency / 10;
        long completionTimestamp = startTimestamp + Stopwatch.Frequency / 4;

        TimerControllerTickResult startResult = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: TerrariaBossStates.Unknown,
                enteredWorld: true),
            startTimestamp,
            []);
        TestAssert.Equal(true, startResult.RunStarted);

        _ = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateSkeletronState(false)),
            initialIncompleteTimestamp,
            []);

        TimerControllerTickResult completionResult = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateSkeletronState(true)),
            completionTimestamp,
            []);

        TestAssert.Equal(0, completionResult.CompletedSplitIndex);
        TimeSpan expected = TimeSpan.FromSeconds((completionTimestamp - startTimestamp) / (double)Stopwatch.Frequency);
        TestAssert.Equal(expected, tracker.Statuses[0].Time);
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
