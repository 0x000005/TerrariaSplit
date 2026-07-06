using System.Drawing;

namespace TerrariaSplit.UI;

internal sealed record TimerOverlayRenderState(
    AppSettings Settings,
    UiPalette Palette,
    IReadOnlyList<SplitStatusSnapshot> Statuses,
    int CurrentSplitIndex,
    SplitTimerState TimerState,
    bool MouseClickThrough,
    Color? TimerFillOverride = null,
    bool ShowPyramidFilterIndicator = false);

internal readonly record struct TimerOverlayStateKey(
    SplitTimerState TimerState,
    int CurrentSplitIndex,
    bool MouseClickThrough,
    int StatusHash,
    long SettingsRevision,
    int? TimerFillOverrideArgb,
    bool ShowPyramidFilterIndicator);
