using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using TerrariaSplit.UI.Wpf.Controls;
using TerrariaSplit.UI.Wpf.Interop;
using FormsClipboard = System.Windows.Forms.Clipboard;
using FormsDialogResult = System.Windows.Forms.DialogResult;
using FormsMessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using FormsMessageBoxIcon = System.Windows.Forms.MessageBoxIcon;
using FormsWin32Window = System.Windows.Forms.IWin32Window;
using WpfButton = System.Windows.Controls.Button;

namespace TerrariaSplit.UI.Wpf.Dialogs;

internal sealed partial class ModernMessageDialog : ModernWindow
{
    private const double StandardWidth = 440;
    private const double SelectableWidth = 660;

    private readonly FormsDialogResult cancelResult;
    private WpfButton? copyDetailsButton;

    public ModernMessageDialog(
        string title,
        string message,
        FormsMessageBoxButtons buttons,
        FormsMessageBoxIcon icon,
        Func<string, string> localize,
        bool selectableMessage = false,
        AppFault? fault = null)
    {
        InitializeComponent();

        Title = title;
        DisplayedMessage = message;
        HasSelectableMessage = selectableMessage;

        Width = selectableMessage ? SelectableWidth : StandardWidth;
        ConfigureIcon(icon);
        ConfigureMessageBody(message, selectableMessage);
        cancelResult = ConfigureButtons(buttons, localize, selectableMessage, fault);
        Result = cancelResult;

        PreviewKeyDown += OnDialogPreviewKeyDown;
    }

    public FormsDialogResult Result { get; private set; }

    internal string DisplayedMessage { get; }

    internal bool HasSelectableMessage { get; }

    internal bool HasCopyDetailsButton => copyDetailsButton is not null;

    public static FormsDialogResult Show(
        FormsWin32Window? owner,
        string title,
        string message,
        FormsMessageBoxButtons buttons,
        FormsMessageBoxIcon icon,
        Func<string, string> localize,
        bool selectableMessage = false,
        AppFault? fault = null)
    {
        var dialog = new ModernMessageDialog(
            title,
            message,
            buttons,
            icon,
            localize,
            selectableMessage,
            fault);

        if (owner is not null && owner.Handle != IntPtr.Zero)
        {
            WpfWindowInterop.AttachOwner(dialog, owner);
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        dialog.ContentRendered += (_, _) =>
        {
            dialog.Activate();
            if (dialog.Handle != IntPtr.Zero)
            {
                NativeMethods.SetForegroundWindow(dialog.Handle);
            }
        };

        _ = dialog.ShowDialog();
        return dialog.Result;
    }

    private void ConfigureIcon(FormsMessageBoxIcon icon)
    {
        if (icon == FormsMessageBoxIcon.Error)
        {
            IconGlyph.Text = "!";
            IconBadge.SetResourceReference(BackgroundProperty, "DangerSoftBrush");
            IconGlyph.SetResourceReference(ForegroundProperty, "DangerBrush");
        }
        else if (icon == FormsMessageBoxIcon.Warning)
        {
            IconGlyph.Text = "!";
            IconBadge.SetResourceReference(BackgroundProperty, "WarningSoftBrush");
            IconGlyph.SetResourceReference(ForegroundProperty, "WarningBrush");
        }
        else if (icon == FormsMessageBoxIcon.Question)
        {
            IconGlyph.Text = "?";
            IconBadge.SetResourceReference(BackgroundProperty, "AccentSoftBrush");
            IconGlyph.SetResourceReference(ForegroundProperty, "AccentBrush");
        }
        else
        {
            IconGlyph.Text = "i";
            IconBadge.SetResourceReference(BackgroundProperty, "AccentSoftBrush");
            IconGlyph.SetResourceReference(ForegroundProperty, "AccentBrush");
        }
    }

    private void ConfigureMessageBody(string message, bool selectableMessage)
    {
        if (selectableMessage)
        {
            PlainMessageHost.Visibility = Visibility.Collapsed;
            SelectableMessageBox.Visibility = Visibility.Visible;
            SelectableMessageBox.Text = message;
        }
        else
        {
            PlainMessageHost.Visibility = Visibility.Visible;
            SelectableMessageBox.Visibility = Visibility.Collapsed;
            PlainMessageText.Text = message;
        }
    }

    private FormsDialogResult ConfigureButtons(
        FormsMessageBoxButtons buttons,
        Func<string, string> localize,
        bool selectableMessage,
        AppFault? fault)
    {
        if (selectableMessage)
        {
            copyDetailsButton = new WpfButton
            {
                Content = localize("Copy details"),
                MinWidth = 84,
                Margin = new Thickness(0, 0, 6, 0)
            };
            copyDetailsButton.Click += (_, _) => CopyMessageToClipboard(localize);
            UtilityButtonsPanel.Children.Add(copyDetailsButton);
        }

        if (fault is not null)
        {
            var exportButton = new WpfButton
            {
                Content = localize("Export diagnostics"),
                MinWidth = 96
            };
            exportButton.Click += async (_, _) =>
                await DiagnosticExportDialog.ExportAsync(WpfWindowInterop.AsWin32Window(this), fault);
            UtilityButtonsPanel.Children.Add(exportButton);
        }

        IReadOnlyList<(string Text, FormsDialogResult DialogResult, bool Accent)> defs = GetButtonDefinitions(buttons);
        FormsDialogResult resolvedCancel = FormsDialogResult.Cancel;

        for (int i = 0; i < defs.Count; i++)
        {
            (string text, FormsDialogResult result, bool accent) = defs[i];
            var button = new WpfButton
            {
                Content = localize(text),
                MinWidth = 74,
                Margin = i > 0 ? new Thickness(6, 0, 0, 0) : new Thickness(0),
                IsDefault = accent
            };
            if (accent)
            {
                button.SetResourceReference(StyleProperty, "AccentButtonStyle");
            }

            if (result is FormsDialogResult.Cancel or FormsDialogResult.No)
            {
                resolvedCancel = result;
            }

            button.Click += (_, _) =>
            {
                Result = result;
                DialogResult = true;
                Close();
            };
            PrimaryButtonsPanel.Children.Add(button);
        }

        if (defs.Count == 1)
        {
            resolvedCancel = defs[0].DialogResult;
        }

        return resolvedCancel;
    }

    private void OnDialogPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Result = cancelResult;
            e.Handled = true;
            Close();
        }
    }

    private void CopyMessageToClipboard(Func<string, string> localize)
    {
        if (string.IsNullOrEmpty(DisplayedMessage))
        {
            return;
        }

        try
        {
            FormsClipboard.SetText(DisplayedMessage);
            if (copyDetailsButton is not null)
            {
                copyDetailsButton.Content = localize("Copied");
            }
        }
        catch (ExternalException)
        {
        }
    }

    private static IReadOnlyList<(string Text, FormsDialogResult DialogResult, bool Accent)> GetButtonDefinitions(
        FormsMessageBoxButtons buttons)
    {
        return buttons switch
        {
            FormsMessageBoxButtons.OKCancel =>
            [
                ("Cancel", FormsDialogResult.Cancel, false),
                ("OK", FormsDialogResult.OK, true)
            ],
            FormsMessageBoxButtons.YesNo =>
            [
                ("No", FormsDialogResult.No, false),
                ("Yes", FormsDialogResult.Yes, true)
            ],
            FormsMessageBoxButtons.YesNoCancel =>
            [
                ("Cancel", FormsDialogResult.Cancel, false),
                ("No", FormsDialogResult.No, false),
                ("Yes", FormsDialogResult.Yes, true)
            ],
            FormsMessageBoxButtons.RetryCancel =>
            [
                ("Cancel", FormsDialogResult.Cancel, false),
                ("Retry", FormsDialogResult.Retry, true)
            ],
            FormsMessageBoxButtons.AbortRetryIgnore =>
            [
                ("Abort", FormsDialogResult.Abort, false),
                ("Ignore", FormsDialogResult.Ignore, false),
                ("Retry", FormsDialogResult.Retry, true)
            ],
            _ =>
            [
                ("OK", FormsDialogResult.OK, true)
            ]
        };
    }
}
