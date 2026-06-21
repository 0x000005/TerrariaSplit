namespace TerrariaSplit.UI;

internal static class HotkeyCommandMapper
{
    public static bool TryMap(
        HotkeyAction action,
        DateTime requestedAtUtc,
        bool createWorldRunning,
        bool enterWorldRunning,
        out AppCommand command)
    {
        command = action switch
        {
            HotkeyAction.PauseResume => AppCommand.TogglePause(),
            HotkeyAction.MouseClickThrough => AppCommand.ToggleMouseClickThrough(),
            HotkeyAction.Reset => AppCommand.QueueMenuAction(MenuActionKind.Reset, requestedAtUtc),
            HotkeyAction.CreateWorld => createWorldRunning
                ? AppCommand.CancelCreateWorld()
                : AppCommand.QueueMenuAction(MenuActionKind.CreateWorld, requestedAtUtc),
            HotkeyAction.PracticeWorld => enterWorldRunning
                ? AppCommand.CancelEnterWorld()
                : AppCommand.QueueMenuAction(MenuActionKind.PracticeWorld, requestedAtUtc),
            _ => null!
        };

        if (command is null)
        {
            return false;
        }

        if (createWorldRunning && action != HotkeyAction.CreateWorld)
        {
            return false;
        }

        if (enterWorldRunning && action != HotkeyAction.PracticeWorld)
        {
            return false;
        }

        return true;
    }
}
