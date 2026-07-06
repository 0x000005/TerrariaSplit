using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal static class UiDpiScale
{
    public const float BaseDpi = 192f;

    private const float MinimumScale = 0.5f;
    private const float MaximumScale = 2.5f;
    private static readonly ConditionalWeakTable<Control, ScaleState> ControlStates = new();

    public static float SystemScale => GetSystemScale();

    public static int ScaleIntFromBase200(int value)
    {
        return ScaleInt(value, SystemScale);
    }

    public static int ScaleIntFromBase200(float value)
    {
        return ScaleInt(value, SystemScale);
    }

    public static int ScaleInt(int value, float scale)
    {
        return value == 0 ? 0 : Math.Max(1, (int)MathF.Round(value * scale));
    }

    public static int ScaleInt(float value, float scale)
    {
        return value == 0f ? 0 : Math.Max(1, (int)MathF.Round(value * scale));
    }

    public static float ScaleFloat(float value, float scale)
    {
        return value == 0f ? 0f : value * scale;
    }

    public static Size ScaleSize(Size size, float scale)
    {
        return new Size(ScaleDimension(size.Width, scale), ScaleDimension(size.Height, scale));
    }

    public static Padding ScalePadding(Padding padding, float scale)
    {
        return new Padding(
            ScaleDimension(padding.Left, scale),
            ScaleDimension(padding.Top, scale),
            ScaleDimension(padding.Right, scale),
            ScaleDimension(padding.Bottom, scale));
    }

    public static void ApplyBase200ClientLayout(Form form, Size baseClientSize, Size baseMinimumSize)
    {
        ApplyBase200Layout(form, baseClientSize, baseMinimumSize, useClientSize: true);
    }

    public static void ApplyBase200WindowLayout(Form form, Size baseWindowSize, Size baseMinimumSize)
    {
        ApplyBase200Layout(form, baseWindowSize, baseMinimumSize, useClientSize: false);
    }

    public static void EnableBase200ControlScaling(Control root)
    {
        ScaleControlTree(root, SystemScale, includeRoot: false);
    }

    public static float GetAppliedScale(Control control)
    {
        for (Control? current = control; current is not null; current = current.Parent)
        {
            if (ControlStates.TryGetValue(current, out ScaleState? state) && state.HasAppliedScale)
            {
                return state.AppliedScale;
            }
        }

        return 1f;
    }

    public static float ScaleFloatForControl(Control control, float value)
    {
        return ScaleFloat(value, GetAppliedScale(control));
    }

    public static int ScaleIntForControl(Control control, float value)
    {
        return ScaleInt(value, GetAppliedScale(control));
    }

    internal static float ClampScaleForTests(float scale)
    {
        return ClampScale(scale);
    }

    private static void ApplyBase200Layout(Form form, Size baseSize, Size baseMinimumSize, bool useClientSize)
    {
        float scale = SystemScale;
        form.AutoScaleMode = AutoScaleMode.None;
        form.MinimumSize = ScaleSize(baseMinimumSize, scale);
        if (useClientSize)
        {
            form.ClientSize = ScaleSize(baseSize, scale);
        }
        else
        {
            form.Size = ScaleSize(baseSize, scale);
        }

        ScaleControlTree(form, scale, includeRoot: false);
    }

    private static float GetSystemScale()
    {
        try
        {
            using Graphics graphics = Graphics.FromHwnd(IntPtr.Zero);
            return ClampScale(graphics.DpiX / BaseDpi);
        }
        catch
        {
            return 1f;
        }
    }

    private static float ClampScale(float scale)
    {
        if (!float.IsFinite(scale) || scale <= 0f)
        {
            return 1f;
        }

        return Math.Clamp(scale, MinimumScale, MaximumScale);
    }

    private static int ScaleDimension(int value, float scale)
    {
        return value == 0 ? 0 : Math.Max(1, (int)MathF.Round(value * scale));
    }

    private static void ScaleControlTree(Control control, float scale, bool includeRoot)
    {
        ScaleState state = ControlStates.GetValue(control, static _ => new ScaleState());
        state.Scale = scale;
        if (!state.ControlAddedHooked)
        {
            control.ControlAdded += HandleControlAdded;
            state.ControlAddedHooked = true;
        }

        if (includeRoot)
        {
            ScaleControl(control, scale, state);
        }

        foreach (Control child in control.Controls)
        {
            ScaleControlTree(child, scale, includeRoot: true);
        }
    }

    private static void HandleControlAdded(object? sender, ControlEventArgs e)
    {
        if (sender is not Control parent ||
            e.Control is null ||
            !ControlStates.TryGetValue(parent, out ScaleState? parentState))
        {
            return;
        }

        ScaleControlTree(e.Control, parentState.Scale, includeRoot: true);
    }

    private static void ScaleControl(Control control, float scale, ScaleState state)
    {
        float ratio = state.HasAppliedScale ? scale / state.AppliedScale : scale;
        if (state.HasAppliedScale && Math.Abs(ratio - 1f) < 0.001f)
        {
            return;
        }

        state.HasAppliedScale = true;
        state.AppliedScale = scale;

        control.Margin = ScalePadding(control.Margin, ratio);
        control.Padding = ScalePadding(control.Padding, ratio);
        control.MinimumSize = ScaleSize(control.MinimumSize, ratio);
        control.MaximumSize = ScaleSize(control.MaximumSize, ratio);
        ScaleExplicitSize(control, ratio);

        if (control is TableLayoutPanel table)
        {
            ScaleTableLayout(table, ratio);
        }

        if (control is Button button)
        {
            RefreshButtonWidth(button, scale);
        }

        if (control is DataGridView grid)
        {
            ScaleDataGridView(grid, ratio);
        }

        if (control is ComboBox comboBox)
        {
            comboBox.ItemHeight = ScaleDimension(comboBox.ItemHeight, ratio);
        }

        if (control is ListBox listBox)
        {
            listBox.ItemHeight = ScaleDimension(listBox.ItemHeight, ratio);
        }
    }

    private static void ScaleExplicitSize(Control control, float ratio)
    {
        if (control.Dock == DockStyle.Fill)
        {
            return;
        }

        control.Size = ScaleSize(control.Size, ratio);
    }

    private static void ScaleTableLayout(TableLayoutPanel table, float ratio)
    {
        foreach (ColumnStyle column in table.ColumnStyles)
        {
            if (column.SizeType == SizeType.Absolute)
            {
                column.Width = ScaleFloat(column.Width, ratio);
            }
        }

        foreach (RowStyle row in table.RowStyles)
        {
            if (row.SizeType == SizeType.Absolute)
            {
                row.Height = ScaleFloat(row.Height, ratio);
            }
        }
    }

    private static void RefreshButtonWidth(Button button, float scale)
    {
        if (string.IsNullOrEmpty(button.Text) || button.Dock == DockStyle.Fill)
        {
            return;
        }

        Size textSize = TextRenderer.MeasureText(button.Text, button.Font);
        int contentPadding = ScaleInt(42, scale);
        button.Width = Math.Max(button.MinimumSize.Width, textSize.Width + contentPadding);
    }

    private static void ScaleDataGridView(DataGridView grid, float ratio)
    {
        grid.RowHeadersWidth = ScaleDimension(grid.RowHeadersWidth, ratio);
        grid.RowTemplate.Height = ScaleDimension(grid.RowTemplate.Height, ratio);
        grid.ColumnHeadersHeight = ScaleDimension(grid.ColumnHeadersHeight, ratio);
        grid.DefaultCellStyle.Padding = ScalePadding(grid.DefaultCellStyle.Padding, ratio);
        grid.AlternatingRowsDefaultCellStyle.Padding = ScalePadding(grid.AlternatingRowsDefaultCellStyle.Padding, ratio);
        grid.ColumnHeadersDefaultCellStyle.Padding = ScalePadding(grid.ColumnHeadersDefaultCellStyle.Padding, ratio);
        grid.RowHeadersDefaultCellStyle.Padding = ScalePadding(grid.RowHeadersDefaultCellStyle.Padding, ratio);

        foreach (DataGridViewRow row in grid.Rows)
        {
            row.Height = ScaleDimension(row.Height, ratio);
            row.DefaultCellStyle.Padding = ScalePadding(row.DefaultCellStyle.Padding, ratio);
        }
    }

    private sealed class ScaleState
    {
        public float Scale { get; set; } = 1f;

        public bool HasAppliedScale { get; set; }

        public float AppliedScale { get; set; } = 1f;

        public bool ControlAddedHooked { get; set; }
    }
}
