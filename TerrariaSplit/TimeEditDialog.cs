using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal static class TimeEditDialog
{
    public static bool TryShow(IWin32Window owner, string title, string value, bool allowEmpty, out string editedText)
    {
        editedText = value;
        using var formFont = new Font("Segoe UI", 11f, FontStyle.Regular);
        using var inputFont = new Font("Segoe UI", 14f, FontStyle.Regular);
        using var form = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedToolWindow,
            MinimizeBox = false,
            MaximizeBox = false,
            AutoScaleMode = AutoScaleMode.Dpi,
            ClientSize = new Size(460, 170),
            MinimumSize = new Size(460, 170),
            BackColor = Color.FromArgb(20, 20, 23),
            ForeColor = Color.Gainsboro,
            Font = formFont
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = form.BackColor,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(14, 14, 14, 12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));

        var textBox = new TextBox
        {
            Text = value,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 6, 0, 8),
            BackColor = Color.FromArgb(40, 40, 47),
            ForeColor = Color.Gainsboro,
            BorderStyle = BorderStyle.FixedSingle,
            Font = inputFont
        };

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Height = 32,
            Width = 96
        };
        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Height = 32,
            Width = 96
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = Padding.Empty,
            Padding = new Padding(0, 8, 0, 0),
            WrapContents = false
        };
        buttons.Controls.Add(okButton);
        buttons.Controls.Add(cancelButton);

        root.Controls.Add(textBox, 0, 0);
        root.Controls.Add(buttons, 0, 1);
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
}
