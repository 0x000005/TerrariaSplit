using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;
using TerrariaSplit.UI.Wpf;
using TerrariaSplit.UI.Wpf.Interop;
using TerrariaSplit.UI.Wpf.Windows;

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
    private Dispatcher? windowDispatcher;
    private SettingsWindow? window;
    private IntPtr windowHandle;
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
                return windowHandle;
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
        Dispatcher? currentDispatcher;
        SettingsWindow? currentWindow;
        Thread? currentThread;
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            suppressClosedCallback = true;
            currentDispatcher = windowDispatcher;
            currentWindow = window;
            currentThread = thread;
            windowHandle = IntPtr.Zero;
        }

        if (currentDispatcher is not null && !currentDispatcher.HasShutdownStarted)
        {
            try
            {
                _ = currentDispatcher.BeginInvoke(new Action(() =>
                {
                    currentWindow?.Close();
                    currentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                }));
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
        WpfBootstrap.EnsureInitialized();
        Dispatcher currentDispatcher = Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(currentDispatcher));

        var dialog = new SettingsWindow(
            initialSettings,
            settingsSnapshots: settingsSnapshots,
            canSaveSettings: canSaveSettings);

        dialog.SourceInitialized += (_, _) =>
        {
            IntPtr handle = WpfWindowInterop.GetHandle(dialog);
            lock (sync)
            {
                windowHandle = handle;
            }
        };

        lock (sync)
        {
            if (disposed)
            {
                dialog.Dispose();
                return;
            }

            windowDispatcher = currentDispatcher;
            window = dialog;
        }

        PositionNearOwner(dialog);

        dialog.Applied += (_, _) => ApplySettingsFromDialog(dialog);
        dialog.UpdateRestartRequested += update =>
        {
            AppSettings applied = settingsSnapshots.CreateSnapshot(dialog.Result);
            DispatchToOwner(() => updateRestartCallback(applied, update));
        };

        DialogResult dialogResult = DialogResult.Cancel;
        AppSettings resultSettings = settingsSnapshots.CreateSnapshot(dialog.Result);
        dialog.Closed += (_, _) =>
        {
            dialogResult = dialog.CloseResult == DialogResult.None
                ? DialogResult.Cancel
                : dialog.CloseResult;
            resultSettings = settingsSnapshots.CreateSnapshot(dialog.Result);
            lock (sync)
            {
                windowHandle = IntPtr.Zero;
            }

            currentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
        };

        dialog.Show();
        Dispatcher.Run();
        dialog.Dispose();

        var result = new SettingsDialogResult(dialogResult, resultSettings);

        bool notifyClosed;
        lock (sync)
        {
            window = null;
            windowDispatcher = null;
            windowHandle = IntPtr.Zero;
            thread = null;
            notifyClosed = !suppressClosedCallback;
        }

        if (notifyClosed)
        {
            DispatchToOwner(() => closedCallback(result));
        }
    }

    private void PositionNearOwner(SettingsWindow dialog)
    {
        Rectangle targetPixels = ownerBounds.Width > 0 && ownerBounds.Height > 0
            ? ownerBounds
            : Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Rectangle screenPixels = Screen.FromRectangle(targetPixels).WorkingArea;

        DpiScale dpi = VisualTreeHelper.GetDpi(dialog);
        double scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1d;
        double scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1d;

        double targetLeft = targetPixels.Left / scaleX;
        double targetTop = targetPixels.Top / scaleY;
        double targetWidth = targetPixels.Width / scaleX;
        double targetHeight = targetPixels.Height / scaleY;

        double screenLeft = screenPixels.Left / scaleX;
        double screenTop = screenPixels.Top / scaleY;
        double screenRight = screenPixels.Right / scaleX;
        double screenBottom = screenPixels.Bottom / scaleY;

        double left = targetLeft + (targetWidth - dialog.Width) / 2d;
        double top = targetTop + (targetHeight - dialog.Height) / 2d;

        dialog.WindowStartupLocation = WindowStartupLocation.Manual;
        dialog.Left = Math.Clamp(left, screenLeft, Math.Max(screenLeft, screenRight - dialog.Width));
        dialog.Top = Math.Clamp(top, screenTop, Math.Max(screenTop, screenBottom - dialog.Height));
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

    private void ApplySettingsFromDialog(SettingsWindow dialog)
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
