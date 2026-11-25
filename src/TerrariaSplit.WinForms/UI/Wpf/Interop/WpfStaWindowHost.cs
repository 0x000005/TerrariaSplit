using System.Windows;
using System.Windows.Threading;

namespace TerrariaSplit.UI.Wpf.Interop;

internal sealed class WpfStaWindowHost<TWindow> : IDisposable
    where TWindow : Window
{
    private static readonly TimeSpan DisposeCloseTimeout = TimeSpan.FromSeconds(5);

    private readonly object sync = new();
    private readonly string threadName;
    private readonly Func<TWindow> createWindow;
    private readonly Action<Action> dispatchToOwner;
    private readonly Action<TWindow>? configureWindow;
    private readonly Action<TWindow>? closedCallback;

    private Thread? thread;
    private Dispatcher? dispatcher;
    private TWindow? window;
    private IntPtr windowHandle;
    private bool started;
    private bool disposed;
    private bool suppressClosedCallback;

    public WpfStaWindowHost(
        string threadName,
        Func<TWindow> createWindow,
        Action<Action> dispatchToOwner,
        Action<TWindow>? configureWindow = null,
        Action<TWindow>? closedCallback = null)
    {
        this.threadName = threadName;
        this.createWindow = createWindow;
        this.dispatchToOwner = dispatchToOwner;
        this.configureWindow = configureWindow;
        this.closedCallback = closedCallback;
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
                Name = threadName
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

    public void PostToWindow(Action<TWindow> action)
    {
        Dispatcher? currentDispatcher;
        TWindow? currentWindow;
        lock (sync)
        {
            currentDispatcher = dispatcher;
            currentWindow = window;
        }

        if (currentDispatcher is null || currentWindow is null || currentDispatcher.HasShutdownStarted)
        {
            return;
        }

        try
        {
            _ = currentDispatcher.BeginInvoke(new Action(() =>
            {
                if (currentWindow.IsLoaded || !currentDispatcher.HasShutdownStarted)
                {
                    action(currentWindow);
                }
            }));
        }
        catch (InvalidOperationException)
        {
        }
    }

    public void DispatchToOwner(Action action)
    {
        try
        {
            dispatchToOwner(action);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    public void Dispose()
    {
        Dispatcher? currentDispatcher;
        TWindow? currentWindow;
        Thread? currentThread;
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            suppressClosedCallback = true;
            currentDispatcher = dispatcher;
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

        TWindow createdWindow = createWindow();
        createdWindow.SourceInitialized += (_, _) =>
        {
            IntPtr handle = WpfWindowInterop.GetHandle(createdWindow);
            lock (sync)
            {
                windowHandle = handle;
            }
        };
        createdWindow.Closed += (_, _) =>
        {
            currentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
        };

        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            dispatcher = currentDispatcher;
            window = createdWindow;
        }

        configureWindow?.Invoke(createdWindow);
        createdWindow.Show();
        Dispatcher.Run();

        bool notifyClosed;
        lock (sync)
        {
            window = null;
            dispatcher = null;
            windowHandle = IntPtr.Zero;
            thread = null;
            notifyClosed = !suppressClosedCallback;
        }

        if (notifyClosed && closedCallback is not null)
        {
            DispatchToOwner(() => closedCallback(createdWindow));
        }
    }
}
