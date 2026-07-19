using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed class HotkeyShell : IDisposable
{
    private readonly IHotkeyRegistrationManager hotkeys;
    private readonly Func<AppSettings> getSettings;
    private readonly Func<IntPtr> getWindowHandle;
    private readonly Func<bool> canUseWindowHandle;
    private readonly bool registerGlobalHotkeys;
    private readonly Action<string> showWarning;
    private string? lastWarningText;

    public HotkeyShell(
        IHotkeyRegistrationManager hotkeys,
        Func<AppSettings> getSettings,
        Func<IntPtr> getWindowHandle,
        Func<bool> canUseWindowHandle,
        bool registerGlobalHotkeys,
        Action<string> showWarning)
    {
        this.hotkeys = hotkeys;
        this.getSettings = getSettings;
        this.getWindowHandle = getWindowHandle;
        this.canUseWindowHandle = canUseWindowHandle;
        this.registerGlobalHotkeys = registerGlobalHotkeys;
        this.showWarning = showWarning;
    }

    public void Register()
    {
        if (!registerGlobalHotkeys || !canUseWindowHandle())
        {
            Unregister();
            return;
        }

        AppSettings settings = getSettings();
        IReadOnlyList<HotkeyRegistrationWarning> warnings = hotkeys.RegisterConfiguredHotkeys(getWindowHandle(), settings);
        ShowRegistrationWarnings(settings, warnings);
    }

    public void Unregister()
    {
        hotkeys.Dispose();
        lastWarningText = null;
    }

    public bool TryGetAction(Message message, out HotkeyAction action)
    {
        return hotkeys.TryGetAction(message, out action);
    }

    private void ShowRegistrationWarnings(AppSettings settings, IReadOnlyList<HotkeyRegistrationWarning> warnings)
    {
        if (warnings.Count == 0)
        {
            lastWarningText = null;
            return;
        }

        string warningText = string.Join(Environment.NewLine, warnings.Select(warning => FormatWarning(settings, warning)));
        if (string.Equals(warningText, lastWarningText, StringComparison.Ordinal))
        {
            return;
        }

        lastWarningText = warningText;
        string message = Localizer.Get("Some hotkeys could not be registered:", settings) +
            Environment.NewLine +
            warningText;
        showWarning(message);
    }

    private static string FormatWarning(AppSettings settings, HotkeyRegistrationWarning warning)
    {
        string actionName = Localizer.Get(GetActionDisplayName(warning.Action), settings);
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

    private static string GetActionDisplayName(HotkeyAction action)
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

    public void Dispose()
    {
        hotkeys.Dispose();
    }
}
