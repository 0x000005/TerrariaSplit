using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal static class SettingsDescriptors
{
    public static IReadOnlyList<TextColorDescriptor> TextColors { get; } =
    [
        new("Reference time (future stage)",
            nameof(UiColorSettings.ReferenceText), colors => colors.ReferenceText, (colors, value) => colors.ReferenceText = value,
            nameof(UiColorSettings.ReferenceTextOutline), colors => colors.ReferenceTextOutline, (colors, value) => colors.ReferenceTextOutline = value,
            nameof(UiColorSettings.ReferenceTextShadow), colors => colors.ReferenceTextShadow, (colors, value) => colors.ReferenceTextShadow = value),
        new("Reference time (current stage)",
            nameof(UiColorSettings.ActiveReferenceText), colors => colors.ActiveReferenceText, (colors, value) => colors.ActiveReferenceText = value,
            nameof(UiColorSettings.ActiveReferenceTextOutline), colors => colors.ActiveReferenceTextOutline, (colors, value) => colors.ActiveReferenceTextOutline = value,
            nameof(UiColorSettings.ActiveReferenceTextShadow), colors => colors.ActiveReferenceTextShadow, (colors, value) => colors.ActiveReferenceTextShadow = value),
        new("Cumulative time (completed stage)",
            nameof(UiColorSettings.SplitText), colors => colors.SplitText, (colors, value) => colors.SplitText = value,
            nameof(UiColorSettings.SplitTextOutline), colors => colors.SplitTextOutline, (colors, value) => colors.SplitTextOutline = value,
            nameof(UiColorSettings.SplitTextShadow), colors => colors.SplitTextShadow, (colors, value) => colors.SplitTextShadow = value),
        new("Name (future stage)",
            nameof(UiColorSettings.NameText), colors => colors.NameText, (colors, value) => colors.NameText = value,
            nameof(UiColorSettings.NameTextOutline), colors => colors.NameTextOutline, (colors, value) => colors.NameTextOutline = value,
            nameof(UiColorSettings.NameTextShadow), colors => colors.NameTextShadow, (colors, value) => colors.NameTextShadow = value),
        new("Name (current stage)",
            nameof(UiColorSettings.ActiveNameText), colors => colors.ActiveNameText, (colors, value) => colors.ActiveNameText = value,
            nameof(UiColorSettings.ActiveNameTextOutline), colors => colors.ActiveNameTextOutline, (colors, value) => colors.ActiveNameTextOutline = value,
            nameof(UiColorSettings.ActiveNameTextShadow), colors => colors.ActiveNameTextShadow, (colors, value) => colors.ActiveNameTextShadow = value),
        new("Name (completed stage)",
            nameof(UiColorSettings.CompletedNameText), colors => colors.CompletedNameText, (colors, value) => colors.CompletedNameText = value,
            nameof(UiColorSettings.CompletedNameTextOutline), colors => colors.CompletedNameTextOutline, (colors, value) => colors.CompletedNameTextOutline = value,
            nameof(UiColorSettings.CompletedNameTextShadow), colors => colors.CompletedNameTextShadow, (colors, value) => colors.CompletedNameTextShadow = value),
        new("Delta (fast)",
            nameof(UiColorSettings.DeltaAheadText), colors => colors.DeltaAheadText, (colors, value) => colors.DeltaAheadText = value,
            nameof(UiColorSettings.DeltaAheadTextOutline), colors => colors.DeltaAheadTextOutline, (colors, value) => colors.DeltaAheadTextOutline = value,
            nameof(UiColorSettings.DeltaAheadTextShadow), colors => colors.DeltaAheadTextShadow, (colors, value) => colors.DeltaAheadTextShadow = value),
        new("Delta (equal)",
            nameof(UiColorSettings.DeltaEqualText), colors => colors.DeltaEqualText, (colors, value) => colors.DeltaEqualText = value,
            nameof(UiColorSettings.DeltaEqualTextOutline), colors => colors.DeltaEqualTextOutline, (colors, value) => colors.DeltaEqualTextOutline = value,
            nameof(UiColorSettings.DeltaEqualTextShadow), colors => colors.DeltaEqualTextShadow, (colors, value) => colors.DeltaEqualTextShadow = value),
        new("Delta (slow)",
            nameof(UiColorSettings.DeltaBehindText), colors => colors.DeltaBehindText, (colors, value) => colors.DeltaBehindText = value,
            nameof(UiColorSettings.DeltaBehindTextOutline), colors => colors.DeltaBehindTextOutline, (colors, value) => colors.DeltaBehindTextOutline = value,
            nameof(UiColorSettings.DeltaBehindTextShadow), colors => colors.DeltaBehindTextShadow, (colors, value) => colors.DeltaBehindTextShadow = value),
        new("Main timer (not timing)",
            nameof(UiColorSettings.TimerText), colors => colors.TimerText, (colors, value) => colors.TimerText = value,
            nameof(UiColorSettings.TimerTextOutline), colors => colors.TimerTextOutline, (colors, value) => colors.TimerTextOutline = value,
            nameof(UiColorSettings.TimerTextShadow), colors => colors.TimerTextShadow, (colors, value) => colors.TimerTextShadow = value),
        new("Main timer (fast)",
            nameof(UiColorSettings.TimerAheadText), colors => colors.TimerAheadText, (colors, value) => colors.TimerAheadText = value,
            nameof(UiColorSettings.TimerAheadTextOutline), colors => colors.TimerAheadTextOutline, (colors, value) => colors.TimerAheadTextOutline = value,
            nameof(UiColorSettings.TimerAheadTextShadow), colors => colors.TimerAheadTextShadow, (colors, value) => colors.TimerAheadTextShadow = value),
        new("Main timer (equal)",
            nameof(UiColorSettings.TimerEqualText), colors => colors.TimerEqualText, (colors, value) => colors.TimerEqualText = value,
            nameof(UiColorSettings.TimerEqualTextOutline), colors => colors.TimerEqualTextOutline, (colors, value) => colors.TimerEqualTextOutline = value,
            nameof(UiColorSettings.TimerEqualTextShadow), colors => colors.TimerEqualTextShadow, (colors, value) => colors.TimerEqualTextShadow = value),
        new("Main timer (slow)",
            nameof(UiColorSettings.TimerBehindText), colors => colors.TimerBehindText, (colors, value) => colors.TimerBehindText = value,
            nameof(UiColorSettings.TimerBehindTextOutline), colors => colors.TimerBehindTextOutline, (colors, value) => colors.TimerBehindTextOutline = value,
            nameof(UiColorSettings.TimerBehindTextShadow), colors => colors.TimerBehindTextShadow, (colors, value) => colors.TimerBehindTextShadow = value),
        new("Main timer (total fast)",
            nameof(UiColorSettings.TimerRecordText), colors => colors.TimerRecordText, (colors, value) => colors.TimerRecordText = value,
            nameof(UiColorSettings.TimerRecordTextOutline), colors => colors.TimerRecordTextOutline, (colors, value) => colors.TimerRecordTextOutline = value,
            nameof(UiColorSettings.TimerRecordTextShadow), colors => colors.TimerRecordTextShadow, (colors, value) => colors.TimerRecordTextShadow = value),
        new("Main timer (total slow)",
            nameof(UiColorSettings.TimerNoRecordText), colors => colors.TimerNoRecordText, (colors, value) => colors.TimerNoRecordText = value,
            nameof(UiColorSettings.TimerNoRecordTextOutline), colors => colors.TimerNoRecordTextOutline, (colors, value) => colors.TimerNoRecordTextOutline = value,
            nameof(UiColorSettings.TimerNoRecordTextShadow), colors => colors.TimerNoRecordTextShadow, (colors, value) => colors.TimerNoRecordTextShadow = value),
        new("Main timer (paused)",
            nameof(UiColorSettings.TimerPausedText), colors => colors.TimerPausedText, (colors, value) => colors.TimerPausedText = value,
            nameof(UiColorSettings.TimerPausedTextOutline), colors => colors.TimerPausedTextOutline, (colors, value) => colors.TimerPausedTextOutline = value,
            nameof(UiColorSettings.TimerPausedTextShadow), colors => colors.TimerPausedTextShadow, (colors, value) => colors.TimerPausedTextShadow = value)
    ];

    public static IReadOnlyList<ColorDescriptor> IconColors { get; } =
    [
        new("Icon outline",
            nameof(UiColorSettings.IconOutline),
            colors => colors.IconOutline,
            (colors, value) => colors.IconOutline = value),
        new("Icon shadow",
            nameof(UiColorSettings.IconShadow),
            colors => colors.IconShadow,
            (colors, value) => colors.IconShadow = value)
    ];

    public static IReadOnlyList<ColorDescriptor> AnimationColors { get; } =
    [
        new("Segment time hint text",
            nameof(UiColorSettings.SplitCompletionSegmentLabelText),
            colors => colors.SplitCompletionSegmentLabelText,
            (colors, value) => colors.SplitCompletionSegmentLabelText = value),
        new("Cumulative time hint text",
            nameof(UiColorSettings.SplitCompletionLabelText),
            colors => colors.SplitCompletionLabelText,
            (colors, value) => colors.SplitCompletionLabelText = value),
        new("Segment time",
            nameof(UiColorSettings.SplitCompletionSegmentTimeText),
            colors => colors.SplitCompletionSegmentTimeText,
            (colors, value) => colors.SplitCompletionSegmentTimeText = value),
        new("Cumulative time",
            nameof(UiColorSettings.SplitCompletionTimeText),
            colors => colors.SplitCompletionTimeText,
            (colors, value) => colors.SplitCompletionTimeText = value)
    ];

    public static IReadOnlyList<SoundDescriptor> Sounds { get; } =
    [
        new("Pause sound", nameof(UiSoundSettings.Pause), sounds => sounds.Pause, (sounds, value) => sounds.Pause = value),
        new("Resume sound", nameof(UiSoundSettings.Resume), sounds => sounds.Resume, (sounds, value) => sounds.Resume = value),
        new("Reset sound", nameof(UiSoundSettings.Reset), sounds => sounds.Reset, (sounds, value) => sounds.Reset = value),
        new("Timer start sound", nameof(UiSoundSettings.EnterWorld), sounds => sounds.EnterWorld, (sounds, value) => sounds.EnterWorld = value),
        new("Stage reached: cumulative not faster, segment not faster", nameof(UiSoundSettings.SplitBehindReferenceBehindSegment), sounds => sounds.SplitBehindReferenceBehindSegment, (sounds, value) => sounds.SplitBehindReferenceBehindSegment = value),
        new("Stage reached: cumulative not faster, segment faster", nameof(UiSoundSettings.SplitBehindReferenceAheadSegment), sounds => sounds.SplitBehindReferenceAheadSegment, (sounds, value) => sounds.SplitBehindReferenceAheadSegment = value),
        new("Stage reached: cumulative faster, segment not faster", nameof(UiSoundSettings.SplitAheadReferenceBehindSegment), sounds => sounds.SplitAheadReferenceBehindSegment, (sounds, value) => sounds.SplitAheadReferenceBehindSegment = value),
        new("Stage reached: cumulative faster, segment faster", nameof(UiSoundSettings.SplitAheadReferenceAheadSegment), sounds => sounds.SplitAheadReferenceAheadSegment, (sounds, value) => sounds.SplitAheadReferenceAheadSegment = value),
        new("cumulative not faster, segment not faster", nameof(UiSoundSettings.FinalGroupBehindReferenceBehindSegment), sounds => sounds.FinalGroupBehindReferenceBehindSegment, (sounds, value) => sounds.FinalGroupBehindReferenceBehindSegment = value, PrefixWithFinalGroupName: true),
        new("cumulative not faster, segment faster", nameof(UiSoundSettings.FinalGroupBehindReferenceAheadSegment), sounds => sounds.FinalGroupBehindReferenceAheadSegment, (sounds, value) => sounds.FinalGroupBehindReferenceAheadSegment = value, PrefixWithFinalGroupName: true),
        new("cumulative faster, segment not faster", nameof(UiSoundSettings.FinalGroupAheadReferenceBehindSegment), sounds => sounds.FinalGroupAheadReferenceBehindSegment, (sounds, value) => sounds.FinalGroupAheadReferenceBehindSegment = value, PrefixWithFinalGroupName: true),
        new("cumulative faster, segment faster", nameof(UiSoundSettings.FinalGroupAheadReferenceAheadSegment), sounds => sounds.FinalGroupAheadReferenceAheadSegment, (sounds, value) => sounds.FinalGroupAheadReferenceAheadSegment = value, PrefixWithFinalGroupName: true)
    ];
}

internal sealed record TextColorDescriptor(
    string Label,
    string TextKey,
    Func<UiColorSettings, string> GetText,
    Action<UiColorSettings, string> SetText,
    string OutlineKey,
    Func<UiColorSettings, string> GetOutline,
    Action<UiColorSettings, string> SetOutline,
    string ShadowKey,
    Func<UiColorSettings, string> GetShadow,
    Action<UiColorSettings, string> SetShadow);

internal sealed record ColorDescriptor(
    string Label,
    string Key,
    Func<UiColorSettings, string> GetValue,
    Action<UiColorSettings, string> SetValue);

internal sealed record SoundDescriptor(
    string Label,
    string Key,
    Func<UiSoundSettings, string> GetValue,
    Action<UiSoundSettings, string> SetValue,
    bool PrefixWithFinalGroupName = false);

internal static class SettingsBinder
{
    public static void ApplyColors(AppSettings targetSettings, IReadOnlyDictionary<string, TextBox> colorTextBoxes)
    {
        targetSettings.Overlay.Colors ??= new UiColorSettings();

        foreach (TextColorDescriptor descriptor in SettingsDescriptors.TextColors)
        {
            ApplyColor(colorTextBoxes, descriptor.TextKey, value => descriptor.SetText(targetSettings.Overlay.Colors, value));
            ApplyColor(colorTextBoxes, descriptor.OutlineKey, value => descriptor.SetOutline(targetSettings.Overlay.Colors, value));
            ApplyColor(colorTextBoxes, descriptor.ShadowKey, value => descriptor.SetShadow(targetSettings.Overlay.Colors, value));
        }

        foreach (ColorDescriptor descriptor in SettingsDescriptors.IconColors)
        {
            ApplyColor(colorTextBoxes, descriptor.Key, value => descriptor.SetValue(targetSettings.Overlay.Colors, value));
        }

        foreach (ColorDescriptor descriptor in SettingsDescriptors.AnimationColors)
        {
            ApplyColor(colorTextBoxes, descriptor.Key, value => descriptor.SetValue(targetSettings.Overlay.Colors, value));
        }
    }

    public static void ApplySounds(AppSettings targetSettings, IReadOnlyDictionary<string, TextBox> soundTextBoxes)
    {
        targetSettings.Overlay.Sounds ??= new UiSoundSettings();

        foreach (SoundDescriptor descriptor in SettingsDescriptors.Sounds)
        {
            if (soundTextBoxes.TryGetValue(descriptor.Key, out TextBox? textBox))
            {
                descriptor.SetValue(targetSettings.Overlay.Sounds, textBox.Text.Trim());
            }
        }
    }

    private static void ApplyColor(
        IReadOnlyDictionary<string, TextBox> colorTextBoxes,
        string key,
        Action<string> setter)
    {
        if (colorTextBoxes.TryGetValue(key, out TextBox? textBox))
        {
            setter(ColorText.Format(ColorText.Parse(textBox.Text, System.Drawing.Color.White)));
        }
    }
}
