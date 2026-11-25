namespace TerrariaSplit.Terraria.Automation;

public sealed record AutomationResult(
    bool Succeeded,
    bool Cancelled,
    string UserMessage,
    string DiagnosticMessage,
    Exception? Exception = null)
{
    public bool Failed => !Succeeded && !Cancelled;

    public string FailureCode { get; init; } = "automation.failed";
    public string? Stage { get; init; }
    public AppFault? Fault { get; init; }

    public AutomationResult RecordFailure()
    {
        if (!Failed || Fault is not null) return this;
        using var stage = AppLog.BeginStep(Stage ?? Exception?.Data["AutomationStage"]?.ToString() ?? "Workflow");
        return this with { Fault = AppLog.Failure("Automation", FailureCode, UserMessage, DiagnosticMessage, Exception) };
    }

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
        Exception? exception = null,
        string failureCode = "automation.failed",
        string? stage = null)
    {
        return new AutomationResult(false, false, userMessage, diagnostic, exception)
        {
            FailureCode = failureCode,
            Stage = stage
        };
    }
}
