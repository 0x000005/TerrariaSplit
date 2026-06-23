using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Rendering;

internal static class TextEffectRenderer
{
    private const int SupersampleScale = 3;
    private const int MaxImageEffectPercent = 100;
    private const float ImageShadowOffsetScale = 0.6f;
    private const int ImageShadowReferencePercent = 20;
    private const float ImageShadowPercentOffsetScale = 0.6f;
    private const int ImageShadowReferenceMaxOffset = 3;
    private const int ImageShadowMaxOffset = 5;
    private const float ImageShadowOpacityScale = 0.8f;

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
        DrawImage(graphics, image, bounds, opacity, ImageRenderStyle.Empty, brighten);
    }

    public static void DrawImage(
        Graphics graphics,
        Image image,
        Rectangle bounds,
        float opacity,
        ImageRenderStyle style,
        float brighten = 0f)
    {
        Rectangle drawBounds = GetAspectFitBounds(image, bounds);
        if (drawBounds.IsEmpty)
        {
            return;
        }

        if (style.HasEffects)
        {
            DrawImageEffects(graphics, image, bounds, drawBounds, style, opacity);
        }

        DrawImageCore(graphics, image, drawBounds, opacity, brighten);
    }

    private static void DrawImageCore(Graphics graphics, Image image, Rectangle drawBounds, float opacity, float brighten)
    {
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

    private static void DrawImageEffects(
        Graphics graphics,
        Image image,
        Rectangle effectBounds,
        Rectangle drawBounds,
        ImageRenderStyle style,
        float opacity)
    {
        float shadowOpacity = GetImageShadowOpacity(style.ShadowPercent) * opacity;
        if (shadowOpacity > 0f)
        {
            int shadowOffset = GetImageShadowOffset(effectBounds, style.ShadowPercent);
            DrawTintedImage(
                graphics,
                image,
                OffsetRectangle(drawBounds, shadowOffset, shadowOffset),
                style.Shadow,
                shadowOpacity);
        }

        int outlineRadius = GetImageOutlineRadius(effectBounds, style.OutlineThicknessPercent);
        float outlineOpacity = GetImageOutlineOpacity(style.OutlineThicknessPercent) * opacity;
        if (outlineRadius <= 0 || outlineOpacity <= 0f)
        {
            return;
        }

        ReadOnlySpan<Point> offsets =
        [
            new Point(-outlineRadius, 0),
            new Point(outlineRadius, 0),
            new Point(0, -outlineRadius),
            new Point(0, outlineRadius),
            new Point(-outlineRadius, -outlineRadius),
            new Point(outlineRadius, -outlineRadius),
            new Point(-outlineRadius, outlineRadius),
            new Point(outlineRadius, outlineRadius)
        ];
        foreach (Point offset in offsets)
        {
            DrawTintedImage(
                graphics,
                image,
                OffsetRectangle(drawBounds, offset.X, offset.Y),
                style.Outline,
                outlineOpacity);
        }
    }

    private static Rectangle OffsetRectangle(Rectangle rectangle, int x, int y)
    {
        return new Rectangle(rectangle.X + x, rectangle.Y + y, rectangle.Width, rectangle.Height);
    }

    private static int GetImageShadowOffset(Rectangle drawBounds, int shadowPercent)
    {
        if (shadowPercent <= 0)
        {
            return 0;
        }

        int size = Math.Min(drawBounds.Width, drawBounds.Height);
        int referenceOffset = Math.Clamp(
            (int)Math.Round(size * TextEffectGeometry.TextShadowOffsetRatio * ImageShadowOffsetScale),
            (int)TextEffectGeometry.TextShadowMinOffset,
            ImageShadowReferenceMaxOffset);
        float amount = Math.Clamp(shadowPercent, 0, MaxImageEffectPercent) /
            (float)ImageShadowReferencePercent *
            ImageShadowPercentOffsetScale;
        int offset = Math.Clamp(
            (int)Math.Round(referenceOffset * amount),
            (int)TextEffectGeometry.TextShadowMinOffset,
            ImageShadowMaxOffset);
        return offset;
    }

    private static int GetImageOutlineRadius(Rectangle drawBounds, int thicknessPercent)
    {
        if (thicknessPercent <= 0)
        {
            return 0;
        }

        int size = Math.Min(drawBounds.Width, drawBounds.Height);
        float amount = Math.Clamp(thicknessPercent, 0, MaxImageEffectPercent) / (float)MaxImageEffectPercent;
        int radius = (int)Math.Ceiling(size * 0.055f * amount);
        return Math.Clamp(radius, 1, 6);
    }

    private static float GetImageOutlineOpacity(int outlinePercent)
    {
        float amount = Math.Clamp(outlinePercent, 0, MaxImageEffectPercent) / (float)MaxImageEffectPercent;
        return amount <= 0f
            ? 0f
            : Math.Clamp(MathF.Pow(amount, 0.65f), 0f, 1f);
    }

    private static float GetImageShadowOpacity(int shadowPercent)
    {
        return Math.Clamp(
            TextEffectGeometry.GetTextShadowOpacity(shadowPercent) * ImageShadowOpacityScale,
            0f,
            1f);
    }

    private static void DrawTintedImage(
        Graphics graphics,
        Image image,
        Rectangle drawBounds,
        Color color,
        float opacity)
    {
        float alpha = Math.Clamp(opacity, 0f, 1f) * (color.A / 255f);
        if (alpha <= 0.001f)
        {
            return;
        }

        using var attributes = new ImageAttributes();
        var matrix = new ColorMatrix
        {
            Matrix00 = 0f,
            Matrix11 = 0f,
            Matrix22 = 0f,
            Matrix33 = alpha,
            Matrix40 = color.R / 255f,
            Matrix41 = color.G / 255f,
            Matrix42 = color.B / 255f
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

        float shadowOpacity = TextEffectGeometry.GetTextShadowOpacity(style);
        if (shadowOpacity > 0f)
        {
            using GraphicsPath shadowPath = (GraphicsPath)path.Clone();
            using var matrix = new Matrix();
            float offset = TextEffectGeometry.GetTextShadowOffset(graphics, font, style);
            matrix.Translate(offset, offset);
            shadowPath.Transform(matrix);

            using var shadowBrush = new SolidBrush(WithOpacity(style.Shadow, opacity * shadowOpacity));
            graphics.FillPath(shadowBrush, shadowPath);
        }

        if (style.OutlineThicknessPercent > 0)
        {
            float radius = TextEffectGeometry.GetTextOutlineRadius(graphics, font, style);
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
        float shadowOpacity = TextEffectGeometry.GetTextShadowOpacity(style);
        if (shadowOpacity > 0f)
        {
            int offset = (int)Math.Round(TextEffectGeometry.GetTextShadowOffset(graphics, font, style));
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
        float shadowOpacity = TextEffectGeometry.GetTextShadowOpacity(style);
        if (shadowOpacity > 0f)
        {
            float offset = TextEffectGeometry.GetTextShadowOffset(graphics, font, style);
            using var shadowBrush = new SolidBrush(WithOpacity(style.Shadow, opacity * shadowOpacity));
            graphics.DrawString(text, font, shadowBrush, x + offset, y + offset, format);
        }

        using var fillBrush = new SolidBrush(WithOpacity(style.Fill, opacity));
        graphics.DrawString(text, font, fillBrush, x, y, format);
    }

}
