namespace TerrariaSplit;

internal sealed record TimerOverlayRenderState(
    AppSettings Settings,
    UiPalette Palette,
    IReadOnlyList<BossSplitStatus> Statuses,
    int CurrentSplitIndex,
    SplitTimerState TimerState,
    bool MouseClickThrough);

internal readonly record struct TimerOverlayStateKey(
    SplitTimerState TimerState,
    int CurrentSplitIndex,
    bool MouseClickThrough,
    int StatusHash,
    long SettingsRevision);
