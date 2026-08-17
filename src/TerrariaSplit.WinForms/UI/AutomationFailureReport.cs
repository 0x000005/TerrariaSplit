namespace TerrariaSplit.UI;

internal static class AutomationFailureReport
{
    public static string BuildSummary(AutomationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        List<string> sections = [];
        AddSection(
            sections,
            "Failure type",
            result.Exception is null
                ? "Automation step failure"
                : "Internal code exception");
        AddSection(sections, "Reason", result.UserMessage);
        if (!string.Equals(
                result.DiagnosticMessage?.Trim(),
                result.UserMessage?.Trim(),
                StringComparison.Ordinal))
        {
            AddSection(sections, "Diagnostic", result.DiagnosticMessage);
        }

        if (sections.Count == 1)
        {
            AddSection(sections, "Diagnostic", "No additional failure details were provided.");
        }

        return string.Join(Environment.NewLine + Environment.NewLine, sections);
    }

    public static bool TryBuild(
        AutomationResult result,
        out string report)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.UseDetailedFailureReport)
        {
            report = string.Empty;
            return false;
        }

        List<string> sections = [];
        AddSection(
            sections,
            "Failure type",
            result.Exception is null
                ? "Advanced seed pre-screen failure"
                : "Advanced seed pre-screen internal exception");
        if (result.Exception is not null)
        {
            AddSection(sections, "Exception", result.Exception.ToString());
        }
        AddSection(sections, "Diagnostic", result.DiagnosticMessage);
        if (!string.Equals(
                result.DiagnosticMessage?.Trim(),
                result.UserMessage?.Trim(),
                StringComparison.Ordinal))
        {
            AddSection(sections, "User message", result.UserMessage);
        }

        report = string.Join(Environment.NewLine + Environment.NewLine, sections);
        return true;
    }

    private static void AddSection(
        ICollection<string> sections,
        string title,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            sections.Add(title + ":" + Environment.NewLine + value.Trim());
        }
    }
}
