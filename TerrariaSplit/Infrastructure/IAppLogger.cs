namespace TerrariaSplit.Infrastructure;

internal interface IAppLogger
{
    void Info(string message);

    void Error(Exception exception, string message);
}
