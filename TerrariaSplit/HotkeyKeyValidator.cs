using System.Windows.Forms;

namespace TerrariaSplit;

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
        Keys keyCode = keys & Keys.KeyCode;
        if (keyCode == Keys.None || BlockedKeyCodes.Contains(keyCode) || (uint)keyCode > byte.MaxValue)
        {
            normalized = Keys.None;
            return false;
        }

        normalized = keyCode | (keys & Keys.Modifiers);
        return true;
    }
}
