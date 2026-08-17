using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class SettingsMessageDialog : Form
{
    private const int DialogWidth = 640;
    private const int SelectableDialogWidth = 820;
    private const int ScreenMargin = 96;
    private const int TitleBarHeight = 52;
    private const int FooterHeight = 76;
    private const int BodyHorizontalPadding = 48;
    private const int BodyVerticalPadding = 38;
    private const int MessageHeightSafetyPadding = 12;

    private readonly Panel titleBar;
    private readonly Control messageControl;
    private Button? copyDetailsButton;
    private bool dragging;
    private Point dragStartCursor;
    private Point dragStartLocation;

    public static DialogResult ShowThemed(
        IWin32Window? owner,
        string title,
        string message,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        Func<string, string> localize,
        bool selectableMessage = false)
    {
        using var dialog = new SettingsMessageDialog(
            title,
            message,
            buttons,
            icon,
            localize,
            selectableMessage);
        dialog.Shown += (_, _) =>
        {
            dialog.BringToFront();
            dialog.Activate();
            NativeMethods.SetForegroundWindow(dialog.Handle);
        };
        return owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
    }

    public SettingsMessageDialog(
        string title,
        string message,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        Func<string, string> localize,
        bool selectableMessage = false)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = CalculateClientSize(message, selectableMessage);
        Padding = new Padding(1);
        UiTheme.ConfigureForm(
            this,
            selectableMessage
                ? new Size(640, 360)
                : new Size(480, 220));

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
        messageControl = CreateMessageControl(message, selectableMessage);
        root.Controls.Add(titleBar, 0, 0);
        root.Controls.Add(CreateBody(messageControl), 0, 1);
        root.Controls.Add(
            CreateFooter(
                buttons,
                localize,
                selectableMessage),
            0,
            2);
        Controls.Add(root);
    }

    internal string DisplayedMessage => messageControl.Text;

    internal bool HasSelectableMessage => messageControl is TextBoxBase;

    internal bool HasCopyDetailsButton => copyDetailsButton is not null;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(UiTheme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        FitToCurrentScreen();
    }

    private static Size CalculateClientSize(
        string message,
        bool selectableMessage)
    {
        using Font font = UiTheme.FormFont(10f);
        Rectangle workingArea = Screen.PrimaryScreen?.WorkingArea ??
            new Rectangle(0, 0, SelectableDialogWidth, 900);
        int preferredWidth = selectableMessage
            ? SelectableDialogWidth
            : DialogWidth;
        int width = Math.Min(
            preferredWidth,
            Math.Max(480, workingArea.Width - ScreenMargin));
        int textWidth = width - BodyHorizontalPadding;
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
        int maximumHeight = Math.Max(360, workingArea.Height - ScreenMargin);
        if (selectableMessage)
        {
            height = Math.Max(520, height);
        }

        return new Size(width, Math.Min(height, maximumHeight));
    }

    private void FitToCurrentScreen()
    {
        Rectangle workingArea = Screen.FromControl(this).WorkingArea;
        int maximumWidth = Math.Max(480, workingArea.Width - ScreenMargin);
        int maximumHeight = Math.Max(360, workingArea.Height - ScreenMargin);
        MinimumSize = new Size(
            Math.Min(MinimumSize.Width, maximumWidth),
            Math.Min(MinimumSize.Height, maximumHeight));
        Size = new Size(
            Math.Min(Width, maximumWidth),
            Math.Min(Height, maximumHeight));
        Location = new Point(
            Math.Clamp(Left, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - Width)),
            Math.Clamp(Top, workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - Height)));
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

    private static Control CreateBody(Control messageControl)
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

        body.Controls.Add(messageControl, 0, 0);
        return body;
    }

    private static Control CreateMessageControl(
        string message,
        bool selectableMessage)
    {
        if (selectableMessage)
        {
            return new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Field,
                BorderStyle = BorderStyle.None,
                DetectUrls = false,
                ForeColor = UiTheme.Text,
                Font = UiTheme.FormFont(10f),
                Margin = Padding.Empty,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.ForcedVertical,
                ShortcutsEnabled = true,
                Text = message,
                WordWrap = true
            };
        }

        return new Label
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
    }

    private Control CreateFooter(
        MessageBoxButtons buttons,
        Func<string, string> localize,
        bool selectableMessage)
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

        if (selectableMessage)
        {
            copyDetailsButton = new Button
            {
                Text = localize("Copy details")
            };
            UiTheme.StyleButton(copyDetailsButton, minimumWidth: 140);
            copyDetailsButton.Click += (_, _) => CopyDisplayedMessage();
            footer.Controls.Add(copyDetailsButton);
        }

        if (AcceptButton is null && footer.Controls.OfType<Button>().FirstOrDefault() is Button firstButton)
        {
            AcceptButton = firstButton;
        }

        CancelButton ??= AcceptButton;
        return footer;
    }

    private void CopyDisplayedMessage()
    {
        if (string.IsNullOrEmpty(messageControl.Text))
        {
            return;
        }

        try
        {
            Clipboard.SetText(messageControl.Text);
        }
        catch (ExternalException)
        {
            // Keep the dialog usable when another process temporarily owns the clipboard.
        }
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
