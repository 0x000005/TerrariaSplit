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
        float opacity,
        bool supersampleEffects = true)
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
            if (!supersampleEffects && style.OutlineThicknessPercent <= 0)
            {
                DrawShadowedStringDirect(graphics, text, font, style, bounds, format, opacity);
                return;
            }

            using GraphicsPath path = CreateTextPath(
                graphics,
                text,
                font,
                new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height),
                format);
            if (!supersampleEffects)
            {
                DrawStyledPathDirect(graphics, path, font, style, opacity);
                return;
            }

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
        float opacity,
        bool supersampleEffects = true)
    {
        if (string.IsNullOrEmpty(text) || opacity <= 0.01f)
        {
            return;
        }

        if (HasTextEffects(style))
        {
            if (!supersampleEffects && style.OutlineThicknessPercent <= 0)
            {
                DrawShadowedStringDirect(graphics, text, font, style, x, y, format, opacity);
                return;
            }

            using GraphicsPath path = CreateTextPath(graphics, text, font, x, y, format);
            if (!supersampleEffects)
            {
                DrawStyledPathDirect(graphics, path, font, style, opacity);
                return;
            }

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
        RectangleF gradientBounds = TextEffectGeometry.InflateBounds(bounds, Math.Max(4f, font.Size * 0.35f));
        using var outlineBrush = new LinearGradientBrush(gradientBounds, Color.White, Color.White, LinearGradientMode.Horizontal);
        Color[] colors = SplitCompletionOutlineStyles.GetColors(style, elapsed.TotalSeconds)
            .Select(color => WithOpacity(color, opacity))
            .ToArray();
        var blend = new ColorBlend
        {
            Positions = TextEffectGeometry.CreateColorPositions(colors.Length),
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
        Rectangle drawBounds = GetAspectFitBounds(image, bounds);
        if (opacity >= 0.99f && brighten <= 0.001f)
        {
            graphics.DrawImage(image, drawBounds);
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
            drawBounds,
            0,
            0,
            image.Width,
            image.Height,
            GraphicsUnit.Pixel,
            attributes);
    }

    internal static Rectangle GetAspectFitBounds(Image image, Rectangle bounds)
    {
        return TextEffectGeometry.GetAspectFitBounds(image.Size, bounds);
    }

    public static GraphicsPath CreateTextPath(Graphics graphics, string text, Font font, float x, float y, StringFormat format)
    {
        return TextEffectGeometry.CreateTextPath(graphics, text, font, x, y, format);
    }

    public static GraphicsPath CreateTextPath(Graphics graphics, string text, Font font, RectangleF bounds, StringFormat format)
    {
        return TextEffectGeometry.CreateTextPath(graphics, text, font, bounds, format);
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
        return TextEffectGeometry.AlignTextPathBottom(
            graphics,
            referenceText,
            referenceFont,
            referenceX,
            referenceY,
            text,
            font,
            x,
            y,
            format);
    }

    public static Color WithOpacity(Color color, float opacity)
    {
        return TextEffectGeometry.WithOpacity(color, opacity);
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

        RectangleF layerBounds = TextEffectGeometry.GetTextEffectLayerBounds(graphics, path, font, style);
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

        float shadowOpacity = TextEffectGeometry.GetTextShadowOpacity(style.ShadowPercent);
        if (shadowOpacity > 0f)
        {
            using GraphicsPath shadowPath = (GraphicsPath)path.Clone();
            using var matrix = new Matrix();
            float offset = TextEffectGeometry.GetTextShadowOffset(graphics, font);
            matrix.Translate(offset, offset);
            shadowPath.Transform(matrix);

            using var shadowBrush = new SolidBrush(WithOpacity(style.Shadow, opacity * shadowOpacity));
            graphics.FillPath(shadowBrush, shadowPath);
        }

        if (style.OutlineThicknessPercent > 0)
        {
            float radius = TextEffectGeometry.GetTextOutlineRadius(graphics, font, style.OutlineThicknessPercent);
            using var outlinePen = new Pen(WithOpacity(style.Outline, opacity), Math.Max(0.2f, radius * 2f))
            {
                LineJoin = LineJoin.Round,
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            graphics.DrawPath(outlinePen, path);
        }
    }

    private static void DrawStyledPathDirect(
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

        DrawTextEffects(graphics, path, font, style, opacity);
        using var fillBrush = new SolidBrush(WithOpacity(style.Fill, opacity));
        graphics.FillPath(fillBrush, path);
    }

    private static void DrawShadowedStringDirect(
        Graphics graphics,
        string text,
        Font font,
        TextRenderStyle style,
        Rectangle bounds,
        StringFormat format,
        float opacity)
    {
        float shadowOpacity = TextEffectGeometry.GetTextShadowOpacity(style.ShadowPercent);
        if (shadowOpacity > 0f)
        {
            int offset = (int)Math.Round(TextEffectGeometry.GetTextShadowOffset(graphics, font));
            Rectangle shadowBounds = new(bounds.X + offset, bounds.Y + offset, bounds.Width, bounds.Height);
            using var shadowBrush = new SolidBrush(WithOpacity(style.Shadow, opacity * shadowOpacity));
            graphics.DrawString(text, font, shadowBrush, shadowBounds, format);
        }

        using var fillBrush = new SolidBrush(WithOpacity(style.Fill, opacity));
        graphics.DrawString(text, font, fillBrush, bounds, format);
    }

    private static void DrawShadowedStringDirect(
        Graphics graphics,
        string text,
        Font font,
        TextRenderStyle style,
        float x,
        float y,
        StringFormat format,
        float opacity)
    {
        float shadowOpacity = TextEffectGeometry.GetTextShadowOpacity(style.ShadowPercent);
        if (shadowOpacity > 0f)
        {
            float offset = TextEffectGeometry.GetTextShadowOffset(graphics, font);
            using var shadowBrush = new SolidBrush(WithOpacity(style.Shadow, opacity * shadowOpacity));
            graphics.DrawString(text, font, shadowBrush, x + offset, y + offset, format);
        }

        using var fillBrush = new SolidBrush(WithOpacity(style.Fill, opacity));
        graphics.DrawString(text, font, fillBrush, x, y, format);
    }

}
