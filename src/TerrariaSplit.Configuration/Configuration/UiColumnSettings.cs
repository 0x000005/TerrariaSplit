namespace TerrariaSplit.Configuration;

public static class UiColumnAlignment
{
    public const string Left = "Left";
    public const string Center = "Center";
    public const string Right = "Right";

    public static IReadOnlyList<string> All { get; } = [Left, Center, Right];

    public static string Normalize(string? value, string fallback)
    {
        foreach (string alignment in All)
        {
            if (string.Equals(value, alignment, StringComparison.OrdinalIgnoreCase))
            {
                return alignment;
            }
        }

        return fallback;
    }

    public static string GetDisplayName(string alignment)
    {
        return Normalize(alignment, Left) switch
        {
            Center => "Centered",
            Right => "Right aligned",
            _ => "Left aligned"
        };
    }
}

public sealed class UiColumnSettings
{
    public bool Show { get; set; } = true;
    public int Width { get; set; }
    public string FontFamily { get; set; } = UiFontDefaults.DefaultFamilyName;
    public float FontSize { get; set; }
    public bool Bold { get; set; }
    public bool Italic { get; set; }
}

public sealed class UiTextEffectSettings
{
    public int IconOpacityPercent { get; set; } = 100;
    public int IconShadowPercent { get; set; } = 20;
    public int IconOutlineThicknessPercent { get; set; }
    public int TimeOpacityPercent { get; set; } = 100;
    public int TimeShadowPercent { get; set; } = 40;
    public int TimeOutlineThicknessPercent { get; set; } = 30;
    public int NameOpacityPercent { get; set; } = 100;
    public int NameShadowPercent { get; set; } = 40;
    public int NameOutlineThicknessPercent { get; set; } = 30;
    public int DeltaOpacityPercent { get; set; } = 100;
    public int DeltaShadowPercent { get; set; } = 40;
    public int DeltaOutlineThicknessPercent { get; set; } = 30;
    public int AttachedIconOpacityPercent { get; set; } = 100;
    public int AttachedIconShadowPercent { get; set; } = 20;
    public int AttachedIconOutlineThicknessPercent { get; set; }
    public int AttachedTimeOpacityPercent { get; set; } = 100;
    public int AttachedTimeShadowPercent { get; set; } = 40;
    public int AttachedTimeOutlineThicknessPercent { get; set; } = 30;
    public int AttachedNameOpacityPercent { get; set; } = 100;
    public int AttachedNameShadowPercent { get; set; } = 40;
    public int AttachedNameOutlineThicknessPercent { get; set; } = 30;
    public int AttachedDeltaOpacityPercent { get; set; } = 100;
    public int AttachedDeltaShadowPercent { get; set; } = 40;
    public int AttachedDeltaOutlineThicknessPercent { get; set; } = 30;
    public int TimerOpacityPercent { get; set; } = 100;
    public int TimerShadowPercent { get; set; }
    public int TimerOutlineThicknessPercent { get; set; } = 25;
    public int TimerMillisecondsOpacityPercent { get; set; } = 100;
    public int TimerMillisecondsShadowPercent { get; set; }
    public int TimerMillisecondsOutlineThicknessPercent { get; set; } = 33;
}

public sealed class UiColumnLayoutSettings
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
        Width = 200,
        FontSize = 24f,
        Bold = true
    };

    public UiColumnSettings Name { get; set; } = new()
    {
        Show = false,
        Width = 260,
        FontSize = 16f,
        Bold = true
    };

    public UiColumnSettings Delta { get; set; } = new()
    {
        Show = true,
        Width = 200,
        FontSize = 6f,
        Bold = true
    };

    public UiColumnSettings AttachedIcon { get; set; } = new()
    {
        Show = true,
        Width = 240,
        FontSize = 55f,
        Bold = false
    };

    public UiColumnSettings AttachedTime { get; set; } = new()
    {
        Show = true,
        Width = 200,
        FontSize = 24f,
        Bold = true
    };

    public UiColumnSettings AttachedName { get; set; } = new()
    {
        Show = false,
        Width = 260,
        FontSize = 16f,
        Bold = true
    };

    public UiColumnSettings AttachedDelta { get; set; } = new()
    {
        Show = true,
        Width = 200,
        FontSize = 24f,
        Bold = true
    };

    public UiColumnSettings Timer { get; set; } = new()
    {
        Show = true,
        Width = 0,
        FontSize = 55f,
        Bold = true
    };

    public UiColumnSettings TimerMilliseconds { get; set; } = new()
    {
        Show = true,
        Width = 0,
        FontSize = 35f,
        Bold = true
    };

    public int IconNameGap { get; set; } = 5;
    public int NameTimeGap { get; set; } = 5;
    public int TimeDeltaGap { get; set; } = 5;
    public string IconAlignment { get; set; } = UiColumnAlignment.Right;
    public string NameAlignment { get; set; } = UiColumnAlignment.Center;
    public string TimeAlignment { get; set; } = UiColumnAlignment.Right;
    public string DeltaAlignment { get; set; } = UiColumnAlignment.Left;
    public int TimerOffsetX { get; set; } = 130;
    public int TimerOffsetY { get; set; } = 0;
}
