using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class SettingsHotkeyTextBox : TextBox
{
    public Keys Hotkey { get; private set; } = Keys.None;

    public event EventHandler? HotkeyCaptured;

    public void SetHotkey(Keys hotkey)
    {
        Hotkey = hotkey == Keys.None
            ? Keys.None
            : HotkeyKeyValidator.TryNormalize(hotkey, out Keys normalizedHotkey)
                ? normalizedHotkey
                : Keys.None;
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
        if ((keyData & Keys.KeyCode) == Keys.Escape &&
            (keyData & Keys.Modifiers) == Keys.None)
        {
            SetHotkey(Keys.None);
            HotkeyCaptured?.Invoke(this, EventArgs.Empty);
            return true;
        }

        if (!HotkeyKeyValidator.IsAllowed(keyData))
        {
            return false;
        }

        SetHotkey(keyData);
        HotkeyCaptured?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
