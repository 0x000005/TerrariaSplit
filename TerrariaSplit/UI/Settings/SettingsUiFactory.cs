using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class SettingsUiFactory
{
    [ThreadStatic]
    private static ToolTip? overflowToolTip;
    private readonly Func<string, string> localize;

    public SettingsUiFactory(Func<string, string> localize)
    {
        this.localize = localize;
    }

    public static SettingsUiFactory For(SettingsForm owner)
    {
        return new SettingsUiFactory(owner.Localize);
    }

    public Control BuildScrollPage(Action<TableLayoutPanel> populate)
    {
        TableLayoutPanel content = CreatePageContent();
        content.SuspendLayout();
        try
        {
            populate(content);
        }
        finally
        {
            content.ResumeLayout(false);
        }

        return CreateScrollPage(content);
    }

    public TableLayoutPanel CreateSection(string title)
    {
        var section = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.Surface,
            ColumnCount = 1,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 18),
            Padding = new Padding(22, 18, 22, 20)
        };
        UiTheme.EnableDoubleBuffering(section);
        section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        AddSectionControl(section, CreateSectionTitle(title));
        return section;
    }

    public TableLayoutPanel CreateGrid(params ColumnStyle[] columnStyles)
    {
        var grid = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.Surface,
            ColumnCount = columnStyles.Length,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        UiTheme.EnableDoubleBuffering(grid);

        foreach (ColumnStyle columnStyle in columnStyles)
        {
            grid.ColumnStyles.Add(columnStyle);
        }

        return grid;
    }

    public FlowLayoutPanel CreateActionBar()
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 18),
            Padding = Padding.Empty,
            WrapContents = false
        };
        UiTheme.EnableDoubleBuffering(panel);
        return panel;
    }

    public TableLayoutPanel CreateTwoColumnGrid(float controlWidth)
    {
        return CreateGrid(ColumnStylePercent(100f), ColumnStyleAbsolute(controlWidth));
    }

    public Label CreateRowLabel(string text)
    {
        Label label = new()
        {
            AutoEllipsis = true,
            AutoSize = false,
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.Text,
            Margin = new Padding(0, 8, 14, 8),
            Text = localize(text),
            TextAlign = ContentAlignment.MiddleLeft
        };
        AttachOverflowToolTip(label);
        return label;
    }

    public Label CreateHeaderLabel(string text, ContentAlignment align = ContentAlignment.MiddleLeft)
    {
        Label label = new()
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            AutoSize = false,
            ForeColor = UiTheme.MutedText,
            Font = UiTheme.FormFont(9.5f, FontStyle.Bold),
            Margin = align == ContentAlignment.MiddleLeft ? new Padding(0, 0, 12, 0) : Padding.Empty,
            Text = localize(text),
            TextAlign = align
        };
        AttachOverflowToolTip(label);
        return label;
    }

    public Label CreateValueLabel()
    {
        Label label = new()
        {
            AutoEllipsis = true,
            AutoSize = false,
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.Text,
            Margin = new Padding(0, 8, 0, 8),
            TextAlign = ContentAlignment.MiddleLeft
        };
        AttachOverflowToolTip(label);
        return label;
    }

    public Label CreateMutedLabel(string text)
    {
        return new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.MutedText,
            Margin = new Padding(0, 12, 0, 10),
            Text = localize(text),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    public Label CreateFieldLabel(string text)
    {
        return new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = UiTheme.FormFont(),
            ForeColor = UiTheme.Text,
            Margin = new Padding(0, 14, 0, 8),
            Text = localize(text)
        };
    }

    public Label CreateWrappedFieldLabel(string text, Color color)
    {
        Label label = new()
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Font = UiTheme.FormFont(),
            ForeColor = color,
            Margin = new Padding(0, 8, 0, 8),
            Text = localize(text),
            TextAlign = ContentAlignment.TopLeft
        };
        label.SizeChanged += (_, _) => UpdateWrappedLabelHeight(label);
        label.TextChanged += (_, _) => UpdateWrappedLabelHeight(label);
        label.FontChanged += (_, _) => UpdateWrappedLabelHeight(label);
        label.ParentChanged += (_, _) => UpdateWrappedLabelHeight(label);
        UpdateWrappedLabelHeight(label);
        return label;
    }

    public Label CreateSubsectionLabel(string text)
    {
        return new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = UiTheme.FormFont(11f, FontStyle.Bold),
            ForeColor = UiTheme.Text,
            Margin = new Padding(0, 14, 0, 8),
            Text = localize(text)
        };
    }

    public TextBox CreateMultilineValueBox(int height)
    {
        return new TextBox
        {
            BackColor = UiTheme.Field,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Top,
            Font = UiTheme.FormFont(9.5f),
            ForeColor = UiTheme.Text,
            Height = height,
            Margin = Padding.Empty,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            ShortcutsEnabled = true,
            TabStop = false,
            WordWrap = false
        };
    }

    public Button CreateActionButton(string text)
    {
        var button = new Button
        {
            AutoSize = true,
            Margin = Padding.Empty,
            Text = localize(text)
        };
        UiTheme.StyleButton(button, accent: true, minimumWidth: 200);
        return button;
    }

    public Button CreateButton(string text, bool accent, int minimumWidth = 128)
    {
        var button = new Button
        {
            Text = localize(text)
        };
        UiTheme.StyleButton(button, accent, minimumWidth);
        return button;
    }

    public Button CreateSmallButton(string text)
    {
        var button = new Button
        {
            Height = 36,
            Margin = new Padding(8, 8, 0, 8),
            Text = localize(text),
            Width = 136
        };
        UiTheme.StyleButton(button, accent: false, minimumWidth: 132);
        button.MinimumSize = new Size(132, 36);
        return button;
    }

    public FlowLayoutPanel CreateButtonPanel(params Button[] buttons)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            WrapContents = false
        };
        UiTheme.EnableDoubleBuffering(panel);

        foreach (Button button in buttons)
        {
            button.Margin = new Padding(6, 2, 0, 2);
            button.Height = 48;
            button.MinimumSize = new Size(Math.Max(72, button.Width), 48);
            panel.Controls.Add(button);
        }

        return panel;
    }

    public CheckBox CreateCheckBox(bool selected)
    {
        var checkBox = new CheckBox
        {
            Checked = selected,
            Dock = DockStyle.Fill
        };
        UiTheme.StyleCheckBox(checkBox);
        return checkBox;
    }

    public SettingsHotkeyTextBox CreateHotkeyBox(Keys hotkey)
    {
        var textBox = new SettingsHotkeyTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true
        };
        UiTheme.StyleTextBox(textBox);
        textBox.SetHotkey(hotkey);
        return textBox;
    }

    public TextBox CreateTextBox(string value)
    {
        var textBox = new TextBox
        {
            Text = value,
            Dock = DockStyle.Fill
        };
        UiTheme.StyleTextBox(textBox);
        return textBox;
    }

    public TextBox CreateNumberBox(int value, int minimum, int maximum)
    {
        var textBox = CreateTextBox(string.Empty);
        textBox.Text = Math.Clamp(value, minimum, maximum).ToString(CultureInfo.InvariantCulture);
        return textBox;
    }

    public TextBox CreateDecimalBox(float value, decimal minimum, decimal maximum)
    {
        var textBox = CreateTextBox(string.Empty);
        textBox.Text = Math.Clamp((decimal)value, minimum, maximum).ToString("0.#", CultureInfo.InvariantCulture);
        return textBox;
    }

    public TextBox CreateTimeBox(int valueSeconds, int minimumSeconds, int maximumSeconds)
    {
        var textBox = CreateTextBox(string.Empty);
        textBox.Text = TimeText.FormatSplit(TimeSpan.FromSeconds(Math.Clamp(valueSeconds, minimumSeconds, maximumSeconds)));
        textBox.PlaceholderText = "m:ss or h:mm:ss";
        return textBox;
    }

    public ComboBox CreateComboBox()
    {
        var comboBox = new ComboBox
        {
            Dock = DockStyle.Fill
        };
        UiTheme.StyleComboBox(comboBox);
        return comboBox;
    }

    public ComboBox CreateDropDownList()
    {
        ComboBox comboBox = CreateComboBox();
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        return comboBox;
    }

    public Panel CreateCenteredCell(Control control, int width)
    {
        return CreateAlignedCell(control, width, HorizontalAlignment.Center);
    }

    public Panel CreateAlignedCell(Control control, int width, HorizontalAlignment alignment)
    {
        control.Dock = DockStyle.None;
        control.Anchor = AnchorStyles.None;
        control.Width = width;

        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = UiTheme.Surface
        };
        panel.Controls.Add(control);
        panel.Resize += (_, _) => AlignControlInPanel(panel, control, alignment);
        control.SizeChanged += (_, _) => AlignControlInPanel(panel, control, alignment);
        AlignControlInPanel(panel, control, alignment);
        return panel;
    }

    public static void AddSection(TableLayoutPanel parent, Control section)
    {
        int row = parent.RowCount++;
        parent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        parent.Controls.Add(section, 0, row);
    }

    public static void AddSectionControl(TableLayoutPanel section, Control control)
    {
        int row = section.RowCount++;
        section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        section.Controls.Add(control, 0, row);
    }

    public void AddSettingRow(TableLayoutPanel grid, string label, Control control)
    {
        int row = AddGridRow(grid);
        grid.Controls.Add(CreateRowLabel(label), 0, row);
        grid.Controls.Add(control, 1, row);
    }

    public void AddSettingRow(TableLayoutPanel grid, string label, Control control, int controlWidth)
    {
        int row = AddGridRow(grid);
        grid.Controls.Add(CreateRowLabel(label), 0, row);
        grid.Controls.Add(CreateAlignedCell(control, controlWidth, HorizontalAlignment.Right), 1, row);
    }

    public void AddHeaderRow(TableLayoutPanel grid, params string[] labels)
    {
        AddHeaderRow(grid, ContentAlignment.MiddleLeft, labels);
    }

    public void AddHeaderRow(TableLayoutPanel grid, ContentAlignment firstColumnAlign, params string[] labels)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        for (int i = 0; i < labels.Length; i++)
        {
            ContentAlignment align = i == 0 ? firstColumnAlign : ContentAlignment.MiddleCenter;
            grid.Controls.Add(CreateHeaderLabel(labels[i], align), i, row);
        }
    }

    public int AddGridRow(TableLayoutPanel grid)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));
        return row;
    }

    public static ColumnStyle ColumnStylePercent(float width)
    {
        return new ColumnStyle(SizeType.Percent, width);
    }

    public static ColumnStyle ColumnStyleAbsolute(float width)
    {
        return new ColumnStyle(SizeType.Absolute, width);
    }

    public static void ClearGrid(TableLayoutPanel grid)
    {
        foreach (Control control in grid.Controls.Cast<Control>().ToArray())
        {
            control.Dispose();
        }

        grid.Controls.Clear();
        grid.RowStyles.Clear();
        grid.RowCount = 0;
    }

    private Label CreateSectionTitle(string title)
    {
        return new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = UiTheme.FormFont(13f, FontStyle.Bold),
            ForeColor = UiTheme.Text,
            Margin = new Padding(0, 0, 0, 14),
            Text = localize(title)
        };
    }

    private static TableLayoutPanel CreatePageContent()
    {
        var content = new TableLayoutPanel
        {
            AutoSize = false,
            BackColor = UiTheme.Window,
            ColumnCount = 1,
            Dock = DockStyle.None,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        UiTheme.EnableDoubleBuffering(content);
        return content;
    }

    private static Control CreateScrollPage(Control content)
    {
        content.Dock = DockStyle.None;
        var panel = new ThemedScrollPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Window,
            Padding = new Padding(22, 18, 20, 12)
        };
        panel.BeginContentUpdate();
        try
        {
            panel.Controls.Add(content);
        }
        finally
        {
            panel.EndContentUpdate();
        }

        UiTheme.EnableDoubleBuffering(panel);
        return panel;
    }

    private static void AlignControlInPanel(Panel panel, Control control, HorizontalAlignment alignment)
    {
        control.Left = alignment switch
        {
            HorizontalAlignment.Left => 0,
            HorizontalAlignment.Right => Math.Max(0, panel.ClientSize.Width - control.Width),
            _ => Math.Max(0, (panel.ClientSize.Width - control.Width) / 2)
        };
        control.Top = Math.Max(0, (panel.ClientSize.Height - control.Height) / 2);
    }

    private static void UpdateWrappedLabelHeight(Label label)
    {
        int width = label.Width;
        if (width <= 0 && label.Parent is not null)
        {
            width = label.Parent.ClientSize.Width - label.Margin.Horizontal;
        }

        width = Math.Max(1, width);
        Size measured = TextRenderer.MeasureText(
            label.Text,
            label.Font,
            new Size(width, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
        int height = Math.Max(label.Font.Height, measured.Height);
        if (label.Height != height)
        {
            label.Height = height;
        }
    }

    private static ToolTip CreateOverflowToolTip()
    {
        return new ToolTip
        {
            AutoPopDelay = 12000,
            InitialDelay = 300,
            ReshowDelay = 100,
            ShowAlways = true
        };
    }

    private static void AttachOverflowToolTip(Control control)
    {
        control.MouseHover += (_, _) => UpdateOverflowToolTip(control);
        control.MouseLeave += (_, _) => GetOverflowToolTip().Hide(control);
        control.SizeChanged += (_, _) => UpdateOverflowToolTip(control);
        control.TextChanged += (_, _) => UpdateOverflowToolTip(control);
    }

    private static void UpdateOverflowToolTip(Control control)
    {
        string tooltipText = IsTextClipped(control) ? control.Text : string.Empty;
        ToolTip toolTip = GetOverflowToolTip();
        toolTip.SetToolTip(control, tooltipText);
        if (string.IsNullOrEmpty(tooltipText))
        {
            toolTip.Hide(control);
        }
    }

    private static bool IsTextClipped(Control control)
    {
        if (string.IsNullOrWhiteSpace(control.Text) ||
            control.ClientSize.Width <= 0 ||
            control.ClientSize.Height <= 0)
        {
            return false;
        }

        Size textSize = TextRenderer.MeasureText(
            control.Text,
            control.Font,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        return textSize.Width > control.ClientSize.Width;
    }

    private static ToolTip GetOverflowToolTip()
    {
        overflowToolTip ??= CreateOverflowToolTip();
        return overflowToolTip;
    }
}
