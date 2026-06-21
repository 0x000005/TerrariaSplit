namespace TerrariaSplit.Infrastructure;

internal readonly record struct OperationResult(
    bool Succeeded,
    string? UserMessage = null,
    Exception? Exception = null)
{
    public bool Failed => !Succeeded;

    public string Message => UserMessage ?? string.Empty;

    public static OperationResult Success()
    {
        return new OperationResult(true);
    }

    public static OperationResult Failure(string message, Exception? exception = null)
    {
        return new OperationResult(false, message, exception);
    }
}
