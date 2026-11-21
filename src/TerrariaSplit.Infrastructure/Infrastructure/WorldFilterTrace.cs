using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TerrariaSplit.Infrastructure;

// Structured diagnostics use the same opt-in log file as the general app logger.
public static class WorldFilterTrace
{
    private static readonly object Sync = new();
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    private static bool started;
    public static void Write(string eventName, object data)
    {
        if (!FileAppLogger.Instance.IsEnabled)
        {
            return;
        }

        try
        {
            lock (Sync)
            {
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

    private static void Append(string eventName, object data) => FileAppLogger.Instance.Diagnostic(
        JsonSerializer.Serialize(new { utc = DateTime.UtcNow, monotonicTicks = Stopwatch.GetTimestamp(),
            processId = Environment.ProcessId, threadId = Environment.CurrentManagedThreadId,
            eventName, data }, Options));

    public static void LibraryLoaded(string path, int abi)
    {
        if (!FileAppLogger.Instance.IsEnabled)
        {
            return;
        }

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
