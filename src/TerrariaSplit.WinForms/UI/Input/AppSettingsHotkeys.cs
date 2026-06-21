using System.Windows.Forms;

namespace TerrariaSplit.UI.Input;

internal static class AppSettingsHotkeys
{
    public static Keys GetPauseResumeKeys(this AppSettings settings)
    {
        return ParseKey(settings.Hotkeys.PauseResumeKey, Keys.F12);
    }

    public static Keys GetResetKeys(this AppSettings settings)
    {
        return ParseKey(settings.Hotkeys.ResetKey, Keys.F6);
    }

    public static Keys GetMouseClickThroughKeys(this AppSettings settings)
    {
        return ParseKey(settings.Hotkeys.MouseClickThroughKey, Keys.F9);
    }

    public static Keys GetCreateWorldKeys(this AppSettings settings)
    {
        return ParseKey(settings.Hotkeys.CreateWorldKey, Keys.F7);
    }

    public static Keys GetPracticeWorldKeys(this AppSettings settings)
    {
        return ParseKey(settings.Hotkeys.PracticeWorldKey, Keys.F8);
    }

    private static Keys ParseKey(string? value, Keys fallback)
    {
        if (Enum.TryParse(value, ignoreCase: true, out Keys key) &&
            HotkeyKeyValidator.TryNormalize(key, out Keys normalizedKey))
        {
            return normalizedKey;
        }

        return fallback;
    }
}
