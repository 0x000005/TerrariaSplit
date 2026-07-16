using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed class SettingsDialogHost : IDisposable
{
    private static readonly TimeSpan DisposeCloseTimeout = TimeSpan.FromSeconds(1);
    private readonly AppSettings initialSettings;
    private readonly ISettingsSnapshotFactory settingsSnapshots;
    private readonly Action<Action> dispatchToOwner;
    private readonly Func<bool> canSaveSettings;
    private readonly Action<AppSettings> appliedCallback;
    private readonly Action<SettingsDialogResult> closedCallback;
    private readonly Action<AppSettings, PreparedApplicationUpdate> updateRestartCallback;
    private readonly Rectangle ownerBounds;
    private readonly object sync = new();
    private Thread? thread;
    private SettingsForm? form;
    private IntPtr formHandle;
    private bool started;
    private bool disposed;
    private bool applyInProgress;
    private bool suppressClosedCallback;

    public SettingsDialogHost(
        AppSettings initialSettings,
        ISettingsSnapshotFactory settingsSnapshots,
        Action<Action> dispatchToOwner,
        Func<bool> canSaveSettings,
        Action<AppSettings> appliedCallback,
        Action<SettingsDialogResult> closedCallback,
        Action<AppSettings, PreparedApplicationUpdate> updateRestartCallback,
        Rectangle ownerBounds)
    {
        this.settingsSnapshots = settingsSnapshots;
        this.initialSettings = settingsSnapshots.CreateSnapshot(initialSettings);
        this.dispatchToOwner = dispatchToOwner;
        this.canSaveSettings = canSaveSettings;
        this.appliedCallback = appliedCallback;
        this.closedCallback = closedCallback;
        this.updateRestartCallback = updateRestartCallback;
        this.ownerBounds = ownerBounds;
    }

    public IntPtr WindowHandle
    {
        get
        {
            lock (sync)
            {
                return formHandle;
            }
        }
    }

    public void Show()
    {
        lock (sync)
        {
            if (started || disposed)
            {
                return;
            }

            started = true;
            thread = new Thread(ThreadEntry)
            {
                IsBackground = true,
                Name = "TerrariaSplit Settings UI"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
    }

    public void Activate()
    {
        IntPtr handle = WindowHandle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);
        NativeMethods.SetForegroundWindow(handle);
    }

    public void Dispose()
    {
        SettingsForm? currentForm;
        Thread? currentThread;
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            suppressClosedCallback = true;
            currentForm = form;
            currentThread = thread;
            formHandle = IntPtr.Zero;
        }

        if (currentForm is not null && !currentForm.IsDisposed && currentForm.IsHandleCreated)
        {
            try
            {
                currentForm.BeginInvoke(new Action(currentForm.Close));
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        if (currentThread is not null && currentThread != Thread.CurrentThread)
        {
            currentThread.Join(DisposeCloseTimeout);
        }
    }

    private void ThreadEntry()
    {
        SettingsDialogResult result;
        using var dialog = new SettingsForm(
            initialSettings,
            settingsSnapshots: settingsSnapshots,
            canSaveSettings: canSaveSettings);
        dialog.HandleCreated += (_, _) =>
        {
            lock (sync)
            {
                formHandle = dialog.Handle;
            }
        };
        dialog.HandleDestroyed += (_, _) =>
        {
            lock (sync)
            {
                formHandle = IntPtr.Zero;
            }
        };
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            form = dialog;
        }

        dialog.StartPosition = FormStartPosition.Manual;
        dialog.Location = ResolveStartLocation(dialog.Size);
        dialog.Applied += (_, _) => ApplySettingsFromDialog(dialog);
        dialog.UpdateRestartRequested += update =>
        {
            AppSettings applied = settingsSnapshots.CreateSnapshot(dialog.Result);
            DispatchToOwner(() => updateRestartCallback(applied, update));
        };

        DialogResult dialogResult = DialogResult.Cancel;
        AppSettings resultSettings = settingsSnapshots.CreateSnapshot(dialog.Result);
        dialog.FormClosed += (_, _) =>
        {
            dialogResult = dialog.DialogResult == DialogResult.None
                ? DialogResult.Cancel
                : dialog.DialogResult;
            resultSettings = settingsSnapshots.CreateSnapshot(dialog.Result);
        };

        System.Windows.Forms.Application.Run(dialog);
        result = new SettingsDialogResult(dialogResult, resultSettings);

        bool notifyClosed;
        lock (sync)
        {
            form = null;
            formHandle = IntPtr.Zero;
            thread = null;
            notifyClosed = !suppressClosedCallback;
        }

        if (notifyClosed)
        {
            DispatchToOwner(() => closedCallback(result));
        }
    }

    private Point ResolveStartLocation(Size dialogSize)
    {
        Rectangle targetArea = ownerBounds.Width > 0 && ownerBounds.Height > 0
            ? ownerBounds
            : Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Rectangle screenBounds = Screen.FromRectangle(targetArea).Bounds;
        int x = targetArea.Left + Math.Max(0, (targetArea.Width - dialogSize.Width) / 2);
        int y = targetArea.Top + Math.Max(0, (targetArea.Height - dialogSize.Height) / 2);
        y = Math.Max(screenBounds.Top, y);
        return new Point(x, y);
    }

    private void DispatchToOwner(Action action)
    {
        _ = TryDispatchToOwner(action);
    }

    private bool TryDispatchToOwner(Action action)
    {
        try
        {
            dispatchToOwner(action);
            return true;
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        return false;
    }

    private void ApplySettingsFromDialog(SettingsForm dialog)
    {
        lock (sync)
        {
            if (disposed || applyInProgress)
            {
                return;
            }

            applyInProgress = true;
        }

        AppSettings applied = settingsSnapshots.CreateSnapshot(dialog.Result);
        if (!TryDispatchToOwner(() =>
        {
            try
            {
                appliedCallback(applied);
            }
            finally
            {
                lock (sync)
                {
                    applyInProgress = false;
                }
            }
        }))
        {
            lock (sync)
            {
                applyInProgress = false;
            }
        }
    }

}

internal readonly record struct SettingsDialogResult(
    DialogResult DialogResult,
    AppSettings Result);
