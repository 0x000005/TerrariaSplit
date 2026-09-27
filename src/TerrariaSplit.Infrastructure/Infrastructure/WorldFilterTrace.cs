using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TerrariaSplit.Infrastructure;

// Dedicated diagnostic stream: independent of the optional general app logger.
public static class WorldFilterTrace
{
    private const long MaximumFileBytes = 20 * 1024 * 1024;
    private static readonly object Sync = new();
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    private static bool started;
    public static string LogPath { get; } = Path.Combine(AppContext.BaseDirectory, "Logs",
        $"world-filter-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Environment.ProcessId}.jsonl");

    public static void Write(string eventName, object data)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length >= MaximumFileBytes)
                    File.Move(LogPath, LogPath + ".previous", overwrite: true);
                if (!started)
                {
                    Append("process.start", new { executable = Environment.ProcessPath,
                        baseDirectory = AppContext.BaseDirectory, processors = Environment.ProcessorCount,
                        version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(),
                        framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription });
                    started = true;
                }
                Append(eventName, data);
            }
        }
        catch { /* Diagnostics must never alter filtering or UI behavior. */ }
    }

    private static void Append(string eventName, object data) => File.AppendAllText(LogPath,
        JsonSerializer.Serialize(new { utc = DateTime.UtcNow, monotonicTicks = Stopwatch.GetTimestamp(),
            processId = Environment.ProcessId, threadId = Environment.CurrentManagedThreadId,
            eventName, data }, Options) + Environment.NewLine, Encoding.UTF8);

    public static void LibraryLoaded(string path, int abi)
    {
        try
        {
            using var input = File.OpenRead(path);
            Write("dll.loaded", new { path, abi, sha256 = Convert.ToHexString(SHA256.HashData(input)) });
        }
        catch (Exception ex) { Write("dll.identity-error", new { path, abi, error = ex.Message }); }
    }

    public static void WorkflowMessage(string message, Exception? exception = null)
    {
        if (message.Contains("world", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("seed", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("pyramid", StringComparison.OrdinalIgnoreCase))
            Write("workflow.message", new { message, error = exception?.ToString() });
    }
}
