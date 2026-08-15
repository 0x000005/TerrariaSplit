namespace TerrariaSplit.Infrastructure;

public sealed class FileAppLogger : IAppLogger
{
    public const string EnableLogEnvironmentVariable = "TERRARIA_SPLIT_ENABLE_LOG";
    private readonly object sync = new();

    public static FileAppLogger Instance { get; } = new();

    private FileAppLogger()
    {
    }

    public bool IsEnabled => IsEnabledValue(Environment.GetEnvironmentVariable(EnableLogEnvironmentVariable));

    public string LogPath => AppContextRuntimeDataPaths.Default.LogPath;

    public void Info(string message)
    {
        if (!IsEnabled)
        {
            return;
        }

        TryAppend($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] INFO {message}{Environment.NewLine}");
    }

    public void Error(Exception exception, string message)
    {
        if (!IsEnabled)
        {
            return;
        }

        TryAppend(
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ERROR {message}{Environment.NewLine}" +
            $"{exception}{Environment.NewLine}");
    }

    private void TryAppend(string entry)
    {
        try
        {
            lock (sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, entry);
            }
        }
        catch
        {
            // Logging must not break the timer UI or storage paths.
        }
    }

    private static bool IsEnabledValue(string? value)
    {
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
    }
}
