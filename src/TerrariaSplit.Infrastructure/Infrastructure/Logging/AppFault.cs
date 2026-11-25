namespace TerrariaSplit.Infrastructure;

public sealed record AppFault(
    string ErrorId,
    DateTimeOffset Utc,
    string Code,
    string Message,
    string Diagnostic,
    string? OperationId,
    string? Operation,
    string? Stage,
    string? Exception,
    string LogPath)
{
    public bool LoggingDisabled { get; init; }
    public string ToReport() => string.Join(Environment.NewLine + Environment.NewLine,
        new[]
        {
            "Error ID: " + ErrorId,
            "Failure code: " + Code,
            "Operation: " + (Operation ?? "Not specified"),
            "Operation ID: " + (OperationId ?? "Not specified"),
            "Stage: " + (Stage ?? "Not specified"),
            "Reason:" + Environment.NewLine + Message,
            "Diagnostic:" + Environment.NewLine + Diagnostic,
            string.IsNullOrWhiteSpace(Exception) ? null : "Exception:" + Environment.NewLine + Exception,
            LoggingDisabled ? "Logging: Disabled." : "Log file: " + LogPath
        }.Where(section => section is not null));
}
