using System.Drawing;

namespace TerrariaSplit;

internal sealed class OverlayRenderResources : IDisposable
{
    public OverlayFontCache Fonts { get; } = new();

    public BossIconCache BossIcons { get; } = new();

    public void Dispose()
    {
        Fonts.Dispose();
        BossIcons.Dispose();
    }
}

internal sealed class OverlayFontCache : IDisposable
{
    private readonly Dictionary<FontKey, Font> cache = new();

    public Font GetColumnFont(
        UiColumnSettings columnSettings,
        float scaleFactor,
        bool forceBold = false,
        float sizeScale = 1f)
    {
        float size = GetColumnFontSize(columnSettings, scaleFactor, sizeScale);
        bool bold = forceBold || columnSettings.Bold;
        var key = new FontKey(size, bold);
        if (cache.TryGetValue(key, out Font? font))
        {
            return font;
        }

        font = new Font(UiTheme.FontFamilyName, size, bold ? FontStyle.Bold : FontStyle.Regular);
        cache[key] = font;
        return font;
    }

    public static float GetColumnFontSize(
        UiColumnSettings columnSettings,
        float scaleFactor,
        float sizeScale = 1f)
    {
        return Math.Clamp(columnSettings.FontSize * scaleFactor * Math.Max(0.1f, sizeScale), 6f, 144f);
    }

    public void Dispose()
    {
        foreach (Font font in cache.Values)
        {
            font.Dispose();
        }

        cache.Clear();
    }

    private readonly record struct FontKey(float Size, bool Bold);
}
