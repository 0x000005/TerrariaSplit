using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace TerrariaSplit.Infrastructure;

/// <summary>Four-level logging with bounded recent history and error-triggered persistence.</summary>
public sealed class AppLogProvider : ILoggerProvider, ISupportExternalScope
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly object sync = new();
    private readonly AppLogOptions options;
    private readonly LinkedList<Entry> recent = new();
    private readonly SortedDictionary<long, Entry> pending = new();
    private readonly List<TaskCompletionSource> flushRequests = [];
    private readonly Dictionary<string, JsonElement?> dependencies = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim wake = new(0, 1);
    private readonly RollingLogWriter writer;
    private readonly Task pump;
    private readonly long origin;
    private readonly string sessionId = Guid.NewGuid().ToString("N");
    private readonly JsonElement environment;
    private IExternalScopeProvider scopes = new LoggerExternalScopeProvider();
    private int minimumLevel;
    private int recentBytes;
    private int pendingBytes;
    private long sequence;
    private long evictedRecent;
    private long droppedPending;
    private bool stopping;
    private bool disposed;
    private string? lastWriteError;

    public AppLogProvider(AppLogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.RecentWindow <= TimeSpan.Zero || options.RecentByteLimit < 1024 ||
            options.PendingByteLimit < 1024 || options.FileByteLimit < 1024 || options.FileCount < 1)
            throw new ArgumentOutOfRangeException(nameof(options));
        this.options = options;
        minimumLevel = (int)Normalize(options.MinimumLevel);
        origin = options.Clock.GetTimestamp();
        environment = JsonSerializer.SerializeToElement(new
        {
            schemaVersion = 1, sessionId, processId = Environment.ProcessId,
            version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(),
            build = System.Reflection.Assembly.GetEntryAssembly()?.GetCustomAttributes(false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion,
            executable = Environment.ProcessPath, baseDirectory = AppContext.BaseDirectory,
            framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            processors = Environment.ProcessorCount, startedUtc = options.Clock.GetUtcNow()
        }, JsonOptions);
        writer = new RollingLogWriter(options, JsonSerializer.Serialize(new
        {
            utc = options.Clock.GetUtcNow(), level = "INFO", category = "App",
            eventCode = "session.start", message = "Logging session started.", data = environment
        }, JsonOptions));
        pump = Task.Run(PumpAsync);
    }

    public LogLevel MinimumLevel
    {
        get => (LogLevel)Volatile.Read(ref minimumLevel);
        set
        {
            lock (sync)
            {
                Volatile.Write(ref minimumLevel, (int)Normalize(value));
                if (value != LogLevel.None) return;
                recent.Clear();
                pending.Clear();
                dependencies.Clear();
                recentBytes = pendingBytes = 0;
                evictedRecent = droppedPending = 0;
            }
        }
    }
    public string LogPath => writer.Path;
    public string? LastWriteError => Volatile.Read(ref lastWriteError) ?? writer.RecoveryReason;
    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => scopes = scopeProvider;
    public void SetDependency(string name, object value)
    {
        JsonElement? snapshot = Freeze(value);
        lock (sync)
        {
            if (MinimumLevel != LogLevel.None && (dependencies.ContainsKey(name) || dependencies.Count < 32))
                dependencies[name] = snapshot;
        }
    }

    internal JsonElement? Freeze(object? data)
    {
        if (data is null) return null;
        try
        {
            JsonElement value = JsonSerializer.SerializeToElement(data, JsonOptions);
            string json = value.GetRawText();
            return Encoding.UTF8.GetByteCount(json) <= 64 * 1024
                ? value
                : JsonSerializer.SerializeToElement(new
                {
                    truncated = true, originalBytes = Encoding.UTF8.GetByteCount(json),
                    preview = json[..Math.Min(json.Length, 16 * 1024)]
                }, JsonOptions);
        }
        catch (Exception exception)
        {
            return JsonSerializer.SerializeToElement(new { serializationError = exception.Message }, JsonOptions);
        }
    }

    private void Capture<TState>(string category, LogLevel level, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (level == LogLevel.None || MinimumLevel == LogLevel.None) return;
        try
        {
            level = Normalize(level);
            string message = formatter(state, exception);
            if (message.Length > 32768) message = message[..32768] + " [truncated]";
            object? data = state;
            if (state is IEnumerable<KeyValuePair<string, object?>> properties)
                data = properties.Where(pair => pair.Key != "{OriginalFormat}")
                    .GroupBy(pair => pair.Key).ToDictionary(group => group.Key, group => group.Last().Value);
            JsonElement? frozen = Freeze(data);
            List<JsonElement?> scopeData = [];
            scopes.ForEachScope((scope, list) => list.Add(Freeze(scope)), scopeData);
            AppLog.OperationContext? operation = AppLog.CurrentOperation;
            DateTimeOffset now = options.Clock.GetUtcNow();
            long timestamp = options.Clock.GetTimestamp();
            lock (sync)
            {
                if (stopping || MinimumLevel == LogLevel.None) return;
                PruneRecent(timestamp);
                var record = new
                {
                    utc = now, elapsedMs = options.Clock.GetElapsedTime(origin, timestamp).TotalMilliseconds,
                    sequence = ++sequence, sessionId, processId = Environment.ProcessId,
                    threadId = Environment.CurrentManagedThreadId, level = LevelName(level), category,
                    eventCode = eventId.Name ?? category + ".message", message,
                    errorId = level == LogLevel.Error ? (state is AppFault fault ? fault.ErrorId : Guid.NewGuid().ToString("N")) : null,
                    operationId = exception?.Data["OperationId"]?.ToString() ?? operation?.Id,
                    operation = exception?.Data["Operation"]?.ToString() ?? operation?.Name,
                    stage = operation?.Stage ?? exception?.Data["AutomationStage"]?.ToString(),
                    operationParameters = level == LogLevel.Error ? operation?.Parameters : null,
                    dependencies = level == LogLevel.Error ? dependencies : null,
                    scopes = scopeData.Count == 0 ? null : scopeData,
                    data = frozen, exception = exception?.ToString(),
                    historyEvicted = level == LogLevel.Error ? evictedRecent : (long?)null
                };
                string json = JsonSerializer.Serialize(record, JsonOptions);
                var entry = new Entry(sequence, timestamp, level, json, Encoding.UTF8.GetByteCount(json) + 1);
                recent.AddLast(entry);
                recentBytes += entry.Bytes;
                if (level == LogLevel.Error)
                {
                    foreach (Entry previous in recent) Enqueue(previous);
                }
                else if (level >= MinimumLevel) Enqueue(entry);
                PruneRecent(timestamp);
            }
            lock (sync) { if (pending.Count > 0) Signal(); }
        }
        catch (Exception failure)
        {
            // A malformed log payload must not fail the operation being diagnosed.
            Volatile.Write(ref lastWriteError, "Could not capture log event: " + failure.Message);
        }
    }

    private void PruneRecent(long now)
    {
        while (recent.First is { } first &&
            (recentBytes > options.RecentByteLimit || options.Clock.GetElapsedTime(first.Value.Timestamp, now) > options.RecentWindow))
        {
            recentBytes -= first.Value.Bytes;
            recent.RemoveFirst();
            evictedRecent++;
        }
    }

    private void Enqueue(Entry entry)
    {
        if (entry.Scheduled) return;
        while (pendingBytes + entry.Bytes > options.PendingByteLimit && pending.Count > 0)
        {
            Entry? discard = pending.Values.FirstOrDefault(candidate => candidate.Level < LogLevel.Error);
            if (discard is null && entry.Level < LogLevel.Error)
            {
                droppedPending++;
                return;
            }
            discard ??= pending.Values.First();
            pending.Remove(discard.Sequence);
            pendingBytes -= discard.Bytes;
            discard.Scheduled = false;
            droppedPending++;
        }
        entry.Scheduled = true;
        pending.Add(entry.Sequence, entry);
        pendingBytes += entry.Bytes;
    }

    private void Signal()
    {
        try { wake.Release(); }
        catch (SemaphoreFullException) { }
    }

    private async Task PumpAsync()
    {
        while (true)
        {
            await wake.WaitAsync().ConfigureAwait(false);
            Entry[] batch;
            TaskCompletionSource[] waiters;
            long dropped;
            bool stop;
            lock (sync)
            {
                batch = pending.Values.ToArray();
                pending.Clear();
                pendingBytes = 0;
                dropped = droppedPending;
                droppedPending = 0;
                waiters = flushRequests.ToArray();
                flushRequests.Clear();
                stop = stopping;
            }
            try
            {
                if (dropped > 0 && MinimumLevel != LogLevel.None) writer.Write(JsonSerializer.Serialize(new
                {
                    utc = options.Clock.GetUtcNow(), sessionId, level = "WARN", category = "Logging",
                    eventCode = "logging.queue.truncated", message = "Pending log history exceeded its capacity.",
                    droppedRecords = dropped
                }, JsonOptions));
                foreach (Entry entry in batch)
                {
                    if (MinimumLevel == LogLevel.None) break;
                    writer.Write(entry.Json);
                    if (options.ConsoleOutput)
                    {
                        try { Console.WriteLine(entry.Json); }
                        catch (IOException exception) { Volatile.Write(ref lastWriteError, "Could not write console output: " + exception.Message); }
                    }
                }
                writer.Flush();
                foreach (var waiter in waiters) waiter.TrySetResult();
            }
            catch (Exception exception)
            {
                Volatile.Write(ref lastWriteError, "Could not write log file: " + exception);
                lock (sync)
                {
                    // Keep failed writes eligible for the next error-triggered save.
                    foreach (Entry entry in batch) entry.Scheduled = false;
                }
                foreach (var waiter in waiters) waiter.TrySetResult();
            }
            if (stop) break;
        }
        writer.Dispose();
    }

    public Task FlushAsync(TimeSpan? timeout = null)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (sync)
        {
            if (stopping) return pump.WaitAsync(timeout ?? TimeSpan.FromSeconds(2));
            flushRequests.Add(completion);
        }
        Signal();
        return completion.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(2));
    }

    public async Task ExportAsync(string destination, AppFault? fault = null, CancellationToken cancellationToken = default)
    {
        try { await FlushAsync().ConfigureAwait(false); }
        catch (TimeoutException) { /* Export the memory buffer even if file I/O is stalled. */ }
        string[] history;
        Dictionary<string, JsonElement?> dependencySnapshot;
        lock (sync)
        {
            PruneRecent(options.Clock.GetTimestamp());
            history = recent.Select(entry => entry.Json).ToArray();
            dependencySnapshot = new(dependencies);
        }
        await Task.Run(() =>
        {
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
                {
                    WriteArchiveText(archive, "diagnostics.json", JsonSerializer.Serialize(new
                    {
                        environment, dependencies = dependencySnapshot, fault, actualLogPath = LogPath, lastWriteError = LastWriteError
                    }, JsonOptions));
                    WriteArchiveText(archive, "recent.jsonl", string.Join('\n', history));
                    foreach (string path in writer.GetPaths())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                            using var target = archive.CreateEntry(System.IO.Path.GetFileName(path)).Open();
                            source.CopyTo(target);
                        }
                        catch (FileNotFoundException) { /* Rotation can replace an archive during export. */ }
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, destination, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }, cancellationToken).ConfigureAwait(false);
    }

    private static void WriteArchiveText(ZipArchive archive, string name, string value)
    {
        using var target = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
        target.Write(value);
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            stopping = true;
        }
        Signal();
        try { pump.Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException) { }
    }

    private static LogLevel Normalize(LogLevel level) => level switch
    {
        <= LogLevel.Debug => LogLevel.Debug,
        LogLevel.Information => LogLevel.Information,
        LogLevel.Warning => LogLevel.Warning,
        LogLevel.None => LogLevel.None,
        _ => LogLevel.Error
    };
    private static string LevelName(LogLevel level) => level switch
    {
        LogLevel.Debug => "DEBUG", LogLevel.Information => "INFO", LogLevel.Warning => "WARN", _ => "ERROR"
    };
    private sealed class Entry(long sequence, long timestamp, LogLevel level, string json, int bytes)
    {
        public long Sequence { get; } = sequence;
        public long Timestamp { get; } = timestamp;
        public LogLevel Level { get; } = level;
        public string Json { get; } = json;
        public int Bytes { get; } = bytes;
        public bool Scheduled { get; set; }
    }
    private sealed class Logger(AppLogProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => owner.scopes.Push(state);
        public bool IsEnabled(LogLevel level) => level != LogLevel.None && owner.MinimumLevel != LogLevel.None;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => owner.Capture(category, logLevel, eventId, state, exception, formatter);
    }
}
