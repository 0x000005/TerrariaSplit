using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed record MainShellServices(
    WorldPoolStore WorldPoolStore,
    ISettingsRepository SettingsRepository,
    Func<AppSettings, OperationResult> SaveSettings,
    ISettingsSnapshotFactory SettingsSnapshots,
    IAppLogger AppLogger,
    WorldPoolFillService WorldPoolFillService,
    MainFormContextMenuBuilder ContextMenuBuilder,
    SoundPlayerService SoundPlayer,
    IHotkeyRegistrationManager HotkeyManager,
    OverlayRenderResources RenderResources,
    OverlayAnimationController OverlayAnimations,
    ContextMenuStrip ContextMenu,
    RuntimePerformanceTracker Performance,
    ApplicationController ApplicationController);
