namespace TerrariaSplit.Application;

internal enum AppCommandKind
{
    TogglePause,
    ResetRun,
    ToggleMouseClickThrough,
    TogglePyramidFilter,
    QueueMenuAction,
    CancelCreateWorld,
    CancelEnterWorld,
    EditPracticeSplitTime,
    EditPracticeTotalTime,
    ApplySettings
}

internal sealed record AppCommand
{
    private AppCommand(AppCommandKind kind)
    {
        Kind = kind;
    }

    public AppCommandKind Kind { get; }

    public bool RecordStats { get; private init; }

    public bool PlayResetSound { get; private init; }

    public int SplitIndex { get; private init; } = -1;

    public TimeSpan? Time { get; private init; }

    public AppSettings? Settings { get; private init; }

    public MenuActionKind MenuAction { get; private init; }

    public DateTime RequestedAtUtc { get; private init; }

    public static AppCommand TogglePause() => new(AppCommandKind.TogglePause);

    public static AppCommand ResetRun(bool recordStats, bool playResetSound)
    {
        return new AppCommand(AppCommandKind.ResetRun)
        {
            RecordStats = recordStats,
            PlayResetSound = playResetSound
        };
    }

    public static AppCommand ToggleMouseClickThrough() => new(AppCommandKind.ToggleMouseClickThrough);

    public static AppCommand TogglePyramidFilter() => new(AppCommandKind.TogglePyramidFilter);

    public static AppCommand QueueMenuAction(MenuActionKind action, DateTime requestedAtUtc)
    {
        return new AppCommand(AppCommandKind.QueueMenuAction)
        {
            MenuAction = action,
            RequestedAtUtc = requestedAtUtc
        };
    }

    public static AppCommand CancelCreateWorld() => new(AppCommandKind.CancelCreateWorld);

    public static AppCommand CancelEnterWorld() => new(AppCommandKind.CancelEnterWorld);

    public static AppCommand EditPracticeSplitTime(int splitIndex, TimeSpan? time)
    {
        return new AppCommand(AppCommandKind.EditPracticeSplitTime)
        {
            SplitIndex = splitIndex,
            Time = time
        };
    }

    public static AppCommand EditPracticeTotalTime(TimeSpan time)
    {
        return new AppCommand(AppCommandKind.EditPracticeTotalTime)
        {
            Time = time
        };
    }

    public static AppCommand ApplySettings(AppSettings settings)
    {
        return new AppCommand(AppCommandKind.ApplySettings)
        {
            Settings = settings
        };
    }
}
