namespace TerrariaSplit;

internal static class AppLogger
{
    private static readonly object Lock = new();

    public static string LogPath => Path.Combine(AppContext.BaseDirectory, "terrariasplit.log");

    public static void Error(Exception exception, string message)
    {
        try
        {
            lock (Lock)
            {
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
}
