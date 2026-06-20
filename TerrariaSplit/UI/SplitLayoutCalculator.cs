using System.Drawing;

namespace TerrariaSplit;

internal static class SplitLayoutCalculator
{
    public static bool TryCreate(
        Rectangle bounds,
        int statusCount,
        int baseRowGap,
        Func<int, int> scaleInt,
        out SplitLayout layout)
    {
        layout = default;

        int margin = scaleInt(12);
        if (bounds.Width < scaleInt(160) || bounds.Height < scaleInt(160))
        {
            return false;
        }

        Rectangle content = Rectangle.Inflate(bounds, -margin, -margin);
        int timerHeight = Math.Clamp((int)(content.Height * 0.17), scaleInt(82), scaleInt(110));
        int rowGap = scaleInt(baseRowGap);
        int listSpace = content.Height - timerHeight - scaleInt(10);
        int rowHeight = Math.Clamp(
            (listSpace - Math.Max(0, statusCount - 1) * rowGap) / Math.Max(1, statusCount),
            scaleInt(42),
            scaleInt(58));
        if (rowHeight <= 0)
        {
            return false;
        }

        int timerY = content.Y + statusCount * rowHeight + Math.Max(0, statusCount - 1) * rowGap + scaleInt(2);
        if (timerY + timerHeight > content.Bottom)
        {
            return false;
        }

        layout = new SplitLayout(
            new Rectangle(content.X + scaleInt(2), content.Y, content.Width - scaleInt(4), rowHeight),
            new Rectangle(content.X, timerY, content.Width, timerHeight),
            rowGap);
        return true;
    }

    public static int GetDefaultWindowWidth(AppSettings settings)
    {
        float scale = Math.Clamp(settings.Columns.ScalePercent, 25, 300) / 100f;
        int columnsWidth = 0;
        columnsWidth += settings.Columns.Icon.Show ? (int)Math.Round(settings.Columns.Icon.Width * scale) : 0;
        columnsWidth += settings.Columns.Time.Show ? (int)Math.Round(settings.Columns.Time.Width * scale) : 0;
        columnsWidth += settings.Columns.Delta.Show ? (int)Math.Round(settings.Columns.Delta.Width * scale) : 0;
        return Math.Clamp(columnsWidth + (int)Math.Round(28 * scale), 300, 2400);
    }

    public static int GetDefaultWindowHeight(AppSettings settings)
    {
        float scale = Math.Clamp(settings.Columns.ScalePercent, 25, 300) / 100f;
        return Math.Clamp((int)Math.Round(720 * scale), 420, 2160);
    }

    public static int GetMinimumWindowHeightForRows(AppSettings settings, int statusCount, int baseRowGap)
    {
        int rows = Math.Max(1, Math.Max(statusCount, SplitCompletionAnimationRenderer.ReservedRowCount));
        int margin = ScaleInt(settings, 12);
        int rowHeight = ScaleInt(settings, 42);
        int rowGap = ScaleInt(settings, baseRowGap);
        int timerHeight = ScaleInt(settings, 110);
        int timerGap = ScaleInt(settings, 2);
        int height =
            margin * 2 +
            rows * rowHeight +
            Math.Max(0, rows - 1) * rowGap +
            timerGap +
            timerHeight;
        return Math.Max(GetMinimumWindowSize(settings).Height, height);
    }

    public static Size GetMinimumWindowSize(AppSettings settings)
    {
        float scale = Math.Clamp(settings.Columns.ScalePercent, 25, 300) / 100f;
        return new Size(
            Math.Clamp((int)Math.Round(300 * scale), 220, 1800),
            Math.Clamp((int)Math.Round(420 * scale), 260, 1600));
    }

    private static int ScaleInt(AppSettings settings, int value)
    {
        float scale = Math.Clamp(settings.Columns.ScalePercent, 25, 300) / 100f;
        return Math.Max(1, (int)Math.Round(value * scale, MidpointRounding.AwayFromZero));
    }
}
