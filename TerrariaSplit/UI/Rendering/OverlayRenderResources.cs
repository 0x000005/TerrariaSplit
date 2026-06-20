using System.Drawing;

namespace TerrariaSplit;

internal sealed class OverlayRenderResources : IDisposable
{
    private const int MeasureCacheCapacity = 512;

    private readonly Dictionary<Font, FontMetrics> fontMetricsCache = new();
    private readonly Dictionary<Font, (float TopOffset, float Height)> digitVisualBoundsCache = new();
    private readonly Dictionary<TextMeasureKey, SizeF> measureCache = new();
    private StringFormat? typographicFormat;

    public OverlayFontCache Fonts { get; } = new();

    public BossIconCache BossIcons { get; } = new();

    /// <summary>
    /// Shared GenericTypographic format for timer text layout and drawing. The
    /// instance is never mutated after creation; resources are per render thread,
    /// so no cross-thread GDI+ access occurs.
    /// </summary>
    public StringFormat TypographicFormat =>
        typographicFormat ??= new StringFormat(StringFormat.GenericTypographic);

    public FontMetrics GetFontMetrics(Graphics graphics, Font font)
    {
        if (fontMetricsCache.TryGetValue(font, out FontMetrics metrics))
        {
            return metrics;
        }

        metrics = OverlayTextMetrics.GetFontMetrics(graphics, font);
        fontMetricsCache[font] = metrics;
        return metrics;
    }

    /// <summary>
    /// Vertical visual bounds for timer digit strings, cached per font. Digits
    /// share their cap height, so a reference string covering every glyph the
    /// timer can produce yields the same top/height as measuring the live text
    /// while avoiding a GraphicsPath build per frame.
    /// </summary>
    public (float TopOffset, float Height) GetTimerDigitsVisualBounds(Graphics graphics, Font font)
    {
        if (digitVisualBoundsCache.TryGetValue(font, out (float TopOffset, float Height) bounds))
        {
            return bounds;
        }

        RectangleF visualBounds = OverlayTextMetrics.GetTextVisualBounds(
            graphics,
            "0123456789:.",
            font,
            0f,
            0f,
            TypographicFormat);
        bounds = visualBounds.Height > 0f
            ? (visualBounds.Top, visualBounds.Height)
            : (0f, 0f);
        digitVisualBoundsCache[font] = bounds;
        return bounds;
    }

    public SizeF MeasureTimerText(Graphics graphics, string text, Font font, SizeF layoutSize)
    {
        var key = new TextMeasureKey(font, text, layoutSize.Width, layoutSize.Height);
        if (measureCache.TryGetValue(key, out SizeF size))
        {
            return size;
        }

        if (measureCache.Count >= MeasureCacheCapacity)
        {
            measureCache.Clear();
        }

        size = graphics.MeasureString(text, font, layoutSize, TypographicFormat);
        measureCache[key] = size;
        return size;
    }

    public void Dispose()
    {
        Fonts.Dispose();
        BossIcons.Dispose();
        typographicFormat?.Dispose();
        typographicFormat = null;
        fontMetricsCache.Clear();
        digitVisualBoundsCache.Clear();
        measureCache.Clear();
    }

    private readonly record struct TextMeasureKey(Font Font, string Text, float Width, float Height);
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
        string familyName = UiFontSettings.NormalizeFamilyName(columnSettings.FontFamily);
        var key = new FontKey(familyName, size, bold);
        if (cache.TryGetValue(key, out Font? font))
        {
            return font;
        }

        font = UiFontSettings.CreateFont(familyName, size, bold ? FontStyle.Bold : FontStyle.Regular);
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

    private readonly record struct FontKey(string FamilyName, float Size, bool Bold);
}
