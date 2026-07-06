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

public sealed record RacePackageSystemEvent(string RoomCode, string PackageRevision) : SystemEvent;

public sealed record RaceProgressSystemEvent(string RoomCode) : SystemEvent;

public sealed record RaceRosterSystemEvent(string RoomCode) : SystemEvent;

public sealed record JobProgressSystemEvent(string JobKey, int ProgressPercent) : SystemEvent;

public sealed record DisplaySystemEvent(DisplayInvalidation Invalidation) : SystemEvent;

public sealed record SystemState(
    AppSettings Settings,
    IReadOnlyList<SplitDefinition> Definitions,
    ApplicationViewState ViewState,
    RaceSystemState Race,
    JobSystemState Jobs,
    DisplaySystemState Display);

public sealed record RaceSystemState(
    bool IsInRoom = false,
    string RoomCode = "",
    string PackageRevision = "");

public sealed record JobSystemState(
    string ActiveJobKey = "",
    int ProgressPercent = 0);

public sealed record DisplaySystemState(
    DisplayInvalidationTarget ActiveTargets = DisplayInvalidationTarget.All);

public sealed record ApplicationUpdate(
    IReadOnlyList<ApplicationEffect> Effects,
    IReadOnlyList<DisplayInvalidation> DisplayInvalidations)
{
    public static ApplicationUpdate Empty { get; } = new([], []);
}
