namespace TerrariaSplit.Configuration;

public sealed record SplitCompletionTextStyle
{
    public string FontFamily { get; set; } = UiFontDefaults.DefaultFamilyName;
    public bool Bold { get; set; } = true;
    public bool Italic { get; set; }
    public int OpacityPercent { get; set; } = 100;
    public int ShadowPercent { get; set; }
    public int OutlineThicknessPercent { get; set; } = 25;

    public void Normalize()
    {
        FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? UiFontDefaults.DefaultFamilyName : FontFamily.Trim();
        OpacityPercent = Math.Clamp(OpacityPercent, 0, 100);
        ShadowPercent = Math.Clamp(ShadowPercent, 0, 100);
        OutlineThicknessPercent = Math.Clamp(OutlineThicknessPercent, 0, 100);
    }
}

public sealed class SplitCompletionTextSettings
{
    public SplitCompletionTextStyle Time { get; set; } = new();
    public SplitCompletionTextStyle Hint { get; set; } = new() { OpacityPercent = 86 };
    public SplitCompletionTextStyle Delta { get; set; } = new();

    public SplitCompletionTextSettings Clone() => new()
    {
        Time = (Time ?? new()) with { },
        Hint = (Hint ?? new() { OpacityPercent = 86 }) with { },
        Delta = (Delta ?? new()) with { }
    };

    public void Normalize()
    {
        (Time ??= new()).Normalize();
        (Hint ??= new() { OpacityPercent = 86 }).Normalize();
        (Delta ??= new()).Normalize();
    }
}
