namespace TerrariaSplit;

internal sealed class UiColumnSettings
{
    public bool Show { get; set; } = true;
    public int Width { get; set; }
    public float FontSize { get; set; }
    public bool Bold { get; set; }
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
