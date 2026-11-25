using System.Windows;

namespace TerrariaSplit.UI.Wpf.Controls;

internal static class TextBoxHelper
{
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.RegisterAttached(
        "Placeholder",
        typeof(string),
        typeof(TextBoxHelper),
        new PropertyMetadata(string.Empty));

    public static string GetPlaceholder(DependencyObject element)
    {
        return (string)element.GetValue(PlaceholderProperty);
    }

    public static void SetPlaceholder(DependencyObject element, string value)
    {
        element.SetValue(PlaceholderProperty, value ?? string.Empty);
    }
}
