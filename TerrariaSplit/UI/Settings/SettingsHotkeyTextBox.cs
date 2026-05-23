using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class SettingsHotkeyTextBox : TextBox
{
    public Keys Hotkey { get; private set; } = Keys.None;

    public void SetHotkey(Keys hotkey)
    {
        Hotkey = HotkeyKeyValidator.TryNormalize(hotkey, out Keys normalizedHotkey)
            ? normalizedHotkey
            : Keys.F12;
        Text = HotkeyKeyValidator.Format(Hotkey);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        TryCaptureHotkey(e.KeyData);
        e.SuppressKeyPress = true;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (TryCaptureHotkey(keyData))
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private bool TryCaptureHotkey(Keys keyData)
    {
        if (!HotkeyKeyValidator.IsAllowed(keyData))
        {
            return false;
        }

        SetHotkey(keyData);
        return true;
    }
}
