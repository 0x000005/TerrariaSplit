namespace TerrariaSplit.Application;

public static class RaceInteractionPolicy
{
    public static bool Allows(
        AppCommand command,
        bool isRaceModeEnabled,
        bool isInRaceRoom)
    {
        if (isInRaceRoom &&
            command is ApplySettingsCommand or ApplyTemporarySettingsCommand)
        {
            return false;
        }

        if (!isRaceModeEnabled)
        {
            return true;
        }

        return command switch
        {
            TogglePauseCommand => false,
            ToggleCheatsCommand => false,
            ResetRunCommand reset => reset.AllowDuringRace,
            QueueMenuActionCommand queued => Allows(queued.Action, isRaceModeEnabled),
            EditPracticeSplitTimeCommand => false,
            EditPracticeTotalTimeCommand => false,
            _ => true
        };
    }

    public static bool Allows(MenuActionKind action, bool isRaceModeEnabled)
    {
        return !isRaceModeEnabled ||
            action is not (MenuActionKind.Reset or
                MenuActionKind.CreateWorld or
                MenuActionKind.PracticeWorld);
    }
}
