using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Rendering;

internal static class TimerRenderer
{
    private static readonly Color MouseClickThroughIndicatorColor = Color.FromArgb(255, 179, 92, 255);

    public static void Render(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources)
    {
        if (!context.Settings.Overlay.Columns.Timer.Show && !context.Settings.Overlay.Columns.TimerMilliseconds.Show)
        {
            return;
        }

        Rectangle timerRect = context.Layout.TimerRect;
        Rectangle timeRect = GetTimerTextBounds(context, timerRect);
        TimerTextLayout timerTextLayout = DrawTimerText(
            graphics,
            context,
            resources,
            GetTimerTextStyle(context, milliseconds: false),
            GetTimerTextStyle(context, milliseconds: true),
            timeRect);
        bool showMouseIndicator = context.Settings.General.ShowMouseClickThroughIndicator && !context.MouseClickThrough;
        bool showPyramidFilterIndicator =
            context.CheatFilterIndicator != CheatFilterIndicatorLevel.None;
        int visibleIndicatorCount = GetVisibleTimerIndicatorCount(showMouseIndicator, showPyramidFilterIndicator);
        int indicatorIndex = 0;
        if (showMouseIndicator)
        {
            DrawMouseClickThroughIndicator(graphics, timerRect, timerTextLayout, indicatorIndex++, visibleIndicatorCount);
        }

        if (showPyramidFilterIndicator)
        {
            DrawPyramidFilterIndicator(
                graphics,
                timerRect,
                timerTextLayout,
                indicatorIndex,
                visibleIndicatorCount,
                context.CheatFilterIndicator);
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

        float millisecondsWidth = context.Settings.Overlay.Columns.TimerMilliseconds.Show
            ? resources.MeasureStableTimerTextWidth(graphics, millisecondsText, millisecondsFont, bounds.Size)
            : 0f;
        float mainWidth = context.Settings.Overlay.Columns.Timer.Show
            ? resources.MeasureStableTimerTextWidth(graphics, mainText, mainFont, bounds.Size)
            : 0f;

        float gap = context.Settings.Overlay.Columns.Timer.Show && context.Settings.Overlay.Columns.TimerMilliseconds.Show
            ? context.ScaleInt(2)
            : 0f;
        return mainWidth + gap + millisecondsWidth;
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

        Rectangle timerRect = context.Layout.TimerRect;
        Rectangle timeRect = GetTimerTextBounds(context, timerRect);
        TimerTextGeometry geometry = CreateTimerTextGeometry(
            graphics,
            context,
            resources,
            GetTimerTextStyle(context, milliseconds: false),
            GetTimerTextStyle(context, milliseconds: true),
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

        bool showMouseIndicator = context.Settings.General.ShowMouseClickThroughIndicator && !context.MouseClickThrough;
        bool showPyramidFilterIndicator =
            context.CheatFilterIndicator != CheatFilterIndicatorLevel.None;
        int visibleIndicatorCount = GetVisibleTimerIndicatorCount(showMouseIndicator, showPyramidFilterIndicator);
        int indicatorIndex = 0;

        TimerPaintElement indicator = TimerPaintElement.Empty;
        if (showMouseIndicator)
        {
            indicator = CreateMouseClickThroughIndicatorPaintElement(graphics, timerRect, geometry.Layout, indicatorIndex++, visibleIndicatorCount);
        }

        TimerPaintElement pyramidFilterIndicator = TimerPaintElement.Empty;
        if (showPyramidFilterIndicator)
        {
            pyramidFilterIndicator = CreatePyramidFilterIndicatorPaintElement(
                graphics,
                timerRect,
                geometry.Layout,
                indicatorIndex,
                visibleIndicatorCount,
                context.CheatFilterIndicator);
        }

        return new TimerPaintFrame(main, milliseconds, indicator, pyramidFilterIndicator);
    }

    private static TextRenderStyle GetTimerTextStyle(OverlayRenderContext context, bool milliseconds)
    {
        TextRenderStyle style = OverlayTextStyles.GetTimerTextStyle(
            context.Settings,
            context.Statuses,
            context.CurrentSplitIndex,
            context.TimerPhase,
            context.TimerElapsed,
            context.Palette,
            milliseconds);
        return context.TimerFillOverride is Color fill
            ? style with { Fill = fill }
            : style;
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

        float millisecondsWidth = context.Settings.Overlay.Columns.TimerMilliseconds.Show
            ? resources.MeasureStableTimerTextWidth(graphics, millisecondsText, millisecondsFont, bounds.Size)
            : 0f;
        float mainWidth = context.Settings.Overlay.Columns.Timer.Show
            ? resources.MeasureStableTimerTextWidth(graphics, mainText, mainFont, bounds.Size)
            : 0f;

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
        float millisecondsX = mainX + mainWidth + gap;
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

        float groupWidth = mainWidth + gap + millisecondsWidth;
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
        TimerTextLayout timerTextLayout,
        int positionIndex,
        int visibleIndicatorCount)
    {
        DrawTimerIndicator(
            graphics,
            timerBounds,
            timerTextLayout,
            positionIndex,
            visibleIndicatorCount,
            MouseClickThroughIndicatorColor);
    }

    private static void DrawPyramidFilterIndicator(
        Graphics graphics,
        Rectangle timerBounds,
        TimerTextLayout timerTextLayout,
        int positionIndex,
        int visibleIndicatorCount,
        CheatFilterIndicatorLevel level)
    {
        DrawTimerIndicator(
            graphics,
            timerBounds,
            timerTextLayout,
            positionIndex,
            visibleIndicatorCount,
            CheatFilterIndicator.GetColor(level));
    }

    private static void DrawTimerIndicator(
        Graphics graphics,
        Rectangle timerBounds,
        TimerTextLayout timerTextLayout,
        int positionIndex,
        int visibleIndicatorCount,
        Color color)
    {
        RectangleF dotBounds = GetTimerIndicatorBounds(timerBounds, timerTextLayout, positionIndex, visibleIndicatorCount);
        if (dotBounds.Width <= 0f || dotBounds.Height <= 0f)
        {
            return;
        }

        SmoothingMode previousSmoothingMode = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        try
        {
            using var dotBrush = new SolidBrush(TextEffectRenderer.WithOpacity(color, timerTextLayout.Opacity));
            graphics.FillEllipse(dotBrush, dotBounds);
        }
        finally
        {
            graphics.SmoothingMode = previousSmoothingMode;
        }
    }

    private static RectangleF GetTimerIndicatorBounds(
        Rectangle timerBounds,
        TimerTextLayout timerTextLayout,
        int positionIndex,
        int visibleIndicatorCount)
    {
        if (timerTextLayout.Right <= 0f || timerTextLayout.Height <= 0f)
        {
            return RectangleF.Empty;
        }

        int count = Math.Max(1, visibleIndicatorCount);
        float diameter = Math.Clamp(timerTextLayout.Height * 0.18f, 9f, 28f);
        float gap = Math.Max(6f, diameter * 0.45f);
        float indicatorGap = Math.Max(4f, diameter * 0.28f);
        if (timerBounds.Width <= 0 || timerBounds.Height <= 0)
        {
            return RectangleF.Empty;
        }

        int clampedIndex = Math.Clamp(positionIndex, 0, count - 1);
        float x = timerTextLayout.Right + gap + clampedIndex * (diameter + indicatorGap);
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
        TimerTextLayout timerTextLayout,
        int positionIndex,
        int visibleIndicatorCount)
    {
        return CreateTimerIndicatorPaintElement(
            graphics,
            timerBounds,
            timerTextLayout,
            positionIndex,
            visibleIndicatorCount,
            MouseClickThroughIndicatorColor);
    }

    private static TimerPaintElement CreatePyramidFilterIndicatorPaintElement(
        Graphics graphics,
        Rectangle timerBounds,
        TimerTextLayout timerTextLayout,
        int positionIndex,
        int visibleIndicatorCount,
        CheatFilterIndicatorLevel level)
    {
        return CreateTimerIndicatorPaintElement(
            graphics,
            timerBounds,
            timerTextLayout,
            positionIndex,
            visibleIndicatorCount,
            CheatFilterIndicator.GetColor(level));
    }

    private static TimerPaintElement CreateTimerIndicatorPaintElement(
        Graphics graphics,
        Rectangle timerBounds,
        TimerTextLayout timerTextLayout,
        int positionIndex,
        int visibleIndicatorCount,
        Color color)
    {
        RectangleF bounds = GetTimerIndicatorBounds(timerBounds, timerTextLayout, positionIndex, visibleIndicatorCount);
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return TimerPaintElement.Empty;
        }

        return new TimerPaintElement(
            true,
            string.Empty,
            new TextRenderStyle(color, Color.Empty, Color.Empty, 0, 0),
            timerTextLayout.Opacity,
            ToPaintRectangle(bounds, GetDevicePixelGuard(graphics)));
    }

    private static int GetVisibleTimerIndicatorCount(bool showMouseIndicator, bool showPyramidFilterIndicator)
    {
        int count = 0;
        if (showMouseIndicator)
        {
            count++;
        }

        if (showPyramidFilterIndicator)
        {
            count++;
        }

        return count;
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
    TimerPaintElement Indicator,
    TimerPaintElement PyramidFilterIndicator)
{
    public static TimerPaintFrame Empty => new(
        TimerPaintElement.Empty,
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
            AddElementBounds(ref bounds, PyramidFilterIndicator);
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
