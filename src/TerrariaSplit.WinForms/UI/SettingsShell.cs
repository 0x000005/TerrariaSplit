using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed class SettingsShell : IDisposable
{
    private readonly Func<AppSettings> getSettings;
    private readonly ISettingsRepository settingsRepository;
    private readonly ISettingsSnapshotFactory settingsSnapshots;
    private readonly Action<Action> dispatch;
    private readonly Action<AppSettings> applySettings;
    private readonly Action clearPendingMenuActions;
    private readonly Action disposeHotkeys;
    private readonly Action registerHotkeys;
    private readonly Func<bool> isMainHandleCreated;
    private readonly Func<Rectangle> getOwnerBounds;
    private readonly Action<PreparedApplicationUpdate> restartForUpdate;
    private SettingsDialogHost? dialogHost;
    private bool isOpen;

    public SettingsShell(
        Func<AppSettings> getSettings,
        ISettingsRepository settingsRepository,
        ISettingsSnapshotFactory settingsSnapshots,
        Action<Action> dispatch,
        Action<AppSettings> applySettings,
        Action clearPendingMenuActions,
        Action disposeHotkeys,
        Action registerHotkeys,
        Func<bool> isMainHandleCreated,
        Func<Rectangle> getOwnerBounds,
        Action<PreparedApplicationUpdate> restartForUpdate)
    {
        this.getSettings = getSettings;
        this.settingsRepository = settingsRepository;
        this.settingsSnapshots = settingsSnapshots;
        this.dispatch = dispatch;
        this.applySettings = applySettings;
        this.clearPendingMenuActions = clearPendingMenuActions;
        this.disposeHotkeys = disposeHotkeys;
        this.registerHotkeys = registerHotkeys;
        this.isMainHandleCreated = isMainHandleCreated;
        this.getOwnerBounds = getOwnerBounds;
        this.restartForUpdate = restartForUpdate;
    }

    public bool IsOpen => isOpen;

    public void Open()
    {
        if (isOpen)
        {
            dialogHost?.Activate();
            return;
        }

        isOpen = true;
        disposeHotkeys();
        clearPendingMenuActions();
        dialogHost = new SettingsDialogHost(
            getSettings(),
            settingsSnapshots,
            dispatch,
            applySettings,
            Complete,
            (applied, update) =>
            {
                applySettings(applied);
                restartForUpdate(update);
            },
            getOwnerBounds());
        dialogHost.Show();
    }

    public void SwitchSettingsFile(string path)
    {
        if (string.Equals(
                Path.GetFullPath(path),
                Path.GetFullPath(settingsRepository.SettingsPath),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AppSettings nextSettings = settingsRepository.Load(path);
        applySettings(nextSettings);
    }

    public void Dispose()
    {
        dialogHost?.Dispose();
        dialogHost = null;
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
