using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal static class TimeEditDialog
{
    public static bool TryShow(IWin32Window owner, AppSettings settings, string title, string value, bool allowEmpty, out string editedText)
    {
        editedText = value;
        using var inputFont = UiTheme.FormFont(14f);
        using var form = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedToolWindow,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(500, 178)
        };
        UiTheme.ConfigureForm(form, new Size(500, 178));

        if (owner is Form ownerForm)
        {
            form.TopMost = ownerForm.TopMost;
        }

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = form.BackColor,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18, 18, 18, 14)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));

        var textBox = new TextBox
        {
            Text = value,
            Dock = DockStyle.Fill,
            Font = inputFont
        };
        UiTheme.StyleTextBox(textBox);

        var okButton = new Button
        {
            Text = Localizer.Get("OK", settings),
            DialogResult = DialogResult.OK
        };
        UiTheme.StyleButton(okButton, accent: true);
        ApplyDialogButtonPaint(okButton, accent: true);

        var cancelButton = new Button
        {
            Text = Localizer.Get("Cancel", settings),
            DialogResult = DialogResult.Cancel
        };
        UiTheme.StyleButton(cancelButton);
        ApplyDialogButtonPaint(cancelButton, accent: false);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = form.BackColor,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = Padding.Empty,
            Padding = new Padding(0, 6, 0, 0),
            WrapContents = false
        };
        UiTheme.EnableDoubleBuffering(buttons);
        buttons.Controls.Add(okButton);
        buttons.Controls.Add(cancelButton);

        root.Controls.Add(textBox, 0, 0);
        root.Controls.Add(buttons, 0, 2);
        form.Controls.Add(root);
        form.AcceptButton = okButton;
        form.CancelButton = cancelButton;

        if (form.ShowDialog(owner) != DialogResult.OK)
        {
            return false;
        }

        editedText = textBox.Text.Trim();
        return allowEmpty || editedText.Length > 0;
    }

    private static void ApplyDialogButtonPaint(Button button, bool accent)
    {
        bool hover = false;
        bool pressed = false;
        Color normal = accent ? UiTheme.Accent : UiTheme.SurfaceRaised;
        Color hoverColor = accent ? UiTheme.AccentHover : Color.FromArgb(47, 58, 64);
        Color downColor = accent ? UiTheme.AccentDown : Color.FromArgb(41, 50, 56);
        Color border = accent ? UiTheme.Accent : UiTheme.Border;

        button.FlatAppearance.BorderSize = 0;
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
        button.Paint += (_, e) =>
        {
            Color fill = pressed ? downColor : hover ? hoverColor : normal;
            using var fillBrush = new SolidBrush(fill);
            e.Graphics.FillRectangle(fillBrush, button.ClientRectangle);
            using var borderPen = new Pen(border);
            e.Graphics.DrawRectangle(borderPen, 0, 0, Math.Max(0, button.ClientSize.Width - 1), Math.Max(0, button.ClientSize.Height - 1));

            TextRenderer.DrawText(
                e.Graphics,
                button.Text,
                button.Font,
                button.ClientRectangle,
                button.ForeColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.SingleLine);
        };
    }
}
