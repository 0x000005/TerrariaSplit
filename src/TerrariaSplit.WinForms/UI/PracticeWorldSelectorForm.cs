using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed class PracticeWorldSelectorForm : Form
{
    private readonly AppSettings settings;
    private readonly PracticeWorldSelectorLayoutMetrics layoutMetrics;

    public PracticeWorldSlot? SelectedSlot { get; private set; }

    public PracticeWorldSelectorForm(AppSettings settings)
    {
        this.settings = settings;
        layoutMetrics = CalculateLayoutMetrics(
            Screen.FromPoint(Cursor.Position).WorkingArea,
            GetSystemDpiScale());
        Text = Localizer.Get("Save Selector", settings);
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        KeyPreview = true;
        Opacity = 0.92d;
        BackColor = UiTheme.Surface;
        ForeColor = UiTheme.Text;
        ClientSize = layoutMetrics.ClientSize;
        Padding = new Padding(layoutMetrics.Padding);
        UiTheme.EnableDoubleBuffering(this);
        BuildLayout();
    }

    internal static PracticeWorldSelectorLayoutMetrics CalculateLayoutMetrics(Rectangle workingArea, float dpiScale)
    {
        float normalizedDpiScale = Math.Clamp(dpiScale, 0.75f, 3f);
        float resolutionScale = MathF.Sqrt(
            Math.Max(1f, workingArea.Width) *
            Math.Max(1f, workingArea.Height) /
            (1920f * 1080f));
        resolutionScale = Math.Clamp(resolutionScale, 0.9f, 1.18f);

        float dimensionScale = Math.Clamp(normalizedDpiScale * resolutionScale, 0.82f, 2.1f);
        float availableWidthScale = Math.Max(0.75f, (workingArea.Width - 96f * normalizedDpiScale) / 540f);
        float availableHeightScale = Math.Max(0.75f, (workingArea.Height - 96f * normalizedDpiScale) / 600f);
        dimensionScale = Math.Min(dimensionScale, Math.Min(availableWidthScale, availableHeightScale));

        float fontScale = Math.Clamp(resolutionScale, 0.96f, 1.16f);
        int ScaleInt(int value) => Math.Max(1, (int)Math.Round(value * dimensionScale, MidpointRounding.AwayFromZero));

        int padding = ScaleInt(20);
        int titleHeight = ScaleInt(50);
        int slotHeight = ScaleInt(44);
        int footerHeight = ScaleInt(36);
        int spacerHeight = ScaleInt(44);
        int width = ScaleInt(540);
        int height = padding * 2 + titleHeight + PracticeWorldSettings.SlotCount * slotHeight + footerHeight + spacerHeight;

        return new PracticeWorldSelectorLayoutMetrics(
            new Size(width, height),
            padding,
            titleHeight,
            slotHeight,
            footerHeight,
            ScaleInt(58),
            14f * fontScale,
            12f * fontScale,
            11.3f * fontScale,
            9.5f * fontScale);
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
            FileAppLogger.Instance.Info($"Practice world slot {index + 1} is not configured.");
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

        Focus();
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Surface,
            ColumnCount = 1,
            RowCount = PracticeWorldSettings.SlotCount + 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, layoutMetrics.TitleHeight));

        var title = new Label
        {
            Dock = DockStyle.Fill,
            Font = UiTheme.FormFont(layoutMetrics.TitleFontSize, FontStyle.Bold),
            ForeColor = UiTheme.Text,
            Margin = Padding.Empty,
            Text = Localizer.Get("Save Selector", settings),
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(title, 0, 0);

        for (int index = 0; index < PracticeWorldSettings.SlotCount; index++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, layoutMetrics.SlotHeight));
            layout.Controls.Add(CreateSlotRow(index), 0, index + 1);
        }

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, layoutMetrics.FooterHeight));
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Font = UiTheme.FormFont(layoutMetrics.FooterFontSize, FontStyle.Regular),
            ForeColor = UiTheme.MutedText,
            Margin = Padding.Empty,
            Text = Localizer.Get("Press ESC to exit", settings),
            TextAlign = ContentAlignment.BottomRight
        }, 0, PracticeWorldSettings.SlotCount + 2);
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
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, layoutMetrics.KeyColumnWidth));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        row.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Font = UiTheme.FormFont(layoutMetrics.KeyFontSize, FontStyle.Bold),
            ForeColor = configured ? UiTheme.Text : UiTheme.MutedText,
            Text = SlotKeyText(index),
            TextAlign = ContentAlignment.MiddleCenter
        }, 0, 0);

        row.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Font = UiTheme.FormFont(layoutMetrics.NameFontSize, FontStyle.Regular),
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

    private static float GetSystemDpiScale()
    {
        try
        {
            using Graphics graphics = Graphics.FromHwnd(IntPtr.Zero);
            return graphics.DpiX / 96f;
        }
        catch (Exception)
        {
            return 1f;
        }
    }
}

internal readonly record struct PracticeWorldSelectorLayoutMetrics(
    Size ClientSize,
    int Padding,
    int TitleHeight,
    int SlotHeight,
    int FooterHeight,
    int KeyColumnWidth,
    float TitleFontSize,
    float KeyFontSize,
    float NameFontSize,
    float FooterFontSize);
