namespace TerrariaSplit.Infrastructure;

internal readonly record struct OperationResult(bool Succeeded, string Message)
{
    public bool Failed => !Succeeded;

    public static OperationResult Success()
    {
        return new OperationResult(true, string.Empty);
    }

    public static OperationResult Failure(string message)
    {
        return new OperationResult(false, message);
    }
}
