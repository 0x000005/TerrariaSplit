using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed record StartupCore(
    IRuntimeDataPaths RuntimeDataPaths,
    ISettingsRepository SettingsRepository,
    Func<AppSettings, OperationResult> SaveSettings,
    ISettingsSnapshotFactory SettingsSnapshots,
    IAppLogger AppLogger,
    IHotkeyRegistrationManager HotkeyManager,
    OverlayRenderResources RenderResources,
    Task StatusIconPreloadTask,
    OverlayAnimationController OverlayAnimations,
    RuntimePerformanceTracker Performance,
    ApplicationController ApplicationController);

internal sealed record RuntimeServicePreparation(
    WorldPoolStore WorldPoolStore,
    WorldPoolFillService WorldPoolFillService,
    MainFormContextMenuBuilder ContextMenuBuilder,
    SoundPlayerService SoundPlayer) : IDisposable
{
    public void Dispose()
    {
        WorldPoolFillService.Dispose();
    }
}

internal sealed record RuntimeServices(
    RuntimeServicePreparation Preparation,
    TerrariaMonitorCoordinator MonitorCoordinator,
    AutomationShell AutomationShell,
    SettingsShell SettingsShell,
    RaceShell RaceShell,
    ApplicationShellEffectExecutor EffectExecutor,
    HighPrecisionScheduler ControlScheduler,
    HighPrecisionScheduler StatusPaintScheduler,
    ContextMenuStrip ContextMenu)
{
    public WorldPoolStore WorldPoolStore => Preparation.WorldPoolStore;

    public WorldPoolFillService WorldPoolFillService => Preparation.WorldPoolFillService;

    public MainFormContextMenuBuilder ContextMenuBuilder => Preparation.ContextMenuBuilder;

    public SoundPlayerService SoundPlayer => Preparation.SoundPlayer;
}
