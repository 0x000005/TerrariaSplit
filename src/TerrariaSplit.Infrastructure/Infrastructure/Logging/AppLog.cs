using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace TerrariaSplit.Infrastructure;

public static class AppLog
{
    public const string LevelEnvironmentVariable = "TERRARIA_SPLIT_LOG_LEVEL";
    private static readonly AsyncLocal<OperationContext?> Operation = new();
    private static AppLogOptions? startupOptions;
    private static ILoggerFactory? externalFactory;
    private static readonly Lazy<AppLogProvider> SharedProvider = new(() => new AppLogProvider(startupOptions ?? new AppLogOptions
    {
        MinimumLevel = ParseLevel(Environment.GetEnvironmentVariable(LevelEnvironmentVariable))
    }));
    private static readonly Lazy<ILoggerFactory> SharedFactory = new(() => LoggerFactory.Create(builder =>
        builder.SetMinimumLevel(LogLevel.Trace).AddProvider(Provider)));

    public static AppLogProvider Provider => SharedProvider.Value;
    public static ILoggerFactory Factory => externalFactory ?? SharedFactory.Value;
    public static void Initialize(AppLogOptions options)
    {
        if (SharedProvider.IsValueCreated) throw new InvalidOperationException("Logging has already been initialized.");
        startupOptions = options;
    }
    public static void BindFactory(ILoggerFactory factory) => externalFactory = factory;
    public static string? OperationId => Operation.Value?.Id;
    public static ILogger For(string category) => Factory.CreateLogger(category);
    public static ILogger ForSource([System.Runtime.CompilerServices.CallerFilePath] string source = "")
        => For(Path.GetFileNameWithoutExtension(source));
    public static void Debug(string code, object? data = null,
        [System.Runtime.CompilerServices.CallerFilePath] string source = "")
        => Event(LogLevel.Debug, Path.GetFileNameWithoutExtension(source), code, data);
    internal static OperationContext? CurrentOperation => Operation.Value;

    public static LogLevel ParseLevel(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "DEBUG" => LogLevel.Debug,
        "INFO" => LogLevel.Information,
        "WARN" => LogLevel.Warning,
        "NONE" => LogLevel.None,
        _ => LogLevel.Error
    };

    public static void SetMinimumLevel(string? value) => Provider.MinimumLevel = ParseLevel(value);

    public static IDisposable BeginOperation(string name, object? parameters = null)
    {
        OperationContext? previous = Operation.Value;
        Operation.Value = new(Guid.NewGuid().ToString("N"), name, null,
            Provider.MinimumLevel == LogLevel.None ? null : Provider.Freeze(parameters));
        var clock = Stopwatch.StartNew();
        Event(LogLevel.Information, "App", "operation.start", new { name, parameters });
        return new CallbackScope(() =>
        {
            Event(LogLevel.Debug, "App", "operation.end", new { name, elapsedMs = clock.Elapsed.TotalMilliseconds });
            Operation.Value = previous;
        });
    }

    public static IDisposable BeginStep(string stage)
    {
        OperationContext? previous = Operation.Value;
        if (previous is not null) Operation.Value = previous with { Stage = stage };
        return new CallbackScope(() => Operation.Value = previous);
    }

    public static void Event(LogLevel level, string category, string code, object? data = null, Exception? exception = null)
        => For(category).Log(level, new EventId(0, code), data, exception, (_, _) => code);

    public static AppFault Failure(string category, string code, string message, string diagnostic, Exception? exception = null)
    {
        OperationContext? operation = Operation.Value;
        var fault = new AppFault(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, code, message,
            diagnostic, operation?.Id, operation?.Name, operation?.Stage ?? exception?.Data["AutomationStage"]?.ToString(),
            exception?.ToString(), Provider.LogPath) { LoggingDisabled = Provider.MinimumLevel == LogLevel.None };
        For(category).Log(LogLevel.Error, new EventId(0, code), fault, exception, (value, _) => value.Message);
        return fault;
    }

    public static void LibraryLoaded(string path, int abi)
    {
        if (Provider.MinimumLevel == LogLevel.None) return;
        try
        {
            using var input = File.OpenRead(path);
            var identity = new
            {
                path, abi, sha256 = Convert.ToHexString(SHA256.HashData(input))
            };
            Provider.SetDependency("ResourceJudge", identity);
            Event(LogLevel.Information, "Filter", "native.library.loaded", identity);
        }
        catch (Exception exception)
        {
            Event(LogLevel.Warning, "Filter", "native.library.identity-unavailable", new { path, abi }, exception);
        }
    }

    internal sealed record OperationContext(string Id, string Name, string? Stage, System.Text.Json.JsonElement? Parameters);
    private sealed class CallbackScope(Action callback) : IDisposable
    {
        private Action? onDispose = callback;
        public void Dispose() => Interlocked.Exchange(ref onDispose, null)?.Invoke();
    }
}
