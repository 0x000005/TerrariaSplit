namespace TerrariaSplit.Infrastructure;

public interface IAppLogger
{
    void Info(string message);

    void Error(Exception exception, string message);
}
