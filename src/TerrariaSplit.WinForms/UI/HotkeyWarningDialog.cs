using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed class HotkeyWarningDialog : Form
{
    public HotkeyWarningDialog(string title, string message)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(640, 260);
        Padding = new Padding(1);
        UiTheme.ConfigureForm(this, new Size(520, 260));

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Window,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(24, 22, 24, 18)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60f));

        var body = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = false,
            ForeColor = UiTheme.Text,
            Font = UiTheme.FormFont(9.5f),
            Margin = Padding.Empty,
            Text = message,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = UiTheme.Window,
            Padding = new Padding(0, 12, 0, 0),
            WrapContents = false
        };

        var okButton = new Button
        {
            Text = "OK"
        };
        UiTheme.StyleButton(okButton, accent: true, minimumWidth: 120);
        okButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };

        footer.Controls.Add(okButton);
        root.Controls.Add(body, 0, 0);
        root.Controls.Add(footer, 0, 1);
        Controls.Add(root);

        AcceptButton = okButton;
        CancelButton = okButton;
    }
}
