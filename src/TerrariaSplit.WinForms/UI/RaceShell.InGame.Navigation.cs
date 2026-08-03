using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.UI;

internal enum RaceInGamePage
{
    Entry,
    MemberJoin,
    HostWorldSource,
    HostWorldSettings,
    HostSeedSettings,
    HostFilterSettings,
    RoomHome,
    RoomManagement,
    RoomPreparation
}

internal enum RaceInGameTransition
{
    SelectHost,
    SelectMember,
    BackToEntry,
    SelectRandomWorld,
    OpenSeedSettings,
    BackToWorldSource,
    BackToWorldSettings,
    OpenFilterSettings,
    OpenRoomManagement,
    BackToRoomHome,
    RoomPrepared,
    RaceStarted,
    RoomExited
}

internal sealed class RaceInGameNavigator
{
    public RaceInGamePage Current { get; private set; } = RaceInGamePage.Entry;

    public void Reset(bool roomOpen, bool raceStarted = false)
    {
        Current = !roomOpen
            ? RaceInGamePage.Entry
            : raceStarted
                ? RaceInGamePage.RoomHome
                : RaceInGamePage.RoomPreparation;
    }

    public bool TryMove(RaceInGameTransition transition, bool isHost)
    {
        RaceInGamePage? next = (Current, transition) switch
        {
            (RaceInGamePage.Entry, RaceInGameTransition.SelectHost) =>
                RaceInGamePage.HostWorldSource,
            (RaceInGamePage.Entry, RaceInGameTransition.SelectMember) =>
                RaceInGamePage.MemberJoin,
            (RaceInGamePage.MemberJoin, RaceInGameTransition.BackToEntry) =>
                RaceInGamePage.Entry,
            (RaceInGamePage.HostWorldSource, RaceInGameTransition.BackToEntry) =>
                RaceInGamePage.Entry,
            (RaceInGamePage.HostWorldSource, RaceInGameTransition.SelectRandomWorld) =>
                RaceInGamePage.HostWorldSettings,
            (RaceInGamePage.HostWorldSettings, RaceInGameTransition.OpenSeedSettings) =>
                RaceInGamePage.HostSeedSettings,
            (RaceInGamePage.HostWorldSettings, RaceInGameTransition.BackToWorldSource) =>
                RaceInGamePage.HostWorldSource,
            (RaceInGamePage.HostWorldSettings, RaceInGameTransition.OpenFilterSettings) =>
                RaceInGamePage.HostFilterSettings,
            (RaceInGamePage.HostSeedSettings, RaceInGameTransition.BackToWorldSettings) =>
                RaceInGamePage.HostWorldSettings,
            (RaceInGamePage.HostFilterSettings, RaceInGameTransition.BackToWorldSettings) =>
                RaceInGamePage.HostWorldSettings,
            (RaceInGamePage.RoomHome, RaceInGameTransition.OpenRoomManagement) when isHost =>
                RaceInGamePage.RoomManagement,
            (RaceInGamePage.RoomManagement, RaceInGameTransition.BackToRoomHome) =>
                RaceInGamePage.RoomHome,
            (_, RaceInGameTransition.RoomPrepared) =>
                RaceInGamePage.RoomPreparation,
            (_, RaceInGameTransition.RaceStarted) =>
                RaceInGamePage.RoomHome,
            (_, RaceInGameTransition.RoomExited) =>
                RaceInGamePage.Entry,
            _ => null
        };

        if (next is null)
        {
            return false;
        }

        Current = next.Value;
        return true;
    }

    public RaceInGamePage Resolve(RacePanelRole role, bool roomOpen, bool isHost)
    {
        if (roomOpen)
        {
            return Current switch
            {
                RaceInGamePage.RoomManagement when isHost =>
                    RaceInGamePage.RoomManagement,
                RaceInGamePage.RoomPreparation =>
                    RaceInGamePage.RoomPreparation,
                _ => RaceInGamePage.RoomHome
            };
        }

        return Current switch
        {
            RaceInGamePage.MemberJoin when role == RacePanelRole.Member =>
                RaceInGamePage.MemberJoin,
            RaceInGamePage.HostWorldSource when role == RacePanelRole.Host =>
                RaceInGamePage.HostWorldSource,
            RaceInGamePage.HostWorldSettings when role == RacePanelRole.Host =>
                RaceInGamePage.HostWorldSettings,
            RaceInGamePage.HostSeedSettings when role == RacePanelRole.Host =>
                RaceInGamePage.HostSeedSettings,
            RaceInGamePage.HostFilterSettings when role == RacePanelRole.Host =>
                RaceInGamePage.HostFilterSettings,
            _ => RaceInGamePage.Entry
        };
    }
}

internal sealed partial class RaceShell
{
    private readonly RaceInGameNavigator inGameNavigation = new();

    private void ResetInGameNavigation()
    {
        RaceRoomState? state = State;
        inGameNavigation.Reset(
            HasOpenRaceRoom(state),
            HasRaceStarted(state));
    }

    private bool TransitionInGameMenu(RaceInGameTransition transition)
    {
        RaceInGamePage previous = inGameNavigation.Current;
        if (!inGameNavigation.TryMove(transition, IsHostInCurrentRoom))
        {
            logger.Info(
                $"Ignored invalid Terraria Race page transition: {previous} -> {transition}.");
            return false;
        }

        if (transition is RaceInGameTransition.RoomExited)
        {
            // Room exit is the operation boundary. Invalidate the closing
            // operation and every snapshot from the old room before the entry
            // page becomes interactive.
            Interlocked.Increment(ref inGameOperationId);
            CancellationTokenSource? operation = Interlocked.Exchange(
                ref inGameOperationCancellation,
                null);
            operation?.Dispose();
            Interlocked.Exchange(ref inGameMenuBusy, 0);
            Interlocked.Exchange(ref inGameMenuDedicatedProgress, 0);
            ResetInGameActionSnapshots();
        }

        inGameMenuStatus = string.Empty;
        MarkInGameMenuDirty();
        return true;
    }

    private RaceInGamePage ResolveInGamePage(RacePanelRole role, RaceRoomState? room)
    {
        return inGameNavigation.Resolve(
            role,
            HasOpenRaceRoom(room),
            IsHostInCurrentRoom);
    }

    private static bool HasOpenRaceRoom(RaceRoomState? state)
    {
        return state is not null && state.Status != RaceRoomStatus.Closed;
    }

    private static bool HasRaceStarted(RaceRoomState? state)
    {
        return state?.ScheduledStartUtc is not null;
    }
}
