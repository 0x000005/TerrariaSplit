using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class ThemedSlider : Control
{
    private const int PreferredHeight = 36;
    private const int FrameHeight = 30;
    private const int TrackHeight = 8;
    private const int ThumbWidth = 14;
    private const int ThumbHeight = 24;
    private const int CornerRadius = 3;

    private int minimum;
    private int maximum = 100;
    private int value;
    private bool dragging;
    private bool hover;

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

        Rectangle frame = GetFrameBounds();
        Rectangle track = GetTrackBounds(frame);
        int thumbCenterX = ThumbCenterX;
        Rectangle fill = Rectangle.FromLTRB(track.Left, track.Top, Math.Clamp(thumbCenterX, track.Left, track.Right), track.Bottom);

        Color frameColor = Enabled ? UiTheme.Field : UiTheme.SurfaceRaised;
        Color frameBorderColor = Enabled
            ? Focused || dragging
                ? UiTheme.Accent
                : hover
                    ? UiTheme.AccentHover
                    : UiTheme.Border
            : UiTheme.Border;
        Color baseTrackColor = Enabled ? UiTheme.SurfaceRaised : UiTheme.Field;
        Color fillTrackColor = Enabled
            ? dragging
                ? UiTheme.AccentDown
                : hover
                    ? UiTheme.AccentHover
                    : UiTheme.Accent
            : UiTheme.Border;
        Color thumbColor = Enabled ? UiTheme.SurfaceRaised : UiTheme.Field;
        Color thumbBorderColor = Enabled
            ? dragging || Focused
                ? UiTheme.AccentHover
                : UiTheme.Accent
            : UiTheme.Border;

        using (var frameBrush = new SolidBrush(frameColor))
        using (var framePen = new Pen(frameBorderColor, 1f))
        using (var trackBrush = new SolidBrush(baseTrackColor))
        using (var fillBrush = new SolidBrush(fillTrackColor))
        using (var thumbBrush = new SolidBrush(thumbColor))
        using (var thumbPen = new Pen(thumbBorderColor, 1.5f))
        using (var focusPen = new Pen(Color.FromArgb(150, UiTheme.AccentHover), 1f))
        {
            int trackRadius = Math.Max(1, ScaledTrackHeight / 2);
            int cornerRadius = ScaledCornerRadius;
            FillRoundedRectangle(e.Graphics, frameBrush, frame, cornerRadius);
            DrawRoundedRectangle(e.Graphics, framePen, frame, cornerRadius);

            FillRoundedRectangle(e.Graphics, trackBrush, track, trackRadius);
            if (fill.Width > 0)
            {
                FillRoundedRectangle(e.Graphics, fillBrush, fill, trackRadius);
            }

            Rectangle thumb = GetThumbBounds();
            FillRoundedRectangle(e.Graphics, thumbBrush, thumb, cornerRadius);
            DrawRoundedRectangle(e.Graphics, thumbPen, thumb, cornerRadius);

            if (Focused)
            {
                int focusPadding = UiDpiScale.ScaleIntFromBase200(2);
                Rectangle focus = Rectangle.Inflate(frame, focusPadding, focusPadding);
                DrawRoundedRectangle(e.Graphics, focusPen, focus, cornerRadius + focusPadding);
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
        Invalidate();
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
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hover = false;
        Invalidate();
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

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    private Rectangle GetFrameBounds()
    {
        int height = Math.Min(ScaledFrameHeight, Math.Max(1, ClientSize.Height - UiDpiScale.ScaleIntFromBase200(2)));
        return new Rectangle(
            0,
            Math.Max(0, (ClientSize.Height - height) / 2),
            Math.Max(0, ClientSize.Width - 1),
            height);
    }

    private static Rectangle GetTrackBounds(Rectangle frame)
    {
        int thumbWidth = ScaledThumbWidth;
        int horizontalPadding = UiDpiScale.ScaleIntFromBase200(8);
        int trackHeight = ScaledTrackHeight;
        int left = frame.Left + thumbWidth / 2 + horizontalPadding;
        int right = frame.Right - thumbWidth / 2 - horizontalPadding;
        int top = frame.Top + Math.Max(0, (frame.Height - trackHeight) / 2);
        return Rectangle.FromLTRB(left, top, Math.Max(left, right), top + trackHeight);
    }

    private Rectangle GetThumbBounds()
    {
        int centerX = ThumbCenterX;
        int centerY = Math.Max(0, ClientSize.Height / 2);
        int thumbWidth = ScaledThumbWidth;
        int thumbHeight = ScaledThumbHeight;
        return new Rectangle(centerX - thumbWidth / 2, centerY - thumbHeight / 2, thumbWidth, thumbHeight);
    }

    private int ThumbCenterX
    {
        get
        {
            Rectangle track = GetTrackBounds(GetFrameBounds());
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
        Rectangle track = GetTrackBounds(GetFrameBounds());
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

    private static void DrawRoundedRectangle(Graphics graphics, Pen pen, Rectangle bounds, int radius)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        Rectangle adjusted = new(bounds.X, bounds.Y, Math.Max(0, bounds.Width - 1), Math.Max(0, bounds.Height - 1));
        if (radius <= 0)
        {
            graphics.DrawRectangle(pen, adjusted);
            return;
        }

        int diameter = radius * 2;
        using var path = new GraphicsPath();
        path.AddArc(adjusted.Left, adjusted.Top, diameter, diameter, 180, 90);
        path.AddArc(adjusted.Right - diameter, adjusted.Top, diameter, diameter, 270, 90);
        path.AddArc(adjusted.Right - diameter, adjusted.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(adjusted.Left, adjusted.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.DrawPath(pen, path);
    }

    private static int ScaledFrameHeight => UiDpiScale.ScaleIntFromBase200(FrameHeight);

    private static int ScaledTrackHeight => UiDpiScale.ScaleIntFromBase200(TrackHeight);

    private static int ScaledThumbWidth => UiDpiScale.ScaleIntFromBase200(ThumbWidth);

    private static int ScaledThumbHeight => UiDpiScale.ScaleIntFromBase200(ThumbHeight);

    private static int ScaledCornerRadius => UiDpiScale.ScaleIntFromBase200(CornerRadius);
}
