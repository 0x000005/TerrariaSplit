using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class PracticeWorldSelectorForm : Form
{
    private readonly AppSettings settings;

    public PracticeWorldSlot? SelectedSlot { get; private set; }

    public PracticeWorldSelectorForm(AppSettings settings)
    {
        this.settings = settings;
        Text = Localizer.Get("Practice world selector", settings);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        Opacity = 0.92d;
        BackColor = UiTheme.Surface;
        ForeColor = UiTheme.Text;
        ClientSize = new Size(520, 548);
        Padding = new Padding(20);
        UiTheme.EnableDoubleBuffering(this);
        BuildLayout();
    }

    protected override bool ShowWithoutActivation => false;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        RequestKeyboardFocus();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        RequestKeyboardFocus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        int index = KeyToSlotIndex(e.KeyCode);
        if (index < 0)
        {
            return;
        }

        IReadOnlyList<PracticeWorldSlot> slots = settings.PracticeWorlds.Slots;
        if (index >= slots.Count)
        {
            return;
        }

        PracticeWorldSlot slot = slots[index];
        if (!slot.IsConfigured)
        {
            AppLogger.Info($"Practice world slot {index + 1} is not configured.");
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        SelectedSlot = new PracticeWorldSlot
        {
            Name = slot.Name,
            PlayerFilePath = slot.PlayerFilePath,
            WorldFilePath = slot.WorldFilePath
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private void RequestKeyboardFocus()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        TopMost = true;
        BringToFront();
        NativeMethods.SetForegroundWindow(Handle);
        Activate();
        Focus();
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Surface,
            ColumnCount = 1,
            RowCount = PracticeWorldSettings.SlotCount + 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));

        var title = new Label
        {
            Dock = DockStyle.Fill,
            Font = UiTheme.FormFont(13f, FontStyle.Bold),
            ForeColor = UiTheme.Text,
            Margin = Padding.Empty,
            Text = Localizer.Get("Practice world selector", settings),
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(title, 0, 0);

        for (int index = 0; index < PracticeWorldSettings.SlotCount; index++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            layout.Controls.Add(CreateSlotRow(index), 0, index + 1);
        }

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        Controls.Add(layout);
    }

    private Control CreateSlotRow(int index)
    {
        PracticeWorldSlot slot = index < settings.PracticeWorlds.Slots.Count
            ? settings.PracticeWorlds.Slots[index]
            : new PracticeWorldSlot();
        bool configured = slot.IsConfigured;

        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Surface,
            ColumnCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54f));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        row.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Font = UiTheme.FormFont(11f, FontStyle.Bold),
            ForeColor = configured ? UiTheme.Text : UiTheme.MutedText,
            Text = SlotKeyText(index),
            TextAlign = ContentAlignment.MiddleCenter
        }, 0, 0);

        row.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Font = UiTheme.FormFont(10.5f, FontStyle.Regular),
            ForeColor = configured ? UiTheme.Text : UiTheme.MutedText,
            Text = GetSlotDisplayName(slot, index),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        }, 1, 0);

        return row;
    }

    private string GetSlotDisplayName(PracticeWorldSlot slot, int index)
    {
        if (!slot.IsConfigured)
        {
            return Localizer.Get("Not configured", settings);
        }

        if (!string.IsNullOrWhiteSpace(slot.Name))
        {
            return slot.Name;
        }

        if (!string.IsNullOrWhiteSpace(slot.WorldFilePath))
        {
            return Path.GetFileNameWithoutExtension(slot.WorldFilePath);
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} {1}",
            Localizer.Get("Practice world", settings),
            SlotKeyText(index));
    }

    private static int KeyToSlotIndex(Keys key)
    {
        return key switch
        {
            Keys.D1 or Keys.NumPad1 => 0,
            Keys.D2 or Keys.NumPad2 => 1,
            Keys.D3 or Keys.NumPad3 => 2,
            Keys.D4 or Keys.NumPad4 => 3,
            Keys.D5 or Keys.NumPad5 => 4,
            Keys.D6 or Keys.NumPad6 => 5,
            Keys.D7 or Keys.NumPad7 => 6,
            Keys.D8 or Keys.NumPad8 => 7,
            Keys.D9 or Keys.NumPad9 => 8,
            Keys.D0 or Keys.NumPad0 => 9,
            _ => -1
        };
    }

    private static string SlotKeyText(int index)
    {
        return index == 9 ? "0" : (index + 1).ToString(CultureInfo.InvariantCulture);
    }
}
