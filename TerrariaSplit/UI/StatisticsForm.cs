using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class StatisticsForm : Form
{
    private static readonly Color WindowColor = UiTheme.Window;
    private static readonly Color GridColor = UiTheme.Surface;
    private static readonly Color HeaderColor = UiTheme.SurfaceRaised;
    private static readonly Color BorderColor = UiTheme.Border;
    private static readonly Color TextColor = UiTheme.Text;
    private static readonly Color MutedTextColor = UiTheme.MutedText;

    private readonly AppSettings settings;
    private readonly RunStats stats;
    private readonly List<ReferenceSplitSet> referenceTimeSets;
    private readonly List<ReferenceSplitSet> personalBestSets;
    private readonly ComboBox referenceTimeBox = new();
    private readonly ComboBox personalBestBox = new();
    private DataGridView grid = null!;

    public StatisticsForm(AppSettings settings)
    {
        this.settings = settings;
        stats = RunStatsStore.Load();
        referenceTimeSets = settings.UsePersonalBestAsReferenceTime
            ? new List<ReferenceSplitSet> { settings.CreatePersonalBestReferenceSet() }
            : settings.ReferenceSplitSets.ToList();
        personalBestSets = SplitTimeSetStore.LoadLastRunSets();

        Text = Localizer.Get("Statistics", settings);
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1600, 800);
        UiTheme.ConfigureForm(this, new Size(1600, 540));
        Font = UiTheme.FormFont(9.5f);
        ShowInTaskbar = false;

        Controls.Add(CreateContent());
    }

    private Control CreateContent()
    {
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = WindowColor,
            ColumnCount = 1,
            RowCount = 2
        };
        UiTheme.EnableDoubleBuffering(content);
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 122f));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        content.Controls.Add(CreateSelectorBar(), 0, 0);

        grid = CreateGrid();
        content.Controls.Add(grid, 0, 1);
        RefreshRows();

        return content;
    }

    private Control CreateSelectorBar()
    {
        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(18, 12, 18, 12),
            BackColor = WindowColor
        };
        UiTheme.EnableDoubleBuffering(bar);
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        bar.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));
        bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        bar.Controls.Add(CreateSelectorLabel("Reference run"), 0, 0);
        ConfigureSelector(referenceTimeBox);
        foreach (ReferenceSplitSet timeSet in referenceTimeSets)
        {
            referenceTimeBox.Items.Add(timeSet.Name);
        }
        referenceTimeBox.SelectedItem = settings.GetActiveReferenceSet().Name;
        if (referenceTimeBox.SelectedIndex < 0 && referenceTimeBox.Items.Count > 0)
        {
            referenceTimeBox.SelectedIndex = 0;
        }
        referenceTimeBox.Enabled = !settings.UsePersonalBestAsReferenceTime;
        referenceTimeBox.SelectedIndexChanged += (_, _) => RefreshRows();
        bar.Controls.Add(referenceTimeBox, 0, 1);

        bar.Controls.Add(CreateSelectorLabel("Selected run"), 1, 0);
        ConfigureSelector(personalBestBox);
        foreach (ReferenceSplitSet timeSet in personalBestSets)
        {
            personalBestBox.Items.Add(timeSet.Name);
        }
        if (personalBestBox.Items.Count > 0)
        {
            personalBestBox.SelectedIndex = 0;
        }
        personalBestBox.SelectedIndexChanged += (_, _) => RefreshRows();
        bar.Controls.Add(personalBestBox, 1, 1);

        return bar;
    }

    private Label CreateSelectorLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = TextColor,
            AutoEllipsis = true,
            Margin = new Padding(0, 0, 18, 0),
            Text = Localizer.Get(text, settings),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static void ConfigureSelector(ComboBox comboBox)
    {
        comboBox.Dock = DockStyle.None;
        comboBox.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        comboBox.Height = 26;
        UiTheme.StyleComboBox(comboBox);
        comboBox.Margin = new Padding(0, 0, 18, 0);
    }

    private DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToOrderColumns = false,
            AllowUserToResizeRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = WindowColor,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
            EnableHeadersVisualStyles = false,
            GridColor = BorderColor,
            EditMode = DataGridViewEditMode.EditProgrammatically,
            MultiSelect = false,
            RowHeadersVisible = true,
            RowHeadersWidth = 300,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowTemplate = { Height = 40 },
            ColumnHeadersHeight = 44,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
        };
        UiTheme.EnableDoubleBuffering(grid);
        grid.CellPainting += PaintRowHeader;
        grid.CellPainting += PaintMergedSegmentCell;

        grid.ColumnHeadersDefaultCellStyle.BackColor = HeaderColor;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextColor;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = HeaderColor;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextColor;
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.DefaultCellStyle.BackColor = GridColor;
        grid.DefaultCellStyle.ForeColor = TextColor;
        grid.DefaultCellStyle.SelectionBackColor = GridColor;
        grid.DefaultCellStyle.SelectionForeColor = TextColor;
        grid.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
        grid.RowHeadersDefaultCellStyle.BackColor = HeaderColor;
        grid.RowHeadersDefaultCellStyle.ForeColor = TextColor;
        grid.RowHeadersDefaultCellStyle.SelectionBackColor = HeaderColor;
        grid.RowHeadersDefaultCellStyle.SelectionForeColor = TextColor;
        grid.RowHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 6, 0);

        grid.Columns.Add("ReferenceTime", Localizer.Get("Reference time column", settings));
        grid.Columns.Add("PersonalTime", Localizer.Get("Selected run time column", settings));
        grid.Columns.Add("PersonalBest", Localizer.Get("Personal best time column", settings));
        grid.Columns.Add("ReferenceSegment", Localizer.Get("Reference segment time column", settings));
        grid.Columns.Add("PersonalSegment", Localizer.Get("Selected run segment time column", settings));
        grid.Columns.Add("PersonalBestSegment", Localizer.Get("Personal best segment time column", settings));
        foreach (DataGridViewColumn column in grid.Columns)
        {
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        return grid;
    }

    private void RefreshRows()
    {
        if (grid is null)
        {
            return;
        }

        ReferenceSplitSet referenceTimeSet = GetSelectedReferenceTimeSet();
        Dictionary<string, string> personalSplits = GetSelectedPersonalSplits();
        grid.Rows.Clear();

        foreach (StatisticsTableRow row in StatisticsTableBuilder.Build(settings, referenceTimeSet, personalSplits))
        {
            int rowIndex = grid.Rows.Add(
                row.ReferenceTimeText,
                row.PersonalTimeText,
                row.PersonalBestText,
                row.ReferenceSegmentText,
                row.PersonalSegmentText,
                row.PersonalBestSegmentText);
            grid.Rows[rowIndex].HeaderCell.Value = Localizer.Get(row.Unit.DisplayName, settings);
            grid.Rows[rowIndex].Tag = row;
        }

        if (grid.Rows.Count == 0)
        {
            int rowIndex = grid.Rows.Add("--", "--", "--", "--", "--", "--");
            grid.Rows[rowIndex].HeaderCell.Value = Localizer.Get("No splits", settings);
            grid.Rows[rowIndex].DefaultCellStyle.ForeColor = MutedTextColor;
        }
    }

    private void PaintRowHeader(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (sender is not DataGridView grid ||
            e.Graphics is not Graphics graphics ||
            e.RowIndex < 0 ||
            e.ColumnIndex != -1)
        {
            return;
        }

        string text = grid.Rows[e.RowIndex].HeaderCell.Value?.ToString() ?? string.Empty;
        using (var brush = new SolidBrush(HeaderColor))
        {
            graphics.FillRectangle(brush, e.CellBounds);
        }

        Rectangle textBounds = Rectangle.Inflate(e.CellBounds, -6, 0);
        TextRenderer.DrawText(
            graphics,
            text,
            e.CellStyle?.Font ?? grid.Font,
            textBounds,
            TextColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);

        using (var pen = new Pen(BorderColor))
        {
            graphics.DrawLine(pen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
            graphics.DrawLine(pen, e.CellBounds.Right - 1, e.CellBounds.Top, e.CellBounds.Right - 1, e.CellBounds.Bottom);
        }

        e.Handled = true;
    }

    private ReferenceSplitSet GetSelectedReferenceTimeSet()
    {
        if (referenceTimeBox.SelectedItem is string selectedName)
        {
            ReferenceSplitSet? selected = referenceTimeSets.FirstOrDefault(
                timeSet => string.Equals(timeSet.Name, selectedName, StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                return selected;
            }
        }

        return settings.GetActiveReferenceSet();
    }

    private Dictionary<string, string> GetSelectedPersonalSplits()
    {
        if (personalBestBox.SelectedItem is string selectedName)
        {
            ReferenceSplitSet? selected = personalBestSets.FirstOrDefault(
                timeSet => string.Equals(timeSet.Name, selectedName, StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                return selected.Splits;
            }
        }

        return stats.LastRunSplits;
    }

    private void PaintMergedSegmentCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (sender is not DataGridView grid ||
            e.Graphics is not Graphics graphics ||
            e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            !TryGetMergedSegmentText(grid, e, out string? text) ||
            grid.Rows[e.RowIndex].Tag is not StatisticsTableRow row)
        {
            return;
        }

        int firstRowIndex = e.RowIndex - row.GroupOffset;
        if (firstRowIndex < 0 || firstRowIndex >= grid.Rows.Count)
        {
            return;
        }

        int groupRowCount = Math.Min(row.GroupRowCount, grid.Rows.Count - firstRowIndex);
        if (groupRowCount <= 0)
        {
            return;
        }

        Rectangle bounds = grid.GetCellDisplayRectangle(e.ColumnIndex, firstRowIndex, cutOverflow: true);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            e.Handled = true;
            return;
        }

        for (int i = 1; i < groupRowCount; i++)
        {
            bounds.Height += grid.Rows[firstRowIndex + i].Height;
        }

        DataGridViewCellStyle style = e.CellStyle ?? grid.DefaultCellStyle;
        Color backColor = e.State.HasFlag(DataGridViewElementStates.Selected)
            ? style.SelectionBackColor
            : style.BackColor;
        Color foreColor = e.State.HasFlag(DataGridViewElementStates.Selected)
            ? style.SelectionForeColor
            : style.ForeColor;

        using Region oldClip = graphics.Clip.Clone();
        graphics.SetClip(bounds);
        using (var brush = new SolidBrush(backColor))
        {
            graphics.FillRectangle(brush, bounds);
        }

        TextRenderer.DrawText(
            graphics,
            text,
            style.Font,
            bounds,
            foreColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        using (var pen = new Pen(BorderColor))
        {
            graphics.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
        }

        graphics.Clip = oldClip;
        e.Handled = true;
    }

    private static bool TryGetMergedSegmentText(
        DataGridView grid,
        DataGridViewCellPaintingEventArgs e,
        out string? text)
    {
        text = null;
        string columnName = grid.Columns[e.ColumnIndex].Name;
        if (grid.Rows[e.RowIndex].Tag is not StatisticsTableRow row)
        {
            return false;
        }

        text = columnName switch
        {
            "ReferenceSegment" => row.ReferenceSegmentText,
            "PersonalSegment" => row.PersonalSegmentText,
            "PersonalBestSegment" => row.PersonalBestSegmentText,
            _ => null
        };
        return text is not null;
    }
}
