using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed record MainShellServices(
    WorldPoolStore WorldPoolStore,
    ISettingsSnapshotFactory SettingsSnapshots,
    IAppLogger AppLogger,
    WorldPoolFillService WorldPoolFillService,
    MainFormContextMenuBuilder ContextMenuBuilder,
    SoundPlayerService SoundPlayer,
    GlobalHotkeyManager HotkeyManager,
    OverlayRenderResources RenderResources,
    OverlayAnimationController OverlayAnimations,
    ContextMenuStrip ContextMenu,
    RuntimePerformanceTracker Performance,
    ApplicationController ApplicationController);
