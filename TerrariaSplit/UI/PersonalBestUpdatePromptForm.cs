using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class PersonalBestUpdatePromptForm : Form
{
    private readonly System.Windows.Forms.Timer timer = new();
    private readonly Label countdownLabel = new();
    private int remainingSeconds;
    private readonly AppSettings settings;

    public PersonalBestUpdatePromptForm(string updateText, int timeoutSeconds, AppSettings settings)
    {
        this.settings = settings;
        remainingSeconds = Math.Max(1, timeoutSeconds);
        int lineCount = Math.Max(1, updateText.Split(Environment.NewLine).Length);
        int height = Math.Clamp(210 + lineCount * 28, 260, 760);
        UiTheme.ConfigureForm(this, new Size(1040, 260));
        ClientSize = new Size(1040, height);
        Text = Localizer.Get("Update personal data?", settings);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        MaximizeBox = false;
        MinimizeBox = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = BackColor,
            Padding = new Padding(22, 18, 22, 20),
            ColumnCount = 1,
            RowCount = 4
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            BackColor = BackColor,
            Font = UiTheme.FormFont(12.5f, FontStyle.Bold),
            ForeColor = UiTheme.Text,
            Text = Localizer.Get("Update personal data?", settings)
        };

        var detailLabel = new Label
        {
            Dock = DockStyle.Fill,
            BackColor = BackColor,
            ForeColor = UiTheme.Text,
            Font = UiTheme.FormFont(10f),
            Text = updateText,
            TextAlign = ContentAlignment.TopLeft,
            UseMnemonic = false
        };

        countdownLabel.AutoSize = true;
        countdownLabel.Dock = DockStyle.Fill;
        countdownLabel.BackColor = BackColor;
        countdownLabel.ForeColor = UiTheme.MutedText;

        var buttonPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Right,
            BackColor = BackColor,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        var yesButton = new Button { Text = Localizer.Get("Update", settings) };
        UiTheme.StyleButton(yesButton, accent: true, minimumWidth: 118);
        yesButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Yes;
            Close();
        };

        var noButton = new Button { Text = Localizer.Get("Skip", settings) };
        UiTheme.StyleButton(noButton, accent: false, minimumWidth: 118);
        noButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.No;
            Close();
        };

        buttonPanel.Controls.Add(yesButton);
        buttonPanel.Controls.Add(noButton);

        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(detailLabel, 0, 1);
        layout.Controls.Add(countdownLabel, 0, 2);
        layout.Controls.Add(buttonPanel, 0, 3);
        Controls.Add(layout);

        AcceptButton = yesButton;
        CancelButton = noButton;
        DialogResult = DialogResult.Yes;
        UpdateCountdownText();

        timer.Interval = 1000;
        timer.Tick += (_, _) =>
        {
            remainingSeconds--;
            if (remainingSeconds <= 0)
            {
                DialogResult = DialogResult.Yes;
                Close();
                return;
            }

            UpdateCountdownText();
        };
        timer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void UpdateCountdownText()
    {
        countdownLabel.Text = string.Format(
            Localizer.Get("No response updates automatically in {0}s.", settings),
            remainingSeconds);
    }
}
