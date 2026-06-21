using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed partial class DebugSettingsPage : SettingsPageBase
{
    private static void AppendMultilineSection(List<string> lines, SettingsForm owner, string title, string content)
    {
        if (lines.Count > 0)
        {
            lines.Add(string.Empty);
        }

        lines.Add(owner.Localize(title));
        lines.AddRange(content.Split([Environment.NewLine], StringSplitOptions.None));
    }

    private static string BuildAutoCreateSequenceText(
        AutoCreateWorldSettings autoCreate,
        TerrariaMenuGeometry geometry,
        int favoritePlayers,
        SettingsForm owner)
    {
        var lines = new List<string>();
        int step = 1;
        bool usesPooledWorld = UsesPooledWorldPath(autoCreate, owner);

        if (usesPooledWorld)
        {
            lines.Add($"{step++}. {owner.Localize("Install pooled world")}");
        }

        AppendSequenceStep(lines, owner, ref step, "Single Player", geometry.MainMenuSinglePlayer());
        AppendSequenceStep(lines, owner, ref step, "New Player", geometry.SelectMenuNewButton());

        if (!string.IsNullOrWhiteSpace(autoCreate.PlayerTemplateCode))
        {
            AppendSequenceStep(lines, owner, ref step, "Character Clothing Tab", geometry.CharacterClothingCategoryButton());
            AppendSequenceStep(lines, owner, ref step, "Paste Player Template", geometry.CharacterTemplatePasteButton());
        }

        string normalizedPlayerDifficulty = AutoCreatePlayerDifficulty.Normalize(autoCreate.PlayerDifficulty);
        if (!string.Equals(normalizedPlayerDifficulty, AutoCreatePlayerDifficulty.Softcore, StringComparison.OrdinalIgnoreCase))
        {
            AppendSequenceStep(lines, owner, ref step, "Character Info Tab", geometry.CharacterInfoCategoryButton());
            AppendSequenceStep(
                lines,
                owner,
                ref step,
                "Player difficulty",
                geometry.PlayerDifficultyButton(normalizedPlayerDifficulty),
                owner.Localize(normalizedPlayerDifficulty));
        }

        AppendSequenceStep(lines, owner, ref step, "Create Player", geometry.CreatePlayerButton());
        AppendSequenceStep(lines, owner, ref step, "Select Created Player", geometry.PlayerPlayButton(favoritePlayers));
        if (usesPooledWorld)
        {
            lines.Add($"{step++}. {owner.Localize("Stop at world select")}");
            return string.Join(Environment.NewLine, lines);
        }

        AppendSequenceStep(lines, owner, ref step, "New World", geometry.SelectMenuNewButton());

        string normalizedWorldSize = AutoCreateWorldSize.Normalize(autoCreate.WorldSize);
        AppendSequenceStep(
            lines,
            owner,
            ref step,
            "World size",
            geometry.WorldSizeButton(normalizedWorldSize),
            owner.Localize(normalizedWorldSize));

        string normalizedWorldDifficulty = AutoCreateWorldDifficulty.Normalize(autoCreate.WorldDifficulty);
        AppendSequenceStep(
            lines,
            owner,
            ref step,
            "World difficulty",
            geometry.WorldDifficultyButton(normalizedWorldDifficulty),
            owner.Localize(normalizedWorldDifficulty));

        string normalizedWorldEvil = AutoCreateWorldEvil.Normalize(autoCreate.WorldEvil);
        AppendSequenceStep(
            lines,
            owner,
            ref step,
            "World evil",
            geometry.WorldEvilButton(normalizedWorldEvil),
            owner.Localize(normalizedWorldEvil));

        AppendSequenceStep(lines, owner, ref step, "Advanced Seed", geometry.WorldAdvancedSeedButton());

        foreach (string specialSeed in AutoCreateSpecialWorldSeed.ParseList(autoCreate.SpecialSeeds))
        {
            AppendSequenceStep(
                lines,
                owner,
                ref step,
                "Special seeds",
                geometry.AdvancedSpecialSeedButton(specialSeed),
                owner.Localize(specialSeed));
        }

        string secretSeeds = autoCreate.SecretSeeds?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(secretSeeds))
        {
            AppendSequenceStep(lines, owner, ref step, "Secret seeds", geometry.AdvancedSeedTextButton(), secretSeeds);
            AppendSequenceStep(lines, owner, ref step, "Submit World Seed", geometry.VirtualKeyboardSubmitButton());
        }

        AppendSequenceStep(lines, owner, ref step, "Randomize Visible Seed", geometry.AdvancedSeedRandomizeButton());
        AppendSequenceStep(lines, owner, ref step, "Apply visible seed", geometry.WorldAdvancedApplyButton());

        AppendSequenceStep(lines, owner, ref step, "Create World", geometry.CreateWorldButton());

        if (AutoCreateSpecialWorldSeed.ParseList(autoCreate.SpecialSeeds)
                .Contains(AutoCreateSpecialWorldSeed.Zenith, StringComparer.OrdinalIgnoreCase) &&
            autoCreate.EnableZenithStarCatch)
        {
            lines.Add(
                $"{step++}. {owner.Localize("Catch stars through")}: " +
                owner.Localize(AutoCreateZenithStarCatchStage.Normalize(autoCreate.ZenithStarCatchStopStage)));
            lines.Add(
                $"{step++}. {owner.Localize("Catch speed")}: " +
                AutoCreateZenithStarCatchSpeed.FormatMultiplier(autoCreate.ZenithStarCatchSpeedSliderValue));
        }

        if (autoCreate.EnablePyramidFilter)
        {
            string itemDetail = FormatPyramidFilterItems(autoCreate, owner);
            string itemSuffix = HasPyramidFilterItems(autoCreate)
                ? $" ({owner.Localize("Required pyramid items")}: {itemDetail})"
                : string.Empty;
            lines.Add($"{step++}. {owner.Localize("Filter pyramid")}{itemSuffix}");
            if (autoCreate.ReturnToMainMenuOnFilterFailure)
            {
                lines.Add($"{step++}. {owner.Localize("Return to main menu on filter failure")}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static bool UsesPooledWorldPath(AutoCreateWorldSettings autoCreate, SettingsForm owner)
    {
        return autoCreate.EnableWorldPool &&
            owner.GetWorldPoolCount() > 0;
    }

    private static bool HasPyramidFilterItems(AutoCreateWorldSettings autoCreate)
    {
        return AutoCreatePyramidFilterItem.NormalizeMask(autoCreate.PyramidFilterItemMask) != 0;
    }

    private static string FormatPyramidFilterItems(AutoCreateWorldSettings autoCreate, SettingsForm owner)
    {
        IReadOnlyList<string> items = AutoCreatePyramidFilterItem.FromMask(autoCreate.PyramidFilterItemMask);
        return items.Count == 0
            ? owner.Localize("None")
            : string.Join(", ", items.Select(owner.Localize));
    }

    private static void AppendSequenceStep(
        List<string> lines,
        SettingsForm owner,
        ref int step,
        string label,
        Point point,
        string? detail = null)
    {
        string title = owner.Localize(label);
        if (!string.IsNullOrWhiteSpace(detail))
        {
            title += $" ({detail})";
        }

        lines.Add($"{step.ToString(CultureInfo.InvariantCulture)}. {title} -> {FormatPoint(point)}");
        step++;
    }

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

    private static string FormatBounds(Rectangle? bounds, SettingsForm owner)
    {
        if (bounds is not Rectangle rect)
        {
            return owner.Localize("Unknown");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{rect.X}, {rect.Y}, {rect.Width} x {rect.Height}");
    }

    private static string FormatSize(Size? size, SettingsForm owner)
    {
        if (size is not Size value)
        {
            return owner.Localize("Unknown");
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

    private static string FormatProcessId(int? processId, SettingsForm owner)
    {
        return processId?.ToString(CultureInfo.InvariantCulture) ?? owner.Localize("Unknown");
    }

    private static string FormatDateTime(DateTime? value, SettingsForm owner)
    {
        return value?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? owner.Localize("Unknown");
    }

    private static string FormatTimestamp(DateTime? value, SettingsForm owner)
    {
        return value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) ?? owner.Localize("Unknown");
    }

    private static string FormatAddress(IntPtr address, SettingsForm owner)
    {
        return address == IntPtr.Zero
            ? owner.Localize("Unknown")
            : $"0x{address.ToInt64():X}";
    }

    private static string FormatByteCount(int? bytes, SettingsForm owner)
    {
        return bytes.HasValue
            ? FormatBytes(bytes.Value)
            : owner.Localize("Unknown");
    }

    private static string FormatRefreshRateSummary(
        double configuredIntervalMilliseconds,
        double actualIntervalMilliseconds,
        double maxIntervalMilliseconds,
        int sampleCount,
        SettingsForm owner)
    {
        string configured = FormatFrequency(configuredIntervalMilliseconds, owner);
        bool hasSamples = sampleCount >= 2 && actualIntervalMilliseconds > 0;
        string actual = hasSamples
            ? FormatFrequency(actualIntervalMilliseconds, owner)
            : owner.Localize("Waiting for samples");
        string average = hasSamples
            ? FormatMilliseconds(actualIntervalMilliseconds)
            : owner.Localize("Waiting for samples");
        string maximum = hasSamples && maxIntervalMilliseconds > 0
            ? FormatMilliseconds(maxIntervalMilliseconds)
            : owner.Localize("Waiting for samples");
        return string.Format(
            CultureInfo.InvariantCulture,
            owner.Localize("configured {0}, actual {1}, avg {2}, max {3}"),
            configured,
            actual,
            average,
            maximum);
    }

    private static string FormatConfiguredWaitingSummary(
        double configuredIntervalMilliseconds,
        string waitingText,
        SettingsForm owner)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            owner.Localize("configured {0}, waiting {1}"),
            FormatFrequency(configuredIntervalMilliseconds, owner),
            owner.Localize(waitingText));
    }

    private static string FormatConfiguredHzWaitingSummary(
        int configuredHz,
        string waitingText,
        SettingsForm owner)
    {
        return FormatConfiguredWaitingSummary(
            RefreshRateSettings.ToInterval(configuredHz).TotalMilliseconds,
            waitingText,
            owner);
    }

    private static string FormatWatcherPollSummary(RuntimeDebugSnapshot debugSnapshot, SettingsForm owner)
    {
        if (!debugSnapshot.WatchSnapshot.IsAttached || !debugSnapshot.WatchSnapshot.IsReady)
        {
            return FormatConfiguredHzWaitingSummary(
                RefreshRateSettings.NormalizeReadyWatcherPollHz(
                    owner.Result.Advanced?.ReadyWatcherPollHz ?? AppSettingsDefaults.Advanced.ReadyWatcherPollHz),
                "Waiting for attached memory",
                owner);
        }

        return FormatRefreshRateSummary(
            debugSnapshot.Performance.WatcherPollIntervalMilliseconds,
            debugSnapshot.Performance.ActualWatcherPollIntervalMilliseconds,
            debugSnapshot.Performance.MaxWatcherPollIntervalMilliseconds,
            debugSnapshot.Performance.WatcherPollCount,
            owner);
    }

    private static string FormatControlTickSummary(RuntimeDebugSnapshot debugSnapshot, SettingsForm owner)
    {
        if (!debugSnapshot.WatchSnapshot.IsAttached || !debugSnapshot.WatchSnapshot.IsReady)
        {
            return FormatConfiguredHzWaitingSummary(
                RefreshRateSettings.NormalizeReadyUiControlHz(
                    owner.Result.Advanced?.ReadyUiControlHz ?? AppSettingsDefaults.Advanced.ReadyUiControlHz),
                "Waiting for attached memory",
                owner);
        }

        return FormatRefreshRateSummary(
            debugSnapshot.Performance.ControlTickIntervalMilliseconds,
            debugSnapshot.Performance.ActualControlTickIntervalMilliseconds,
            debugSnapshot.Performance.MaxControlTickIntervalMilliseconds,
            debugSnapshot.Performance.ControlTickCount,
            owner);
    }

    private static string FormatStatusPaintSummary(RuntimeDebugSnapshot debugSnapshot, SettingsForm owner)
    {
        if (!debugSnapshot.WatchSnapshot.IsAttached || !debugSnapshot.WatchSnapshot.IsReady)
        {
            return FormatConfiguredWaitingSummary(
                debugSnapshot.Performance.StatusPaintIntervalMilliseconds,
                "Waiting for attached memory",
                owner);
        }

        if (debugSnapshot.TimerPhase != SplitTimerPhase.Running)
        {
            return FormatConfiguredWaitingSummary(
                debugSnapshot.Performance.StatusPaintIntervalMilliseconds,
                "Waiting for timer start",
                owner);
        }

        return FormatRefreshRateSummary(
            debugSnapshot.Performance.StatusPaintIntervalMilliseconds,
            debugSnapshot.Performance.ActualStatusPaintIntervalMilliseconds,
            debugSnapshot.Performance.MaxStatusPaintIntervalMilliseconds,
            debugSnapshot.Performance.StatusPaintCount,
            owner);
    }

    private static string FormatTimerPaintSummary(RuntimeDebugSnapshot debugSnapshot, SettingsForm owner)
    {
        if (!debugSnapshot.WatchSnapshot.IsAttached || !debugSnapshot.WatchSnapshot.IsReady)
        {
            return FormatConfiguredWaitingSummary(
                debugSnapshot.Performance.TimerOverlayPaintIntervalMilliseconds,
                "Waiting for attached memory",
                owner);
        }

        if (debugSnapshot.TimerPhase != SplitTimerPhase.Running)
        {
            return FormatConfiguredWaitingSummary(
                debugSnapshot.Performance.TimerOverlayPaintIntervalMilliseconds,
                "Waiting for timer start",
                owner);
        }

        return FormatRefreshRateSummary(
            debugSnapshot.Performance.TimerOverlayPaintIntervalMilliseconds,
            debugSnapshot.Performance.ActualTimerOverlayPaintIntervalMilliseconds,
            debugSnapshot.Performance.MaxTimerOverlayPaintIntervalMilliseconds,
            debugSnapshot.Performance.TimerOverlayPaintCount,
            owner);
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return milliseconds.ToString("0.###", CultureInfo.InvariantCulture) + " ms";
    }

    private static string FormatFrequency(double intervalMilliseconds, SettingsForm owner)
    {
        if (intervalMilliseconds <= 0)
        {
            return owner.Localize("Unknown");
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

    private static string FormatScanStats(SignatureScanDiagnostics? diagnostics, SettingsForm owner)
    {
        if (diagnostics is not SignatureScanDiagnostics value)
        {
            return owner.Localize("Unknown");
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            owner.Localize("private {0}/{1} scanned, {2} read; image {3}/{4} scanned, {5} read; total {6}; {7}"),
            value.PrivateExecutablePagesScanned,
            value.PrivateExecutablePagesSeen,
            FormatBytes(value.PrivateExecutableBytesScanned),
            value.ImageExecutablePagesScanned,
            value.ImageExecutablePagesSeen,
            FormatBytes(value.ImageExecutableBytesScanned),
            FormatBytes(value.TotalExecutableBytesScanned),
            FormatMilliseconds(value.ElapsedMilliseconds));
    }

    private static string FormatScanFailures(SignatureScanDiagnostics? diagnostics, SettingsForm owner)
    {
        if (diagnostics is not SignatureScanDiagnostics value)
        {
            return owner.Localize("Unknown");
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            owner.Localize("read failures {0}, oversized skipped {1}"),
            value.ReadFailures,
            value.OversizedPagesSkipped);
    }

    private static string FormatPlayerName(string? playerName)
    {
        string trimmed = playerName?.Trim() ?? string.Empty;
        return trimmed.Length == 0 ? "1" : trimmed;
    }

    private static string FormatPoint(Point point)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{point.X}, {point.Y}");
    }

    private static string FormatText(string? value, SettingsForm owner)
    {
        return string.IsNullOrWhiteSpace(value) ? owner.Localize("Unknown") : value;
    }

    private static string FormatOptionalBool(bool? value, SettingsForm owner)
    {
        return value.HasValue ? FormatBool(value.Value, owner) : owner.Localize("Unknown");
    }

    private static string FormatPercent(double? value, SettingsForm owner)
    {
        return value.HasValue
            ? value.Value.ToString("P1", CultureInfo.InvariantCulture)
            : owner.Localize("Unknown");
    }

    private static string FormatWorldGenerationText(string? value, IntPtr slotAddress, SettingsForm owner)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return slotAddress != IntPtr.Zero
            ? owner.Localize("World generation idle")
            : owner.Localize("Unknown");
    }

    private static string FormatWorldCreationSeed(
        TerrariaWorldCreationSeedSnapshot snapshot,
        SettingsForm owner)
    {
        return snapshot.Status switch
        {
            TerrariaWorldCreationSeedStatus.Seed => FormatText(snapshot.SeedText, owner),
            TerrariaWorldCreationSeedStatus.Empty => owner.Localize("Empty"),
            TerrariaWorldCreationSeedStatus.NotOnWorldCreationPage => owner.Localize("Not on world creation page"),
            _ => owner.Localize("Unknown")
        };
    }

    private static string FormatWorldGenerationPercent(double? value, IntPtr slotAddress, SettingsForm owner)
    {
        if (value.HasValue)
        {
            return value.Value.ToString("P1", CultureInfo.InvariantCulture);
        }

        return slotAddress != IntPtr.Zero
            ? owner.Localize("World generation idle")
            : owner.Localize("Unknown");
    }

    private static string LocalizeStage(string stage, SettingsForm owner)
    {
        if (string.IsNullOrWhiteSpace(stage))
        {
            return owner.Localize("Unknown");
        }

        const string startPendingSuffix = "; start pending";
        if (stage.EndsWith(startPendingSuffix, StringComparison.Ordinal))
        {
            string prefix = stage[..^startPendingSuffix.Length];
            return $"{owner.Localize(prefix)}\uFF1B{owner.Localize("start pending")}";
        }

        return owner.Localize(stage);
    }

    private static string LocalizeStatus(string status, SettingsForm owner)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return owner.Localize("Unknown");
        }

        const string processChangedPrefix = "Terraria process changed while reading window state: ";
        const string cannotReadPrefix = "cannot read Terraria process: ";
        const string cannotAttachPrefix = "cannot attach to Terraria process: ";
        const string attachedPidPrefix = "attached to Terraria PID ";
        const string attachedProcessPrefix = "attached to Terraria process";

        if (string.Equals(status, "waiting for Terraria.exe", StringComparison.OrdinalIgnoreCase))
        {
            return owner.Localize("waiting for Terraria.exe");
        }

        if (status.StartsWith(processChangedPrefix, StringComparison.Ordinal))
        {
            return string.Format(owner.Localize("Terraria process changed while reading window state: {0}"), status[processChangedPrefix.Length..]);
        }

        if (status.StartsWith(cannotReadPrefix, StringComparison.Ordinal))
        {
            return string.Format(owner.Localize("cannot read Terraria process: {0}"), status[cannotReadPrefix.Length..]);
        }

        if (status.StartsWith(cannotAttachPrefix, StringComparison.Ordinal))
        {
            return string.Format(owner.Localize("cannot attach to Terraria process: {0}"), status[cannotAttachPrefix.Length..]);
        }

        if (status.StartsWith(attachedPidPrefix, StringComparison.Ordinal))
        {
            string remainder = status[attachedPidPrefix.Length..];
            int separatorIndex = remainder.IndexOf(',');
            if (separatorIndex < 0)
            {
                return string.Format(owner.Localize("attached to Terraria PID {0}"), remainder.Trim());
            }

            string processId = remainder[..separatorIndex].Trim();
            string detail = remainder[(separatorIndex + 1)..].Trim();
            return string.Format(owner.Localize("attached to Terraria PID {0}, {1}"), processId, LocalizeStatusDetail(detail, owner));
        }

        if (status.StartsWith(attachedProcessPrefix, StringComparison.Ordinal))
        {
            string remainder = status[attachedProcessPrefix.Length..].Trim();
            if (string.IsNullOrEmpty(remainder))
            {
                return owner.Localize("attached to Terraria process");
            }

            if (remainder.StartsWith(",", StringComparison.Ordinal))
            {
                remainder = remainder[1..].TrimStart();
            }

            return string.Format(owner.Localize("attached to Terraria process, {0}"), LocalizeStatusDetail(remainder, owner));
        }

        return owner.Localize(status);
    }

    private static string LocalizeStatusDetail(string detail, SettingsForm owner)
    {
        const string armTimerSuffix = "; return to menu once to arm timer start";
        const string scanMemoryPrefix = "scanning for ";
        const string scanMemorySuffix = " memory";
        const string windowHandleUnavailablePrefix = "window handle 0x";
        const string windowHandleUnavailableSuffix = ", client rect unavailable";
        const string windowHandlePrefix = "window handle 0x";

        string localizedSuffix = string.Empty;
        if (detail.EndsWith(armTimerSuffix, StringComparison.Ordinal))
        {
            detail = detail[..^armTimerSuffix.Length];
            localizedSuffix = "\uFF1B" + owner.Localize("return to menu once to arm timer start");
        }

        if (detail.StartsWith(scanMemoryPrefix, StringComparison.Ordinal) &&
            detail.EndsWith(scanMemorySuffix, StringComparison.Ordinal))
        {
            string version = detail.Substring(scanMemoryPrefix.Length, detail.Length - scanMemoryPrefix.Length - scanMemorySuffix.Length);
            return string.Format(owner.Localize("scanning for {0} memory"), version) + localizedSuffix;
        }

        if (detail.StartsWith(windowHandleUnavailablePrefix, StringComparison.Ordinal) &&
            detail.EndsWith(windowHandleUnavailableSuffix, StringComparison.Ordinal))
        {
            string handle = detail.Substring(windowHandleUnavailablePrefix.Length, detail.Length - windowHandleUnavailablePrefix.Length - windowHandleUnavailableSuffix.Length);
            return string.Format(owner.Localize("window handle 0x{0}, client rect unavailable"), handle) + localizedSuffix;
        }

        if (detail.StartsWith(windowHandlePrefix, StringComparison.Ordinal))
        {
            string handle = detail[windowHandlePrefix.Length..];
            return string.Format(owner.Localize("window handle 0x{0}"), handle) + localizedSuffix;
        }

        return owner.Localize(detail) + localizedSuffix;
    }

    private static string FormatBool(bool value, SettingsForm owner)
    {
        return owner.Localize(value ? "Yes" : "No");
    }

    private static void SetQuickBool(Label label, SettingsForm owner, bool value)
    {
        SetValue(label, FormatBool(value, owner), value ? QuickStatusNormalColor : QuickStatusProblemColor);
    }

    private static void SetQuickGameState(Label label, SettingsForm owner, bool? isGameMenu)
    {
        Color color = isGameMenu switch
        {
            false => QuickStatusNormalColor,
            true => QuickStatusMenuColor,
            null => QuickStatusProblemColor
        };
        SetValue(label, owner.Localize(FormatGameState(isGameMenu)), color);
    }

    private static void SetQuickStatus(Label label, string status, SettingsForm owner)
    {
        SetValue(
            label,
            LocalizeStatus(status, owner),
            IsNormalStatus(status) ? QuickStatusNormalColor : QuickStatusProblemColor);
    }

    private static void SetBool(Label label, SettingsForm owner, bool value)
    {
        SetValue(label, FormatBool(value, owner));
    }

    private static void SetBossState(Label label, bool? value, SettingsForm owner)
    {
        if (!value.HasValue)
        {
            SetValue(label, owner.Localize("Unknown"));
            return;
        }

        SetBool(label, owner, value.Value);
    }

    private static void SetOptionalBool(Label label, SettingsForm owner, bool? value)
    {
        if (!value.HasValue)
        {
            SetValue(label, owner.Localize("Unknown"));
            return;
        }

        SetBool(label, owner, value.Value);
    }

    private static void SetValue(Label label, string text)
    {
        SetValue(label, text, UiTheme.Text);
    }

    private static void SetValue(Label label, string text, Color color)
    {
        if (!string.Equals(label.Text, text, StringComparison.Ordinal))
        {
            label.Text = text;
        }

        if (label.ForeColor != color)
        {
            label.ForeColor = color;
        }
    }

    private static bool IsNormalStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        if (ContainsStatusText(status, "cannot") ||
            ContainsStatusText(status, "changed while") ||
            ContainsStatusText(status, "unreadable") ||
            ContainsStatusText(status, "lost") ||
            ContainsStatusText(status, "missing") ||
            ContainsStatusText(status, "unavailable"))
        {
            return false;
        }

        if (ContainsStatusText(status, "not ready") ||
            ContainsStatusText(status, "pending") ||
            ContainsStatusText(status, "scanning") ||
            ContainsStatusText(status, "found signature but not"))
        {
            return false;
        }

        return status.StartsWith("attached to Terraria", StringComparison.OrdinalIgnoreCase) ||
            status.StartsWith("ready", StringComparison.OrdinalIgnoreCase) ||
            status.StartsWith("timer ready", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsStatusText(string status, string value)
    {
        return status.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private static void SetSequenceText(TextBox textBox, string text)
    {
        if (!string.Equals(textBox.Text, text, StringComparison.Ordinal))
        {
            textBox.Text = text;
        }
    }

    private static TableLayoutPanel CreateSection(SettingsForm owner, string title)
    {
        return SettingsUiFactory.For(owner).CreateSection(title);
    }

    private static FlowLayoutPanel CreateActionBar(SettingsForm owner)
    {
        return SettingsUiFactory.For(owner).CreateActionBar();
    }

    private static TableLayoutPanel CreateGrid(SettingsForm owner)
    {
        SettingsUiFactory factory = SettingsUiFactory.For(owner);
        return factory.CreateGrid(
            SettingsUiFactory.ColumnStyleAbsolute(290f),
            SettingsUiFactory.ColumnStylePercent(100f));
    }

    private static void AddValueRow(TableLayoutPanel grid, SettingsForm owner, string label, Label valueLabel)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));
        grid.Controls.Add(CreateRowLabel(owner, label), 0, row);
        grid.Controls.Add(valueLabel, 1, row);
    }

    private static Label CreateRowLabel(SettingsForm owner, string text)
    {
        return SettingsUiFactory.For(owner).CreateRowLabel(text);
    }

    private static Label CreateValueLabel()
    {
        return new SettingsUiFactory(static key => key).CreateValueLabel();
    }

    private static Label CreateMutedLabel(SettingsForm owner, string text)
    {
        return SettingsUiFactory.For(owner).CreateMutedLabel(text);
    }

    private static TextBox CreateMultilineValueBox(int height)
    {
        return new SettingsUiFactory(static key => key).CreateMultilineValueBox(height);
    }

    private static Button CreateActionButton(SettingsForm owner, string text)
    {
        return SettingsUiFactory.For(owner).CreateActionButton(text);
    }

    private static void AddSection(TableLayoutPanel parent, Control section)
    {
        SettingsUiFactory.AddSection(parent, section);
    }

    private static void AddSectionControl(TableLayoutPanel section, Control control)
    {
        SettingsUiFactory.AddSectionControl(section, control);
    }
}
