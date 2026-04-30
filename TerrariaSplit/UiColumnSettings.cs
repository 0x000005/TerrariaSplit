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
    public UiColumnSettings Icon { get; set; } = new()
    {
        Show = true,
        Width = 116,
        FontSize = 30f,
        Bold = false
    };

    public UiColumnSettings Time { get; set; } = new()
    {
        Show = true,
        Width = 86,
        FontSize = 13.5f,
        Bold = true
    };

    public UiColumnSettings Delta { get; set; } = new()
    {
        Show = true,
        Width = 72,
        FontSize = 13.5f,
        Bold = true
    };

    public UiColumnSettings Timer { get; set; } = new()
    {
        Show = true,
        Width = 0,
        FontSize = 34f,
        Bold = true
    };

    public UiColumnSettings TimerMilliseconds { get; set; } = new()
    {
        Show = true,
        Width = 0,
        FontSize = 18f,
        Bold = true
    };
}
