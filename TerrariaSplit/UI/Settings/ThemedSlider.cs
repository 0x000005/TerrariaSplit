using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class ThemedSlider : Control
{
    private const int PreferredHeight = 36;
    private const int TrackHeight = 6;
    private const int ThumbRadius = 8;

    private int minimum;
    private int maximum = 100;
    private int value;
    private bool dragging;

    public ThemedSlider()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);
        TabStop = true;
        BackColor = UiTheme.Surface;
        ForeColor = UiTheme.Text;
        Cursor = Cursors.Hand;
        Height = PreferredHeight;
        MinimumSize = new Size(0, PreferredHeight);
        Margin = new Padding(0, 2, 0, 2);
    }

    [DefaultValue(0)]
    public int Minimum
    {
        get => minimum;
        set
        {
            if (maximum < value)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            minimum = value;
            if (maximum < minimum)
            {
                maximum = minimum;
            }

            Value = this.value;
            Invalidate();
        }
    }

    [DefaultValue(100)]
    public int Maximum
    {
        get => maximum;
        set
        {
            if (value < minimum)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            maximum = Math.Max(minimum, value);
            Value = this.value;
            Invalidate();
        }
    }

    [DefaultValue(0)]
    public int Value
    {
        get => value;
        set
        {
            int clamped = Math.Clamp(value, minimum, maximum);
            if (this.value == clamped)
            {
                return;
            }

            this.value = clamped;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? ValueChanged;

    protected override Size DefaultSize => new(220, PreferredHeight);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);

        Rectangle track = GetTrackBounds();
        Rectangle fill = track;
        fill.Width = Math.Max(0, ThumbCenterX - track.Left);

        Color baseTrackColor = Enabled ? UiTheme.Field : UiTheme.SurfaceRaised;
        Color fillTrackColor = Enabled ? UiTheme.Accent : UiTheme.Border;
        Color thumbColor = Enabled ? UiTheme.Accent : UiTheme.Border;
        Color outlineColor = Enabled ? UiTheme.AccentHover : UiTheme.Border;

        using (var trackBrush = new SolidBrush(baseTrackColor))
        using (var fillBrush = new SolidBrush(fillTrackColor))
        using (var outlinePen = new Pen(outlineColor, 1.5f))
        using (var thumbBrush = new SolidBrush(thumbColor))
        using (var focusPen = new Pen(Color.FromArgb(120, UiTheme.Accent), 1f))
        {
            FillRoundedRectangle(e.Graphics, trackBrush, track, TrackHeight / 2);
            if (fill.Width > 0)
            {
                FillRoundedRectangle(e.Graphics, fillBrush, fill, TrackHeight / 2);
            }

            Rectangle thumb = GetThumbBounds();
            e.Graphics.FillEllipse(thumbBrush, thumb);
            e.Graphics.DrawEllipse(outlinePen, thumb);

            if (Focused)
            {
                Rectangle focus = Rectangle.Inflate(thumb, 3, 3);
                e.Graphics.DrawEllipse(focusPen, focus);
            }
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        Focus();
        dragging = true;
        Capture = true;
        SetValueFromClientX(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!dragging)
        {
            return;
        }

        SetValueFromClientX(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        dragging = false;
        Capture = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        int next = Value;
        switch (e.KeyCode)
        {
            case Keys.Left:
            case Keys.Down:
                next--;
                break;
            case Keys.Right:
            case Keys.Up:
                next++;
                break;
            case Keys.PageDown:
                next -= 10;
                break;
            case Keys.PageUp:
                next += 10;
                break;
            case Keys.Home:
                next = Minimum;
                break;
            case Keys.End:
                next = Maximum;
                break;
            default:
                return;
        }

        Value = next;
        e.Handled = true;
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    private Rectangle GetTrackBounds()
    {
        int left = ThumbRadius;
        int width = Math.Max(0, ClientSize.Width - ThumbRadius * 2);
        int top = Math.Max(0, (ClientSize.Height - TrackHeight) / 2);
        return new Rectangle(left, top, width, TrackHeight);
    }

    private Rectangle GetThumbBounds()
    {
        int centerX = ThumbCenterX;
        int centerY = Math.Max(0, ClientSize.Height / 2);
        return new Rectangle(centerX - ThumbRadius, centerY - ThumbRadius, ThumbRadius * 2, ThumbRadius * 2);
    }

    private int ThumbCenterX
    {
        get
        {
            Rectangle track = GetTrackBounds();
            if (maximum == minimum || track.Width <= 0)
            {
                return track.Left;
            }

            double progress = (double)(value - minimum) / (maximum - minimum);
            return track.Left + (int)Math.Round(track.Width * progress);
        }
    }

    private void SetValueFromClientX(int clientX)
    {
        Rectangle track = GetTrackBounds();
        if (maximum == minimum || track.Width <= 0)
        {
            Value = minimum;
            return;
        }

        double progress = (double)(clientX - track.Left) / track.Width;
        int next = minimum + (int)Math.Round((maximum - minimum) * Math.Clamp(progress, 0d, 1d));
        Value = next;
    }

    private static void FillRoundedRectangle(Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        if (radius <= 0)
        {
            graphics.FillRectangle(brush, bounds);
            return;
        }

        int diameter = radius * 2;
        using var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }
}
