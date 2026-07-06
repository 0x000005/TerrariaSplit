namespace TerrariaSplit.Application;

public abstract record AppCommand
{
    public static AppCommand TogglePause() => new TogglePauseCommand();

    public static AppCommand ResetRun(bool recordStats, bool playResetSound) =>
        new ResetRunCommand(recordStats, playResetSound);

    public static AppCommand ToggleMouseClickThrough() => new ToggleMouseClickThroughCommand();

    public static AppCommand TogglePyramidFilter() => new TogglePyramidFilterCommand();

    public static AppCommand QueueMenuAction(MenuActionKind action, DateTime requestedAtUtc) =>
        new QueueMenuActionCommand(action, requestedAtUtc);

    public static AppCommand CancelCreateWorld() => new CancelCreateWorldCommand();

    public static AppCommand CancelEnterWorld() => new CancelEnterWorldCommand();

    public static AppCommand EditPracticeSplitTime(int splitIndex, TimeSpan? time) =>
        new EditPracticeSplitTimeCommand(splitIndex, time);

    public static AppCommand EditPracticeTotalTime(TimeSpan time) =>
        new EditPracticeTotalTimeCommand(time);

    public static AppCommand ApplySettings(AppSettings settings) => new ApplySettingsCommand(settings);

    public static AppCommand ApplyTemporarySettings(AppSettings settings) => new ApplyTemporarySettingsCommand(settings);

    public static AppCommand ApplyRouteOverride(SettingsRouteOverridePackage package) =>
        new ApplyRouteOverrideCommand(package);

    public static AppCommand ClearRouteOverride() => new ClearRouteOverrideCommand();
}

public sealed record TogglePauseCommand : AppCommand;

public sealed record ResetRunCommand(bool RecordStats, bool PlayResetSound) : AppCommand;

public sealed record ToggleMouseClickThroughCommand : AppCommand;

public sealed record TogglePyramidFilterCommand : AppCommand;

public sealed record QueueMenuActionCommand(MenuActionKind Action, DateTime RequestedAtUtc) : AppCommand;

public sealed record CancelCreateWorldCommand : AppCommand;

public sealed record CancelEnterWorldCommand : AppCommand;

public sealed record EditPracticeSplitTimeCommand(int SplitIndex, TimeSpan? Time) : AppCommand;

public sealed record EditPracticeTotalTimeCommand(TimeSpan Time) : AppCommand;

public sealed record ApplySettingsCommand(AppSettings Settings) : AppCommand;

public sealed record ApplyTemporarySettingsCommand(AppSettings Settings) : AppCommand;

public sealed record ApplyRouteOverrideCommand(SettingsRouteOverridePackage Package) : AppCommand;

public sealed record ClearRouteOverrideCommand : AppCommand;
