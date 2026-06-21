namespace TerrariaSplit.Application;

internal abstract record AppCommand
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
}

internal sealed record TogglePauseCommand : AppCommand;

internal sealed record ResetRunCommand(bool RecordStats, bool PlayResetSound) : AppCommand;

internal sealed record ToggleMouseClickThroughCommand : AppCommand;

internal sealed record TogglePyramidFilterCommand : AppCommand;

internal sealed record QueueMenuActionCommand(MenuActionKind Action, DateTime RequestedAtUtc) : AppCommand;

internal sealed record CancelCreateWorldCommand : AppCommand;

internal sealed record CancelEnterWorldCommand : AppCommand;

internal sealed record EditPracticeSplitTimeCommand(int SplitIndex, TimeSpan? Time) : AppCommand;

internal sealed record EditPracticeTotalTimeCommand(TimeSpan Time) : AppCommand;

internal sealed record ApplySettingsCommand(AppSettings Settings) : AppCommand;
