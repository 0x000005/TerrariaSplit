namespace TerrariaSplit.Infrastructure;

internal static class AppLogger
{
    public const string EnableLogEnvironmentVariable = "TERRARIA_SPLIT_ENABLE_LOG";

    private static readonly object Lock = new();

    public static bool IsEnabled => IsEnabledValue(Environment.GetEnvironmentVariable(EnableLogEnvironmentVariable));

    public static string LogPath => RuntimeDataPaths.LogPath;

    public static void Info(string message)
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(
                    LogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] INFO {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must not break the timer UI or storage paths.
        }
    }

    public static void Error(Exception exception, string message)
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(
                    LogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ERROR {message}{Environment.NewLine}{exception}{Environment.NewLine}");
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
