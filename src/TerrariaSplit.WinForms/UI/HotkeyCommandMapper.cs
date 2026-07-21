namespace TerrariaSplit.UI;

internal static class HotkeyCommandMapper
{
    public static bool TryMap(
        HotkeyAction action,
        DateTime requestedAtUtc,
        bool createWorldRunning,
        bool enterWorldRunning,
        bool isRaceModeEnabled,
        bool isInRaceRoom,
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
            HotkeyAction.ManualSplit => AppCommand.CompleteNextSplitManually(),
            _ => null!
        };

        if (command is null)
        {
            return false;
        }

        if (!RaceInteractionPolicy.Allows(command, isRaceModeEnabled, isInRaceRoom))
        {
            return false;
        }

        // Window interaction is independent from Terraria automation. Keeping this
        // available also lets the user recover the overlay while an automation
        // cancellation is still unwinding.
        if (action == HotkeyAction.MouseClickThrough)
        {
            return true;
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
