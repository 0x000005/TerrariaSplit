using System.Drawing;
using System.Drawing.Drawing2D;

namespace TerrariaSplit.UI.Rendering;

internal static class OverlayTextMetrics
{
    public static FontMetrics GetFontMetrics(Graphics graphics, Font font)
    {
        FontFamily family = font.FontFamily;
        FontStyle style = font.Style;
        float emHeight = family.GetEmHeight(style);
        float pixelsPerEm = GetFontPixelsPerEm(graphics, font);
        float ascent = family.GetCellAscent(style) * pixelsPerEm / emHeight;
        float descent = family.GetCellDescent(style) * pixelsPerEm / emHeight;
        return new FontMetrics(ascent, descent);
    }

    public static RectangleF GetTextVisualBounds(
        Graphics graphics,
        string text,
        Font font,
        float x,
        float y,
        StringFormat format)
    {
        using GraphicsPath path = TextEffectRenderer.CreateTextPath(graphics, text, font, x, y, format);
        return path.PointCount > 0 ? path.GetBounds() : RectangleF.Empty;
    }

    public static Font CreatePixelFont(float size, FontStyle style, string? familyName = null)
    {
        return UiFontFactory.Default.CreateFont(familyName, Math.Max(1f, size), style, GraphicsUnit.Pixel);
    }

    private static float GetFontPixelsPerEm(Graphics graphics, Font font)
    {
        return font.Unit == GraphicsUnit.Pixel
            ? font.Size
            : font.SizeInPoints * graphics.DpiY / 72f;
    }
}
