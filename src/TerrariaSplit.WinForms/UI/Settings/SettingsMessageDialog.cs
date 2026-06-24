using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class SettingsMessageDialog : Form
{
    private const int DialogWidth = 640;
    private const int TitleBarHeight = 52;
    private const int FooterHeight = 76;
    private const int BodyHorizontalPadding = 48;
    private const int BodyVerticalPadding = 38;
    private const int MessageHeightSafetyPadding = 12;

    private readonly Panel titleBar;
    private bool dragging;
    private Point dragStartCursor;
    private Point dragStartLocation;

    public SettingsMessageDialog(
        string title,
        string message,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        Func<string, string> localize)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = CalculateClientSize(message);
        Padding = new Padding(1);
        UiTheme.ConfigureForm(this, new Size(480, 220));

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Window,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, TitleBarHeight));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, FooterHeight));

        titleBar = CreateTitleBar(title);
        root.Controls.Add(titleBar, 0, 0);
        root.Controls.Add(CreateBody(message), 0, 1);
        root.Controls.Add(CreateFooter(buttons, localize), 0, 2);
        Controls.Add(root);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(UiTheme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    private static Size CalculateClientSize(string message)
    {
        using Font font = UiTheme.FormFont(10f);
        int textWidth = DialogWidth - BodyHorizontalPadding;
        int lineHeight = Math.Max(1, TextRenderer.MeasureText(
            "A",
            font,
            new Size(textWidth, int.MaxValue),
            TextFormatFlags.NoPadding).Height);
        Size measured = TextRenderer.MeasureText(
            string.IsNullOrEmpty(message) ? " " : message,
            font,
            new Size(textWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix);
        int messageHeight = Math.Max(lineHeight, measured.Height) + MessageHeightSafetyPadding;
        int height = TitleBarHeight + FooterHeight + BodyVerticalPadding + messageHeight;
        return new Size(DialogWidth, height);
    }

    private Panel CreateTitleBar(string title)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.SurfaceRaised,
            Padding = new Padding(18, 0, 8, 0)
        };
        UiTheme.EnableDoubleBuffering(panel);
        panel.MouseDown += (_, e) => BeginDrag(e);
        panel.MouseMove += (_, _) => ContinueDrag();
        panel.MouseUp += (_, e) => EndDrag(e);

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            ForeColor = UiTheme.Text,
            Font = UiTheme.FormFont(10.5f, FontStyle.Bold),
            Text = title,
            TextAlign = ContentAlignment.MiddleLeft
        };
        titleLabel.MouseDown += (_, e) => BeginDrag(e);
        titleLabel.MouseMove += (_, _) => ContinueDrag();
        titleLabel.MouseUp += (_, e) => EndDrag(e);

        var closeButton = new Button
        {
            Dock = DockStyle.Right,
            FlatStyle = FlatStyle.Flat,
            ForeColor = UiTheme.Text,
            BackColor = UiTheme.SurfaceRaised,
            Text = "X",
            Width = 44
        };
        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(78, 57, 57);
        closeButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(98, 48, 48);
        closeButton.Click += (_, _) =>
        {
            DialogResult = GetCancelResult();
            Close();
        };

        panel.Controls.Add(titleLabel);
        panel.Controls.Add(closeButton);
        return panel;
    }

    private static Control CreateBody(string message)
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Window,
            ColumnCount = 1,
            RowCount = 1,
            Padding = new Padding(24, 22, 24, 16)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var bodyLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = false,
            ForeColor = UiTheme.Text,
            Font = UiTheme.FormFont(10f),
            Margin = Padding.Empty,
            Text = message,
            TextAlign = ContentAlignment.TopLeft,
            UseMnemonic = false
        };

        body.Controls.Add(bodyLabel, 0, 0);
        return body;
    }

    private Control CreateFooter(MessageBoxButtons buttons, Func<string, string> localize)
    {
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = UiTheme.Window,
            Padding = new Padding(18, 12, 18, 12),
            WrapContents = false
        };

        foreach ((string text, DialogResult result, bool accent) in GetButtonDefinitions(buttons))
        {
            Button button = new()
            {
                DialogResult = result,
                Text = localize(text)
            };
            UiTheme.StyleButton(button, accent, minimumWidth: 118);
            button.Click += (_, _) =>
            {
                DialogResult = result;
                Close();
            };
            footer.Controls.Add(button);

            if (AcceptButton is null && accent)
            {
                AcceptButton = button;
            }

            if (CancelButton is null && IsCancelResult(result))
            {
                CancelButton = button;
            }
        }

        if (AcceptButton is null && footer.Controls.OfType<Button>().FirstOrDefault() is Button firstButton)
        {
            AcceptButton = firstButton;
        }

        CancelButton ??= AcceptButton;
        return footer;
    }

    private static IReadOnlyList<(string Text, DialogResult Result, bool Accent)> GetButtonDefinitions(MessageBoxButtons buttons)
    {
        return buttons switch
        {
            MessageBoxButtons.OKCancel =>
            [
                ("OK", DialogResult.OK, true),
                ("Cancel", DialogResult.Cancel, false)
            ],
            MessageBoxButtons.YesNo =>
            [
                ("Yes", DialogResult.Yes, true),
                ("No", DialogResult.No, false)
            ],
            MessageBoxButtons.YesNoCancel =>
            [
                ("Yes", DialogResult.Yes, true),
                ("No", DialogResult.No, false),
                ("Cancel", DialogResult.Cancel, false)
            ],
            MessageBoxButtons.RetryCancel =>
            [
                ("Retry", DialogResult.Retry, true),
                ("Cancel", DialogResult.Cancel, false)
            ],
            MessageBoxButtons.AbortRetryIgnore =>
            [
                ("Abort", DialogResult.Abort, false),
                ("Retry", DialogResult.Retry, true),
                ("Ignore", DialogResult.Ignore, false)
            ],
            _ =>
            [
                ("OK", DialogResult.OK, true)
            ]
        };
    }

    private DialogResult GetCancelResult()
    {
        return CancelButton is Button button ? button.DialogResult : DialogResult.Cancel;
    }

    private static bool IsCancelResult(DialogResult result)
    {
        return result is DialogResult.Cancel or DialogResult.No;
    }

    private void BeginDrag(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        dragging = true;
        dragStartCursor = Cursor.Position;
        dragStartLocation = Location;
    }

    private void ContinueDrag()
    {
        if (!dragging)
        {
            return;
        }

        Point delta = new(Cursor.Position.X - dragStartCursor.X, Cursor.Position.Y - dragStartCursor.Y);
        Location = new Point(dragStartLocation.X + delta.X, dragStartLocation.Y + delta.Y);
    }

    private void EndDrag(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            dragging = false;
        }
    }
}
