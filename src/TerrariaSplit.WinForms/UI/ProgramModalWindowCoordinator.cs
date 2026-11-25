using System.Windows;
using System.Windows.Forms;
using TerrariaSplit.UI.Wpf.Interop;

namespace TerrariaSplit.UI;

internal sealed class ProgramModalWindowCoordinator
{
    private readonly Form mainWindow;
    private readonly WindowLayerController layerController;

    public ProgramModalWindowCoordinator(
        Form mainWindow,
        Action<bool> applyTimerInteractionBlocked,
        Func<IntPtr> getTimerWindowHandle)
    {
        this.mainWindow = mainWindow;
        layerController = new WindowLayerController(
            mainWindow,
            applyTimerInteractionBlocked,
            getTimerWindowHandle);
    }

    public bool AlwaysOnTop => layerController.AlwaysOnTop;

    public bool HasModalWindow => layerController.HasModalWindow;

    public void SetAlwaysOnTop(bool topMost)
    {
        layerController.SetAlwaysOnTop(topMost);
    }

    public void ApplyWindowState()
    {
        layerController.ApplyWindowState();
    }

    public IDisposable DeferWindowStateUpdates()
    {
        return layerController.DeferWindowStateUpdates();
    }

    public bool ActivateCurrentModal()
    {
        return layerController.ActivateCurrentModal();
    }

    public bool TryRedirectMainWindowInput(ContextMenuStrip? contextMenu = null)
    {
        if (!HasModalWindow)
        {
            return false;
        }

        contextMenu?.Close();
        layerController.ActivateCurrentModal();
        return true;
    }

    public void SyncMainWindowGroup(IntPtr activatedHandle)
    {
        layerController.SyncMainWindowGroup(activatedHandle);
    }

    public IDisposable RegisterModalWindow(Func<IntPtr> getWindowHandle, ModalWindowOptions options = default)
    {
        return layerController.RegisterModalWindow(getWindowHandle, options);
    }

    public IDisposable RegisterModalForm(Form form, ModalWindowOptions options = default)
    {
        if (!form.IsHandleCreated)
        {
            _ = form.Handle;
        }

        return RegisterModalWindow(
            () => !form.IsDisposed && form.IsHandleCreated ? form.Handle : IntPtr.Zero,
            options);
    }

    public IDisposable RegisterModalWpfWindow(Window window, ModalWindowOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(window);
        _ = WpfWindowInterop.EnsureHandle(window);
        return RegisterModalWindow(
            () => WpfWindowInterop.GetHandle(window),
            options);
    }

    public bool? ShowWpfDialog(Window window, ModalWindowOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(window);
        WpfWindowInterop.AttachOwner(window, mainWindow);
        using IDisposable modalWindow = RegisterModalWpfWindow(window, options);
        EventHandler? deactivatedHandler = null;
        if (options.KeepForeground)
        {
            deactivatedHandler = (_, _) => QueueCurrentModalActivation(window);
            window.Deactivated += deactivatedHandler;
        }

        try
        {
            return window.ShowDialog();
        }
        finally
        {
            if (deactivatedHandler is not null)
            {
                window.Deactivated -= deactivatedHandler;
            }
        }
    }

    public DialogResult ShowDialog(Form form, ModalWindowOptions options = default)
    {
        using IDisposable modalWindow = RegisterModalForm(form, options);
        EventHandler? deactivateHandler = null;
        if (options.KeepForeground)
        {
            deactivateHandler = (_, _) => QueueCurrentModalActivation(form);
            form.Deactivate += deactivateHandler;
        }

        try
        {
            return form.ShowDialog(mainWindow);
        }
        finally
        {
            if (deactivateHandler is not null)
            {
                form.Deactivate -= deactivateHandler;
            }
        }
    }

    private void QueueCurrentModalActivation(Form form)
    {
        if (form.IsDisposed || !form.Visible)
        {
            return;
        }

        try
        {
            form.BeginInvoke(new Action(() =>
            {
                if (!form.IsDisposed && form.Visible && form.Enabled)
                {
                    layerController.ActivateCurrentModal();
                }
            }));
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void QueueCurrentModalActivation(Window window)
    {
        if (!window.IsVisible || window.Dispatcher.HasShutdownStarted)
        {
            return;
        }

        try
        {
            _ = window.Dispatcher.BeginInvoke(new Action(() =>
            {
                IntPtr handle = WpfWindowInterop.GetHandle(window);
                if (window.IsVisible && window.IsEnabled && handle != IntPtr.Zero && NativeMethods.IsWindowEnabled(handle))
                {
                    layerController.ActivateCurrentModal();
                }
            }));
        }
        catch (InvalidOperationException)
        {
        }
    }
}
