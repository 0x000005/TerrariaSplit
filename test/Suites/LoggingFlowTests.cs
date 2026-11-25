using System.IO.Compression;
using System.Text.Json;
using TerrariaSplit.Terraria.Automation;

namespace TerrariaSplit.Tests;

internal static class LoggingFlowTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Async("logging persists all recent levels on error without duplicating earlier events", TestSuite.Flow, ErrorHistory);
        yield return TestCase.Async("logging thresholds and no-logging selection control disk output and history capture", TestSuite.Flow, Thresholds);
        yield return TestCase.Async("logging expires and bounds recent history with explicit eviction information", TestSuite.Flow, HistoryLimits);
        yield return TestCase.Async("logging rotates bounded files isolates writers and exports fallback diagnostics", TestSuite.Flow, FilesAndExport);
        yield return TestCase.Async("logging correlates concurrent candidates and records one automation failure", TestSuite.Flow, CorrelationAndFault);
        yield return TestCase.Sync("release logging assemblies embed symbols and retain source locations", TestSuite.Release, ReleaseSymbols);
    }

    private static async Task ErrorHistory(CancellationToken token)
    {
        using var directory = new TestDirectory();
        using var provider = new AppLogProvider(Options(directory));
        ILogger logger = provider.CreateLogger("Automation");
        logger.LogDebug("Button coordinates resolved.");
        logger.LogInformation("World creation started.");
        logger.LogWarning("An optional feature is unavailable.");
        await provider.FlushAsync();
        Check.False(File.Exists(provider.LogPath));
        logger.LogError(new InvalidOperationException("Inner evidence."), "World creation failed.");
        await provider.FlushAsync();
        JsonElement[] first = Records(provider.LogPath);
        Check.Sequence(new[] { "DEBUG", "INFO", "WARN", "ERROR" }, first.Select(item => item.GetProperty("level").GetString()!));
        Check.True(first[^1].GetProperty("exception").GetString()!.Contains("Inner evidence.", StringComparison.Ordinal));
        Check.True(Guid.TryParse(first[^1].GetProperty("errorId").GetString(), out _));
        logger.LogDebug("Another operation started.");
        logger.LogError("Another operation failed.");
        await provider.FlushAsync();
        JsonElement[] final = Records(provider.LogPath);
        Check.Equal(6, final.Length);
        Check.Equal(6, final.Select(item => item.GetProperty("sequence").GetInt64()).Distinct().Count());
    }

    private static async Task Thresholds(CancellationToken token)
    {
        using var directory = new TestDirectory();
        foreach (LogLevel level in new[] { LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.None })
        {
            using var provider = new AppLogProvider(Options(directory, level + ".log") with { MinimumLevel = level });
            ILogger logger = provider.CreateLogger("Test");
            logger.LogDebug("Debug detail.");
            logger.LogInformation("Information detail.");
            logger.LogWarning("Warning detail.");
            await provider.FlushAsync();
            int count = level >= LogLevel.Error ? 0 : 4 - (int)level;
            Check.Equal(count, File.Exists(provider.LogPath) ? Records(provider.LogPath).Length : 0);
            provider.MinimumLevel = LogLevel.None;
            logger.LogError("This must not be captured.");
            provider.MinimumLevel = LogLevel.Error;
            logger.LogError("Logging was re-enabled.");
            await provider.FlushAsync();
            JsonElement[] records = Records(provider.LogPath);
            Check.Equal(count + 1, records.Length);
            Check.False(records.Any(item => item.GetProperty("message").GetString() == "This must not be captured."));
        }
        Check.Equal(LogLevel.None, AppLog.ParseLevel("NONE"));
        Check.Equal("ERROR", DiagnosticLogLevels.Normalize("invalid"));
        Check.Equal("WARN", DiagnosticLogLevels.Normalize(" warn "));
    }

    private static async Task HistoryLimits(CancellationToken token)
    {
        using var directory = new TestDirectory();
        var clock = new LogClock();
        using var provider = new AppLogProvider(Options(directory) with { Clock = clock, RecentByteLimit = 2048 });
        ILogger logger = provider.CreateLogger("Filter");
        logger.LogDebug("Expired candidate.");
        clock.Advance(TimeSpan.FromSeconds(61));
        for (int index = 0; index < 20; index++) logger.LogDebug("Candidate {Candidate}", index);
        logger.LogError("Candidate processing failed.");
        await provider.FlushAsync();
        JsonElement[] records = Records(provider.LogPath);
        Check.False(records.Any(item => item.GetProperty("message").GetString() == "Expired candidate."));
        Check.True(records.Length < 20);
        Check.Equal("ERROR", records[^1].GetProperty("level").GetString());
        Check.True(records[^1].GetProperty("historyEvicted").GetInt64() > 0);
    }

    private static async Task FilesAndExport(CancellationToken token)
    {
        using var directory = new TestDirectory();
        File.WriteAllText(directory.Combine("legacy.log"), "Previous text log.\n");
        using (var migrated = new AppLogProvider(Options(directory, "legacy.log")))
        {
            migrated.CreateLogger("Test").LogError("The new logging system failed.");
            await migrated.FlushAsync();
            Check.Equal(1, Records(migrated.LogPath).Length);
            Check.Equal("Previous text log.\n", File.ReadAllText(directory.Combine("legacy.1.log")));
        }
        using (var provider = new AppLogProvider(Options(directory) with
        {
            FilePath = directory.Combine("timer.log"), MinimumLevel = LogLevel.Debug, FileByteLimit = 4096, FileCount = 3
        }))
        {
            ILogger logger = provider.CreateLogger("Test");
            for (int index = 0; index < 20; index++) logger.LogInformation("Record {Index}: {Detail}", index, new string('x', 512));
            await provider.FlushAsync();
            if (provider.LogPath != directory.Combine("timer.log") || provider.LastWriteError is not null)
                throw new InvalidOperationException("Rotation did not retain its preferred log path: " + provider.LastWriteError);
            Check.Equal(3, Directory.GetFiles(directory.Path, "timer*.log").Length);
            Check.True(Directory.GetFiles(directory.Path, "timer*.log").All(path => new FileInfo(path).Length <= 4096));
            using var another = new AppLogProvider(Options(directory) with { FilePath = provider.LogPath });
            another.CreateLogger("OtherInstance").LogError("A separate writer failed.");
            await another.FlushAsync();
            if (string.Equals(provider.LogPath, another.LogPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Concurrent writers were not isolated. First={provider.LogPath}; FirstError={provider.LastWriteError}; SecondError={another.LastWriteError}");
        }
        File.WriteAllText(directory.Combine("blocked"), "Not a directory.");
        using var fallback = new AppLogProvider(new()
        {
            FilePath = directory.Combine("blocked", "timer.log"), FallbackDirectory = directory.Combine("fallback")
        });
        fallback.CreateLogger("Test").LogError("Fallback test failure.");
        await fallback.FlushAsync();
        Check.Equal(directory.Combine("fallback", "timer.log"), fallback.LogPath);
        string archivePath = directory.Combine("diagnostics.zip");
        await fallback.ExportAsync(archivePath, cancellationToken: token);
        await fallback.ExportAsync(archivePath, cancellationToken: token);
        using var archive = ZipFile.OpenRead(archivePath);
        Check.True(archive.GetEntry("diagnostics.json") is not null);
        Check.True(archive.GetEntry("recent.jsonl") is not null);
        Check.True(archive.GetEntry("timer.log") is not null);
    }

    private static async Task CorrelationAndFault(CancellationToken token)
    {
        using var directory = new TestDirectory();
        using var provider = new AppLogProvider(Options(directory));
        ILogger logger = provider.CreateLogger("Filter");
        using var operation = AppLog.BeginOperation("Concurrent filtering", new { version = "1.4.6.1", threads = 1 });
        string operationId = AppLog.OperationId!;
        await Task.WhenAll(Enumerable.Range(0, 3).Select(async candidate =>
        {
            using var scope = logger.BeginScope(new Dictionary<string, object?> { ["candidateId"] = candidate });
            await Task.Yield();
            logger.LogDebug("Candidate {Candidate} completed.", candidate);
        }));
        logger.LogError("An operation failed.");
        await provider.FlushAsync();
        JsonElement[] records = Records(provider.LogPath);
        Check.True(records.All(item => item.GetProperty("operationId").GetString() == operationId));
        Check.Equal(3, records.Where(item => item.GetProperty("level").GetString() == "DEBUG")
            .Select(item => item.GetProperty("scopes")[0].GetProperty("candidateId").GetInt32()).Distinct().Count());
        AutomationResult failure = AutomationResult.Failure("Could not activate a button.", "Expected page B; actual page A; attempts=3.",
            new InvalidOperationException("Inner failure."), failureCode: "automation.step.timeout", stage: "Confirm player type").RecordFailure();
        Check.True(ReferenceEquals(failure.Fault, failure.RecordFailure().Fault));
        Check.Equal(operationId, failure.Fault!.OperationId);
        Check.Equal("Confirm player type", failure.Fault.Stage);
        Check.True(AutomationFailureReport.Build(failure).Contains(failure.Fault.ErrorId, StringComparison.Ordinal));
        await AppLog.Provider.FlushAsync();
        Check.Equal(1, Records(AppLog.Provider.LogPath).Count(item =>
            item.GetProperty("errorId").GetString() == failure.Fault.ErrorId &&
            item.GetProperty("data").TryGetProperty("errorId", out JsonElement id) && id.GetString() == failure.Fault.ErrorId));
    }

    private static JsonElement[] Records(string path)
    {
        using var input = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
        List<JsonElement> records = [];
        while (input.ReadLine() is string line)
        {
            using var document = JsonDocument.Parse(line);
            if (document.RootElement.TryGetProperty("sequence", out _)) records.Add(document.RootElement.Clone());
        }
        return records.ToArray();
    }

    private static AppLogOptions Options(TestDirectory directory, string name = "timer.log") => new()
    {
        FilePath = directory.Combine(name), FallbackDirectory = directory.Combine("fallback")
    };

    private static void ReleaseSymbols()
    {
        using var assembly = File.OpenRead(typeof(AppLogProvider).Assembly.Location);
        using var reader = new System.Reflection.PortableExecutable.PEReader(assembly);
        Check.True(reader.ReadDebugDirectory().Any(entry =>
            entry.Type == System.Reflection.PortableExecutable.DebugDirectoryEntryType.EmbeddedPortablePdb));
        try
        {
            using var invalid = new AppLogProvider(new() { RecentByteLimit = 0 });
            throw new InvalidOperationException("Expected options validation to fail.");
        }
        catch (ArgumentOutOfRangeException exception)
        {
            System.Diagnostics.StackFrame? frame = new System.Diagnostics.StackTrace(exception, true).GetFrames()
                .FirstOrDefault(item => item.GetMethod()?.DeclaringType == typeof(AppLogProvider));
            Check.True(frame is not null && frame.GetFileLineNumber() > 0);
            Check.True(frame!.GetFileName()!.Contains("TerrariaSplit", StringComparison.Ordinal));
        }
    }

    private sealed class LogClock : TimeProvider
    {
        private long ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ticks;
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 11, 25, 0, 0, 0, TimeSpan.Zero).AddTicks(ticks);
        public void Advance(TimeSpan duration) => ticks += duration.Ticks;
    }
}
