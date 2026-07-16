using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed class UiProgressBar : Control
{
    private int value;

    public UiProgressBar()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        BackColor = UiTheme.Surface;
        Height = 18;
        MinimumSize = new Size(0, 18);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => value;
        set
        {
            int clamped = Math.Clamp(value, 0, 100);
            if (this.value == clamped)
            {
                return;
            }

            this.value = clamped;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);

        var frame = new Rectangle(0, 0, Math.Max(0, ClientSize.Width - 1), Math.Max(0, ClientSize.Height - 1));
        var fill = Rectangle.Inflate(frame, -1, -1);
        fill.Width = (int)Math.Round(fill.Width * (Value / 100d));

        int radius = Math.Min(UiDpiScale.ScaleIntFromBase200(6), Math.Min(frame.Width, frame.Height) / 2);
        using var frameBrush = new SolidBrush(UiTheme.Field);
        using var fillBrush = new SolidBrush(UiTheme.Accent);
        using var borderPen = new Pen(UiTheme.Border);
        FillRoundedRectangle(e.Graphics, frameBrush, frame, radius);
        if (fill.Width > 0)
        {
            FillRoundedRectangle(e.Graphics, fillBrush, fill, Math.Max(0, radius - 1));
        }

        DrawRoundedRectangle(e.Graphics, borderPen, frame, radius);
    }

    private static void FillRoundedRectangle(Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        using GraphicsPath path = CreateRoundedPath(bounds, radius);
        graphics.FillPath(brush, path);
    }

    private static void DrawRoundedRectangle(Graphics graphics, Pen pen, Rectangle bounds, int radius)
    {
        using GraphicsPath path = CreateRoundedPath(bounds, radius);
        graphics.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return path;
        }

        int effectiveRadius = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2);
        if (effectiveRadius <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        int diameter = effectiveRadius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
