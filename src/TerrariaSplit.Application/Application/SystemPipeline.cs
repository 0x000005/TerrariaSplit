namespace TerrariaSplit.Application;

[Flags]
public enum DisplayInvalidationTarget
{
    None = 0,
    SplitOverlay = 1,
    TimerOverlay = 2,
    RaceLeaderboard = 4,
    All = SplitOverlay | TimerOverlay | RaceLeaderboard
}

public enum DisplayRefreshLevel
{
    Frame,
    RuntimeFacts,
    SplitProgress,
    DisplaySettings,
    RoutePackage,
    RunReset,
    FullRebuild
}

public sealed record DisplayInvalidation(
    DisplayRefreshLevel Level,
    DisplayInvalidationTarget Targets)
{
    public static DisplayInvalidation For(
        DisplayRefreshLevel level,
        DisplayInvalidationTarget targets)
    {
        return new DisplayInvalidation(level, targets);
    }
}

public abstract record SystemEvent;

public sealed record RuntimeWatcherSystemEvent(WatcherPollNotification Notification) : SystemEvent;

public sealed record ControlCommandSystemEvent(AppCommand Command) : SystemEvent;

public sealed record RacePackageSystemEvent(
    string RoomCode,
    string PackageRevision,
    bool IsInRoom = true) : SystemEvent;

public sealed record RaceProgressSystemEvent(string RoomCode) : SystemEvent;

public sealed record RaceRosterSystemEvent(string RoomCode, bool IsInRoom = true) : SystemEvent;

public sealed record RaceModeSystemEvent(bool Enabled) : SystemEvent;

public sealed record RaceTimePenaltySystemEvent(TimeSpan Penalty) : SystemEvent;

public sealed record PersonalBestFinalizationSystemEvent(PersonalBestFinalizationResult Result) : SystemEvent;

public sealed record SystemState(RaceSystemState Race);

public sealed record RaceSystemState(
    bool IsInRoom = false,
    string RoomCode = "",
    string PackageRevision = "",
    bool IsModeEnabled = false);

public sealed record ApplicationUpdate(
    IReadOnlyList<ApplicationEffect> Effects,
    IReadOnlyList<DisplayInvalidation> DisplayInvalidations)
{
    public static ApplicationUpdate Empty { get; } = new([], []);
}
