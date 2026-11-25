namespace TerrariaSplit.UI;

internal static class AutomationFailureReport
{
    public static string Build(AutomationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Fault is not null) return result.Fault.ToReport();
        List<string> sections = [];
        AddSection(
            sections,
            "Failure code", result.FailureCode);
        AddSection(sections, "Reason", result.UserMessage);
        if (!string.Equals(
                result.DiagnosticMessage?.Trim(),
                result.UserMessage?.Trim(),
                StringComparison.Ordinal))
        {
            AddSection(sections, "Diagnostic", result.DiagnosticMessage);
        }

        if (result.Exception is not null) AddSection(sections, "Exception", result.Exception.ToString());

        return string.Join(Environment.NewLine + Environment.NewLine, sections);
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
