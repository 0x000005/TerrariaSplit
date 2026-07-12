namespace TerrariaSplit.Configuration;

public sealed record UiTextEffectDescriptor(
    string Key,
    Func<UiTextEffectSettings, int> GetOpacity,
    Action<UiTextEffectSettings, int> SetOpacity,
    Func<UiTextEffectSettings, int>? GetShadow,
    Action<UiTextEffectSettings, int>? SetShadow,
    Func<UiTextEffectSettings, int>? GetOutline,
    Action<UiTextEffectSettings, int>? SetOutline);

public static class UiTextEffectDescriptors
{
    public static UiTextEffectDescriptor Icon { get; } = new(
        nameof(UiTextEffectSettings.IconOpacityPercent),
        effects => effects.IconOpacityPercent,
        (effects, value) => effects.IconOpacityPercent = value,
        effects => effects.IconShadowPercent,
        (effects, value) => effects.IconShadowPercent = value,
        effects => effects.IconOutlineThicknessPercent,
        (effects, value) => effects.IconOutlineThicknessPercent = value);

    public static UiTextEffectDescriptor Time { get; } = new(
        nameof(UiTextEffectSettings.TimeOpacityPercent),
        effects => effects.TimeOpacityPercent,
        (effects, value) => effects.TimeOpacityPercent = value,
        effects => effects.TimeShadowPercent,
        (effects, value) => effects.TimeShadowPercent = value,
        effects => effects.TimeOutlineThicknessPercent,
        (effects, value) => effects.TimeOutlineThicknessPercent = value);

    public static UiTextEffectDescriptor Name { get; } = new(
        nameof(UiTextEffectSettings.NameOpacityPercent),
        effects => effects.NameOpacityPercent,
        (effects, value) => effects.NameOpacityPercent = value,
        effects => effects.NameShadowPercent,
        (effects, value) => effects.NameShadowPercent = value,
        effects => effects.NameOutlineThicknessPercent,
        (effects, value) => effects.NameOutlineThicknessPercent = value);

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
        effects => effects.AttachedIconShadowPercent,
        (effects, value) => effects.AttachedIconShadowPercent = value,
        effects => effects.AttachedIconOutlineThicknessPercent,
        (effects, value) => effects.AttachedIconOutlineThicknessPercent = value);

    public static UiTextEffectDescriptor AttachedTime { get; } = new(
        nameof(UiTextEffectSettings.AttachedTimeOpacityPercent),
        effects => effects.AttachedTimeOpacityPercent,
        (effects, value) => effects.AttachedTimeOpacityPercent = value,
        effects => effects.AttachedTimeShadowPercent,
        (effects, value) => effects.AttachedTimeShadowPercent = value,
        effects => effects.AttachedTimeOutlineThicknessPercent,
        (effects, value) => effects.AttachedTimeOutlineThicknessPercent = value);

    public static UiTextEffectDescriptor AttachedName { get; } = new(
        nameof(UiTextEffectSettings.AttachedNameOpacityPercent),
        effects => effects.AttachedNameOpacityPercent,
        (effects, value) => effects.AttachedNameOpacityPercent = value,
        effects => effects.AttachedNameShadowPercent,
        (effects, value) => effects.AttachedNameShadowPercent = value,
        effects => effects.AttachedNameOutlineThicknessPercent,
        (effects, value) => effects.AttachedNameOutlineThicknessPercent = value);

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
        Name,
        Time,
        Delta,
        AttachedIcon,
        AttachedName,
        AttachedTime,
        AttachedDelta,
        Timer,
        TimerMilliseconds
    ];
}

public sealed record UiColumnDescriptor(
    string Key,
    string Label,
    Func<UiColumnLayoutSettings, UiColumnSettings?> GetValue,
    Action<UiColumnLayoutSettings, UiColumnSettings> SetValue,
    UiTextEffectDescriptor TextEffect,
    bool ShowWidth = true,
    bool ShowFontFamily = true,
    bool ShowBold = true,
    bool ShowItalic = true);

public static class UiColumnDescriptors
{
    public static UiColumnDescriptor Icon { get; } = new(
        nameof(UiColumnLayoutSettings.Icon),
        "Icon",
        columns => columns.Icon,
        (columns, value) => columns.Icon = value,
        UiTextEffectDescriptors.Icon,
        ShowFontFamily: false,
        ShowBold: false,
        ShowItalic: false);

    public static UiColumnDescriptor Time { get; } = new(
        nameof(UiColumnLayoutSettings.Time),
        "Time",
        columns => columns.Time,
        (columns, value) => columns.Time = value,
        UiTextEffectDescriptors.Time);

    public static UiColumnDescriptor Name { get; } = new(
        nameof(UiColumnLayoutSettings.Name),
        "Name",
        columns => columns.Name,
        (columns, value) => columns.Name = value,
        UiTextEffectDescriptors.Name);

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
        ShowBold: false,
        ShowItalic: false);

    public static UiColumnDescriptor AttachedTime { get; } = new(
        nameof(UiColumnLayoutSettings.AttachedTime),
        "Time (attached)",
        columns => columns.AttachedTime,
        (columns, value) => columns.AttachedTime = value,
        UiTextEffectDescriptors.AttachedTime);

    public static UiColumnDescriptor AttachedName { get; } = new(
        nameof(UiColumnLayoutSettings.AttachedName),
        "Name (attached)",
        columns => columns.AttachedName,
        (columns, value) => columns.AttachedName = value,
        UiTextEffectDescriptors.AttachedName);

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

    public static IReadOnlyList<(UiColumnDescriptor Primary, UiColumnDescriptor Attached)> SharedWidthPairs { get; } =
    [
        (Icon, AttachedIcon),
        (Name, AttachedName),
        (Time, AttachedTime),
        (Delta, AttachedDelta)
    ];

    public static UiColumnDescriptor GetWidthOwner(UiColumnDescriptor descriptor)
    {
        foreach ((UiColumnDescriptor primary, UiColumnDescriptor attached) in SharedWidthPairs)
        {
            if (string.Equals(descriptor.Key, attached.Key, StringComparison.Ordinal))
            {
                return primary;
            }
        }

        return descriptor;
    }

    public static int GetSharedWidth(UiColumnLayoutSettings columns, UiColumnDescriptor descriptor)
    {
        UiColumnDescriptor owner = GetWidthOwner(descriptor);
        return owner.GetValue(columns)?.Width ?? descriptor.GetValue(columns)?.Width ?? 0;
    }

    public static string GetSharedAlignment(UiColumnLayoutSettings columns, UiColumnDescriptor descriptor)
    {
        UiColumnDescriptor owner = GetWidthOwner(descriptor);
        if (string.Equals(owner.Key, Icon.Key, StringComparison.Ordinal))
        {
            return columns.IconAlignment;
        }

        if (string.Equals(owner.Key, Name.Key, StringComparison.Ordinal))
        {
            return columns.NameAlignment;
        }

        if (string.Equals(owner.Key, Time.Key, StringComparison.Ordinal))
        {
            return columns.TimeAlignment;
        }

        return columns.DeltaAlignment;
    }

    public static void SetSharedAlignment(
        UiColumnLayoutSettings columns,
        UiColumnDescriptor descriptor,
        string alignment)
    {
        UiColumnDescriptor owner = GetWidthOwner(descriptor);
        if (string.Equals(owner.Key, Icon.Key, StringComparison.Ordinal))
        {
            columns.IconAlignment = UiColumnAlignment.Normalize(alignment, UiColumnAlignment.Right);
        }
        else if (string.Equals(owner.Key, Name.Key, StringComparison.Ordinal))
        {
            columns.NameAlignment = UiColumnAlignment.Normalize(alignment, UiColumnAlignment.Center);
        }
        else if (string.Equals(owner.Key, Time.Key, StringComparison.Ordinal))
        {
            columns.TimeAlignment = UiColumnAlignment.Normalize(alignment, UiColumnAlignment.Right);
        }
        else
        {
            columns.DeltaAlignment = UiColumnAlignment.Normalize(alignment, UiColumnAlignment.Left);
        }
    }

    public static void SynchronizeSharedWidths(UiColumnLayoutSettings columns)
    {
        foreach ((UiColumnDescriptor primaryDescriptor, UiColumnDescriptor attachedDescriptor) in SharedWidthPairs)
        {
            UiColumnSettings? primary = primaryDescriptor.GetValue(columns);
            UiColumnSettings? attached = attachedDescriptor.GetValue(columns);
            if (primary is not null && attached is not null)
            {
                attached.Width = primary.Width;
            }
        }
    }

    public static IReadOnlyList<UiColumnDescriptor> SplitDisplay { get; } =
    [
        Icon,
        AttachedIcon,
        Name,
        AttachedName,
        Time,
        AttachedTime,
        Delta,
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
        AttachedIcon,
        Name,
        AttachedName,
        Time,
        AttachedTime,
        Delta,
        AttachedDelta,
        Timer,
        TimerMilliseconds
    ];
}
