using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TerrariaSplit.Terraria.WorldGeneration;

internal sealed class JungleSeedJudgeWorkerClient : IDisposable, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string workerPath;
    private readonly TimeSpan requestTimeout;
    private readonly SemaphoreSlim requestGate = new(1, 1);
    private Process? process;
    private StringBuilder? stderrBuffer;
    private long nextRequestId;
    private bool disposed;

    public static JungleSeedJudgeWorkerClient CreateDefault(
        TimeSpan? requestTimeout = null)
    {
        return new JungleSeedJudgeWorkerClient(
            JungleSeedJudgeWorkerLocator.ResolvePath(),
            requestTimeout);
    }

    public JungleSeedJudgeWorkerClient(
        string workerPath,
        TimeSpan? requestTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerPath);
        this.workerPath = Path.GetFullPath(workerPath);
        this.requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(5);
        if (this.requestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestTimeout),
                "The worker request timeout must be positive.");
        }
    }

    public async Task<JungleSeedJudgeResult> AnalyzeAsync(
        string seedText,
        JungleSeedJudgeGameMode gameMode,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(seedText);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(requestTimeout);
        try
        {
            await requestGate.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw CreateTimeoutException();
        }

        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            string requestId = Interlocked.Increment(ref nextRequestId)
                .ToString(CultureInfo.InvariantCulture);
            var request = new JungleSeedJudgeRequest(
                JungleSeedJudgeProtocol.Version,
                requestId,
                seedText,
                gameMode);
            string requestJson = JsonSerializer.Serialize(request, JsonOptions);

            try
            {
                Process worker = await EnsureWorkerAsync(timeout.Token)
                    .ConfigureAwait(false);
                timeout.Token.ThrowIfCancellationRequested();
                await worker.StandardInput.WriteLineAsync(
                        requestJson.AsMemory(),
                        timeout.Token)
                    .ConfigureAwait(false);
                await worker.StandardInput.FlushAsync(timeout.Token)
                    .ConfigureAwait(false);
                string? responseJson = await worker.StandardOutput
                    .ReadLineAsync(timeout.Token)
                    .ConfigureAwait(false);
                if (responseJson is null)
                {
                    string detail = await ReadExitedWorkerDetailAsync(
                            worker,
                            timeout.Token)
                        .ConfigureAwait(false);
                    throw new IOException(
                        "Jungle Judge worker closed stdout before responding. " +
                        detail);
                }

                JungleSeedJudgeResult result =
                    JungleSeedJudgeWorkerProtocol.DeserializeResponse(
                        responseJson,
                        requestId);
                return result;
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                ResetWorker();
                throw CreateTimeoutException();
            }
            catch
            {
                ResetWorker();
                throw;
            }
        }
        finally
        {
            requestGate.Release();
        }
    }

    private TimeoutException CreateTimeoutException()
    {
        return new TimeoutException(
            $"Jungle Judge worker request exceeded {requestTimeout.TotalSeconds:F1} seconds.");
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await requestGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            ResetWorker();
        }
        finally
        {
            requestGate.Release();
            requestGate.Dispose();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        requestGate.Wait();
        try
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            ResetWorker();
        }
        finally
        {
            requestGate.Release();
            requestGate.Dispose();
        }
    }

    private async Task<Process> EnsureWorkerAsync(
        CancellationToken cancellationToken)
    {
        if (process is not null)
        {
            try
            {
                if (!process.HasExited)
                {
                    return process;
                }
            }
            catch (InvalidOperationException)
            {
                // The cached process is no longer usable; replace it below.
            }

            ResetWorker();
        }

        Task<StartedWorker> startTask = Task.Run(StartWorker);
        StartedWorker started;
        try
        {
            started = await startTask.WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            _ = startTask.ContinueWith(
                static completed =>
                {
                    if (completed.Status == TaskStatus.RanToCompletion)
                    {
                        StopAndDispose(completed.Result.Process);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            throw;
        }

        process = started.Process;
        stderrBuffer = started.StderrBuffer;
        return started.Process;
    }

    private StartedWorker StartWorker()
    {
        if (!File.Exists(workerPath))
        {
            throw new FileNotFoundException(
                "Terraria Jungle Judge worker was not found.",
                workerPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = workerPath,
            WorkingDirectory =
                Path.GetDirectoryName(workerPath) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false)
        };
        var worker = new Process { StartInfo = startInfo };
        if (!worker.Start())
        {
            worker.Dispose();
            throw new InvalidOperationException(
                "Terraria Jungle Judge worker could not be started.");
        }

        var errorBuffer = new StringBuilder();
        worker.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null)
            {
                return;
            }

            lock (errorBuffer)
            {
                errorBuffer.AppendLine(eventArgs.Data);
            }
        };
        worker.BeginErrorReadLine();
        return new StartedWorker(worker, errorBuffer);
    }

    private async Task<string> ReadExitedWorkerDetailAsync(
        Process worker,
        CancellationToken cancellationToken)
    {
        try
        {
            await worker.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            string stderr = string.Empty;
            StringBuilder? errorBuffer = stderrBuffer;
            if (errorBuffer is not null)
            {
                lock (errorBuffer)
                {
                    stderr = errorBuffer.ToString();
                }
            }
            return $"ExitCode={worker.ExitCode}; stderr={stderr.Trim()}";
        }
        catch (Exception ex)
            when (ex is InvalidOperationException or ObjectDisposedException)
        {
            return ex.Message;
        }
    }

    private void ResetWorker()
    {
        Process? worker = process;
        process = null;
        stderrBuffer = null;
        if (worker is null)
        {
            return;
        }

        try
        {
            StopAndDispose(worker);
        }
        catch (Exception ex)
            when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Debug.WriteLine(
                "Terraria Jungle Judge worker could not be stopped cleanly: " +
                ex);
        }
    }

    private static void StopAndDispose(Process worker)
    {
        try
        {
            if (!worker.HasExited)
            {
                worker.Kill(entireProcessTree: true);
            }
        }
        finally
        {
            worker.Dispose();
        }
    }

    private sealed record StartedWorker(
        Process Process,
        StringBuilder StderrBuffer);
}

internal static class JungleSeedJudgeWorkerLocator
{
    public const string WorkerFileName = "TerrariaSplit.WorldFilter.exe";

    public static string ResolvePath()
    {
        string? configured = Environment.GetEnvironmentVariable(
            "TERRARIA_WORLD_FILTER");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string fullPath = Path.GetFullPath(configured);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }

            throw new FileNotFoundException(
                "Configured Terraria Jungle Judge worker was not found.",
                fullPath);
        }

        foreach (string candidate in EnumerateCandidatePaths())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"Could not locate {WorkerFileName}. Expected it in the application's Tools directory.");
    }

    private static IEnumerable<string> EnumerateCandidatePaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, WorkerFileName);

        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        for (int depth = 0; directory is not null && depth < 8; depth++)
        {
            yield return Path.Combine(
                directory.FullName,
                "TerrariaJungleJudge",
                "out",
                "build",
                "win32-release",
                "Release",
                WorkerFileName);
            directory = directory.Parent;
        }
    }
}

internal static class JungleSeedJudgeWorkerProtocol
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    public static JungleSeedJudgeResult DeserializeResponse(
        string responseJson,
        string expectedRequestId)
    {
        JungleSeedJudgeResult result;
        try
        {
            result = JsonSerializer.Deserialize<JungleSeedJudgeResult>(
                responseJson,
                JsonOptions) ?? throw new InvalidDataException(
                    "Jungle Judge worker returned an empty JSON value.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "Jungle Judge worker returned invalid protocol JSON.",
                ex);
        }

        if (result.ProtocolVersion != JungleSeedJudgeProtocol.Version)
        {
            throw new InvalidDataException(
                $"Unsupported Jungle Judge protocolVersion {result.ProtocolVersion}.");
        }
        if (!string.Equals(
                result.CompatibilityId,
                JungleSeedJudgeProtocol.CompatibilityId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Jungle Judge compatibilityId does not match TerrariaSplit.");
        }
        if (!string.Equals(
                result.RequestId,
                expectedRequestId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Jungle Judge response requestId does not match the request.");
        }
        if (result.Status == JungleSeedJudgeStatus.Complete)
        {
            if (result.CheckpointPassIndex != 62 ||
                result.Jungle is null ||
                result.CrimsonVertices is not { Count: 2 })
            {
                throw new InvalidDataException(
                    "Complete Jungle Judge response is missing required analysis data.");
            }
        }

        return result;
    }
}
