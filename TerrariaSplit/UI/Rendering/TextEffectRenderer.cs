using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace TerrariaSplit;

internal static class TextEffectRenderer
{
    private const int SupersampleScale = 3;

    public static void DrawText(
        Graphics graphics,
        string text,
        Font font,
        Brush brush,
        Rectangle bounds,
        ContentAlignment alignment)
    {
        using var format = new StringFormat
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
            LineAlignment = StringAlignment.Center
        };

        format.Alignment = alignment switch
        {
            ContentAlignment.MiddleRight => StringAlignment.Far,
            ContentAlignment.MiddleCenter => StringAlignment.Center,
            _ => StringAlignment.Near
        };

        graphics.DrawString(text, font, brush, bounds, format);
    }

    public static void DrawStyledText(
        Graphics graphics,
        string text,
        Font font,
        TextRenderStyle style,
        Rectangle bounds,
        ContentAlignment alignment,
        float opacity)
    {
        if (string.IsNullOrEmpty(text) || opacity <= 0.01f)
        {
            return;
        }

        using var format = new StringFormat
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
            LineAlignment = StringAlignment.Center
        };

        format.Alignment = alignment switch
        {
            ContentAlignment.MiddleRight => StringAlignment.Far,
            ContentAlignment.MiddleCenter => StringAlignment.Center,
            _ => StringAlignment.Near
        };

        if (HasTextEffects(style))
        {
            using GraphicsPath path = CreateTextPath(
                graphics,
                text,
                font,
                new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height),
                format);
            DrawSupersampledTextLayer(
                graphics,
                path,
                font,
                style,
                opacity,
                targetGraphics =>
                {
                    using var fillBrush = new SolidBrush(WithOpacity(style.Fill, opacity));
                    targetGraphics.DrawString(text, font, fillBrush, bounds, format);
                });
            return;
        }

        using var textBrush = new SolidBrush(WithOpacity(style.Fill, opacity));
        graphics.DrawString(text, font, textBrush, bounds, format);
    }

    public static void DrawStyledString(
        Graphics graphics,
        string text,
        Font font,
        TextRenderStyle style,
        float x,
        float y,
        StringFormat format,
        float opacity)
    {
        if (string.IsNullOrEmpty(text) || opacity <= 0.01f)
        {
            return;
        }

        if (HasTextEffects(style))
        {
            using GraphicsPath path = CreateTextPath(graphics, text, font, x, y, format);
            DrawSupersampledTextLayer(
                graphics,
                path,
                font,
                style,
                opacity,
                targetGraphics =>
                {
                    using var fillBrush = new SolidBrush(WithOpacity(style.Fill, opacity));
                    targetGraphics.DrawString(text, font, fillBrush, x, y, format);
                });
            return;
        }

        using var textBrush = new SolidBrush(WithOpacity(style.Fill, opacity));
        graphics.DrawString(text, font, textBrush, x, y, format);
    }

    public static void DrawString(
        Graphics graphics,
        string text,
        Font font,
        Color color,
        float x,
        float y,
        StringFormat format,
        float opacity)
    {
        using var textBrush = new SolidBrush(WithOpacity(color, opacity));
        graphics.DrawString(text, font, textBrush, x, y, format);
    }

    public static void DrawOutlinedString(
        Graphics graphics,
        string text,
        Font font,
        Color fillColor,
        float x,
        float y,
        StringFormat format,
        TimeSpan elapsed,
        int thicknessPercent,
        string outlineStyle,
        float opacity)
    {
        using GraphicsPath path = CreateTextPath(graphics, text, font, x, y, format);
        if (path.PointCount == 0)
        {
            return;
        }

        string style = SplitCompletionOutlineStyles.Normalize(outlineStyle);
        if (style == SplitCompletionOutlineStyles.None)
        {
            DrawString(graphics, text, font, fillColor, x, y, format, opacity);
            return;
        }

        RectangleF bounds = path.GetBounds();
        RectangleF gradientBounds = InflateBounds(bounds, Math.Max(4f, font.Size * 0.35f));
        using var outlineBrush = new LinearGradientBrush(gradientBounds, Color.White, Color.White, LinearGradientMode.Horizontal);
        Color[] colors = SplitCompletionOutlineStyles.GetColors(style, elapsed.TotalSeconds)
            .Select(color => WithOpacity(color, opacity))
            .ToArray();
        var blend = new ColorBlend
        {
            Positions = CreateColorPositions(colors.Length),
            Colors = colors
        };
        outlineBrush.InterpolationColors = blend;

        float thickness = font.Size * Math.Clamp(thicknessPercent, 0, 100) / 100f;
        if (style is SplitCompletionOutlineStyles.Rainbow)
        {
            using var backingPen = new Pen(WithOpacity(Color.FromArgb(42, 255, 255, 255), opacity), Math.Max(1f, thickness * 1.35f))
            {
                LineJoin = LineJoin.Round
            };
            graphics.DrawPath(backingPen, path);
        }

        using var outlinePen = new Pen(outlineBrush, Math.Max(1f, thickness))
        {
            LineJoin = LineJoin.Round
        };
        graphics.DrawPath(outlinePen, path);

        using var fillBrush = new SolidBrush(WithOpacity(fillColor, opacity));
        graphics.FillPath(fillBrush, path);
    }

    public static void DrawImage(Graphics graphics, Image image, Rectangle bounds, float opacity, float brighten = 0f)
    {
        if (opacity >= 0.99f && brighten <= 0.001f)
        {
            graphics.DrawImage(image, bounds);
            return;
        }

        using var attributes = new ImageAttributes();
        float brightness = Math.Clamp(brighten, 0f, 0.5f);
        var matrix = new ColorMatrix
        {
            Matrix00 = 1f + brightness,
            Matrix11 = 1f + brightness,
            Matrix22 = 1f + brightness,
            Matrix33 = Math.Clamp(opacity, 0f, 1f),
            Matrix40 = brightness * 0.08f,
            Matrix41 = brightness * 0.08f,
            Matrix42 = brightness * 0.08f
        };
        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        graphics.DrawImage(
            image,
            bounds,
            0,
            0,
            image.Width,
            image.Height,
            GraphicsUnit.Pixel,
            attributes);
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

    public static Color WithOpacity(Color color, float opacity)
    {
        int alpha = (int)Math.Round(color.A * Math.Clamp(opacity, 0f, 1f));
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static bool HasTextEffects(TextRenderStyle style)
    {
        return style.ShadowPercent > 0 || style.OutlineThicknessPercent > 0;
    }

    private static void DrawSupersampledTextLayer(
        Graphics graphics,
        GraphicsPath path,
        Font font,
        TextRenderStyle style,
        float opacity,
        Action<Graphics> drawFill)
    {
        if (path.PointCount == 0)
        {
            return;
        }

        RectangleF layerBounds = GetTextEffectLayerBounds(graphics, path, font, style);
        if (layerBounds.Width <= 0f || layerBounds.Height <= 0f)
        {
            return;
        }

        int scale = SupersampleScale;
        int layerWidth = (int)Math.Ceiling(layerBounds.Width * scale);
        int layerHeight = (int)Math.Ceiling(layerBounds.Height * scale);
        if (layerWidth <= 0 || layerHeight <= 0 || layerWidth > 4096 || layerHeight > 4096)
        {
            DrawTextEffects(graphics, path, font, style, opacity);
            drawFill(graphics);
            return;
        }

        using var layer = new Bitmap(layerWidth, layerHeight, PixelFormat.Format32bppPArgb);
        using (Graphics layerGraphics = Graphics.FromImage(layer))
        {
            layerGraphics.Clear(Color.Transparent);
            layerGraphics.SmoothingMode = SmoothingMode.AntiAlias;
            layerGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            layerGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            layerGraphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            layerGraphics.CompositingMode = CompositingMode.SourceOver;
            layerGraphics.CompositingQuality = CompositingQuality.HighQuality;
            using var transform = new Matrix(
                scale,
                0f,
                0f,
                scale,
                -layerBounds.X * scale,
                -layerBounds.Y * scale);
            layerGraphics.Transform = transform;

            DrawTextEffects(layerGraphics, path, font, style, opacity);
            drawFill(layerGraphics);
        }

        GraphicsState state = graphics.Save();
        try
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.DrawImage(
                layer,
                new RectangleF(
                    layerBounds.X,
                    layerBounds.Y,
                    layerWidth / (float)scale,
                    layerHeight / (float)scale));
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    private static RectangleF GetTextEffectLayerBounds(
        Graphics graphics,
        GraphicsPath path,
        Font font,
        TextRenderStyle style)
    {
        RectangleF bounds = path.GetBounds();
        float shadowOffset = GetTextShadowOpacity(style.ShadowPercent) > 0f
            ? GetTextShadowOffset(graphics, font)
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

        float outlineRadius = style.OutlineThicknessPercent > 0
            ? GetTextOutlineRadius(graphics, font, style.OutlineThicknessPercent)
            : 0f;
        float padding = MathF.Ceiling(Math.Max(outlineRadius, shadowOffset) + 3f);
        return RectangleF.FromLTRB(
            MathF.Floor(bounds.Left - padding),
            MathF.Floor(bounds.Top - padding),
            MathF.Ceiling(bounds.Right + padding),
            MathF.Ceiling(bounds.Bottom + padding));
    }

    private static void DrawTextEffects(
        Graphics graphics,
        GraphicsPath path,
        Font font,
        TextRenderStyle style,
        float opacity)
    {
        if (path.PointCount == 0)
        {
            return;
        }

        float shadowOpacity = GetTextShadowOpacity(style.ShadowPercent);
        if (shadowOpacity > 0f)
        {
            using GraphicsPath shadowPath = (GraphicsPath)path.Clone();
            using var matrix = new Matrix();
            float offset = GetTextShadowOffset(graphics, font);
            matrix.Translate(offset, offset);
            shadowPath.Transform(matrix);

            using var shadowBrush = new SolidBrush(WithOpacity(style.Shadow, opacity * shadowOpacity));
            graphics.FillPath(shadowBrush, shadowPath);
        }

        if (style.OutlineThicknessPercent > 0)
        {
            using GraphicsPath outlinePath = CreateWidenedOutlinePath(
                path,
                GetTextOutlineRadius(graphics, font, style.OutlineThicknessPercent));
            using var outlineBrush = new SolidBrush(WithOpacity(style.Outline, opacity));
            graphics.FillPath(outlineBrush, outlinePath);
        }
    }

    private static GraphicsPath CreateWidenedOutlinePath(GraphicsPath path, float radius)
    {
        GraphicsPath outlinePath = (GraphicsPath)path.Clone();
        using var outlinePen = new Pen(Color.Black, Math.Max(0.2f, radius * 2f))
        {
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        outlinePath.Widen(outlinePen);

        return outlinePath;
    }

    private static float GetTextShadowOpacity(int shadowPercent)
    {
        float amount = Math.Clamp(shadowPercent, 0, 100) / 100f;
        if (amount <= 0f)
        {
            return 0f;
        }

        return Math.Clamp(0.08f + 0.58f * MathF.Pow(amount, 0.85f), 0f, 0.66f);
    }

    private static float GetTextShadowOffset(Graphics graphics, Font font)
    {
        return Math.Clamp(GetFontPixelsPerEm(graphics, font) * 0.08f, 1f, 4f);
    }

    private static float GetTextOutlineRadius(Graphics graphics, Font font, int thicknessPercent)
    {
        float amount = Math.Clamp(thicknessPercent, 0, 100) / 100f;
        float radius = GetFontPixelsPerEm(graphics, font) * 0.075f * amount + 0.15f;
        return Math.Clamp(radius, 0.2f, 3.5f);
    }

    private static float[] CreateColorPositions(int count)
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

    private static RectangleF InflateBounds(RectangleF bounds, float amount)
    {
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return new RectangleF(bounds.X - amount, bounds.Y - amount, amount * 2f + 1f, amount * 2f + 1f);
        }

        bounds.Inflate(amount, amount);
        return bounds;
    }

    private static float GetFontPixelsPerEm(Graphics graphics, Font font)
    {
        return font.SizeInPoints * graphics.DpiY / 72f;
    }
}
