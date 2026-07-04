using System.Drawing;

namespace TerrariaSplit.UI.Settings;

internal readonly record struct DebugSettingsDisplayValue(string Text, Color Color);

internal sealed record DebugSettingsSnapshot(
    DebugQuickStatusSnapshot QuickStatus,
    DebugPerformanceSnapshot Performance,
    DebugWindowInfoSnapshot Window,
    DebugAutomationSnapshot Automation,
    DebugBossProgressSnapshot BossProgress,
    DebugWorldGenerationSnapshot WorldGeneration,
    DebugMemorySnapshot Memory,
    string Report);

internal sealed record DebugQuickStatusSnapshot(
    DebugSettingsDisplayValue ProcessDetected,
    DebugSettingsDisplayValue WindowDetected,
    DebugSettingsDisplayValue WindowStatus,
    DebugSettingsDisplayValue WatcherAttached,
    DebugSettingsDisplayValue MemoryReady,
    DebugSettingsDisplayValue BossFlagsReady,
    DebugSettingsDisplayValue GameState,
    DebugSettingsDisplayValue LastUpdated);

internal sealed record DebugPerformanceSnapshot(
    string WatcherPoll,
    string ControlTick,
    string StatusPaint,
    string TimerPaint,
    string TimerLayeredUpdate);

internal sealed record DebugWindowInfoSnapshot(
    string ProcessId,
    string ProcessStartTime,
    string ProcessPath,
    string ProcessArchitecture,
    string ProcessVersion,
    string WindowHandle,
    string WindowTitle,
    string Responding,
    string Visible,
    string Minimized,
    string Maximized,
    string Foreground,
    string WindowBounds,
    string ClientSize,
    string MenuScale,
    string LogicalMenuSize);

internal sealed record DebugAutomationSnapshot(
    string PlayerFiles,
    string WorldFiles,
    string FavoritePlayers,
    string FavoriteWorlds,
    string PlayerName,
    string PlayerDifficulty,
    string WorldSize,
    string WorldDifficulty,
    string WorldEvil,
    string CatchStars,
    string CatchStarsThrough,
    string CatchSpeed,
    string PyramidFilter,
    string PyramidItems,
    string ReturnToMainMenuOnFilterFailure,
    string WindowActivationDelay,
    string ClickFocusDelay,
    string InputPressDuration,
    string ShortActionDelay,
    string MenuActionDelay,
    string PyramidFilterPostDelay,
    string AutoCreateSequence);

internal sealed record DebugBossProgressSnapshot(
    string Skeletron,
    string WallOfFlesh,
    string Destroyer,
    string Twins,
    string SkeletronPrime,
    string Plantera,
    string Golem,
    string LunaticCultist,
    string MoonLord);

internal sealed record DebugWorldGenerationSnapshot(
    string CurrentPass,
    string CurrentSeed,
    string ProgressMessage,
    string CurrentProgress,
    string TotalProgress);

internal sealed record DebugMemorySnapshot(
    string ProbeAttempts,
    string LastProbe,
    string LayoutStatus,
    string ProbeError,
    string MainModuleBase,
    string MainModuleSize,
    string GameMenuAddress,
    string BossFactAddresses,
    string HardmodeAddress,
    string GenerationProgressAddress,
    string GenerationControllerAddress,
    string FailureStage);
