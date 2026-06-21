using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed partial class MainForm : Form
{
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.Style = OverlayWindowController.ComposeBorderlessStyle(parameters.Style);
            parameters.ExStyle |= WsExLayered;
            if (mouseClickThrough)
            {
                parameters.ExStyle |= WsExTransparent;
            }

            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        overlayWindowController.ApplyWindowStyle(mouseClickThrough);
        InitializeOverlayWindows();
        modalWindows.ApplyWindowState();
        if (!settingsShell.IsOpen)
        {
            hotkeyShell.Register();
        }

        Invalidate();
        QueueStatusOverlayRender();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (overlayWindowsInitialized)
        {
            ApplyOverlayLayout(overlayBoundsController.CurrentLayout);
        }

        worldPoolFillService.UpdateSettings(settings);
        QueueStatusOverlayRender();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        if (IsHandleCreated)
        {
            QueueMainWindowForegroundGroupSync(Handle);
        }
    }

    private void QueueMainWindowForegroundGroupSync(IntPtr activatedHandle)
    {
        if (activatedHandle == IntPtr.Zero || !CanDispatchToUiThread())
        {
            return;
        }

        try
        {
            BeginInvoke(new Action(() =>
            {
                if (CanDispatchToUiThread())
                {
                    modalWindows.SyncMainWindowGroup(activatedHandle);
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

    protected override void OnInvalidated(InvalidateEventArgs e)
    {
        base.OnInvalidated(e);
        statusOverlayContentDirty = true;
        QueueStatusOverlayRender();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        QueueStatusOverlayRender();
        NotifyStatusBoundsChanged();
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        QueueStatusOverlayRender();
        NotifyStatusBoundsChanged();
    }

    private void UpdateContextMenu()
    {
        contextMenuBuilder.Rebuild(
            contextMenu,
            settings,
            OpenStatistics,
            settingsShell.Open,
            TogglePyramidFilter,
            settingsShell.SwitchSettingsFile,
            Close);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        windowShell.MarkClosing();
        DisposeRuntimeResources();
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            windowShell.MarkClosing();
            DisposeRuntimeResources();
        }

        base.Dispose(disposing);
    }

    private void DisposeRuntimeResources()
    {
        if (runtimeResourcesDisposed)
        {
            return;
        }

        runtimeResourcesDisposed = true;
        controlScheduler.Dispose();
        statusPaintScheduler.Dispose();
        hotkeyShell.Dispose();
        monitorCoordinator.Dispose();
        worldPoolFillService.Dispose();
        automationShell.Dispose();
        settingsShell.Dispose();
        timerOverlayHost.Dispose();
        overlayWindowController.Dispose();
        renderResources.Dispose();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        hotkeyShell.Unregister();
        base.OnHandleDestroyed(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        WindowCloseAction closeAction = windowShell.RequestClose();
        if (closeAction == WindowCloseAction.AllowClose)
        {
            base.OnFormClosing(e);
            return;
        }

        if (closeAction == WindowCloseAction.CancelAlreadyPending)
        {
            e.Cancel = true;
            return;
        }

        e.Cancel = true;
        BeginInvoke(new Action(() =>
        {
            try
            {
                FinalizeRunBeforeExit();
            }
            finally
            {
                windowShell.CompleteCloseFinalization();
                Close();
            }
        }));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (mainWindowModalInputRouter.TryRedirectFromMainInput())
        {
            return;
        }

        base.OnMouseDown(e);
        if (IsHandleCreated)
        {
            modalWindows.SyncMainWindowGroup(Handle);
        }

        if (e.Button == MouseButtons.Left &&
            !OverlayResizeHitTest.IsResizeZone(
                e.Location,
                ClientSize,
                ResizeBorder,
                OverlayResizeEdges.Left | OverlayResizeEdges.Top | OverlayResizeEdges.Right | OverlayResizeEdges.Bottom))
        {
            windowShell.BeginDrag(Cursor.Position);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (mainWindowModalInputRouter.HasModalWindow)
        {
            windowShell.CancelDrag();
            return;
        }

        base.OnMouseMove(e);
        if (!windowShell.TryMoveDrag(Cursor.Position, out Point delta))
        {
            return;
        }

        overlayBoundsController.MoveBy(delta);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (mainWindowModalInputRouter.TryRedirectFromMainInput())
        {
            windowShell.CancelDrag();
            return;
        }

        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            windowShell.CancelDrag();
        }

        if (e.Button == MouseButtons.Right && settings.General.PracticeMode)
        {
            TryOpenPracticeEdit(e.Location);
        }
    }

    private bool IsEditablePracticePoint(Point point)
    {
        if (!TryGetSplitRowAt(point, out int rowIndex, out Rectangle rowRect))
        {
            return false;
        }

        SplitStatusSnapshot status = splitStatuses[rowIndex];
        ColumnRects columns = SplitListRenderer.GetColumnRects(settings, rowRect, status.Definition.IsAttached);
        if (columns.Time is Rectangle timeRect && timeRect.Contains(point))
        {
            return status.IsCompleted;
        }

        return false;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        QueueStatusOverlayRender();
    }

    protected override void WndProc(ref Message m)
    {
        const int wmNcHitTest = 0x84;
        const int htTransparent = -1;
        const int htClient = 1;

        if (mainWindowModalInputRouter.TryHandleWindowMessage(ref m))
        {
            return;
        }

        if (hotkeyShell.TryGetAction(m, out HotkeyAction action))
        {
            if (HotkeyCommandMapper.TryMap(
                    action,
                    DateTime.UtcNow,
                    automationShell.IsCreateWorldRunning,
                    automationShell.IsEnterWorldRunning,
                    out AppCommand command))
            {
                ExecuteAppCommand(command);
            }

            m.Result = IntPtr.Zero;
            return;
        }

        base.WndProc(ref m);

        if (mouseClickThrough && m.Msg == wmNcHitTest)
        {
            m.Result = (IntPtr)htTransparent;
            return;
        }

        if (m.Msg != wmNcHitTest || m.Result != (IntPtr)htClient)
        {
            return;
        }

        long lParam = m.LParam.ToInt64();
        int x = unchecked((short)(lParam & 0xFFFF));
        int y = unchecked((short)((lParam >> 16) & 0xFFFF));
        Point point = PointToClient(new Point(x, y));
        IntPtr? hit = OverlayResizeHitTest.Resolve(
            point,
            ClientSize,
            ResizeBorder,
            OverlayResizeEdges.Left | OverlayResizeEdges.Top | OverlayResizeEdges.Right | OverlayResizeEdges.Bottom);
        if (hit.HasValue)
        {
            m.Result = hit.Value;
        }
    }
}
