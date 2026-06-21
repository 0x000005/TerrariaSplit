using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed class SettingsDialogHost : IDisposable
{
    private readonly AppSettings initialSettings;
    private readonly Func<RuntimePerformanceDiagnostics> runtimeDiagnosticsProvider;
    private readonly Func<RuntimeDebugSnapshot> runtimeDebugSnapshotProvider;
    private readonly Func<AppSettings, int> worldPoolCountProvider;
    private readonly ISettingsSnapshotFactory settingsSnapshots;
    private readonly Action<Action> dispatchToOwner;
    private readonly Action<AppSettings> appliedCallback;
    private readonly Action<SettingsDialogResult> closedCallback;
    private readonly Action windowHandleChangedCallback;
    private readonly Rectangle ownerBounds;
    private readonly object sync = new();
    private Thread? thread;
    private SettingsForm? form;
    private IntPtr formHandle;
    private IntPtr activeChildDialogHandle;
    private bool started;
    private bool disposed;
    private bool applyInProgress;

    public SettingsDialogHost(
        AppSettings initialSettings,
        Func<RuntimePerformanceDiagnostics> runtimeDiagnosticsProvider,
        Func<RuntimeDebugSnapshot> runtimeDebugSnapshotProvider,
        Func<AppSettings, int> worldPoolCountProvider,
        ISettingsSnapshotFactory settingsSnapshots,
        Action<Action> dispatchToOwner,
        Action<AppSettings> appliedCallback,
        Action<SettingsDialogResult> closedCallback,
        Action windowHandleChangedCallback,
        Rectangle ownerBounds)
    {
        this.settingsSnapshots = settingsSnapshots;
        this.initialSettings = settingsSnapshots.CreateSnapshot(initialSettings);
        this.runtimeDiagnosticsProvider = runtimeDiagnosticsProvider;
        this.runtimeDebugSnapshotProvider = runtimeDebugSnapshotProvider;
        this.worldPoolCountProvider = worldPoolCountProvider;
        this.dispatchToOwner = dispatchToOwner;
        this.appliedCallback = appliedCallback;
        this.closedCallback = closedCallback;
        this.windowHandleChangedCallback = windowHandleChangedCallback;
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

    public IntPtr ChildDialogWindowHandle
    {
        get
        {
            lock (sync)
            {
                return activeChildDialogHandle;
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

    public void Dispose()
    {
        Thread? currentThread;
        SettingsForm? currentForm;
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            currentThread = thread;
            currentForm = form;
            formHandle = IntPtr.Zero;
            activeChildDialogHandle = IntPtr.Zero;
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

        if (currentThread is not null &&
            currentThread.IsAlive &&
            currentThread.ManagedThreadId != Environment.CurrentManagedThreadId)
        {
            currentThread.Join(1000);
        }
    }

    private void ThreadEntry()
    {
        SettingsDialogResult result;
        using var dialog = new SettingsForm(
            initialSettings,
            runtimeDiagnosticsProvider,
            runtimeDebugSnapshotProvider,
            worldPoolCountProvider,
            modalHandleChanged: UpdateActiveChildDialogHandle,
            settingsSnapshots: settingsSnapshots);
        dialog.HandleCreated += (_, _) =>
        {
            lock (sync)
            {
                formHandle = dialog.Handle;
            }

            DispatchToOwner(windowHandleChangedCallback);
        };
        dialog.HandleDestroyed += (_, _) =>
        {
            lock (sync)
            {
                formHandle = IntPtr.Zero;
                activeChildDialogHandle = IntPtr.Zero;
            }

            DispatchToOwner(windowHandleChangedCallback);
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

        lock (sync)
        {
            form = null;
            formHandle = IntPtr.Zero;
            activeChildDialogHandle = IntPtr.Zero;
            thread = null;
        }

        DispatchToOwner(() => closedCallback(result));
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

    private void UpdateActiveChildDialogHandle(IntPtr handle)
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            activeChildDialogHandle = handle;
        }

        DispatchToOwner(windowHandleChangedCallback);
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
