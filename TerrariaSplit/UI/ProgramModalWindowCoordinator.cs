using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class ProgramModalWindowCoordinator
{
    private readonly WindowLayerController layerController;

    public ProgramModalWindowCoordinator(
        Form mainWindow,
        Action<bool> applyTimerInteractionBlocked,
        Func<IntPtr> getTimerWindowHandle)
    {
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
            return form.ShowDialog();
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
}
