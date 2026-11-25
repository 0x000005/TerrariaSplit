using System.Windows;
using TerrariaSplit.UI.Wpf.Controls;

namespace TerrariaSplit.UI.Wpf.Dialogs;

internal sealed partial class ModernTimeEditDialog : ModernWindow
{
    private readonly bool allowEmpty;

    public ModernTimeEditDialog(
        AppSettings settings,
        string title,
        string initialValue,
        bool allowEmpty)
    {
        InitializeComponent();

        this.allowEmpty = allowEmpty;

        Title = title;
        TimeTextBox.Text = initialValue;
        OkActionButton.Content = Localizer.Get("OK", settings);
        CancelActionButton.Content = Localizer.Get("Cancel", settings);

        TimeTextBox.TextChanged += (_, _) => UpdateValidation();
        OkActionButton.Click += OnOkClick;
        CancelActionButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };

        Loaded += (_, _) =>
        {
            TimeTextBox.Focus();
            TimeTextBox.SelectAll();
            UpdateValidation();
        };

        UpdateValidation();
    }

    public string EditedText { get; private set; } = string.Empty;

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        string trimmed = TimeTextBox.Text.Trim();
        if (!IsInputValid(trimmed))
        {
            return;
        }

        EditedText = trimmed;
        DialogResult = true;
        Close();
    }

    private void UpdateValidation()
    {
        OkActionButton.IsEnabled = IsInputValid(TimeTextBox.Text.Trim());
    }

    private bool IsInputValid(string trimmed)
    {
        if (trimmed.Length == 0)
        {
            return allowEmpty;
        }

        return TimeText.TryParse(trimmed, out _);
    }
}
