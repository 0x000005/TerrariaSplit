using TerrariaSplit;

namespace TerrariaSplit.Tests;

internal static class HotkeyTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("TimerController consumes menu hotkeys only on menu", TimerControllerConsumesMenuHotkeysOnlyOnMenu);
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
}
