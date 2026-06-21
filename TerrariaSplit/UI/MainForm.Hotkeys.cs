using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed partial class MainForm : Form
{
    private void RegisterConfiguredHotkeys()
    {
        if (!registerGlobalHotkeys)
        {
            hotkeyManager.Dispose();
            lastHotkeyWarningText = null;
            return;
        }

        IReadOnlyList<HotkeyRegistrationWarning> warnings = hotkeyManager.RegisterConfiguredHotkeys(Handle, settings);
        ShowHotkeyRegistrationWarnings(warnings);
    }

    private void ShowHotkeyRegistrationWarnings(IReadOnlyList<HotkeyRegistrationWarning> warnings)
    {
        if (warnings.Count == 0)
        {
            lastHotkeyWarningText = null;
            return;
        }

        string warningText = string.Join(Environment.NewLine, warnings.Select(FormatHotkeyRegistrationWarning));
        if (string.Equals(warningText, lastHotkeyWarningText, StringComparison.Ordinal))
        {
            return;
        }

        lastHotkeyWarningText = warningText;
        string message = Localizer.Get("Some hotkeys could not be registered:", settings) +
            Environment.NewLine +
            warningText;
        ShowHotkeyWarning(message);
    }

    private void ShowHotkeyWarning(string message)
    {
        using var dialog = new HotkeyWarningDialog(
            Localizer.Get("Hotkey warning", settings),
            message);
        modalWindows.ShowDialog(dialog);
    }

    private string FormatHotkeyRegistrationWarning(HotkeyRegistrationWarning warning)
    {
        string actionName = Localizer.Get(GetHotkeyActionDisplayName(warning.Action), settings);
        return warning.Kind switch
        {
            HotkeyRegistrationWarningKind.Duplicate => string.Format(
                Localizer.Get("{0}: {1} is duplicated; only the first action using this key is active.", settings),
                actionName,
                HotkeyKeyValidator.Format(warning.Keys)),
            HotkeyRegistrationWarningKind.Invalid => string.Format(
                Localizer.Get("{0}: {1} is not allowed as a hotkey.", settings),
                actionName,
                HotkeyKeyValidator.Format(warning.Keys)),
            HotkeyRegistrationWarningKind.SystemRegistrationFailed => string.Format(
                Localizer.Get("{0}: {1} registration failed. It may be used by another program. ({2})", settings),
                actionName,
                HotkeyKeyValidator.Format(warning.Keys),
                warning.Detail),
            _ => $"{actionName}: {warning.Keys}"
        };
    }

    private static string GetHotkeyActionDisplayName(HotkeyAction action)
    {
        return action switch
        {
            HotkeyAction.PauseResume => "Pause / Resume",
            HotkeyAction.Reset => "Reset (Disabled in world)",
            HotkeyAction.MouseClickThrough => "Mouse passthrough",
            HotkeyAction.CreateWorld => "Create world (Disabled in world)",
            HotkeyAction.PracticeWorld => "Load world (Disabled in world)",
            _ => action.ToString()
        };
    }
}
