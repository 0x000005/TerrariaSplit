using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed class SettingsShell : IDisposable
{
    private readonly Func<AppSettings> getSettings;
    private readonly Func<RuntimePerformanceDiagnostics> getRuntimeDiagnostics;
    private readonly Func<RuntimeDebugSnapshot> getRuntimeDebugSnapshot;
    private readonly Func<AppSettings, int> getWorldPoolCount;
    private readonly Action<Action> dispatch;
    private readonly Action<AppSettings> applySettings;
    private readonly Action clearPendingMenuActions;
    private readonly Action disposeHotkeys;
    private readonly Action registerHotkeys;
    private readonly Func<bool> isMainHandleCreated;
    private readonly ProgramModalWindowCoordinator modalWindows;
    private readonly Func<Rectangle> getOwnerBounds;
    private SettingsDialogHost? dialogHost;
    private IDisposable? modalRegistration;
    private IDisposable? childModalRegistration;
    private bool isOpen;

    public SettingsShell(
        Func<AppSettings> getSettings,
        Func<RuntimePerformanceDiagnostics> getRuntimeDiagnostics,
        Func<RuntimeDebugSnapshot> getRuntimeDebugSnapshot,
        Func<AppSettings, int> getWorldPoolCount,
        Action<Action> dispatch,
        Action<AppSettings> applySettings,
        Action clearPendingMenuActions,
        Action disposeHotkeys,
        Action registerHotkeys,
        Func<bool> isMainHandleCreated,
        ProgramModalWindowCoordinator modalWindows,
        Func<Rectangle> getOwnerBounds)
    {
        this.getSettings = getSettings;
        this.getRuntimeDiagnostics = getRuntimeDiagnostics;
        this.getRuntimeDebugSnapshot = getRuntimeDebugSnapshot;
        this.getWorldPoolCount = getWorldPoolCount;
        this.dispatch = dispatch;
        this.applySettings = applySettings;
        this.clearPendingMenuActions = clearPendingMenuActions;
        this.disposeHotkeys = disposeHotkeys;
        this.registerHotkeys = registerHotkeys;
        this.isMainHandleCreated = isMainHandleCreated;
        this.modalWindows = modalWindows;
        this.getOwnerBounds = getOwnerBounds;
    }

    public bool IsOpen => isOpen;

    public void Open()
    {
        if (isOpen)
        {
            modalWindows.ActivateCurrentModal();
            return;
        }

        isOpen = true;
        childModalRegistration?.Dispose();
        childModalRegistration = null;
        modalRegistration?.Dispose();
        disposeHotkeys();
        clearPendingMenuActions();
        dialogHost = new SettingsDialogHost(
            getSettings(),
            getRuntimeDiagnostics,
            getRuntimeDebugSnapshot,
            getWorldPoolCount,
            dispatch,
            applySettings,
            Complete,
            modalWindows.ApplyWindowState,
            getOwnerBounds());
        modalRegistration = modalWindows.RegisterModalWindow(
            () => dialogHost?.WindowHandle ?? IntPtr.Zero);
        childModalRegistration = modalWindows.RegisterModalWindow(
            () => dialogHost?.ChildDialogWindowHandle ?? IntPtr.Zero);
        dialogHost.Show();
    }

    public void SwitchSettingsFile(string path)
    {
        if (string.Equals(
                Path.GetFullPath(path),
                Path.GetFullPath(AppSettingsStore.SettingsPath),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AppSettings nextSettings = AppSettingsStore.Load(path);
        applySettings(nextSettings);
    }

    public void Dispose()
    {
        dialogHost?.Dispose();
        dialogHost = null;
        childModalRegistration?.Dispose();
        childModalRegistration = null;
        modalRegistration?.Dispose();
        modalRegistration = null;
        isOpen = false;
    }

    private void Complete(SettingsDialogResult result)
    {
        if (result.DialogResult == DialogResult.OK)
        {
            applySettings(result.Result);
        }

        Dispose();
        if (isMainHandleCreated())
        {
            registerHotkeys();
        }
    }
}
