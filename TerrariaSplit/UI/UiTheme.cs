using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace TerrariaSplit;

internal static class UiTheme
{
    public static readonly Color Window = Color.FromArgb(17, 19, 24);
    public static readonly Color Surface = Color.FromArgb(25, 29, 36);
    public static readonly Color SurfaceRaised = Color.FromArgb(31, 36, 45);
    public static readonly Color Field = Color.FromArgb(20, 24, 31);
    public static readonly Color Border = Color.FromArgb(58, 66, 79);
    public static readonly Color Accent = Color.FromArgb(62, 139, 255);
    public static readonly Color AccentHover = Color.FromArgb(82, 154, 255);
    public static readonly Color AccentDown = Color.FromArgb(40, 111, 214);
    public static readonly Color Text = Color.FromArgb(239, 243, 248);
    public static readonly Color MutedText = Color.FromArgb(169, 178, 191);
    public static readonly Color Selection = Color.FromArgb(49, 78, 115);

    public static Font FormFont(float size = 10f, FontStyle style = FontStyle.Regular)
    {
        return new Font("Segoe UI", size, style);
    }

    public static void ConfigureForm(Form form, Size minimumSize)
    {
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.BackColor = Window;
        form.ForeColor = Text;
        form.Font = FormFont();
        form.MinimumSize = minimumSize;
        EnableDoubleBuffering(form);
    }

    public static void StyleButton(Button button, bool accent = false, int minimumWidth = 112)
    {
        button.AutoEllipsis = true;
        button.BackColor = accent ? Accent : SurfaceRaised;
        button.FlatStyle = FlatStyle.Flat;
        button.ForeColor = Text;
        button.Height = 38;
        button.Margin = new Padding(8, 0, 0, 0);
        button.MinimumSize = new Size(minimumWidth, 38);
        button.Padding = new Padding(10, 0, 10, 1);
        button.UseVisualStyleBackColor = false;

        button.FlatAppearance.BorderColor = accent ? Accent : Border;
        button.FlatAppearance.MouseDownBackColor = accent ? AccentDown : Color.FromArgb(39, 45, 56);
        button.FlatAppearance.MouseOverBackColor = accent ? AccentHover : Color.FromArgb(43, 49, 61);

        Size textSize = TextRenderer.MeasureText(button.Text, button.Font);
        button.Width = Math.Max(minimumWidth, textSize.Width + 30);
    }

    public static void StyleTextBox(TextBox textBox)
    {
        textBox.BackColor = Field;
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.ForeColor = Text;
        textBox.Margin = new Padding(0, 5, 2, 5);
    }

    public static void StyleComboBox(ComboBox comboBox)
    {
        comboBox.BackColor = Field;
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox.FlatStyle = FlatStyle.Flat;
        comboBox.ForeColor = Text;
        comboBox.Margin = new Padding(0, 5, 2, 5);
    }

    public static void StyleNumericBox(NumericUpDown numericBox)
    {
        numericBox.BackColor = Field;
        numericBox.BorderStyle = BorderStyle.FixedSingle;
        numericBox.ForeColor = Text;
        numericBox.Margin = new Padding(0, 5, 2, 5);
        numericBox.TextAlign = HorizontalAlignment.Right;
    }

    public static void StyleCheckBox(CheckBox checkBox)
    {
        checkBox.FlatStyle = FlatStyle.Flat;
        checkBox.ForeColor = Text;
        checkBox.Margin = new Padding(0, 7, 0, 7);
    }

    public static void EnableDoubleBuffering(Control control)
    {
        typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(control, true);
    }
}
