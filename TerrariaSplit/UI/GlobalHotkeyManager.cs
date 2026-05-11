using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class GlobalHotkeyManager : IDisposable
{
    public const int HotkeyMessage = 0x0312;

    private const int IdBase = 0x54530000;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;

    private readonly Dictionary<int, TimerHotkeyAction> actionsById = new();
    private IntPtr handle;

    public IReadOnlyList<HotkeyRegistrationWarning> RegisterConfiguredHotkeys(IntPtr windowHandle, AppSettings settings)
    {
        UnregisterAll();
        handle = windowHandle;

        var warnings = new List<HotkeyRegistrationWarning>();
        HashSet<HotkeyChord> registeredChords = new();
        RegisterAction(TimerHotkeyAction.PauseResume, settings.PauseResumeKeys, registeredChords, warnings);
        RegisterAction(TimerHotkeyAction.Reset, settings.ResetKeys, registeredChords, warnings);
        RegisterAction(TimerHotkeyAction.MouseClickThrough, settings.MouseClickThroughKeys, registeredChords, warnings);
        RegisterAction(TimerHotkeyAction.CreateWorld, settings.CreateWorldKeys, registeredChords, warnings);
        return warnings;
    }

    public bool TryGetAction(Message message, out TimerHotkeyAction action)
    {
        action = default;
        return message.Msg == HotkeyMessage &&
            actionsById.TryGetValue(message.WParam.ToInt32(), out action);
    }

    public void Dispose()
    {
        UnregisterAll();
    }

    private void RegisterAction(
        TimerHotkeyAction action,
        Keys keys,
        HashSet<HotkeyChord> registeredChords,
        List<HotkeyRegistrationWarning> warnings)
    {
        if (!TryCreateChord(keys, out HotkeyChord chord))
        {
            AppLogger.Info($"Ignored invalid hotkey for {action}: {keys}.");
            warnings.Add(new HotkeyRegistrationWarning(
                action,
                keys,
                HotkeyRegistrationWarningKind.Invalid,
                "Invalid hotkey."));
            return;
        }

        if (!registeredChords.Add(chord))
        {
            AppLogger.Info($"Ignored duplicate hotkey for {action}: {keys}.");
            warnings.Add(new HotkeyRegistrationWarning(
                action,
                keys,
                HotkeyRegistrationWarningKind.Duplicate,
                "Duplicate hotkey."));
            return;
        }

        int id = IdBase + (int)action;
        uint modifiers = chord.Modifiers | ModNoRepeat;
        if (NativeMethods.RegisterHotKey(handle, id, modifiers, chord.VirtualKey))
        {
            actionsById[id] = action;
            return;
        }

        int error = Marshal.GetLastWin32Error();
        string detail = new Win32Exception(error).Message;
        AppLogger.Info($"Failed to register hotkey for {action}: {keys}. {detail}");
        warnings.Add(new HotkeyRegistrationWarning(
            action,
            keys,
            HotkeyRegistrationWarningKind.SystemRegistrationFailed,
            detail));
    }

    private void UnregisterAll()
    {
        if (handle == IntPtr.Zero)
        {
            actionsById.Clear();
            return;
        }

        foreach (int id in actionsById.Keys.ToArray())
        {
            NativeMethods.UnregisterHotKey(handle, id);
        }

        actionsById.Clear();
        handle = IntPtr.Zero;
    }

    private static bool TryCreateChord(Keys keys, out HotkeyChord chord)
    {
        if (!HotkeyKeyValidator.TryNormalize(keys, out Keys normalizedKeys))
        {
            chord = default;
            return false;
        }

        uint modifiers = 0;
        if ((normalizedKeys & Keys.Alt) == Keys.Alt)
        {
            modifiers |= ModAlt;
        }

        if ((normalizedKeys & Keys.Control) == Keys.Control)
        {
            modifiers |= ModControl;
        }

        if ((normalizedKeys & Keys.Shift) == Keys.Shift)
        {
            modifiers |= ModShift;
        }

        Keys keyCode = normalizedKeys & Keys.KeyCode;
        uint virtualKey = (uint)keyCode;
        chord = new HotkeyChord(modifiers, virtualKey);
        return true;
    }

    private readonly record struct HotkeyChord(uint Modifiers, uint VirtualKey);
}

internal enum HotkeyRegistrationWarningKind
{
    Invalid,
    Duplicate,
    SystemRegistrationFailed
}

internal readonly record struct HotkeyRegistrationWarning(
    TimerHotkeyAction Action,
    Keys Keys,
    HotkeyRegistrationWarningKind Kind,
    string Detail);
