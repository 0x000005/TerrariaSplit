namespace TerrariaSplit.Infrastructure;

public sealed class NullAppLogger : IAppLogger
{
    public static NullAppLogger Instance { get; } = new();

    private NullAppLogger()
    {
    }

    public void Info(string message)
    {
    }

    public void Error(Exception exception, string message)
    {
    }
}
