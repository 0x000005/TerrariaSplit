using System.Windows.Forms;

namespace TerrariaSplit;

internal static class SettingsDescriptors
{
    public static IReadOnlyList<TextColorDescriptor> TextColors { get; } =
    [
        new("Reference text",
            nameof(UiColorSettings.ReferenceText), colors => colors.ReferenceText, (colors, value) => colors.ReferenceText = value,
            nameof(UiColorSettings.ReferenceTextOutline), colors => colors.ReferenceTextOutline, (colors, value) => colors.ReferenceTextOutline = value,
            nameof(UiColorSettings.ReferenceTextShadow), colors => colors.ReferenceTextShadow, (colors, value) => colors.ReferenceTextShadow = value),
        new("Active reference text",
            nameof(UiColorSettings.ActiveReferenceText), colors => colors.ActiveReferenceText, (colors, value) => colors.ActiveReferenceText = value,
            nameof(UiColorSettings.ActiveReferenceTextOutline), colors => colors.ActiveReferenceTextOutline, (colors, value) => colors.ActiveReferenceTextOutline = value,
            nameof(UiColorSettings.ActiveReferenceTextShadow), colors => colors.ActiveReferenceTextShadow, (colors, value) => colors.ActiveReferenceTextShadow = value),
        new("Completed split text",
            nameof(UiColorSettings.SplitText), colors => colors.SplitText, (colors, value) => colors.SplitText = value,
            nameof(UiColorSettings.SplitTextOutline), colors => colors.SplitTextOutline, (colors, value) => colors.SplitTextOutline = value,
            nameof(UiColorSettings.SplitTextShadow), colors => colors.SplitTextShadow, (colors, value) => colors.SplitTextShadow = value),
        new("Delta ahead text",
            nameof(UiColorSettings.DeltaAheadText), colors => colors.DeltaAheadText, (colors, value) => colors.DeltaAheadText = value,
            nameof(UiColorSettings.DeltaAheadTextOutline), colors => colors.DeltaAheadTextOutline, (colors, value) => colors.DeltaAheadTextOutline = value,
            nameof(UiColorSettings.DeltaAheadTextShadow), colors => colors.DeltaAheadTextShadow, (colors, value) => colors.DeltaAheadTextShadow = value),
        new("Delta behind text",
            nameof(UiColorSettings.DeltaBehindText), colors => colors.DeltaBehindText, (colors, value) => colors.DeltaBehindText = value,
            nameof(UiColorSettings.DeltaBehindTextOutline), colors => colors.DeltaBehindTextOutline, (colors, value) => colors.DeltaBehindTextOutline = value,
            nameof(UiColorSettings.DeltaBehindTextShadow), colors => colors.DeltaBehindTextShadow, (colors, value) => colors.DeltaBehindTextShadow = value),
        new("Timer text",
            nameof(UiColorSettings.TimerText), colors => colors.TimerText, (colors, value) => colors.TimerText = value,
            nameof(UiColorSettings.TimerTextOutline), colors => colors.TimerTextOutline, (colors, value) => colors.TimerTextOutline = value,
            nameof(UiColorSettings.TimerTextShadow), colors => colors.TimerTextShadow, (colors, value) => colors.TimerTextShadow = value),
        new("Timer ahead text",
            nameof(UiColorSettings.TimerAheadText), colors => colors.TimerAheadText, (colors, value) => colors.TimerAheadText = value,
            nameof(UiColorSettings.TimerAheadTextOutline), colors => colors.TimerAheadTextOutline, (colors, value) => colors.TimerAheadTextOutline = value,
            nameof(UiColorSettings.TimerAheadTextShadow), colors => colors.TimerAheadTextShadow, (colors, value) => colors.TimerAheadTextShadow = value),
        new("Timer behind text",
            nameof(UiColorSettings.TimerBehindText), colors => colors.TimerBehindText, (colors, value) => colors.TimerBehindText = value,
            nameof(UiColorSettings.TimerBehindTextOutline), colors => colors.TimerBehindTextOutline, (colors, value) => colors.TimerBehindTextOutline = value,
            nameof(UiColorSettings.TimerBehindTextShadow), colors => colors.TimerBehindTextShadow, (colors, value) => colors.TimerBehindTextShadow = value),
        new("Timer record text",
            nameof(UiColorSettings.TimerRecordText), colors => colors.TimerRecordText, (colors, value) => colors.TimerRecordText = value,
            nameof(UiColorSettings.TimerRecordTextOutline), colors => colors.TimerRecordTextOutline, (colors, value) => colors.TimerRecordTextOutline = value,
            nameof(UiColorSettings.TimerRecordTextShadow), colors => colors.TimerRecordTextShadow, (colors, value) => colors.TimerRecordTextShadow = value),
        new("Timer no record text",
            nameof(UiColorSettings.TimerNoRecordText), colors => colors.TimerNoRecordText, (colors, value) => colors.TimerNoRecordText = value,
            nameof(UiColorSettings.TimerNoRecordTextOutline), colors => colors.TimerNoRecordTextOutline, (colors, value) => colors.TimerNoRecordTextOutline = value,
            nameof(UiColorSettings.TimerNoRecordTextShadow), colors => colors.TimerNoRecordTextShadow, (colors, value) => colors.TimerNoRecordTextShadow = value),
        new("Timer paused text",
            nameof(UiColorSettings.TimerPausedText), colors => colors.TimerPausedText, (colors, value) => colors.TimerPausedText = value,
            nameof(UiColorSettings.TimerPausedTextOutline), colors => colors.TimerPausedTextOutline, (colors, value) => colors.TimerPausedTextOutline = value,
            nameof(UiColorSettings.TimerPausedTextShadow), colors => colors.TimerPausedTextShadow, (colors, value) => colors.TimerPausedTextShadow = value)
    ];

    public static IReadOnlyList<ColorDescriptor> AnimationColors { get; } =
    [
        new("Animation text",
            nameof(UiColorSettings.SplitCompletionLabelText),
            colors => colors.SplitCompletionLabelText,
            (colors, value) => colors.SplitCompletionLabelText = value),
        new("Animation main time",
            nameof(UiColorSettings.SplitCompletionTimeText),
            colors => colors.SplitCompletionTimeText,
            (colors, value) => colors.SplitCompletionTimeText = value)
    ];

    public static IReadOnlyList<SoundDescriptor> Sounds { get; } =
    [
        new("Pause sound", nameof(UiSoundSettings.Pause), sounds => sounds.Pause, (sounds, value) => sounds.Pause = value),
        new("Resume sound", nameof(UiSoundSettings.Resume), sounds => sounds.Resume, (sounds, value) => sounds.Resume = value),
        new("Reset sound", nameof(UiSoundSettings.Reset), sounds => sounds.Reset, (sounds, value) => sounds.Reset = value),
        new("Enter world sound", nameof(UiSoundSettings.EnterWorld), sounds => sounds.EnterWorld, (sounds, value) => sounds.EnterWorld = value),
        new("Split: total slower, segment slower", nameof(UiSoundSettings.SplitBehindReferenceBehindSegment), sounds => sounds.SplitBehindReferenceBehindSegment, (sounds, value) => sounds.SplitBehindReferenceBehindSegment = value),
        new("Split: total slower, segment not slower", nameof(UiSoundSettings.SplitBehindReferenceAheadSegment), sounds => sounds.SplitBehindReferenceAheadSegment, (sounds, value) => sounds.SplitBehindReferenceAheadSegment = value),
        new("Split: total not slower, segment slower", nameof(UiSoundSettings.SplitAheadReferenceBehindSegment), sounds => sounds.SplitAheadReferenceBehindSegment, (sounds, value) => sounds.SplitAheadReferenceBehindSegment = value),
        new("Split: total not slower, segment not slower", nameof(UiSoundSettings.SplitAheadReferenceAheadSegment), sounds => sounds.SplitAheadReferenceAheadSegment, (sounds, value) => sounds.SplitAheadReferenceAheadSegment = value)
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
    Action<UiSoundSettings, string> SetValue);

internal static class SettingsBinder
{
    public static void ApplyColors(AppSettings targetSettings, IReadOnlyDictionary<string, TextBox> colorTextBoxes)
    {
        targetSettings.Colors ??= new UiColorSettings();

        foreach (TextColorDescriptor descriptor in SettingsDescriptors.TextColors)
        {
            ApplyColor(colorTextBoxes, descriptor.TextKey, value => descriptor.SetText(targetSettings.Colors, value));
            ApplyColor(colorTextBoxes, descriptor.OutlineKey, value => descriptor.SetOutline(targetSettings.Colors, value));
            ApplyColor(colorTextBoxes, descriptor.ShadowKey, value => descriptor.SetShadow(targetSettings.Colors, value));
        }

        foreach (ColorDescriptor descriptor in SettingsDescriptors.AnimationColors)
        {
            ApplyColor(colorTextBoxes, descriptor.Key, value => descriptor.SetValue(targetSettings.Colors, value));
        }
    }

    public static void ApplySounds(AppSettings targetSettings, IReadOnlyDictionary<string, TextBox> soundTextBoxes)
    {
        targetSettings.Sounds ??= new UiSoundSettings();

        foreach (SoundDescriptor descriptor in SettingsDescriptors.Sounds)
        {
            if (soundTextBoxes.TryGetValue(descriptor.Key, out TextBox? textBox))
            {
                descriptor.SetValue(targetSettings.Sounds, textBox.Text.Trim());
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
