using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class SettingsHotkeyTextBox : TextBox
{
    public Keys Hotkey { get; private set; } = Keys.None;

    public void SetHotkey(Keys hotkey)
    {
        Hotkey = HotkeyKeyValidator.IsAllowed(hotkey) ? hotkey : Keys.F12;
        Text = Hotkey.ToString();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        Keys key = e.KeyCode;
        if (!HotkeyKeyValidator.IsAllowed(key))
        {
            e.SuppressKeyPress = true;
            return;
        }

        if (key != Keys.None)
        {
            SetHotkey(key);
        }

        e.SuppressKeyPress = true;
    }
}
