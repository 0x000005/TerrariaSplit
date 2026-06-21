using System.Drawing;
using System.Globalization;

namespace TerrariaSplit.UI.Settings;

internal static partial class DebugSettingsSnapshotBuilder
{
    private static readonly Color QuickStatusNormalColor = UiTheme.Accent;
    private static readonly Color QuickStatusProblemColor = Color.FromArgb(225, 92, 88);
    private static readonly Color QuickStatusMenuColor = Color.FromArgb(107, 157, 216);

    public static DebugSettingsSnapshot Build(
        TerrariaWindowSnapshot window,
        RuntimeDebugSnapshot debugSnapshot,
        TerrariaSaveInventorySnapshot inventory,
        AutoCreateWorldSettings autoCreate,
        AdvancedSettings advanced,
        Func<int> worldPoolCountProvider,
        Func<string, string> localize)
    {
        string lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        TerrariaWatchSnapshot watch = debugSnapshot.WatchSnapshot;
        TerrariaWatcherDiagnostics diagnostics = debugSnapshot.WatcherDiagnostics;

        var snapshot = new DebugSettingsSnapshot(
            BuildQuickStatus(window, watch, lastUpdated, localize),
            BuildPerformance(debugSnapshot, advanced, localize),
            BuildWindowInfo(window, diagnostics, localize),
            BuildAutomation(window, inventory, autoCreate, worldPoolCountProvider, localize),
            BuildBossProgress(watch.Facts, localize),
            BuildWorldGeneration(watch, diagnostics, localize),
            BuildMemory(diagnostics, localize),
            string.Empty);

        return snapshot with { Report = BuildReport(snapshot, localize) };
    }

    private static DebugQuickStatusSnapshot BuildQuickStatus(
        TerrariaWindowSnapshot window,
        TerrariaWatchSnapshot watch,
        string lastUpdated,
        Func<string, string> localize)
    {
        bool bossFlagsReady = HasAnyBossState(watch.Facts);
        return new DebugQuickStatusSnapshot(
            QuickBool(window.HasProcess, localize),
            QuickBool(window.HasWindow, localize),
            QuickStatus(window.Status, localize),
            QuickBool(watch.IsAttached, localize),
            QuickBool(watch.IsReady, localize),
            QuickBool(bossFlagsReady, localize),
            QuickGameState(watch.IsGameMenu, localize),
            new DebugSettingsDisplayValue(lastUpdated, UiTheme.MutedText));
    }

    private static DebugPerformanceSnapshot BuildPerformance(
        RuntimeDebugSnapshot debugSnapshot,
        AdvancedSettings advanced,
        Func<string, string> localize)
    {
        return new DebugPerformanceSnapshot(
            FormatWatcherPollSummary(debugSnapshot, advanced, localize),
            FormatControlTickSummary(debugSnapshot, advanced, localize),
            FormatStatusPaintSummary(debugSnapshot, localize),
            FormatTimerPaintSummary(debugSnapshot, localize));
    }

    private static DebugWindowInfoSnapshot BuildWindowInfo(
        TerrariaWindowSnapshot window,
        TerrariaWatcherDiagnostics diagnostics,
        Func<string, string> localize)
    {
        string menuScale = localize("Unknown");
        string logicalMenuSize = localize("Unknown");
        if (TryCreateGeometry(window.ClientSize, out TerrariaMenuGeometry geometry))
        {
            menuScale = FormatScale(geometry.Scale);
            logicalMenuSize = FormatLogicalSize(geometry);
        }

        return new DebugWindowInfoSnapshot(
            FormatProcessId(window.ProcessId, localize),
            FormatDateTime(window.ProcessStartTime, localize),
            FormatText(diagnostics.ProcessPath, localize),
            FormatText(diagnostics.ProcessArchitecture, localize),
            FormatText(diagnostics.ProcessVersion, localize),
            window.HasWindow ? $"0x{window.WindowHandle.ToInt64():X}" : localize("Unknown"),
            string.IsNullOrWhiteSpace(window.WindowTitle) ? localize("Unknown") : window.WindowTitle,
            window.HasProcess ? FormatBool(window.IsResponding, localize) : localize("Unknown"),
            window.HasWindow ? FormatBool(window.IsVisible, localize) : localize("Unknown"),
            window.HasWindow ? FormatBool(window.IsMinimized, localize) : localize("Unknown"),
            window.HasWindow ? FormatBool(window.IsMaximized, localize) : localize("Unknown"),
            window.HasWindow ? FormatBool(window.IsForeground, localize) : localize("Unknown"),
            FormatBounds(window.WindowBounds, localize),
            FormatSize(window.ClientSize, localize),
            menuScale,
            logicalMenuSize);
    }

    private static DebugAutomationSnapshot BuildAutomation(
        TerrariaWindowSnapshot window,
        TerrariaSaveInventorySnapshot inventory,
        AutoCreateWorldSettings autoCreate,
        Func<int> worldPoolCountProvider,
        Func<string, string> localize)
    {
        string autoCreateSequence = localize("Unavailable because client size is unknown.");
        if (TryCreateGeometry(window.ClientSize, out TerrariaMenuGeometry geometry))
        {
            autoCreateSequence = BuildAutoCreateSequenceText(
                autoCreate,
                geometry,
                inventory.FavoritePlayers,
                worldPoolCountProvider,
                localize);
        }

        return new DebugAutomationSnapshot(
            inventory.PlayerFiles.ToString(CultureInfo.InvariantCulture),
            inventory.WorldFiles.ToString(CultureInfo.InvariantCulture),
            inventory.FavoritePlayers.ToString(CultureInfo.InvariantCulture),
            inventory.FavoriteWorlds.ToString(CultureInfo.InvariantCulture),
            FormatPlayerName(autoCreate.PlayerName),
            localize(AutoCreatePlayerDifficulty.Normalize(autoCreate.PlayerDifficulty)),
            localize(AutoCreateWorldSize.Normalize(autoCreate.WorldSize)),
            localize(AutoCreateWorldDifficulty.Normalize(autoCreate.WorldDifficulty)),
            localize(AutoCreateWorldEvil.Normalize(autoCreate.WorldEvil)),
            FormatBool(autoCreate.EnableZenithStarCatch, localize),
            localize(AutoCreateZenithStarCatchStage.Normalize(autoCreate.ZenithStarCatchStopStage)),
            AutoCreateZenithStarCatchSpeed.FormatMultiplier(autoCreate.ZenithStarCatchSpeedSliderValue),
            FormatBool(autoCreate.EnablePyramidFilter, localize),
            FormatPyramidFilterItems(autoCreate, localize),
            FormatBool(autoCreate.ReturnToMainMenuOnFilterFailure, localize),
            autoCreate.WindowActivationDelayMilliseconds.ToString(CultureInfo.InvariantCulture),
            autoCreate.ClickFocusDelayMilliseconds.ToString(CultureInfo.InvariantCulture),
            autoCreate.InputPressDurationMilliseconds.ToString(CultureInfo.InvariantCulture),
            autoCreate.ShortActionDelayMilliseconds.ToString(CultureInfo.InvariantCulture),
            autoCreate.MenuActionDelayMilliseconds.ToString(CultureInfo.InvariantCulture),
            autoCreate.PyramidFilterPostDelayMilliseconds.ToString(CultureInfo.InvariantCulture),
            autoCreateSequence);
    }

    private static DebugBossProgressSnapshot BuildBossProgress(
        TerrariaGameFacts facts,
        Func<string, string> localize)
    {
        return new DebugBossProgressSnapshot(
            FormatOptionalBool(GetBossFact(facts, SplitCatalog.Skeletron), localize),
            FormatOptionalBool(GetBossFact(facts, SplitCatalog.WallOfFlesh), localize),
            FormatOptionalBool(GetBossFact(facts, SplitCatalog.Destroyer), localize),
            FormatOptionalBool(GetBossFact(facts, SplitCatalog.Twins), localize),
            FormatOptionalBool(GetBossFact(facts, SplitCatalog.SkeletronPrime), localize),
            FormatOptionalBool(GetBossFact(facts, SplitCatalog.Plantera), localize),
            FormatOptionalBool(GetBossFact(facts, SplitCatalog.Golem), localize),
            FormatOptionalBool(GetBossFact(facts, SplitCatalog.LunaticCultist), localize),
            FormatOptionalBool(GetBossFact(facts, SplitCatalog.MoonLord), localize));
    }

    private static DebugWorldGenerationSnapshot BuildWorldGeneration(
        TerrariaWatchSnapshot watch,
        TerrariaWatcherDiagnostics diagnostics,
        Func<string, string> localize)
    {
        return new DebugWorldGenerationSnapshot(
            FormatWorldGenerationText(
                watch.WorldGeneration.CurrentPassName,
                diagnostics.CurrentControllerAddress,
                localize),
            FormatWorldCreationSeed(diagnostics.WorldCreationSeed, localize),
            FormatWorldGenerationText(
                watch.WorldGeneration.ProgressMessage,
                diagnostics.CurrentGenerationProgressAddress,
                localize),
            FormatWorldGenerationPercent(
                watch.WorldGeneration.CurrentProgress,
                diagnostics.CurrentGenerationProgressAddress,
                localize),
            FormatWorldGenerationPercent(
                watch.WorldGeneration.TotalProgress,
                diagnostics.CurrentGenerationProgressAddress,
                localize));
    }

    private static DebugMemorySnapshot BuildMemory(
        TerrariaWatcherDiagnostics diagnostics,
        Func<string, string> localize)
    {
        return new DebugMemorySnapshot(
            diagnostics.SignatureScanAttempts.ToString(CultureInfo.InvariantCulture),
            FormatTimestamp(diagnostics.LastSignatureScanUtc, localize),
            FormatScanStats(diagnostics.LastSignatureScan, localize),
            FormatScanFailures(diagnostics.LastSignatureScan, localize),
            FormatAddress(diagnostics.MainModuleBaseAddress, localize),
            FormatByteCount(diagnostics.MainModuleSize, localize),
            FormatAddress(diagnostics.UpdateTimeAddress, localize),
            FormatAddress(diagnostics.BossFlagsBaseAddress, localize),
            FormatAddress(diagnostics.HardmodeAddress, localize),
            FormatAddress(diagnostics.CurrentGenerationProgressAddress, localize),
            FormatAddress(diagnostics.CurrentControllerAddress, localize),
            LocalizeStage(diagnostics.Stage, localize));
    }
}
