using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TerrariaSplit;

internal static class TimerRenderer
{
    public static void Render(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources)
    {
        if (!context.Settings.Columns.Timer.Show && !context.Settings.Columns.TimerMilliseconds.Show)
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
        if (context.Settings.ShowMouseClickThroughIndicator && !context.MouseClickThrough)
        {
            DrawMouseClickThroughIndicator(graphics, timeRect, timerTextLayout);
        }
    }

    public static Rectangle GetTimerTextBounds(OverlayRenderContext context, Rectangle rect)
    {
        int offsetX = context.ScaleInt(context.Settings.Columns.TimerOffsetX);
        int offsetY = context.ScaleInt(context.Settings.Columns.TimerOffsetY);
        return new Rectangle(
            rect.X + context.ScaleInt(4) + offsetX,
            rect.Y - context.ScaleInt(4) + offsetY,
            rect.Width - context.ScaleInt(8),
            rect.Height - context.ScaleInt(16));
    }

    public static float MeasureTimerTextGroupWidth(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources,
        Rectangle bounds)
    {
        if (!context.Settings.Columns.Timer.Show && !context.Settings.Columns.TimerMilliseconds.Show)
        {
            return bounds.Width;
        }

        string mainText = SplitTimerFormatter.FormatWithoutMilliseconds(context.TimerElapsed);
        string millisecondsText = SplitTimerFormatter.FormatMilliseconds(context.TimerElapsed);
        Font mainFont = resources.Fonts.GetColumnFont(context.Settings.Columns.Timer, context.ScaleFactor);
        Font millisecondsFont = resources.Fonts.GetColumnFont(context.Settings.Columns.TimerMilliseconds, context.ScaleFactor);

        using var format = new StringFormat(StringFormat.GenericTypographic);
        SizeF millisecondsSize = context.Settings.Columns.TimerMilliseconds.Show
            ? graphics.MeasureString(millisecondsText, millisecondsFont, bounds.Size, format)
            : SizeF.Empty;
        SizeF mainSize = context.Settings.Columns.Timer.Show
            ? graphics.MeasureString(mainText, mainFont, bounds.Size, format)
            : SizeF.Empty;

        float gap = context.Settings.Columns.Timer.Show && context.Settings.Columns.TimerMilliseconds.Show
            ? context.ScaleInt(2)
            : 0f;
        return (context.Settings.Columns.Timer.Show ? mainSize.Width : 0f) + gap +
            (context.Settings.Columns.TimerMilliseconds.Show ? millisecondsSize.Width : 0f);
    }

    private static TimerTextLayout DrawTimerText(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources,
        TextRenderStyle mainStyle,
        TextRenderStyle millisecondsStyle,
        Rectangle bounds)
    {
        if (!context.Settings.Columns.Timer.Show && !context.Settings.Columns.TimerMilliseconds.Show)
        {
            return TimerTextLayout.Empty;
        }

        string mainText = SplitTimerFormatter.FormatWithoutMilliseconds(context.TimerElapsed);
        string millisecondsText = SplitTimerFormatter.FormatMilliseconds(context.TimerElapsed);
        Font mainFont = resources.Fonts.GetColumnFont(context.Settings.Columns.Timer, context.ScaleFactor);
        Font millisecondsFont = resources.Fonts.GetColumnFont(context.Settings.Columns.TimerMilliseconds, context.ScaleFactor);

        using var format = new StringFormat(StringFormat.GenericTypographic);
        SizeF millisecondsSize = context.Settings.Columns.TimerMilliseconds.Show
            ? graphics.MeasureString(millisecondsText, millisecondsFont, bounds.Size, format)
            : SizeF.Empty;
        SizeF mainSize = context.Settings.Columns.Timer.Show
            ? graphics.MeasureString(mainText, mainFont, bounds.Size, format)
            : SizeF.Empty;

        float gap = context.Settings.Columns.Timer.Show && context.Settings.Columns.TimerMilliseconds.Show
            ? context.ScaleInt(2)
            : 0f;
        FontMetrics mainMetrics = OverlayTextMetrics.GetFontMetrics(graphics, mainFont);
        FontMetrics millisecondsMetrics = OverlayTextMetrics.GetFontMetrics(graphics, millisecondsFont);
        float groupAscent = Math.Max(mainMetrics.Ascent, millisecondsMetrics.Ascent);
        float groupDescent = Math.Max(mainMetrics.Descent, millisecondsMetrics.Descent);
        float groupHeight = groupAscent + groupDescent;
        float groupY = bounds.Y + Math.Max(0, (bounds.Height - groupHeight) / 2f);
        float baselineY = groupY + groupAscent;

        float mainX = bounds.Left;
        float mainY = baselineY - mainMetrics.Ascent;
        float millisecondsX = mainX + (context.Settings.Columns.Timer.Show ? mainSize.Width : 0f) + gap;
        float millisecondsY = baselineY - millisecondsMetrics.Ascent;

        if (context.Settings.Columns.Timer.Show)
        {
            TextEffectRenderer.DrawStyledString(graphics, mainText, mainFont, mainStyle, mainX, mainY, format, 1f);
        }

        if (context.Settings.Columns.TimerMilliseconds.Show)
        {
            TextEffectRenderer.DrawStyledString(
                graphics,
                millisecondsText,
                millisecondsFont,
                millisecondsStyle,
                millisecondsX,
                millisecondsY,
                format,
                1f);
        }

        float groupWidth = (context.Settings.Columns.Timer.Show ? mainSize.Width : 0f) + gap +
            (context.Settings.Columns.TimerMilliseconds.Show ? millisecondsSize.Width : 0f);
        RectangleF mainVisualBounds = context.Settings.Columns.Timer.Show
            ? OverlayTextMetrics.GetTextVisualBounds(graphics, mainText, mainFont, mainX, mainY, format)
            : RectangleF.Empty;
        float mainHeight = mainMetrics.Ascent + mainMetrics.Descent;
        float anchorTop = mainVisualBounds.Height > 0f ? mainVisualBounds.Top : context.Settings.Columns.Timer.Show ? mainY : groupY;
        float anchorHeight = mainVisualBounds.Height > 0f ? mainVisualBounds.Height : context.Settings.Columns.Timer.Show ? mainHeight : groupHeight;
        return new TimerTextLayout(mainX + groupWidth, anchorTop, anchorHeight);
    }

    private static void DrawMouseClickThroughIndicator(
        Graphics graphics,
        Rectangle timerBounds,
        TimerTextLayout timerTextLayout)
    {
        if (timerTextLayout.Right <= 0f || timerTextLayout.Height <= 0f)
        {
            return;
        }

        float diameter = Math.Clamp(timerTextLayout.Height * 0.22f, 9f, 13f);
        float gap = Math.Max(6f, diameter * 0.7f);
        float x = Math.Min(timerBounds.Right - diameter, timerTextLayout.Right + gap);
        float y = timerTextLayout.Top;
        var dotBounds = new RectangleF(x, y, diameter, diameter);

        SmoothingMode previousSmoothingMode = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        try
        {
            using var dotBrush = new SolidBrush(Color.FromArgb(255, 179, 92, 255));
            graphics.FillEllipse(dotBrush, dotBounds);
        }
        finally
        {
            graphics.SmoothingMode = previousSmoothingMode;
        }
    }
}
