using System.Drawing;
using System.Drawing.Drawing2D;

namespace TerrariaSplit.UI.Rendering;

internal static class TextEffectGeometry
{
    public const float TextShadowOffsetRatio = 0.08f;
    public const float TextShadowMinOffset = 1f;
    public const float TextShadowMaxOffset = 4f;
    public const int TextShadowReferencePercent = 20;
    public const float TextShadowPercentOffsetScale = 0.6f;
    public const float TextShadowDynamicMaxOffset = 5f;

    public static Rectangle GetAspectFitBounds(Size imageSize, Rectangle bounds)
    {
        if (imageSize.Width <= 0 ||
            imageSize.Height <= 0 ||
            bounds.Width <= 0 ||
            bounds.Height <= 0)
        {
            return Rectangle.Empty;
        }

        float scale = Math.Min(
            bounds.Width / (float)imageSize.Width,
            bounds.Height / (float)imageSize.Height);
        int width = Math.Max(1, (int)Math.Round(imageSize.Width * scale));
        int height = Math.Max(1, (int)Math.Round(imageSize.Height * scale));
        return new Rectangle(
            bounds.X + (bounds.Width - width) / 2,
            bounds.Y + (bounds.Height - height) / 2,
            width,
            height);
    }

    public static Color WithOpacity(Color color, float opacity)
    {
        int alpha = (int)Math.Round(color.A * Math.Clamp(opacity, 0f, 1f));
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    public static GraphicsPath CreateTextPath(Graphics graphics, string text, Font font, float x, float y, StringFormat format)
    {
        var path = new GraphicsPath();
        using StringFormat pathFormat = (StringFormat)format.Clone();
        path.AddString(
            text,
            font.FontFamily,
            (int)font.Style,
            emSize: GetFontPixelsPerEm(graphics, font),
            origin: new PointF(x, y),
            format: pathFormat);
        return path;
    }

    public static GraphicsPath CreateTextPath(Graphics graphics, string text, Font font, RectangleF bounds, StringFormat format)
    {
        var path = new GraphicsPath();
        using StringFormat pathFormat = (StringFormat)format.Clone();
        path.AddString(
            text,
            font.FontFamily,
            (int)font.Style,
            emSize: GetFontPixelsPerEm(graphics, font),
            layoutRect: bounds,
            format: pathFormat);
        return path;
    }

    public static float AlignTextPathBottom(
        Graphics graphics,
        string referenceText,
        Font referenceFont,
        float referenceX,
        float referenceY,
        string text,
        Font font,
        float x,
        float y,
        StringFormat format)
    {
        using GraphicsPath referencePath = CreateTextPath(graphics, referenceText, referenceFont, referenceX, referenceY, format);
        using GraphicsPath path = CreateTextPath(graphics, text, font, x, y, format);
        if (referencePath.PointCount == 0 || path.PointCount == 0)
        {
            return y;
        }

        return y + referencePath.GetBounds().Bottom - path.GetBounds().Bottom;
    }

    public static void CenterPath(GraphicsPath path, float centerX, float centerY)
    {
        RectangleF bounds = path.GetBounds();
        using var matrix = new Matrix();
        matrix.Translate(centerX - (bounds.Left + bounds.Width / 2f), centerY - (bounds.Top + bounds.Height / 2f));
        path.Transform(matrix);
    }

    public static RectangleF GetTextEffectLayerBounds(
        Graphics graphics,
        GraphicsPath path,
        Font font,
        TextRenderStyle style)
    {
        RectangleF bounds = path.GetBounds();
        float shadowOffset = GetTextShadowOpacity(style) > 0f
            ? GetTextShadowOffset(graphics, font, style)
            : 0f;
        if (shadowOffset > 0f)
        {
            bounds = RectangleF.Union(
                bounds,
                new RectangleF(
                    bounds.X + shadowOffset,
                    bounds.Y + shadowOffset,
                    bounds.Width,
                    bounds.Height));
        }

        float outlineRadius = GetTextOutlineRadius(graphics, font, style);
        float padding = MathF.Ceiling(Math.Max(outlineRadius, shadowOffset) + 3f);
        return RectangleF.FromLTRB(
            MathF.Floor(bounds.Left - padding),
            MathF.Floor(bounds.Top - padding),
            MathF.Ceiling(bounds.Right + padding),
            MathF.Ceiling(bounds.Bottom + padding));
    }

    public static float GetTextShadowOpacity(int shadowPercent)
    {
        float amount = Math.Clamp(shadowPercent, 0, 100) / 10f;
        if (amount <= 0f)
        {
            return 0f;
        }

        if (amount <= 1f)
        {
            return Math.Clamp(0.08f + 0.58f * MathF.Pow(amount, 0.85f), 0f, 0.66f);
        }

        return Math.Clamp(0.66f + 0.34f * ((amount - 1f) / 4f), 0f, 1f);
    }

    public static float GetTextShadowOpacity(TextRenderStyle style)
    {
        return style.LinearEffects
            ? Math.Clamp(style.ShadowPercent, 0, 100) / 100f
            : GetTextShadowOpacity(style.ShadowPercent);
    }

    public static float GetTextShadowOffset(Graphics graphics, Font font)
    {
        return GetTextShadowReferenceOffset(graphics, font);
    }

    private static float GetTextShadowReferenceOffset(Graphics graphics, Font font)
    {
        return Math.Clamp(
            GetFontPixelsPerEm(graphics, font) * TextShadowOffsetRatio,
            TextShadowMinOffset,
            TextShadowMaxOffset);
    }

    public static float GetTextShadowOffset(Graphics graphics, Font font, TextRenderStyle style)
    {
        if (style.ShadowPercent <= 0)
        {
            return 0f;
        }

        float referenceOffset = GetTextShadowReferenceOffset(graphics, font);
        float amount = Math.Clamp(style.ShadowPercent, 0, 100) /
            (float)TextShadowReferencePercent *
            TextShadowPercentOffsetScale;
        float offset = Math.Clamp(
            referenceOffset * amount,
            TextShadowMinOffset,
            TextShadowDynamicMaxOffset);
        return offset;
    }

    public static float GetTextOutlineRadius(Graphics graphics, Font font, int thicknessPercent)
    {
        float amount = Math.Clamp(thicknessPercent, 0, 100) / 40f;
        float radius = GetFontPixelsPerEm(graphics, font) * 0.075f * amount + 0.15f;
        return Math.Clamp(radius, 0.2f, 8f);
    }

    public static float GetTextOutlineRadius(Graphics graphics, Font font, TextRenderStyle style)
    {
        if (!style.LinearEffects)
        {
            return style.OutlineThicknessPercent > 0
                ? GetTextOutlineRadius(graphics, font, style.OutlineThicknessPercent)
                : 0f;
        }

        if (style.OutlineThicknessPercent <= 0)
        {
            return 0f;
        }

        float amount = Math.Clamp(style.OutlineThicknessPercent, 0, 100) / 40f;
        float radius = GetFontPixelsPerEm(graphics, font) * 0.075f * amount;
        return Math.Clamp(radius, 0.2f, 8f);
    }

    public static float[] CreateColorPositions(int count)
    {
        if (count <= 1)
        {
            return new[] { 0f };
        }

        var positions = new float[count];
        for (int i = 0; i < count; i++)
        {
            positions[i] = i / (float)(count - 1);
        }

        return positions;
    }

    public static RectangleF InflateBounds(RectangleF bounds, float amount)
    {
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return new RectangleF(bounds.X - amount, bounds.Y - amount, amount * 2f + 1f, amount * 2f + 1f);
        }

        bounds.Inflate(amount, amount);
        return bounds;
    }

    public static float GetFontPixelsPerEm(Graphics graphics, Font font)
    {
        return font.SizeInPoints * graphics.DpiY / 72f;
    }
}
