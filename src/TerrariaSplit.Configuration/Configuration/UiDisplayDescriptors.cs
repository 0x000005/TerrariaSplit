namespace TerrariaSplit.Configuration;

internal sealed record UiTextEffectDescriptor(
    string Key,
    Func<UiTextEffectSettings, int> GetOpacity,
    Action<UiTextEffectSettings, int> SetOpacity,
    Func<UiTextEffectSettings, int>? GetShadow,
    Action<UiTextEffectSettings, int>? SetShadow,
    Func<UiTextEffectSettings, int>? GetOutline,
    Action<UiTextEffectSettings, int>? SetOutline);

internal static class UiTextEffectDescriptors
{
    public static UiTextEffectDescriptor Icon { get; } = new(
        nameof(UiTextEffectSettings.IconOpacityPercent),
        effects => effects.IconOpacityPercent,
        (effects, value) => effects.IconOpacityPercent = value,
        null,
        null,
        null,
        null);

    public static UiTextEffectDescriptor Time { get; } = new(
        nameof(UiTextEffectSettings.TimeOpacityPercent),
        effects => effects.TimeOpacityPercent,
        (effects, value) => effects.TimeOpacityPercent = value,
        effects => effects.TimeShadowPercent,
        (effects, value) => effects.TimeShadowPercent = value,
        effects => effects.TimeOutlineThicknessPercent,
        (effects, value) => effects.TimeOutlineThicknessPercent = value);

    public static UiTextEffectDescriptor Delta { get; } = new(
        nameof(UiTextEffectSettings.DeltaOpacityPercent),
        effects => effects.DeltaOpacityPercent,
        (effects, value) => effects.DeltaOpacityPercent = value,
        effects => effects.DeltaShadowPercent,
        (effects, value) => effects.DeltaShadowPercent = value,
        effects => effects.DeltaOutlineThicknessPercent,
        (effects, value) => effects.DeltaOutlineThicknessPercent = value);

    public static UiTextEffectDescriptor AttachedIcon { get; } = new(
        nameof(UiTextEffectSettings.AttachedIconOpacityPercent),
        effects => effects.AttachedIconOpacityPercent,
        (effects, value) => effects.AttachedIconOpacityPercent = value,
        null,
        null,
        null,
        null);

    public static UiTextEffectDescriptor AttachedTime { get; } = new(
        nameof(UiTextEffectSettings.AttachedTimeOpacityPercent),
        effects => effects.AttachedTimeOpacityPercent,
        (effects, value) => effects.AttachedTimeOpacityPercent = value,
        effects => effects.AttachedTimeShadowPercent,
        (effects, value) => effects.AttachedTimeShadowPercent = value,
        effects => effects.AttachedTimeOutlineThicknessPercent,
        (effects, value) => effects.AttachedTimeOutlineThicknessPercent = value);

    public static UiTextEffectDescriptor AttachedDelta { get; } = new(
        nameof(UiTextEffectSettings.AttachedDeltaOpacityPercent),
        effects => effects.AttachedDeltaOpacityPercent,
        (effects, value) => effects.AttachedDeltaOpacityPercent = value,
        effects => effects.AttachedDeltaShadowPercent,
        (effects, value) => effects.AttachedDeltaShadowPercent = value,
        effects => effects.AttachedDeltaOutlineThicknessPercent,
        (effects, value) => effects.AttachedDeltaOutlineThicknessPercent = value);

    public static UiTextEffectDescriptor Timer { get; } = new(
        nameof(UiTextEffectSettings.TimerOpacityPercent),
        effects => effects.TimerOpacityPercent,
        (effects, value) => effects.TimerOpacityPercent = value,
        effects => effects.TimerShadowPercent,
        (effects, value) => effects.TimerShadowPercent = value,
        effects => effects.TimerOutlineThicknessPercent,
        (effects, value) => effects.TimerOutlineThicknessPercent = value);

    public static UiTextEffectDescriptor TimerMilliseconds { get; } = new(
        nameof(UiTextEffectSettings.TimerMillisecondsOpacityPercent),
        effects => effects.TimerMillisecondsOpacityPercent,
        (effects, value) => effects.TimerMillisecondsOpacityPercent = value,
        effects => effects.TimerMillisecondsShadowPercent,
        (effects, value) => effects.TimerMillisecondsShadowPercent = value,
        effects => effects.TimerMillisecondsOutlineThicknessPercent,
        (effects, value) => effects.TimerMillisecondsOutlineThicknessPercent = value);

    public static IReadOnlyList<UiTextEffectDescriptor> All { get; } =
    [
        Icon,
        Time,
        Delta,
        AttachedIcon,
        AttachedTime,
        AttachedDelta,
        Timer,
        TimerMilliseconds
    ];
}

internal sealed record UiColumnDescriptor(
    string Key,
    string Label,
    Func<UiColumnLayoutSettings, UiColumnSettings?> GetValue,
    Action<UiColumnLayoutSettings, UiColumnSettings> SetValue,
    UiTextEffectDescriptor TextEffect,
    bool ShowWidth = true,
    bool ShowFontFamily = true,
    bool ShowBold = true);

internal static class UiColumnDescriptors
{
    public static UiColumnDescriptor Icon { get; } = new(
        nameof(UiColumnLayoutSettings.Icon),
        "Icon",
        columns => columns.Icon,
        (columns, value) => columns.Icon = value,
        UiTextEffectDescriptors.Icon,
        ShowFontFamily: false,
        ShowBold: false);

    public static UiColumnDescriptor Time { get; } = new(
        nameof(UiColumnLayoutSettings.Time),
        "Time",
        columns => columns.Time,
        (columns, value) => columns.Time = value,
        UiTextEffectDescriptors.Time);

    public static UiColumnDescriptor Delta { get; } = new(
        nameof(UiColumnLayoutSettings.Delta),
        "Delta",
        columns => columns.Delta,
        (columns, value) => columns.Delta = value,
        UiTextEffectDescriptors.Delta);

    public static UiColumnDescriptor AttachedIcon { get; } = new(
        nameof(UiColumnLayoutSettings.AttachedIcon),
        "Icon (attached)",
        columns => columns.AttachedIcon,
        (columns, value) => columns.AttachedIcon = value,
        UiTextEffectDescriptors.AttachedIcon,
        ShowFontFamily: false,
        ShowBold: false);

    public static UiColumnDescriptor AttachedTime { get; } = new(
        nameof(UiColumnLayoutSettings.AttachedTime),
        "Time (attached)",
        columns => columns.AttachedTime,
        (columns, value) => columns.AttachedTime = value,
        UiTextEffectDescriptors.AttachedTime);

    public static UiColumnDescriptor AttachedDelta { get; } = new(
        nameof(UiColumnLayoutSettings.AttachedDelta),
        "Delta (attached)",
        columns => columns.AttachedDelta,
        (columns, value) => columns.AttachedDelta = value,
        UiTextEffectDescriptors.AttachedDelta);

    public static UiColumnDescriptor Timer { get; } = new(
        nameof(UiColumnLayoutSettings.Timer),
        "Before decimal",
        columns => columns.Timer,
        (columns, value) => columns.Timer = value,
        UiTextEffectDescriptors.Timer,
        ShowWidth: false);

    public static UiColumnDescriptor TimerMilliseconds { get; } = new(
        nameof(UiColumnLayoutSettings.TimerMilliseconds),
        "After decimal",
        columns => columns.TimerMilliseconds,
        (columns, value) => columns.TimerMilliseconds = value,
        UiTextEffectDescriptors.TimerMilliseconds,
        ShowWidth: false);

    public static IReadOnlyList<UiColumnDescriptor> SplitDisplay { get; } =
    [
        Icon,
        Time,
        Delta,
        AttachedIcon,
        AttachedTime,
        AttachedDelta
    ];

    public static IReadOnlyList<UiColumnDescriptor> TimerDisplay { get; } =
    [
        Timer,
        TimerMilliseconds
    ];

    public static IReadOnlyList<UiColumnDescriptor> All { get; } =
    [
        Icon,
        Time,
        Delta,
        AttachedIcon,
        AttachedTime,
        AttachedDelta,
        Timer,
        TimerMilliseconds
    ];
}
