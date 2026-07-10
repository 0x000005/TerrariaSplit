using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed class MainWindowModalInputRouter
{
    internal const int WmMouseActivate = 0x21;
    internal const int WmContextMenu = 0x7B;
    internal const int MaNoActivateAndEat = 4;

    private readonly ProgramModalWindowCoordinator modalWindows;
    private readonly Func<ContextMenuStrip?> getContextMenu;
    private readonly Action stopMainWindowInteraction;

    public MainWindowModalInputRouter(
        ProgramModalWindowCoordinator modalWindows,
        Func<ContextMenuStrip?> getContextMenu,
        Action stopMainWindowInteraction)
    {
        this.modalWindows = modalWindows;
        this.getContextMenu = getContextMenu;
        this.stopMainWindowInteraction = stopMainWindowInteraction;
    }

    public bool HasModalWindow => modalWindows.HasModalWindow;

    public bool TryRedirectFromMainInput()
    {
        if (!modalWindows.HasModalWindow)
        {
            return false;
        }

        stopMainWindowInteraction();
        modalWindows.TryRedirectMainWindowInput(getContextMenu());
        return true;
    }

    public bool TryHandleWindowMessage(ref Message message)
    {
        if (!modalWindows.HasModalWindow)
        {
            return false;
        }

        if (message.Msg == WmMouseActivate)
        {
            TryRedirectFromMainInput();
            message.Result = (IntPtr)MaNoActivateAndEat;
            return true;
        }

        if (message.Msg == WmContextMenu)
        {
            TryRedirectFromMainInput();
            message.Result = IntPtr.Zero;
            return true;
        }

        return false;
    }
}
