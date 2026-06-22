using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Rendering;

internal static class TimerRenderer
{
    public static void Render(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources)
    {
        if (!context.Settings.Overlay.Columns.Timer.Show && !context.Settings.Overlay.Columns.TimerMilliseconds.Show)
        {
            return;
        }

        Rectangle timeRect = GetTimerTextBounds(context, context.Layout.TimerRect);
        TimerTextLayout timerTextLayout = DrawTimerText(
            graphics,
            context,
            resources,
            OverlayTextStyles.GetTimerTextStyle(
                context.Settings,
                context.Statuses,
                context.CurrentSplitIndex,
                context.TimerPhase,
                context.TimerElapsed,
                context.Palette,
                milliseconds: false),
            OverlayTextStyles.GetTimerTextStyle(
                context.Settings,
                context.Statuses,
                context.CurrentSplitIndex,
                context.TimerPhase,
                context.TimerElapsed,
                context.Palette,
                milliseconds: true),
            timeRect);
        if (context.Settings.General.ShowMouseClickThroughIndicator && !context.MouseClickThrough)
        {
            DrawMouseClickThroughIndicator(graphics, timeRect, timerTextLayout);
        }
    }

    public static Rectangle GetTimerTextBounds(OverlayRenderContext context, Rectangle rect)
    {
        return GetTimerTextBounds(context.Settings, rect);
    }

    public static Rectangle GetTimerTextBounds(AppSettings settings, Rectangle rect)
    {
        int offsetX = OverlayRenderContext.ScaleInt(settings, settings.Overlay.Columns.TimerOffsetX);
        int offsetY = OverlayRenderContext.ScaleInt(settings, settings.Overlay.Columns.TimerOffsetY);
        return new Rectangle(
            rect.X + OverlayRenderContext.ScaleInt(settings, 4) + offsetX,
            rect.Y - OverlayRenderContext.ScaleInt(settings, 4) + offsetY,
            rect.Width - OverlayRenderContext.ScaleInt(settings, 8),
            rect.Height - OverlayRenderContext.ScaleInt(settings, 16));
    }

    public static float MeasureTimerTextGroupWidth(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources,
        Rectangle bounds)
    {
        if (!context.Settings.Overlay.Columns.Timer.Show && !context.Settings.Overlay.Columns.TimerMilliseconds.Show)
        {
            return bounds.Width;
        }

        string mainText = SplitTimerFormatter.FormatWithoutMilliseconds(context.TimerElapsed);
        string millisecondsText = SplitTimerFormatter.FormatMilliseconds(context.TimerElapsed);
        Font mainFont = resources.Fonts.GetColumnFont(context.Settings.Overlay.Columns.Timer, context.ScaleFactor);
        Font millisecondsFont = resources.Fonts.GetColumnFont(context.Settings.Overlay.Columns.TimerMilliseconds, context.ScaleFactor);

        SizeF millisecondsSize = context.Settings.Overlay.Columns.TimerMilliseconds.Show
            ? resources.MeasureTimerText(graphics, millisecondsText, millisecondsFont, bounds.Size)
            : SizeF.Empty;
        SizeF mainSize = context.Settings.Overlay.Columns.Timer.Show
            ? resources.MeasureTimerText(graphics, mainText, mainFont, bounds.Size)
            : SizeF.Empty;

        float gap = context.Settings.Overlay.Columns.Timer.Show && context.Settings.Overlay.Columns.TimerMilliseconds.Show
            ? context.ScaleInt(2)
            : 0f;
        return (context.Settings.Overlay.Columns.Timer.Show ? mainSize.Width : 0f) + gap +
            (context.Settings.Overlay.Columns.TimerMilliseconds.Show ? millisecondsSize.Width : 0f);
    }

    public static Rectangle GetTimerPaintBounds(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources)
    {
        return GetTimerPaintFrame(graphics, context, resources).PaintBounds;
    }

    public static TimerPaintFrame GetTimerPaintFrame(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources)
    {
        if (!context.Settings.Overlay.Columns.Timer.Show && !context.Settings.Overlay.Columns.TimerMilliseconds.Show)
        {
            return TimerPaintFrame.Empty;
        }

        Rectangle timeRect = GetTimerTextBounds(context, context.Layout.TimerRect);
        TimerTextGeometry geometry = CreateTimerTextGeometry(
            graphics,
            context,
            resources,
            OverlayTextStyles.GetTimerTextStyle(
                context.Settings,
                context.Statuses,
                context.CurrentSplitIndex,
                context.TimerPhase,
                context.TimerElapsed,
                context.Palette,
                milliseconds: false),
            OverlayTextStyles.GetTimerTextStyle(
                context.Settings,
                context.Statuses,
                context.CurrentSplitIndex,
                context.TimerPhase,
                context.TimerElapsed,
                context.Palette,
                milliseconds: true),
            timeRect);

        TimerPaintElement main = TimerPaintElement.Empty;
        if (context.Settings.Overlay.Columns.Timer.Show)
        {
            main = CreateStyledStringPaintElement(
                graphics,
                geometry.MainText,
                geometry.MainFont,
                geometry.MainStyle,
                geometry.MainX,
                geometry.MainY,
                resources.TypographicFormat,
                geometry.MainOpacity);
        }

        TimerPaintElement milliseconds = TimerPaintElement.Empty;
        if (context.Settings.Overlay.Columns.TimerMilliseconds.Show)
        {
            milliseconds = CreateStyledStringPaintElement(
                graphics,
                geometry.MillisecondsText,
                geometry.MillisecondsFont,
                geometry.MillisecondsStyle,
                geometry.MillisecondsX,
                geometry.MillisecondsY,
                resources.TypographicFormat,
                geometry.MillisecondsOpacity);
        }

        TimerPaintElement indicator = TimerPaintElement.Empty;
        if (context.Settings.General.ShowMouseClickThroughIndicator && !context.MouseClickThrough)
        {
            indicator = CreateMouseClickThroughIndicatorPaintElement(graphics, timeRect, geometry.Layout);
        }

        return new TimerPaintFrame(main, milliseconds, indicator);
    }

    private static TimerTextLayout DrawTimerText(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources,
        TextRenderStyle mainStyle,
        TextRenderStyle millisecondsStyle,
        Rectangle bounds)
    {
        if (!context.Settings.Overlay.Columns.Timer.Show && !context.Settings.Overlay.Columns.TimerMilliseconds.Show)
        {
            return TimerTextLayout.Empty;
        }

        TimerTextGeometry geometry = CreateTimerTextGeometry(
            graphics,
            context,
            resources,
            mainStyle,
            millisecondsStyle,
            bounds);

        if (context.Settings.Overlay.Columns.Timer.Show)
        {
            TextEffectRenderer.DrawStyledString(
                graphics,
                geometry.MainText,
                geometry.MainFont,
                geometry.MainStyle,
                geometry.MainX,
                geometry.MainY,
                resources.TypographicFormat,
                geometry.MainOpacity,
                supersampleEffects: false);
        }

        if (context.Settings.Overlay.Columns.TimerMilliseconds.Show)
        {
            TextEffectRenderer.DrawStyledString(
                graphics,
                geometry.MillisecondsText,
                geometry.MillisecondsFont,
                geometry.MillisecondsStyle,
                geometry.MillisecondsX,
                geometry.MillisecondsY,
                resources.TypographicFormat,
                geometry.MillisecondsOpacity,
                supersampleEffects: false);
        }

        return geometry.Layout;
    }

    private static TimerTextGeometry CreateTimerTextGeometry(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources,
        TextRenderStyle mainStyle,
        TextRenderStyle millisecondsStyle,
        Rectangle bounds)
    {
        string mainText = SplitTimerFormatter.FormatWithoutMilliseconds(context.TimerElapsed);
        string millisecondsText = SplitTimerFormatter.FormatMilliseconds(context.TimerElapsed);
        Font mainFont = resources.Fonts.GetColumnFont(context.Settings.Overlay.Columns.Timer, context.ScaleFactor);
        Font millisecondsFont = resources.Fonts.GetColumnFont(context.Settings.Overlay.Columns.TimerMilliseconds, context.ScaleFactor);
        float mainOpacity = OverlayTextStyles.GetTimerTextOpacity(context.Settings, milliseconds: false);
        float millisecondsOpacity = OverlayTextStyles.GetTimerTextOpacity(context.Settings, milliseconds: true);

        SizeF millisecondsSize = context.Settings.Overlay.Columns.TimerMilliseconds.Show
            ? resources.MeasureTimerText(graphics, millisecondsText, millisecondsFont, bounds.Size)
            : SizeF.Empty;
        SizeF mainSize = context.Settings.Overlay.Columns.Timer.Show
            ? resources.MeasureTimerText(graphics, mainText, mainFont, bounds.Size)
            : SizeF.Empty;

        float gap = context.Settings.Overlay.Columns.Timer.Show && context.Settings.Overlay.Columns.TimerMilliseconds.Show
            ? context.ScaleInt(2)
            : 0f;
        FontMetrics mainMetrics = resources.GetFontMetrics(graphics, mainFont);
        FontMetrics millisecondsMetrics = resources.GetFontMetrics(graphics, millisecondsFont);
        float groupAscent = Math.Max(mainMetrics.Ascent, millisecondsMetrics.Ascent);
        float groupDescent = Math.Max(mainMetrics.Descent, millisecondsMetrics.Descent);
        float groupHeight = groupAscent + groupDescent;
        float groupY = bounds.Y + Math.Max(0, (bounds.Height - groupHeight) / 2f);
        float baselineY = groupY + groupAscent;

        float mainX = bounds.Left;
        float mainY = baselineY - mainMetrics.Ascent;
        float millisecondsX = mainX + (context.Settings.Overlay.Columns.Timer.Show ? mainSize.Width : 0f) + gap;
        float millisecondsY = baselineY - millisecondsMetrics.Ascent;

        // Timer strings only contain digit-class glyphs, so the cached per-font
        // digit bounds reproduce the live text's vertical visual extent without
        // building a GraphicsPath per frame.
        (float mainTopOffset, float mainVisualHeight) = context.Settings.Overlay.Columns.Timer.Show
            ? resources.GetTimerDigitsVisualBounds(graphics, mainFont)
            : (0f, 0f);
        (float millisecondsTopOffset, float millisecondsVisualHeight) = context.Settings.Overlay.Columns.TimerMilliseconds.Show
            ? resources.GetTimerDigitsVisualBounds(graphics, millisecondsFont)
            : (0f, 0f);

        float groupWidth = (context.Settings.Overlay.Columns.Timer.Show ? mainSize.Width : 0f) + gap +
            (context.Settings.Overlay.Columns.TimerMilliseconds.Show ? millisecondsSize.Width : 0f);
        float mainHeight = mainMetrics.Ascent + mainMetrics.Descent;
        float anchorTop = context.Settings.Overlay.Columns.Timer.Show && mainVisualHeight > 0f
            ? mainY + mainTopOffset
            : context.Settings.Overlay.Columns.TimerMilliseconds.Show && millisecondsVisualHeight > 0f
                ? millisecondsY + millisecondsTopOffset
                : context.Settings.Overlay.Columns.Timer.Show ? mainY : groupY;
        float anchorHeight = context.Settings.Overlay.Columns.Timer.Show && mainVisualHeight > 0f
            ? mainVisualHeight
            : context.Settings.Overlay.Columns.TimerMilliseconds.Show && millisecondsVisualHeight > 0f
                ? millisecondsVisualHeight
                : context.Settings.Overlay.Columns.Timer.Show ? mainHeight : groupHeight;
        TimerTextLayout layout = new(
            mainX + groupWidth,
            anchorTop,
            anchorHeight,
            context.Settings.Overlay.Columns.Timer.Show ? mainOpacity : millisecondsOpacity);

        return new TimerTextGeometry(
            mainText,
            millisecondsText,
            mainFont,
            millisecondsFont,
            mainStyle,
            millisecondsStyle,
            mainX,
            mainY,
            millisecondsX,
            millisecondsY,
            mainOpacity,
            millisecondsOpacity,
            layout);
    }

    private static void DrawMouseClickThroughIndicator(
        Graphics graphics,
        Rectangle timerBounds,
        TimerTextLayout timerTextLayout)
    {
        RectangleF dotBounds = GetMouseClickThroughIndicatorBounds(timerBounds, timerTextLayout);
        if (dotBounds.Width <= 0f || dotBounds.Height <= 0f)
        {
            return;
        }

        SmoothingMode previousSmoothingMode = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        try
        {
            using var dotBrush = new SolidBrush(TextEffectRenderer.WithOpacity(Color.FromArgb(255, 179, 92, 255), timerTextLayout.Opacity));
            graphics.FillEllipse(dotBrush, dotBounds);
        }
        finally
        {
            graphics.SmoothingMode = previousSmoothingMode;
        }
    }

    private static RectangleF GetMouseClickThroughIndicatorBounds(
        Rectangle timerBounds,
        TimerTextLayout timerTextLayout)
    {
        if (timerTextLayout.Right <= 0f || timerTextLayout.Height <= 0f)
        {
            return RectangleF.Empty;
        }

        float diameter = Math.Clamp(timerTextLayout.Height * 0.22f, 9f, 13f);
        float gap = Math.Max(6f, diameter * 0.7f);
        float x = Math.Min(timerBounds.Right - diameter, timerTextLayout.Right + gap);
        float y = timerTextLayout.Top;
        return new RectangleF(x, y, diameter, diameter);
    }

    private static TimerPaintElement CreateStyledStringPaintElement(
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
            return TimerPaintElement.Empty;
        }

        using GraphicsPath path = TextEffectRenderer.CreateTextPath(graphics, text, font, x, y, format);
        if (path.PointCount == 0)
        {
            return TimerPaintElement.Empty;
        }

        RectangleF paintBounds = style.ShadowPercent > 0 || style.OutlineThicknessPercent > 0
            ? TextEffectGeometry.GetTextEffectLayerBounds(graphics, path, font, style)
            : path.GetBounds();
        return new TimerPaintElement(
            true,
            text,
            style,
            opacity,
            ToPaintRectangle(paintBounds, GetDevicePixelGuard(graphics)));
    }

    private static TimerPaintElement CreateMouseClickThroughIndicatorPaintElement(
        Graphics graphics,
        Rectangle timerBounds,
        TimerTextLayout timerTextLayout)
    {
        RectangleF bounds = GetMouseClickThroughIndicatorBounds(timerBounds, timerTextLayout);
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return TimerPaintElement.Empty;
        }

        return new TimerPaintElement(
            true,
            string.Empty,
            new TextRenderStyle(Color.FromArgb(255, 179, 92, 255), Color.Empty, Color.Empty, 0, 0),
            timerTextLayout.Opacity,
            ToPaintRectangle(bounds, GetDevicePixelGuard(graphics)));
    }

    private static Rectangle ToPaintRectangle(RectangleF bounds, int guard)
    {
        return Rectangle.FromLTRB(
            (int)Math.Floor(bounds.Left) - guard,
            (int)Math.Floor(bounds.Top) - guard,
            (int)Math.Ceiling(bounds.Right) + guard,
            (int)Math.Ceiling(bounds.Bottom) + guard);
    }

    private static int GetDevicePixelGuard(Graphics graphics)
    {
        float dpiScale = Math.Max(graphics.DpiX, graphics.DpiY) / 96f;
        return Math.Clamp((int)Math.Ceiling(3f * dpiScale), 3, 12);
    }

    private readonly record struct TimerTextGeometry(
        string MainText,
        string MillisecondsText,
        Font MainFont,
        Font MillisecondsFont,
        TextRenderStyle MainStyle,
        TextRenderStyle MillisecondsStyle,
        float MainX,
        float MainY,
        float MillisecondsX,
        float MillisecondsY,
        float MainOpacity,
        float MillisecondsOpacity,
        TimerTextLayout Layout);
}

internal readonly record struct TimerPaintFrame(
    TimerPaintElement Main,
    TimerPaintElement Milliseconds,
    TimerPaintElement Indicator)
{
    public static TimerPaintFrame Empty => new(
        TimerPaintElement.Empty,
        TimerPaintElement.Empty,
        TimerPaintElement.Empty);

    public Rectangle PaintBounds
    {
        get
        {
            Rectangle bounds = Rectangle.Empty;
            AddElementBounds(ref bounds, Main);
            AddElementBounds(ref bounds, Milliseconds);
            AddElementBounds(ref bounds, Indicator);
            return bounds;
        }
    }

    private static void AddElementBounds(ref Rectangle bounds, TimerPaintElement element)
    {
        if (!element.HasPaint)
        {
            return;
        }

        bounds = bounds.IsEmpty ? element.Bounds : Rectangle.Union(bounds, element.Bounds);
    }
}

internal readonly record struct TimerPaintElement(
    bool Visible,
    string Text,
    TextRenderStyle Style,
    float Opacity,
    Rectangle Bounds)
{
    public static TimerPaintElement Empty => new(
        false,
        string.Empty,
        default,
        0f,
        Rectangle.Empty);

    public bool HasPaint => Visible && Bounds.Width > 0 && Bounds.Height > 0 && Opacity > 0.01f;
}
