namespace TerrariaSplit.Infrastructure;

public sealed class StaticAppLogger : IAppLogger
{
    public static StaticAppLogger Instance { get; } = new();

    private StaticAppLogger()
    {
    }

    public void Info(string message)
    {
        AppLogger.Info(message);
    }

    public void Error(Exception exception, string message)
    {
        AppLogger.Error(exception, message);
    }
}
