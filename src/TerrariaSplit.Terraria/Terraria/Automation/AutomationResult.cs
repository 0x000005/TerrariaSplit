namespace TerrariaSplit.Terraria.Automation;

public sealed record AutomationResult(
    bool Succeeded,
    bool Cancelled,
    string UserMessage,
    string DiagnosticMessage,
    Exception? Exception = null)
{
    public bool Failed => !Succeeded && !Cancelled;

    public static AutomationResult Success(string diagnostic = "")
    {
        return new AutomationResult(true, false, string.Empty, diagnostic);
    }

    public static AutomationResult CancelledByUser(string diagnostic = "Automation was cancelled.")
    {
        return new AutomationResult(false, true, string.Empty, diagnostic);
    }

    public static AutomationResult Failure(
        string userMessage,
        string diagnostic,
        Exception? exception = null)
    {
        return new AutomationResult(false, false, userMessage, diagnostic, exception);
    }
}
