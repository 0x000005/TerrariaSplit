using System.Drawing;

namespace TerrariaSplit.UI.Settings;

internal static partial class DebugSettingsSnapshotBuilder
{
    private static string LocalizeStage(string stage, Func<string, string> localize)
    {
        if (string.IsNullOrWhiteSpace(stage))
        {
            return localize("Unknown");
        }

        const string startPendingSuffix = "; start pending";
        if (stage.EndsWith(startPendingSuffix, StringComparison.Ordinal))
        {
            string prefix = stage[..^startPendingSuffix.Length];
            return $"{localize(prefix)}\uFF1B{localize("start pending")}";
        }

        return localize(stage);
    }

    private static string LocalizeStatus(string status, Func<string, string> localize)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return localize("Unknown");
        }

        const string processChangedPrefix = "Terraria process changed while reading window state: ";
        const string cannotReadPrefix = "cannot read Terraria process: ";
        const string cannotAttachPrefix = "cannot attach to Terraria process: ";
        const string attachedPidPrefix = "attached to Terraria PID ";
        const string attachedProcessPrefix = "attached to Terraria process";

        if (string.Equals(status, "waiting for Terraria.exe", StringComparison.OrdinalIgnoreCase))
        {
            return localize("waiting for Terraria.exe");
        }

        if (status.StartsWith(processChangedPrefix, StringComparison.Ordinal))
        {
            return string.Format(localize("Terraria process changed while reading window state: {0}"), status[processChangedPrefix.Length..]);
        }

        if (status.StartsWith(cannotReadPrefix, StringComparison.Ordinal))
        {
            return string.Format(localize("cannot read Terraria process: {0}"), status[cannotReadPrefix.Length..]);
        }

        if (status.StartsWith(cannotAttachPrefix, StringComparison.Ordinal))
        {
            return string.Format(localize("cannot attach to Terraria process: {0}"), status[cannotAttachPrefix.Length..]);
        }

        if (status.StartsWith(attachedPidPrefix, StringComparison.Ordinal))
        {
            string remainder = status[attachedPidPrefix.Length..];
            int separatorIndex = remainder.IndexOf(',');
            if (separatorIndex < 0)
            {
                return string.Format(localize("attached to Terraria PID {0}"), remainder.Trim());
            }

            string processId = remainder[..separatorIndex].Trim();
            string detail = remainder[(separatorIndex + 1)..].Trim();
            return string.Format(localize("attached to Terraria PID {0}, {1}"), processId, LocalizeStatusDetail(detail, localize));
        }

        if (status.StartsWith(attachedProcessPrefix, StringComparison.Ordinal))
        {
            string remainder = status[attachedProcessPrefix.Length..].Trim();
            if (string.IsNullOrEmpty(remainder))
            {
                return localize("attached to Terraria process");
            }

            if (remainder.StartsWith(",", StringComparison.Ordinal))
            {
                remainder = remainder[1..].TrimStart();
            }

            return string.Format(localize("attached to Terraria process, {0}"), LocalizeStatusDetail(remainder, localize));
        }

        return localize(status);
    }

    private static string LocalizeStatusDetail(string detail, Func<string, string> localize)
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
            localizedSuffix = "\uFF1B" + localize("return to menu once to arm timer start");
        }

        if (detail.StartsWith(scanMemoryPrefix, StringComparison.Ordinal) &&
            detail.EndsWith(scanMemorySuffix, StringComparison.Ordinal))
        {
            string version = detail.Substring(scanMemoryPrefix.Length, detail.Length - scanMemoryPrefix.Length - scanMemorySuffix.Length);
            return string.Format(localize("scanning for {0} memory"), version) + localizedSuffix;
        }

        if (detail.StartsWith(windowHandleUnavailablePrefix, StringComparison.Ordinal) &&
            detail.EndsWith(windowHandleUnavailableSuffix, StringComparison.Ordinal))
        {
            string handle = detail.Substring(windowHandleUnavailablePrefix.Length, detail.Length - windowHandleUnavailablePrefix.Length - windowHandleUnavailableSuffix.Length);
            return string.Format(localize("window handle 0x{0}, client rect unavailable"), handle) + localizedSuffix;
        }

        if (detail.StartsWith(windowHandlePrefix, StringComparison.Ordinal))
        {
            string handle = detail[windowHandlePrefix.Length..];
            return string.Format(localize("window handle 0x{0}"), handle) + localizedSuffix;
        }

        return localize(detail) + localizedSuffix;
    }

    private static DebugSettingsDisplayValue QuickBool(bool value, Func<string, string> localize)
    {
        return new DebugSettingsDisplayValue(
            FormatBool(value, localize),
            value ? QuickStatusNormalColor : QuickStatusProblemColor);
    }

    private static DebugSettingsDisplayValue QuickGameState(
        bool? isGameMenu,
        Func<string, string> localize)
    {
        Color color = isGameMenu switch
        {
            false => QuickStatusNormalColor,
            true => QuickStatusMenuColor,
            null => QuickStatusProblemColor
        };
        return new DebugSettingsDisplayValue(localize(FormatGameState(isGameMenu)), color);
    }

    private static DebugSettingsDisplayValue QuickStatus(
        string status,
        Func<string, string> localize)
    {
        return new DebugSettingsDisplayValue(
            LocalizeStatus(status, localize),
            IsNormalStatus(status) ? QuickStatusNormalColor : QuickStatusProblemColor);
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
}
