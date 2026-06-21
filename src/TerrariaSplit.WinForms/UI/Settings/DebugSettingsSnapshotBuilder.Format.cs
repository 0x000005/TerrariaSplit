using System.Drawing;
using System.Globalization;

namespace TerrariaSplit.UI.Settings;

internal static partial class DebugSettingsSnapshotBuilder
{
    private static bool TryCreateGeometry(Size? clientSize, out TerrariaMenuGeometry geometry)
    {
        if (clientSize is not Size size || size.Width <= 0 || size.Height <= 0)
        {
            geometry = default;
            return false;
        }

        geometry = TerrariaMenuGeometry.From(size);
        return true;
    }

    private static bool HasAnyBossState(TerrariaGameFacts facts)
    {
        return SplitCatalog.BossFacts.Any(boss =>
            facts.Get(boss.FactKey).Kind != FactValueKind.Unknown);
    }

    private static bool? GetBossFact(TerrariaGameFacts facts, string bossTargetId)
    {
        return SplitCatalog.TryGetBossFact(bossTargetId, out BossFactDescriptor descriptor)
            ? facts.Get(descriptor.FactKey).AsBoolean()
            : null;
    }

    private static string FormatGameState(bool? isGameMenu)
    {
        return isGameMenu switch
        {
            true => "In menu",
            false => "In world",
            null => "Unknown"
        };
    }

    private static string FormatBounds(Rectangle? bounds, Func<string, string> localize)
    {
        if (bounds is not Rectangle rect)
        {
            return localize("Unknown");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{rect.X}, {rect.Y}, {rect.Width} x {rect.Height}");
    }

    private static string FormatSize(Size? size, Func<string, string> localize)
    {
        if (size is not Size value)
        {
            return localize("Unknown");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value.Width} x {value.Height}");
    }

    private static string FormatScale(float scale)
    {
        return scale.ToString("0.###", CultureInfo.InvariantCulture) + "x";
    }

    private static string FormatLogicalSize(TerrariaMenuGeometry geometry)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{geometry.LogicalWidth:0.##} x {geometry.LogicalHeight:0.##}");
    }

    private static string FormatProcessId(int? processId, Func<string, string> localize)
    {
        return processId?.ToString(CultureInfo.InvariantCulture) ?? localize("Unknown");
    }

    private static string FormatDateTime(DateTime? value, Func<string, string> localize)
    {
        return value?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? localize("Unknown");
    }

    private static string FormatTimestamp(DateTime? value, Func<string, string> localize)
    {
        return value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) ?? localize("Unknown");
    }

    private static string FormatAddress(IntPtr address, Func<string, string> localize)
    {
        return address == IntPtr.Zero
            ? localize("Unknown")
            : $"0x{address.ToInt64():X}";
    }

    private static string FormatByteCount(int? bytes, Func<string, string> localize)
    {
        return bytes.HasValue
            ? FormatBytes(bytes.Value)
            : localize("Unknown");
    }

    private static string FormatWatcherPollSummary(
        RuntimeDebugSnapshot debugSnapshot,
        AdvancedSettings advanced,
        Func<string, string> localize)
    {
        if (!debugSnapshot.WatchSnapshot.IsAttached || !debugSnapshot.WatchSnapshot.IsReady)
        {
            return FormatConfiguredHzWaitingSummary(
                RefreshRateSettings.NormalizeReadyWatcherPollHz(advanced.ReadyWatcherPollHz),
                "Waiting for attached memory",
                localize);
        }

        return FormatRefreshRateSummary(
            debugSnapshot.Performance.WatcherPollIntervalMilliseconds,
            debugSnapshot.Performance.ActualWatcherPollIntervalMilliseconds,
            debugSnapshot.Performance.MaxWatcherPollIntervalMilliseconds,
            debugSnapshot.Performance.WatcherPollCount,
            localize);
    }

    private static string FormatControlTickSummary(
        RuntimeDebugSnapshot debugSnapshot,
        AdvancedSettings advanced,
        Func<string, string> localize)
    {
        if (!debugSnapshot.WatchSnapshot.IsAttached || !debugSnapshot.WatchSnapshot.IsReady)
        {
            return FormatConfiguredHzWaitingSummary(
                RefreshRateSettings.NormalizeReadyUiControlHz(advanced.ReadyUiControlHz),
                "Waiting for attached memory",
                localize);
        }

        return FormatRefreshRateSummary(
            debugSnapshot.Performance.ControlTickIntervalMilliseconds,
            debugSnapshot.Performance.ActualControlTickIntervalMilliseconds,
            debugSnapshot.Performance.MaxControlTickIntervalMilliseconds,
            debugSnapshot.Performance.ControlTickCount,
            localize);
    }

    private static string FormatStatusPaintSummary(
        RuntimeDebugSnapshot debugSnapshot,
        Func<string, string> localize)
    {
        if (!debugSnapshot.WatchSnapshot.IsAttached || !debugSnapshot.WatchSnapshot.IsReady)
        {
            return FormatConfiguredWaitingSummary(
                debugSnapshot.Performance.StatusPaintIntervalMilliseconds,
                "Waiting for attached memory",
                localize);
        }

        if (debugSnapshot.TimerPhase != SplitTimerPhase.Running)
        {
            return FormatConfiguredWaitingSummary(
                debugSnapshot.Performance.StatusPaintIntervalMilliseconds,
                "Waiting for timer start",
                localize);
        }

        return FormatRefreshRateSummary(
            debugSnapshot.Performance.StatusPaintIntervalMilliseconds,
            debugSnapshot.Performance.ActualStatusPaintIntervalMilliseconds,
            debugSnapshot.Performance.MaxStatusPaintIntervalMilliseconds,
            debugSnapshot.Performance.StatusPaintCount,
            localize);
    }

    private static string FormatTimerPaintSummary(
        RuntimeDebugSnapshot debugSnapshot,
        Func<string, string> localize)
    {
        if (!debugSnapshot.WatchSnapshot.IsAttached || !debugSnapshot.WatchSnapshot.IsReady)
        {
            return FormatConfiguredWaitingSummary(
                debugSnapshot.Performance.TimerOverlayPaintIntervalMilliseconds,
                "Waiting for attached memory",
                localize);
        }

        if (debugSnapshot.TimerPhase != SplitTimerPhase.Running)
        {
            return FormatConfiguredWaitingSummary(
                debugSnapshot.Performance.TimerOverlayPaintIntervalMilliseconds,
                "Waiting for timer start",
                localize);
        }

        return FormatRefreshRateSummary(
            debugSnapshot.Performance.TimerOverlayPaintIntervalMilliseconds,
            debugSnapshot.Performance.ActualTimerOverlayPaintIntervalMilliseconds,
            debugSnapshot.Performance.MaxTimerOverlayPaintIntervalMilliseconds,
            debugSnapshot.Performance.TimerOverlayPaintCount,
            localize);
    }

    private static string FormatRefreshRateSummary(
        double configuredIntervalMilliseconds,
        double actualIntervalMilliseconds,
        double maxIntervalMilliseconds,
        int sampleCount,
        Func<string, string> localize)
    {
        string configured = FormatFrequency(configuredIntervalMilliseconds, localize);
        bool hasSamples = sampleCount >= 2 && actualIntervalMilliseconds > 0;
        string actual = hasSamples
            ? FormatFrequency(actualIntervalMilliseconds, localize)
            : localize("Waiting for samples");
        string average = hasSamples
            ? FormatMilliseconds(actualIntervalMilliseconds)
            : localize("Waiting for samples");
        string maximum = hasSamples && maxIntervalMilliseconds > 0
            ? FormatMilliseconds(maxIntervalMilliseconds)
            : localize("Waiting for samples");
        return string.Format(
            CultureInfo.InvariantCulture,
            localize("configured {0}, actual {1}, avg {2}, max {3}"),
            configured,
            actual,
            average,
            maximum);
    }

    private static string FormatConfiguredWaitingSummary(
        double configuredIntervalMilliseconds,
        string waitingText,
        Func<string, string> localize)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            localize("configured {0}, waiting {1}"),
            FormatFrequency(configuredIntervalMilliseconds, localize),
            localize(waitingText));
    }

    private static string FormatConfiguredHzWaitingSummary(
        int configuredHz,
        string waitingText,
        Func<string, string> localize)
    {
        return FormatConfiguredWaitingSummary(
            RefreshRateSettings.ToInterval(configuredHz).TotalMilliseconds,
            waitingText,
            localize);
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###", CultureInfo.InvariantCulture) + " ms";
    }

    private static string FormatFrequency(double intervalMilliseconds, Func<string, string> localize)
    {
        if (intervalMilliseconds <= 0)
        {
            return localize("Unknown");
        }

        double hertz = 1000d / intervalMilliseconds;
        return hertz.ToString("0.0", CultureInfo.InvariantCulture) + " Hz";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int unitIndex = 0;
        while (value >= 1024d && unitIndex < units.Length - 1)
        {
            value /= 1024d;
            unitIndex++;
        }

        string number = unitIndex == 0
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.0", CultureInfo.InvariantCulture);
        return $"{number} {units[unitIndex]}";
    }

    private static string FormatScanStats(
        SignatureScanDiagnostics? diagnostics,
        Func<string, string> localize)
    {
        if (diagnostics is not SignatureScanDiagnostics value)
        {
            return localize("Unknown");
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            localize("private {0}/{1} scanned, {2} read; image {3}/{4} scanned, {5} read; total {6}; {7}"),
            value.PrivateExecutablePagesScanned,
            value.PrivateExecutablePagesSeen,
            FormatBytes(value.PrivateExecutableBytesScanned),
            value.ImageExecutablePagesScanned,
            value.ImageExecutablePagesSeen,
            FormatBytes(value.ImageExecutableBytesScanned),
            FormatBytes(value.TotalExecutableBytesScanned),
            FormatMilliseconds(value.ElapsedMilliseconds));
    }

    private static string FormatScanFailures(
        SignatureScanDiagnostics? diagnostics,
        Func<string, string> localize)
    {
        if (diagnostics is not SignatureScanDiagnostics value)
        {
            return localize("Unknown");
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            localize("read failures {0}, oversized skipped {1}"),
            value.ReadFailures,
            value.OversizedPagesSkipped);
    }

    private static string FormatPlayerName(string? playerName)
    {
        string trimmed = playerName?.Trim() ?? string.Empty;
        return trimmed.Length == 0 ? "1" : trimmed;
    }

    private static string FormatText(string? value, Func<string, string> localize)
    {
        return string.IsNullOrWhiteSpace(value) ? localize("Unknown") : value;
    }

    private static string FormatOptionalBool(bool? value, Func<string, string> localize)
    {
        return value.HasValue ? FormatBool(value.Value, localize) : localize("Unknown");
    }

    private static string FormatWorldGenerationText(
        string? value,
        IntPtr slotAddress,
        Func<string, string> localize)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return slotAddress != IntPtr.Zero
            ? localize("World generation idle")
            : localize("Unknown");
    }

    private static string FormatWorldCreationSeed(
        TerrariaWorldCreationSeedSnapshot snapshot,
        Func<string, string> localize)
    {
        return snapshot.Status switch
        {
            TerrariaWorldCreationSeedStatus.Seed => FormatText(snapshot.SeedText, localize),
            TerrariaWorldCreationSeedStatus.Empty => localize("Empty"),
            TerrariaWorldCreationSeedStatus.NotOnWorldCreationPage => localize("Not on world creation page"),
            _ => localize("Unknown")
        };
    }

    private static string FormatWorldGenerationPercent(
        double? value,
        IntPtr slotAddress,
        Func<string, string> localize)
    {
        if (value.HasValue)
        {
            return value.Value.ToString("P1", CultureInfo.InvariantCulture);
        }

        return slotAddress != IntPtr.Zero
            ? localize("World generation idle")
            : localize("Unknown");
    }

    private static string FormatBool(bool value, Func<string, string> localize)
    {
        return localize(value ? "Yes" : "No");
    }
}
