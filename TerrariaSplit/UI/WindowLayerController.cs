using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class WindowLayerController
{
    private readonly Form statusWindow;
    private readonly Action<bool> applyTimerInteractionBlocked;
    private readonly Func<IntPtr> getTimerWindowHandle;
    private readonly List<ModalWindowRegistration> modalWindows = new();
    private bool alwaysOnTop;
    private bool mainInteractionBlocked;

    public WindowLayerController(
        Form statusWindow,
        Action<bool> applyTimerInteractionBlocked,
        Func<IntPtr> getTimerWindowHandle)
    {
        this.statusWindow = statusWindow;
        this.applyTimerInteractionBlocked = applyTimerInteractionBlocked;
        this.getTimerWindowHandle = getTimerWindowHandle;
    }

    public bool AlwaysOnTop => alwaysOnTop;

    public bool HasModalWindow => modalWindows.Count > 0;

    public IDisposable RegisterModalWindow(Func<IntPtr> getWindowHandle, ModalWindowOptions options = default)
    {
        var registration = new ModalWindowRegistration(this, getWindowHandle, options);
        modalWindows.Add(registration);
        ApplyWindowState();
        return registration;
    }

    public void SetAlwaysOnTop(bool topMost)
    {
        alwaysOnTop = topMost;
        ApplyWindowState();
    }

    public void ApplyWindowState()
    {
        if (statusWindow.IsDisposed || statusWindow.Disposing)
        {
            return;
        }

        IntPtr[] mainWindowHandles = GetMainWindowHandles();
        ModalWindowState[] modalWindowsState = GetModalWindowStates(mainWindowHandles);
        IntPtr[] modalWindowHandles = modalWindowsState.Select(modalWindow => modalWindow.Handle).ToArray();
        IntPtr previousOwnerHandle = statusWindow.IsHandleCreated ? statusWindow.Handle : IntPtr.Zero;
        if (previousOwnerHandle != IntPtr.Zero)
        {
            foreach (IntPtr modalHandle in modalWindowHandles)
            {
                NativeMethods.SetWindowOwner(modalHandle, previousOwnerHandle);
                previousOwnerHandle = modalHandle;
            }
        }

        ApplyTopMostState(mainWindowHandles, modalWindowsState);
        ApplyInteractionState(modalWindowHandles);
    }

    public bool ActivateCurrentModal()
    {
        ModalWindowRegistration? registration = modalWindows.LastOrDefault();
        if (registration is null)
        {
            return false;
        }

        IntPtr modalHandle = registration.GetHandle();
        if (modalHandle == IntPtr.Zero)
        {
            return false;
        }

        ApplyWindowState();
        NativeMethods.ShowWindow(modalHandle, NativeMethods.SwRestore);
        NativeMethods.SetForegroundWindow(modalHandle);
        return true;
    }

    public bool RedirectMainWindowInputToModal()
    {
        if (!HasModalWindow)
        {
            return false;
        }

        ActivateCurrentModal();
        return true;
    }

    public void SyncMainWindowGroup(IntPtr activatedHandle)
    {
        if (activatedHandle == IntPtr.Zero ||
            statusWindow.IsDisposed ||
            statusWindow.Disposing)
        {
            return;
        }

        if (HasModalWindow)
        {
            ActivateCurrentModal();
            return;
        }

        IntPtr[] mainWindowHandles = GetMainWindowHandles();
        if (!mainWindowHandles.Contains(activatedHandle))
        {
            return;
        }

        ApplyTopMostState(mainWindowHandles, Array.Empty<ModalWindowState>());
        if (alwaysOnTop)
        {
            return;
        }

        WindowTopMostSync.PlaceBehind(
            activatedHandle,
            mainWindowHandles.Where(handle => handle != activatedHandle).ToArray());
    }

    private void ApplyTopMostState(IReadOnlyList<IntPtr> mainWindowHandles, IReadOnlyList<ModalWindowState> modalWindowsState)
    {
        if (mainWindowHandles.Count == 0 && modalWindowsState.Count == 0)
        {
            return;
        }

        WindowTopMostSync.Apply(alwaysOnTop, mainWindowHandles.ToArray());
        foreach (ModalWindowState modalWindow in modalWindowsState)
        {
            WindowTopMostSync.Apply(alwaysOnTop || modalWindow.Options.ForceTopMost, modalWindow.Handle);
        }

        if (modalWindowsState.Count > 0)
        {
            IntPtr currentModalHandle = modalWindowsState[^1].Handle;
            IntPtr[] lowerWindowHandles = mainWindowHandles
                .Concat(modalWindowsState.Take(modalWindowsState.Count - 1).Select(modalWindow => modalWindow.Handle))
                .ToArray();
            WindowTopMostSync.PlaceBehind(currentModalHandle, lowerWindowHandles);
        }
    }

    private void ApplyInteractionState(IReadOnlyList<IntPtr> modalWindowHandles)
    {
        bool blockMainInteraction = HasModalWindow;
        bool changed = mainInteractionBlocked != blockMainInteraction;
        mainInteractionBlocked = blockMainInteraction;
        bool enableMainWindows = !mainInteractionBlocked;
        IntPtr statusHandle = statusWindow.IsHandleCreated ? statusWindow.Handle : IntPtr.Zero;
        if (statusHandle != IntPtr.Zero)
        {
            NativeMethods.EnableWindow(statusHandle, enableMainWindows);
        }

        if (changed)
        {
            applyTimerInteractionBlocked(mainInteractionBlocked);
        }

        for (int i = 0; i < modalWindowHandles.Count; i++)
        {
            NativeMethods.EnableWindow(modalWindowHandles[i], i == modalWindowHandles.Count - 1);
        }
    }

    private IntPtr[] GetMainWindowHandles()
    {
        var handles = new List<IntPtr>(2);
        if (statusWindow.IsHandleCreated)
        {
            handles.Add(statusWindow.Handle);
        }

        IntPtr timerHandle = getTimerWindowHandle();
        if (timerHandle != IntPtr.Zero && !handles.Contains(timerHandle))
        {
            handles.Add(timerHandle);
        }

        return handles.ToArray();
    }

    private ModalWindowState[] GetModalWindowStates(IReadOnlyCollection<IntPtr> mainWindowHandles)
    {
        var states = new List<ModalWindowState>(modalWindows.Count);
        var handles = new List<IntPtr>(modalWindows.Count);
        foreach (ModalWindowRegistration registration in modalWindows)
        {
            IntPtr handle = registration.GetHandle();
            if (handle == IntPtr.Zero ||
                mainWindowHandles.Contains(handle) ||
                handles.Contains(handle))
            {
                continue;
            }

            handles.Add(handle);
            states.Add(new ModalWindowState(handle, registration.Options));
        }

        return states.ToArray();
    }

    private void RemoveModalWindow(ModalWindowRegistration registration)
    {
        if (!modalWindows.Remove(registration))
        {
            return;
        }

        ApplyWindowState();
    }

    private sealed class ModalWindowRegistration : IDisposable
    {
        private WindowLayerController? owner;
        private readonly Func<IntPtr> getWindowHandle;

        public ModalWindowRegistration(WindowLayerController owner, Func<IntPtr> getWindowHandle, ModalWindowOptions options)
        {
            this.owner = owner;
            this.getWindowHandle = getWindowHandle;
            Options = options;
        }

        public ModalWindowOptions Options { get; }

        public IntPtr GetHandle()
        {
            return owner is null ? IntPtr.Zero : getWindowHandle();
        }

        public void Dispose()
        {
            WindowLayerController? controller = owner;
            if (controller is null)
            {
                return;
            }

            owner = null;
            controller.RemoveModalWindow(this);
        }
    }

    private readonly record struct ModalWindowState(IntPtr Handle, ModalWindowOptions Options);
}
