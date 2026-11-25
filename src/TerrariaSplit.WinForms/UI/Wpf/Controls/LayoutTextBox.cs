using WpfSize = System.Windows.Size;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace TerrariaSplit.UI.Wpf.Controls;

internal sealed class LayoutTextBox : WpfTextBox
{
    private const double UnconstrainedWidth = 220;

    public LayoutTextBox()
    {
        SetResourceReference(StyleProperty, typeof(WpfTextBox));
    }

    protected override WpfSize MeasureOverride(WpfSize constraint)
    {
        if (!double.IsPositiveInfinity(constraint.Width))
        {
            return base.MeasureOverride(constraint);
        }

        // A horizontally scrolling page must not use the text length as its layout width.
        WpfSize measured = base.MeasureOverride(new WpfSize(UnconstrainedWidth, constraint.Height));
        return new WpfSize(UnconstrainedWidth, measured.Height);
    }
}
