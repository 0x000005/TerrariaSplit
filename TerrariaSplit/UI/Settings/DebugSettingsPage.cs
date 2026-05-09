using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit;

internal static class DebugSettingsPage
{
    private const int RefreshIntervalMilliseconds = 500;
    private const int SummaryHeight = 220;

    public static Control Build(SettingsForm owner)
    {
        Label autoRefreshValue = CreateValueLabel();
        Label lastUpdatedValue = CreateValueLabel();
        Label processDetectedValue = CreateValueLabel();
        Label processIdValue = CreateValueLabel();
        Label processStartTimeValue = CreateValueLabel();
        Label windowDetectedValue = CreateValueLabel();
        Label windowHandleValue = CreateValueLabel();
        Label windowTitleValue = CreateValueLabel();
        Label respondingValue = CreateValueLabel();
        Label visibleValue = CreateValueLabel();
        Label minimizedValue = CreateValueLabel();
        Label maximizedValue = CreateValueLabel();
        Label foregroundValue = CreateValueLabel();
        Label windowBoundsValue = CreateValueLabel();
        Label clientSizeValue = CreateValueLabel();
        Label windowStatusValue = CreateValueLabel();
        Label watcherAttachedValue = CreateValueLabel();
        Label memoryReadyValue = CreateValueLabel();
        Label bossFlagsValue = CreateValueLabel();
        Label gameStateValue = CreateValueLabel();
        Label watcherStatusValue = CreateValueLabel();
        Label supportedVersionValue = CreateValueLabel();
        Label processArchitectureValue = CreateValueLabel();
        Label processPathValue = CreateValueLabel();
        Label processVersionValue = CreateValueLabel();
        Label mainModuleBaseValue = CreateValueLabel();
        Label mainModuleSizeValue = CreateValueLabel();
        Label signatureProfileValue = CreateValueLabel();
        Label scanAttemptsValue = CreateValueLabel();
        Label lastScanValue = CreateValueLabel();
        Label scanScopeValue = CreateValueLabel();
        Label scanPageStatsValue = CreateValueLabel();
        Label scanFailuresValue = CreateValueLabel();
        Label signatureResultValue = CreateValueLabel();
        Label updateTimeAddressValue = CreateValueLabel();
        Label gameMenuAddressValue = CreateValueLabel();
        Label gameMenuAddressSecondaryValue = CreateValueLabel();
        Label bossFlagsAddressValue = CreateValueLabel();
        Label hardmodeAddressValue = CreateValueLabel();
        Label failureStageValue = CreateValueLabel();
        Label compatibilityHintValue = CreateValueLabel();
        TextBox diagnosticSummaryValue = CreateSummaryTextBox();

        var watcher = new TerrariaWorldWatcher();

        Control page = owner.BuildScrollPage(content =>
        {
            TableLayoutPanel summarySection = CreateSection(owner, "Watcher Diagnostics");
            AddSectionControl(summarySection, CreateSummaryLabel(owner, "Diagnostic summary"));
            AddSectionControl(summarySection, diagnosticSummaryValue);
            AddSection(content, summarySection);

            TableLayoutPanel windowSection = CreateSection(owner, "Window Detection");
            TableLayoutPanel windowGrid = CreateGrid();
            AddValueRow(windowGrid, owner, "Auto refresh", autoRefreshValue);
            AddValueRow(windowGrid, owner, "Last updated", lastUpdatedValue);
            AddValueRow(windowGrid, owner, "Terraria process", processDetectedValue);
            AddValueRow(windowGrid, owner, "PID", processIdValue);
            AddValueRow(windowGrid, owner, "Start time", processStartTimeValue);
            AddValueRow(windowGrid, owner, "Window", windowDetectedValue);
            AddValueRow(windowGrid, owner, "Window handle", windowHandleValue);
            AddValueRow(windowGrid, owner, "Window title", windowTitleValue);
            AddValueRow(windowGrid, owner, "Responding", respondingValue);
            AddValueRow(windowGrid, owner, "Visible", visibleValue);
            AddValueRow(windowGrid, owner, "Minimized", minimizedValue);
            AddValueRow(windowGrid, owner, "Maximized", maximizedValue);
            AddValueRow(windowGrid, owner, "Foreground", foregroundValue);
            AddValueRow(windowGrid, owner, "Window bounds", windowBoundsValue);
            AddValueRow(windowGrid, owner, "Client size", clientSizeValue);
            AddValueRow(windowGrid, owner, "Status", windowStatusValue);
            AddSectionControl(windowSection, windowGrid);
            AddSection(content, windowSection);

            TableLayoutPanel watcherSection = CreateSection(owner, "Watcher State");
            TableLayoutPanel watcherGrid = CreateGrid();
            AddValueRow(watcherGrid, owner, "Watcher attached", watcherAttachedValue);
            AddValueRow(watcherGrid, owner, "Memory ready", memoryReadyValue);
            AddValueRow(watcherGrid, owner, "Boss flags", bossFlagsValue);
            AddValueRow(watcherGrid, owner, "Game state", gameStateValue);
            AddValueRow(watcherGrid, owner, "Supported version", supportedVersionValue);
            AddValueRow(watcherGrid, owner, "Process architecture", processArchitectureValue);
            AddValueRow(watcherGrid, owner, "Process path", processPathValue);
            AddValueRow(watcherGrid, owner, "Process version", processVersionValue);
            AddValueRow(watcherGrid, owner, "Main module base", mainModuleBaseValue);
            AddValueRow(watcherGrid, owner, "Main module size", mainModuleSizeValue);
            AddValueRow(watcherGrid, owner, "Signature profile", signatureProfileValue);
            AddValueRow(watcherGrid, owner, "Scan attempts", scanAttemptsValue);
            AddValueRow(watcherGrid, owner, "Last scan", lastScanValue);
            AddValueRow(watcherGrid, owner, "Scan scope", scanScopeValue);
            AddValueRow(watcherGrid, owner, "Scan page stats", scanPageStatsValue);
            AddValueRow(watcherGrid, owner, "Scan failures", scanFailuresValue);
            AddValueRow(watcherGrid, owner, "Signature result", signatureResultValue);
            AddValueRow(watcherGrid, owner, "UpdateTime address", updateTimeAddressValue);
            AddValueRow(watcherGrid, owner, "gameMenu address", gameMenuAddressValue);
            AddValueRow(watcherGrid, owner, "gameMenu address 2", gameMenuAddressSecondaryValue);
            AddValueRow(watcherGrid, owner, "Boss flags address", bossFlagsAddressValue);
            AddValueRow(watcherGrid, owner, "Hardmode address", hardmodeAddressValue);
            AddValueRow(watcherGrid, owner, "Failure stage", failureStageValue);
            AddValueRow(watcherGrid, owner, "Compatibility hint", compatibilityHintValue);
            AddValueRow(watcherGrid, owner, "Status", watcherStatusValue);
            AddSectionControl(watcherSection, watcherGrid);
            AddSection(content, watcherSection);
        });

        void Refresh()
        {
            TerrariaWindowSnapshot window = TerrariaWindowProbe.Read();
            TerrariaWatchSnapshot snapshot = watcher.Poll();
            TerrariaWatcherDiagnostics diagnostics = watcher.GetDiagnostics();

            SetValue(autoRefreshValue, $"{RefreshIntervalMilliseconds} ms");
            SetValue(lastUpdatedValue, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));

            SetBool(processDetectedValue, owner, window.HasProcess);
            SetValue(processIdValue, window.ProcessId?.ToString(CultureInfo.InvariantCulture) ?? owner.Localize("Unknown"));
            SetValue(
                processStartTimeValue,
                window.ProcessStartTime?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? owner.Localize("Unknown"));

            SetBool(windowDetectedValue, owner, window.HasWindow);
            SetValue(windowHandleValue, window.HasWindow ? $"0x{window.WindowHandle.ToInt64():X}" : owner.Localize("Unknown"));
            SetValue(windowTitleValue, string.IsNullOrWhiteSpace(window.WindowTitle) ? owner.Localize("Unknown") : window.WindowTitle);
            SetOptionalBool(respondingValue, owner, window.HasProcess ? window.IsResponding : null);
            SetOptionalBool(visibleValue, owner, window.HasWindow ? window.IsVisible : null);
            SetOptionalBool(minimizedValue, owner, window.HasWindow ? window.IsMinimized : null);
            SetOptionalBool(maximizedValue, owner, window.HasWindow ? window.IsMaximized : null);
            SetOptionalBool(foregroundValue, owner, window.HasWindow ? window.IsForeground : null);
            SetValue(windowBoundsValue, FormatBounds(window.WindowBounds, owner));
            SetValue(clientSizeValue, FormatSize(window.ClientSize, owner));
            SetValue(windowStatusValue, LocalizeStatus(window.Status, owner));

            SetBool(watcherAttachedValue, owner, snapshot.IsAttached);
            SetBool(memoryReadyValue, owner, snapshot.IsReady);
            SetState(bossFlagsValue, owner, snapshot.BossStates.Skeletron.HasValue ? "Ready" : "Pending");
            SetState(gameStateValue, owner, FormatGameState(snapshot.IsGameMenu));
            SetValue(supportedVersionValue, diagnostics.SupportedVersion);
            SetValue(processArchitectureValue, diagnostics.ProcessArchitecture);
            SetValue(processPathValue, diagnostics.ProcessPath ?? owner.Localize("Unknown"));
            SetValue(processVersionValue, diagnostics.ProcessVersion ?? owner.Localize("Unknown"));
            SetValue(mainModuleBaseValue, FormatAddress(diagnostics.MainModuleBaseAddress, owner));
            SetValue(mainModuleSizeValue, FormatByteCount(diagnostics.MainModuleSize, owner));
            SetValue(signatureProfileValue, owner.Localize(diagnostics.SignatureProfile));
            SetValue(scanAttemptsValue, diagnostics.SignatureScanAttempts.ToString(CultureInfo.InvariantCulture));
            SetValue(lastScanValue, FormatTimestamp(diagnostics.LastSignatureScanUtc, owner));
            SetValue(scanScopeValue, owner.Localize(diagnostics.LastSignatureScan?.ScopeDescription ?? Terraria1456Memory.SignatureScanScopeLabel));
            SetValue(scanPageStatsValue, FormatScanStats(diagnostics.LastSignatureScan, owner));
            SetValue(scanFailuresValue, FormatScanFailures(diagnostics.LastSignatureScan, owner));
            SetValue(signatureResultValue, FormatSignatureResult(diagnostics, owner));
            SetValue(updateTimeAddressValue, FormatAddress(diagnostics.UpdateTimeAddress, owner));
            SetValue(gameMenuAddressValue, FormatAddress(diagnostics.GameMenuAddress, owner));
            SetValue(gameMenuAddressSecondaryValue, FormatAddress(diagnostics.GameMenuSecondaryAddress, owner));
            SetValue(bossFlagsAddressValue, FormatAddress(diagnostics.BossFlagsBaseAddress, owner));
            SetValue(hardmodeAddressValue, FormatAddress(diagnostics.HardmodeAddress, owner));
            SetValue(failureStageValue, LocalizeStage(diagnostics.Stage, owner));
            SetValue(compatibilityHintValue, owner.Localize(diagnostics.CompatibilityHint));
            SetValue(watcherStatusValue, LocalizeStatus(snapshot.Status, owner));
            diagnosticSummaryValue.Text = BuildDiagnosticSummary(window, snapshot, diagnostics, owner);
        }

        var refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = RefreshIntervalMilliseconds
        };
        refreshTimer.Tick += (_, _) => Refresh();

        page.Disposed += (_, _) =>
        {
            refreshTimer.Stop();
            refreshTimer.Dispose();
            watcher.Dispose();
        };

        Refresh();
        refreshTimer.Start();
        return page;
    }

    private static string BuildDiagnosticSummary(
        TerrariaWindowSnapshot window,
        TerrariaWatchSnapshot snapshot,
        TerrariaWatcherDiagnostics diagnostics,
        SettingsForm owner)
    {
        return string.Join(
            Environment.NewLine,
            [
                string.Format(owner.Localize("Process: PID {0} | start {1} | arch {2}"), FormatProcessId(snapshot.ProcessId, owner), FormatDateTime(window.ProcessStartTime, owner), diagnostics.ProcessArchitecture),
                string.Format(owner.Localize("Path: {0}"), diagnostics.ProcessPath ?? owner.Localize("Unknown")),
                string.Format(owner.Localize("Version: {0} | module base {1} | module size {2}"), diagnostics.ProcessVersion ?? owner.Localize("Unknown"), FormatAddress(diagnostics.MainModuleBaseAddress, owner), FormatByteCount(diagnostics.MainModuleSize, owner)),
                string.Format(owner.Localize("Watcher: attached {0} | ready {1} | game state {2}"), FormatBool(snapshot.IsAttached, owner), FormatBool(snapshot.IsReady, owner), owner.Localize(FormatGameState(snapshot.IsGameMenu))),
                string.Format(owner.Localize("Signature: {0} | profile {1} | target {2}"), FormatSignatureResult(diagnostics, owner), owner.Localize(diagnostics.SignatureProfile), diagnostics.SupportedVersion),
                string.Format(owner.Localize("Scan: attempts {0} | last {1}"), diagnostics.SignatureScanAttempts.ToString(CultureInfo.InvariantCulture), FormatTimestamp(diagnostics.LastSignatureScanUtc, owner)),
                string.Format(owner.Localize("Pages: {0}"), FormatScanStats(diagnostics.LastSignatureScan, owner)),
                string.Format(owner.Localize("Failures: {0}"), FormatScanFailures(diagnostics.LastSignatureScan, owner)),
                string.Format(owner.Localize("Pointers: UpdateTime {0} | gameMenu {1} | gameMenu2 {2} | bossFlags {3} | hardmode {4}"), FormatAddress(diagnostics.UpdateTimeAddress, owner), FormatAddress(diagnostics.GameMenuAddress, owner), FormatAddress(diagnostics.GameMenuSecondaryAddress, owner), FormatAddress(diagnostics.BossFlagsBaseAddress, owner), FormatAddress(diagnostics.HardmodeAddress, owner)),
                string.Format(owner.Localize("Hint: {0}"), owner.Localize(diagnostics.CompatibilityHint)),
                string.Format(owner.Localize("Watcher status: {0}"), LocalizeStatus(snapshot.Status, owner)),
                string.Format(owner.Localize("Window status: {0}"), LocalizeStatus(window.Status, owner))
            ]);
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
            owner.Localize("private {0}/{1} scanned, {2} read; image {3}/{4} scanned, {5} read"),
            value.PrivateExecutablePagesScanned,
            value.PrivateExecutablePagesSeen,
            FormatBytes(value.PrivateExecutableBytesScanned),
            value.ImageExecutablePagesScanned,
            value.ImageExecutablePagesSeen,
            FormatBytes(value.ImageExecutableBytesScanned));
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

    private static string FormatSignatureResult(TerrariaWatcherDiagnostics diagnostics, SettingsForm owner)
    {
        if (diagnostics.UpdateTimeAddress != IntPtr.Zero)
        {
            return string.Format(owner.Localize("Matched at {0}"), FormatAddress(diagnostics.UpdateTimeAddress, owner));
        }

        return diagnostics.SignatureScanAttempts > 0 ? owner.Localize("Missing") : owner.Localize("Pending");
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

    private static void SetBool(Label label, SettingsForm owner, bool value)
    {
        SetValue(label, FormatBool(value, owner));
        label.ForeColor = value ? UiTheme.Accent : UiTheme.Text;
    }

    private static void SetOptionalBool(Label label, SettingsForm owner, bool? value)
    {
        if (!value.HasValue)
        {
            SetValue(label, owner.Localize("Unknown"));
            label.ForeColor = UiTheme.MutedText;
            return;
        }

        SetBool(label, owner, value.Value);
    }

    private static void SetState(Label label, SettingsForm owner, string key)
    {
        SetValue(label, owner.Localize(key));
        label.ForeColor = string.Equals(key, "Ready", StringComparison.OrdinalIgnoreCase)
            ? UiTheme.Accent
            : UiTheme.Text;
    }

    private static void SetValue(Label label, string text)
    {
        label.Text = text;
        if (label.ForeColor != UiTheme.MutedText && label.ForeColor != UiTheme.Accent)
        {
            label.ForeColor = UiTheme.Text;
        }
    }

    private static TableLayoutPanel CreateSection(SettingsForm owner, string title)
    {
        var section = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.Surface,
            ColumnCount = 1,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 18),
            Padding = new Padding(22, 18, 22, 20)
        };
        UiTheme.EnableDoubleBuffering(section);
        section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var label = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = UiTheme.FormFont(13f, FontStyle.Bold),
            ForeColor = UiTheme.Text,
            Margin = new Padding(0, 0, 0, 14),
            Text = owner.Localize(title)
        };

        AddSectionControl(section, label);
        return section;
    }

    private static TableLayoutPanel CreateGrid()
    {
        var grid = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.Surface,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        UiTheme.EnableDoubleBuffering(grid);
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        return grid;
    }

    private static void AddValueRow(TableLayoutPanel grid, SettingsForm owner, string label, Label valueLabel)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(CreateRowLabel(owner, label), 0, row);
        grid.Controls.Add(valueLabel, 1, row);
    }

    private static Label CreateRowLabel(SettingsForm owner, string text)
    {
        return new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.Text,
            Margin = new Padding(0, 8, 14, 8),
            Text = owner.Localize(text),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Label CreateValueLabel()
    {
        return new Label
        {
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.Text,
            Margin = new Padding(0, 8, 0, 8),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Label CreateSummaryLabel(SettingsForm owner, string text)
    {
        return new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.MutedText,
            Margin = new Padding(0, 0, 0, 10),
            Text = owner.Localize(text),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static TextBox CreateSummaryTextBox()
    {
        return new TextBox
        {
            BackColor = UiTheme.Field,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Top,
            Font = UiTheme.FormFont(9.5f),
            ForeColor = UiTheme.Text,
            Height = SummaryHeight,
            Margin = Padding.Empty,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            ShortcutsEnabled = true,
            TabStop = false,
            WordWrap = true
        };
    }

    private static void AddSection(TableLayoutPanel parent, Control section)
    {
        int row = parent.RowCount++;
        parent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        parent.Controls.Add(section, 0, row);
    }

    private static void AddSectionControl(TableLayoutPanel section, Control control)
    {
        int row = section.RowCount++;
        section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        section.Controls.Add(control, 0, row);
    }
}
