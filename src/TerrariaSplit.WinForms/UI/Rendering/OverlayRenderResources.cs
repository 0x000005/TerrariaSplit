using System.Drawing;
using System.Runtime.CompilerServices;

namespace TerrariaSplit.UI.Rendering;

internal sealed class OverlayRenderResources : IDisposable
{
    private const int MeasureCacheCapacity = 512;

    private readonly Dictionary<Font, FontMetrics> fontMetricsCache = new();
    private readonly Dictionary<Font, (float TopOffset, float Height)> digitVisualBoundsCache = new();
    private readonly Dictionary<TextMeasureKey, SizeF> measureCache = new();
    private StringFormat? typographicFormat;

    public OverlayRenderResources()
        : this(UiFontFactory.Default)
    {
    }

    internal OverlayRenderResources(IUiFontFactory fontFactory)
    {
        Fonts = new OverlayFontCache(fontFactory);
        SplitCompletionAnimationText = new SplitCompletionAnimationTextCache(fontFactory);
    }

    public OverlayFontCache Fonts { get; }

    public BossIconCache BossIcons { get; } = new();

    internal SplitCompletionAnimationTextCache SplitCompletionAnimationText { get; }

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
        SplitCompletionAnimationText.Dispose();
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

internal readonly struct SplitCompletionAnimationTextCacheKey : IEquatable<SplitCompletionAnimationTextCacheKey>
{
    public SplitCompletionAnimationTextCacheKey(
        SplitCompletionAnimation animation,
        Rectangle textBounds,
        float textCenterX,
        float scale,
        string? fontFamily,
        string? language,
        bool enableDynamicDeltaTimeUnits,
        float dpiX,
        float dpiY)
    {
        Animation = animation;
        TextBounds = textBounds;
        TextCenterX = textCenterX;
        Scale = scale;
        FontFamily = fontFamily ?? string.Empty;
        Language = language ?? string.Empty;
        EnableDynamicDeltaTimeUnits = enableDynamicDeltaTimeUnits;
        DpiX = dpiX;
        DpiY = dpiY;
        SegmentTime = animation.SegmentTime;
        SplitTime = animation.SplitTime;
        SegmentComparison = animation.PersonalBestSegmentComparison;
        SplitComparison = animation.ReferenceSplitComparison;
        ShowSegmentComparison = animation.ShowSegmentComparison;
        ShowSplitComparison = animation.ShowSplitComparison;
    }

    public SplitCompletionAnimation Animation { get; }

    public Rectangle TextBounds { get; }

    public float TextCenterX { get; }

    public float Scale { get; }

    public string FontFamily { get; }

    public string Language { get; }

    public bool EnableDynamicDeltaTimeUnits { get; }

    public float DpiX { get; }

    public float DpiY { get; }

    public TimeSpan SegmentTime { get; }

    public TimeSpan SplitTime { get; }

    public SplitComparison SegmentComparison { get; }

    public SplitComparison SplitComparison { get; }

    public bool ShowSegmentComparison { get; }

    public bool ShowSplitComparison { get; }

    public bool Equals(SplitCompletionAnimationTextCacheKey other)
    {
        return ReferenceEquals(Animation, other.Animation) &&
            TextBounds == other.TextBounds &&
            TextCenterX.Equals(other.TextCenterX) &&
            Scale.Equals(other.Scale) &&
            string.Equals(FontFamily, other.FontFamily, StringComparison.Ordinal) &&
            string.Equals(Language, other.Language, StringComparison.Ordinal) &&
            EnableDynamicDeltaTimeUnits == other.EnableDynamicDeltaTimeUnits &&
            DpiX.Equals(other.DpiX) &&
            DpiY.Equals(other.DpiY) &&
            SegmentTime == other.SegmentTime &&
            SplitTime == other.SplitTime &&
            SegmentComparison == other.SegmentComparison &&
            SplitComparison == other.SplitComparison &&
            ShowSegmentComparison == other.ShowSegmentComparison &&
            ShowSplitComparison == other.ShowSplitComparison;
    }

    public override bool Equals(object? obj)
    {
        return obj is SplitCompletionAnimationTextCacheKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(RuntimeHelpers.GetHashCode(Animation));
        hash.Add(TextBounds);
        hash.Add(TextCenterX);
        hash.Add(Scale);
        hash.Add(FontFamily, StringComparer.Ordinal);
        hash.Add(Language, StringComparer.Ordinal);
        hash.Add(EnableDynamicDeltaTimeUnits);
        hash.Add(DpiX);
        hash.Add(DpiY);
        hash.Add(SegmentTime);
        hash.Add(SplitTime);
        hash.Add(SegmentComparison);
        hash.Add(SplitComparison);
        hash.Add(ShowSegmentComparison);
        hash.Add(ShowSplitComparison);
        return hash.ToHashCode();
    }

    public static bool operator ==(
        SplitCompletionAnimationTextCacheKey left,
        SplitCompletionAnimationTextCacheKey right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(
        SplitCompletionAnimationTextCacheKey left,
        SplitCompletionAnimationTextCacheKey right)
    {
        return !left.Equals(right);
    }
}

internal sealed class SplitCompletionAnimationTextCache : IDisposable
{
    private SplitCompletionAnimationTextResources? cached;

    public SplitCompletionAnimationTextCache(IUiFontFactory fontFactory)
    {
        FontFactory = fontFactory;
    }

    public IUiFontFactory FontFactory { get; }

    public bool TryGet(
        SplitCompletionAnimationTextCacheKey key,
        out SplitCompletionAnimationTextResources resources)
    {
        if (cached is not null && cached.Key == key)
        {
            resources = cached;
            return true;
        }

        resources = null!;
        return false;
    }

    public void Store(SplitCompletionAnimationTextResources resources)
    {
        SplitCompletionAnimationTextResources? previous = cached;
        cached = resources;
        previous?.Dispose();
    }

    public void Clear()
    {
        SplitCompletionAnimationTextResources? previous = cached;
        cached = null;
        previous?.Dispose();
    }

    public void Dispose()
    {
        Clear();
    }
}

internal sealed class SplitCompletionAnimationTextResources : IDisposable
{
    public SplitCompletionAnimationTextResources(
        SplitCompletionAnimationTextCacheKey key,
        string segmentLabel,
        string segmentValue,
        string segmentDelta,
        string splitLabel,
        string splitValue,
        string splitDelta,
        Font labelFont,
        Font valueFont,
        Font deltaFont)
    {
        Key = key;
        SegmentLabel = segmentLabel;
        SegmentValue = segmentValue;
        SegmentDelta = segmentDelta;
        SplitLabel = splitLabel;
        SplitValue = splitValue;
        SplitDelta = splitDelta;
        LabelFont = labelFont;
        ValueFont = valueFont;
        DeltaFont = deltaFont;
    }

    public SplitCompletionAnimationTextCacheKey Key { get; }

    public string SegmentLabel { get; }

    public string SegmentValue { get; }

    public string SegmentDelta { get; }

    public string SplitLabel { get; }

    public string SplitValue { get; }

    public string SplitDelta { get; }

    public Font LabelFont { get; }

    public Font ValueFont { get; }

    public Font DeltaFont { get; }

    public void Dispose()
    {
        LabelFont.Dispose();
        ValueFont.Dispose();
        DeltaFont.Dispose();
    }
}

internal sealed class OverlayFontCache : IDisposable
{
    private readonly Dictionary<FontKey, Font> cache = new();
    private readonly IUiFontFactory fontFactory;

    public OverlayFontCache()
        : this(UiFontFactory.Default)
    {
    }

    internal OverlayFontCache(IUiFontFactory fontFactory)
    {
        this.fontFactory = fontFactory;
    }

    public Font GetColumnFont(
        UiColumnSettings columnSettings,
        float scaleFactor,
        bool forceBold = false,
        float sizeScale = 1f,
        float minimumSize = 6f)
    {
        float size = GetColumnFontSize(columnSettings, scaleFactor, sizeScale, minimumSize);
        bool bold = forceBold || columnSettings.Bold;
        string familyName = fontFactory.NormalizeFamilyName(columnSettings.FontFamily);
        var key = new FontKey(familyName, size, bold);
        if (cache.TryGetValue(key, out Font? font))
        {
            return font;
        }

        font = fontFactory.CreateFont(familyName, size, bold ? FontStyle.Bold : FontStyle.Regular);
        cache[key] = font;
        return font;
    }

    public static float GetColumnFontSize(
        UiColumnSettings columnSettings,
        float scaleFactor,
        float sizeScale = 1f,
        float minimumSize = 6f)
    {
        return Math.Clamp(
            columnSettings.FontSize * scaleFactor * Math.Max(0.01f, sizeScale),
            Math.Clamp(minimumSize, 1f, 144f),
            144f);
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
