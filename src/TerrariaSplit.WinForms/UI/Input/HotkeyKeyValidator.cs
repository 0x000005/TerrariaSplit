using System.Windows.Forms;

namespace TerrariaSplit.UI.Input;

internal static class HotkeyKeyValidator
{
    private static readonly HashSet<Keys> BlockedKeyCodes = new()
    {
        Keys.ControlKey,
        Keys.ShiftKey,
        Keys.Menu,
        Keys.LControlKey,
        Keys.RControlKey,
        Keys.LShiftKey,
        Keys.RShiftKey,
        Keys.LMenu,
        Keys.RMenu,
        Keys.LWin,
        Keys.RWin,
        Keys.CapsLock,
        Keys.NumLock,
        Keys.Scroll,
        Keys.ProcessKey,
        Keys.Packet,
        Keys.KanaMode,
        Keys.JunjaMode,
        Keys.FinalMode,
        Keys.HanjaMode,
        Keys.IMEAccept,
        Keys.IMEConvert,
        Keys.IMEModeChange,
        Keys.IMENonconvert
    };

    public static bool IsAllowed(Keys keys)
    {
        return TryNormalize(keys, out _);
    }

    public static bool TryNormalize(Keys keys, out Keys normalized)
    {
        Keys modifiers = NormalizeModifiers(keys & Keys.Modifiers);
        Keys keyCode = keys & Keys.KeyCode;
        if (keyCode == Keys.None || BlockedKeyCodes.Contains(keyCode) || (uint)keyCode > byte.MaxValue)
        {
            normalized = Keys.None;
            return false;
        }

        normalized = modifiers | keyCode;
        return true;
    }

    public static string Format(Keys keys)
    {
        if (!TryNormalize(keys, out Keys normalized))
        {
            return keys.ToString();
        }

        var parts = new List<string>(4);
        if ((normalized & Keys.Control) == Keys.Control)
        {
            parts.Add("Ctrl");
        }

        if ((normalized & Keys.Alt) == Keys.Alt)
        {
            parts.Add("Alt");
        }

        if ((normalized & Keys.Shift) == Keys.Shift)
        {
            parts.Add("Shift");
        }

        parts.Add((normalized & Keys.KeyCode).ToString());
        return string.Join(" + ", parts);
    }

    private static Keys NormalizeModifiers(Keys modifiers)
    {
        Keys normalized = Keys.None;
        if ((modifiers & Keys.Control) == Keys.Control)
        {
            normalized |= Keys.Control;
        }

        if ((modifiers & Keys.Alt) == Keys.Alt)
        {
            normalized |= Keys.Alt;
        }

        if ((modifiers & Keys.Shift) == Keys.Shift)
        {
            normalized |= Keys.Shift;
        }

        return normalized;
    }
}
