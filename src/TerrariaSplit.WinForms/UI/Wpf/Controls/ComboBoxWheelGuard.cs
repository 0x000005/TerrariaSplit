using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TerrariaSplit.UI.Wpf.Controls;

internal static class ComboBoxWheelGuard
{
    public static void Register()
    {
        EventManager.RegisterClassHandler(
            typeof(System.Windows.Controls.ComboBox),
            Mouse.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(HandlePreviewMouseWheel));
    }

    private static void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var combo = (System.Windows.Controls.ComboBox)sender;
        if (combo.IsDropDownOpen)
        {
            return;
        }

        e.Handled = true;
        DependencyObject? parent = VisualTreeHelper.GetParent(combo) ?? LogicalTreeHelper.GetParent(combo);
        while (parent is not null && parent is not UIElement)
        {
            parent = LogicalTreeHelper.GetParent(parent);
        }

        if (parent is UIElement element)
        {
            element.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = Mouse.MouseWheelEvent,
                Source = combo
            });
        }
    }
}
