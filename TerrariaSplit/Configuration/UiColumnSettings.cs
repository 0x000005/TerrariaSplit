namespace TerrariaSplit;

internal sealed class UiColumnSettings
{
    public bool Show { get; set; } = true;
    public int Width { get; set; }
    public string FontFamily { get; set; } = UiFontSettings.DefaultFamilyName;
    public float FontSize { get; set; }
    public bool Bold { get; set; }
}

internal sealed class UiTextEffectSettings
{
    public int IconOpacityPercent { get; set; } = 100;
    public int TimeOpacityPercent { get; set; } = 100;
    public int TimeShadowPercent { get; set; }
    public int TimeOutlineThicknessPercent { get; set; }
    public int DeltaOpacityPercent { get; set; } = 100;
    public int DeltaShadowPercent { get; set; }
    public int DeltaOutlineThicknessPercent { get; set; }
    public int TimerOpacityPercent { get; set; } = 100;
    public int TimerShadowPercent { get; set; }
    public int TimerOutlineThicknessPercent { get; set; }
    public int TimerMillisecondsOpacityPercent { get; set; } = 100;
    public int TimerMillisecondsShadowPercent { get; set; }
    public int TimerMillisecondsOutlineThicknessPercent { get; set; }
}

internal sealed class UiColumnLayoutSettings
{
    public int ScalePercent { get; set; } = 100;

    public UiColumnSettings Icon { get; set; } = new()
    {
        Show = true,
        Width = 240,
        FontSize = 55f,
        Bold = false
    };

    public UiColumnSettings Time { get; set; } = new()
    {
        Show = true,
        Width = 130,
        FontSize = 13.5f,
        Bold = true
    };

    public UiColumnSettings Delta { get; set; } = new()
    {
        Show = true,
        Width = 130,
        FontSize = 13.5f,
        Bold = true
    };

    public UiColumnSettings Timer { get; set; } = new()
    {
        Show = true,
        Width = 0,
        FontSize = 36f,
        Bold = true
    };

    public UiColumnSettings TimerMilliseconds { get; set; } = new()
    {
        Show = true,
        Width = 0,
        FontSize = 18f,
        Bold = true
    };

    public int TimerOffsetX { get; set; } = 0;
    public int TimerOffsetY { get; set; } = 0;
}
