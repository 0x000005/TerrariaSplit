using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal static class TimeEditDialog
{
    public static bool TryShow(
        IWin32Window owner,
        AppSettings settings,
        string title,
        string value,
        bool allowEmpty,
        Func<Form, IDisposable> registerModalWindow,
        out string editedText)
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

        using IDisposable modalWindow = registerModalWindow(form);

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

        var cancelButton = new Button
        {
            Text = Localizer.Get("Cancel", settings),
            DialogResult = DialogResult.Cancel
        };
        UiTheme.StyleButton(cancelButton);

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

        if (form.ShowDialog() != DialogResult.OK)
        {
            return false;
        }

        editedText = textBox.Text.Trim();
        return allowEmpty || editedText.Length > 0;
    }
}
