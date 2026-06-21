using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal static class UiTheme
{
    public const string FontFamilyName = UiFontSettings.DefaultFamilyName;

    public static readonly Color Window = Color.FromArgb(18, 22, 25);
    public static readonly Color Surface = Color.FromArgb(28, 34, 38);
    public static readonly Color SurfaceRaised = Color.FromArgb(37, 45, 50);
    public static readonly Color Field = Color.FromArgb(22, 27, 31);
    public static readonly Color Border = Color.FromArgb(70, 82, 88);
    public static readonly Color Accent = Color.FromArgb(54, 150, 132);
    public static readonly Color AccentHover = Color.FromArgb(69, 174, 153);
    public static readonly Color AccentDown = Color.FromArgb(38, 119, 105);
    public static readonly Color Text = Color.FromArgb(238, 242, 239);
    public static readonly Color MutedText = Color.FromArgb(161, 173, 169);
    public static readonly Color Selection = Color.FromArgb(53, 87, 82);

    public static Font FormFont(float size = 10f, FontStyle style = FontStyle.Regular)
    {
        return UiFontSettings.CreateFont(FontFamilyName, size, style);
    }

    public static void ConfigureForm(Form form, Size minimumSize)
    {
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.BackColor = Window;
        form.ForeColor = Text;
        form.Font = FormFont();
        form.MinimumSize = minimumSize;
        EnableDoubleBuffering(form);
    }

    public static void StyleButton(Button button, bool accent = false, int minimumWidth = 128)
    {
        button.AutoEllipsis = true;
        button.BackColor = accent ? Accent : SurfaceRaised;
        button.FlatStyle = FlatStyle.Flat;
        button.ForeColor = Text;
        button.Height = 52;
        button.Margin = new Padding(8, 0, 0, 0);
        button.MinimumSize = new Size(minimumWidth, 52);
        button.Padding = new Padding(14, 0, 14, 4);
        button.UseVisualStyleBackColor = false;

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.BorderColor = accent ? Accent : Border;
        button.FlatAppearance.MouseDownBackColor = accent ? AccentDown : Color.FromArgb(41, 50, 56);
        button.FlatAppearance.MouseOverBackColor = accent ? AccentHover : Color.FromArgb(47, 58, 64);

        Size textSize = TextRenderer.MeasureText(button.Text, button.Font);
        button.Width = Math.Max(minimumWidth, textSize.Width + 42);
        ApplyStyledButtonPaint(button);
    }

    public static void StyleTextBox(TextBox textBox)
    {
        textBox.AutoSize = false;
        textBox.BackColor = Field;
        textBox.BorderStyle = BorderStyle.None;
        textBox.ForeColor = Text;
        textBox.Height = 36;
        textBox.Margin = new Padding(0, 8, 2, 8);
        textBox.MinimumSize = new Size(0, 36);
        textBox.TextAlign = HorizontalAlignment.Center;
    }

    public static void StyleComboBox(ComboBox comboBox)
    {
        comboBox.BackColor = Field;
        comboBox.DrawMode = DrawMode.OwnerDrawFixed;
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox.FlatStyle = FlatStyle.Flat;
        comboBox.Font = FormFont(9f);
        comboBox.ForeColor = Text;
        comboBox.Height = 38;
        comboBox.ItemHeight = 28;
        comboBox.Margin = new Padding(0, 7, 2, 7);
        comboBox.MinimumSize = new Size(0, 38);
        comboBox.DrawItem += PaintComboBoxItem;
    }

    public static void StyleNumericBox(NumericUpDown numericBox)
    {
        numericBox.BackColor = Field;
        numericBox.BorderStyle = BorderStyle.None;
        numericBox.ForeColor = Text;
        numericBox.Height = 36;
        numericBox.Margin = new Padding(0, 8, 2, 8);
        numericBox.MinimumSize = new Size(0, 36);
        numericBox.TextAlign = HorizontalAlignment.Right;
    }

    public static void StyleCheckBox(CheckBox checkBox)
    {
        checkBox.Appearance = Appearance.Normal;
        checkBox.BackColor = Surface;
        checkBox.FlatStyle = FlatStyle.Flat;
        checkBox.ForeColor = Text;
        checkBox.Margin = new Padding(0, 9, 0, 9);
        checkBox.Padding = Padding.Empty;
        checkBox.UseVisualStyleBackColor = false;
        checkBox.FlatAppearance.BorderSize = 0;
        checkBox.Paint += PaintCheckBox;
        checkBox.CheckedChanged += (_, _) => checkBox.Invalidate();
        checkBox.EnabledChanged += (_, _) => checkBox.Invalidate();
    }

    private static void PaintCheckBox(object? sender, PaintEventArgs e)
    {
        if (sender is not CheckBox checkBox)
        {
            return;
        }

        e.Graphics.Clear(checkBox.BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        int boxSize = Math.Min(20, Math.Max(14, checkBox.ClientSize.Height - 6));
        bool hasText = !string.IsNullOrWhiteSpace(checkBox.Text);
        int boxX = hasText ? 2 : Math.Max(0, (checkBox.ClientSize.Width - boxSize) / 2);
        int boxY = Math.Max(0, (checkBox.ClientSize.Height - boxSize) / 2);
        var boxRect = new Rectangle(boxX, boxY, boxSize, boxSize);

        Color borderColor = checkBox.Checked ? Accent : Border;
        Color fillColor = checkBox.Checked ? Accent : Field;
        if (!checkBox.Enabled)
        {
            borderColor = Border;
            fillColor = SurfaceRaised;
        }

        using (var fillBrush = new SolidBrush(fillColor))
        using (var borderPen = new Pen(borderColor, 2f))
        {
            e.Graphics.FillRectangle(fillBrush, boxRect);
            e.Graphics.DrawRectangle(borderPen, boxRect);
        }

        if (checkBox.Checked)
        {
            using var checkPen = new Pen(Text, 2.4f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            Point p1 = new(boxRect.Left + boxSize / 4, boxRect.Top + boxSize / 2);
            Point p2 = new(boxRect.Left + boxSize / 2 - 1, boxRect.Bottom - boxSize / 4);
            Point p3 = new(boxRect.Right - boxSize / 5, boxRect.Top + boxSize / 4);
            e.Graphics.DrawLines(checkPen, new[] { p1, p2, p3 });
        }

        if (hasText)
        {
            var textRect = new Rectangle(boxRect.Right + 8, 0, Math.Max(0, checkBox.ClientSize.Width - boxRect.Right - 8), checkBox.ClientSize.Height);
            TextRenderer.DrawText(
                e.Graphics,
                checkBox.Text,
                checkBox.Font,
                textRect,
                checkBox.Enabled ? checkBox.ForeColor : MutedText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    private static void PaintComboBoxItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox comboBox)
        {
            return;
        }

        bool selected = e.State.HasFlag(DrawItemState.Selected);
        Color backColor = selected ? Selection : Field;
        using (var brush = new SolidBrush(backColor))
        {
            e.Graphics.FillRectangle(brush, e.Bounds);
        }

        TextRenderer.DrawText(
            e.Graphics,
            e.Index >= 0
                ? comboBox.GetItemText(comboBox.Items[e.Index])
                : comboBox.Text,
            comboBox.Font,
            Rectangle.Inflate(e.Bounds, -4, 0),
            Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    private static void ApplyStyledButtonPaint(Button button)
    {
        bool hover = false;
        bool pressed = false;
        button.MouseEnter += (_, _) =>
        {
            hover = true;
            button.Invalidate();
        };
        button.MouseLeave += (_, _) =>
        {
            hover = false;
            pressed = false;
            button.Invalidate();
        };
        button.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                pressed = true;
                button.Invalidate();
            }
        };
        button.MouseUp += (_, _) =>
        {
            pressed = false;
            button.Invalidate();
        };
        button.EnabledChanged += (_, _) => button.Invalidate();
        button.Paint += (_, e) =>
        {
            Color fill = button.Enabled
                ? pressed
                    ? button.FlatAppearance.MouseDownBackColor
                    : hover
                        ? button.FlatAppearance.MouseOverBackColor
                        : button.BackColor
                : SurfaceRaised;
            using (var fillBrush = new SolidBrush(fill))
            {
                e.Graphics.FillRectangle(fillBrush, button.ClientRectangle);
            }

            using (var borderPen = new Pen(button.Enabled ? button.FlatAppearance.BorderColor : Border))
            {
                e.Graphics.DrawRectangle(
                    borderPen,
                    0,
                    0,
                    Math.Max(0, button.ClientSize.Width - 1),
                    Math.Max(0, button.ClientSize.Height - 1));
            }

            if (string.IsNullOrEmpty(button.Text))
            {
                return;
            }

            TextRenderer.DrawText(
                e.Graphics,
                button.Text,
                button.Font,
                button.ClientRectangle,
                button.Enabled ? button.ForeColor : MutedText,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.SingleLine);
        };
    }

    public static void EnableDoubleBuffering(Control control)
    {
        typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(control, true);
    }
}
