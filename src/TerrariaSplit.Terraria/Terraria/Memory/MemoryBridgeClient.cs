using System.ComponentModel;
using System.Diagnostics;

namespace TerrariaSplit.Terraria.Memory;

internal sealed class MemoryBridgeClient
{
    private const string ExecutableName = "TerrariaSplit.MemoryBridge.exe";

    public MemoryBridgeCommandResult Execute(
        string command,
        TimeSpan timeout,
        params string[] arguments)
    {
        string? executablePath = FindExecutable();
        if (executablePath is null)
        {
            return MemoryBridgeCommandResult.NotStarted($"{ExecutableName} not found.");
        }

        try
        {
            using Process? process = Process.Start(CreateStartInfo(executablePath, command, arguments));
            if (process is null)
            {
                return MemoryBridgeCommandResult.NotStarted($"Failed to start {ExecutableName}.");
            }

            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(ToTimeoutMilliseconds(timeout)))
            {
                TryKill(process);
                process.WaitForExit();
                return MemoryBridgeCommandResult.TimedOutResult(
                    stdout.GetAwaiter().GetResult().Trim(),
                    stderr.GetAwaiter().GetResult().Trim());
            }

            return MemoryBridgeCommandResult.Completed(
                process.ExitCode,
                stdout.GetAwaiter().GetResult().Trim(),
                stderr.GetAwaiter().GetResult().Trim());
        }
        catch (Exception ex) when (IsLaunchException(ex))
        {
            return MemoryBridgeCommandResult.NotStarted(ex.Message);
        }
    }

    public async Task<MemoryBridgeCommandResult> ExecuteAsync(
        string command,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        string? executablePath = FindExecutable();
        if (executablePath is null)
        {
            return MemoryBridgeCommandResult.NotStarted($"{ExecutableName} not found.");
        }

        try
        {
            using Process? process = Process.Start(CreateStartInfo(executablePath, command, arguments));
            if (process is null)
            {
                return MemoryBridgeCommandResult.NotStarted($"Failed to start {ExecutableName}.");
            }

            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellation.CancelAfter(timeout);
            try
            {
                await process.WaitForExitAsync(timeoutCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                string timeoutOutput = (await stdout.ConfigureAwait(false)).Trim();
                string timeoutError = (await stderr.ConfigureAwait(false)).Trim();
                cancellationToken.ThrowIfCancellationRequested();
                return MemoryBridgeCommandResult.TimedOutResult(timeoutOutput, timeoutError);
            }

            return MemoryBridgeCommandResult.Completed(
                process.ExitCode,
                (await stdout.ConfigureAwait(false)).Trim(),
                (await stderr.ConfigureAwait(false)).Trim());
        }
        catch (Exception ex) when (IsLaunchException(ex))
        {
            return MemoryBridgeCommandResult.NotStarted(ex.Message);
        }
    }

    internal string? FindExecutable()
    {
        return EnumerateCandidatePaths().FirstOrDefault(File.Exists);
    }

    private static ProcessStartInfo CreateStartInfo(
        string executablePath,
        string command,
        IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(command);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static IEnumerable<string> EnumerateCandidatePaths()
    {
        string baseDirectory = AppContext.BaseDirectory;
        yield return Path.Combine(baseDirectory, ExecutableName);
        yield return Path.Combine(baseDirectory, "TerrariaSplit.MemoryBridge", ExecutableName);

        DirectoryInfo? directory = new(baseDirectory);
        for (int depth = 0; directory is not null && depth < 8; depth++, directory = directory.Parent)
        {
            foreach (string configuration in new[] { "Debug", "Release" })
            {
                foreach (string projectRoot in new[]
                {
                    Path.Combine(directory.FullName, "TerrariaSplit.MemoryBridge"),
                    Path.Combine(directory.FullName, "src", "TerrariaSplit.MemoryBridge")
                })
                {
                    yield return Path.Combine(
                        projectRoot,
                        "bin",
                        configuration,
                        "net10.0-windows",
                        "win-x86",
                        ExecutableName);
                    yield return Path.Combine(
                        projectRoot,
                        "bin",
                        configuration,
                        "net10.0-windows",
                        ExecutableName);
                    yield return Path.Combine(
                        projectRoot,
                        ".codex-build",
                        "bin",
                        configuration,
                        "net10.0-windows",
                        "win-x86",
                        ExecutableName);
                    yield return Path.Combine(
                        projectRoot,
                        ".codex-build",
                        "bin",
                        configuration,
                        "net10.0-windows",
                        ExecutableName);
                }
            }
        }
    }

    private static int ToTimeoutMilliseconds(TimeSpan timeout)
    {
        return checked((int)Math.Clamp(Math.Ceiling(timeout.TotalMilliseconds), 1, int.MaxValue));
    }

    private static bool IsLaunchException(Exception exception)
    {
        return exception is InvalidOperationException or Win32Exception or IOException or UnauthorizedAccessException;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (Win32Exception)
        {
        }
    }
}

internal sealed record MemoryBridgeCommandResult(
    bool Started,
    bool TimedOut,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    string? Error)
{
    public bool Succeeded => Started && !TimedOut && ExitCode == 0;

    public string FailureDetail(string fallback)
    {
        if (!string.IsNullOrWhiteSpace(Error))
        {
            return Error;
        }
        if (!string.IsNullOrWhiteSpace(StandardError))
        {
            return StandardError;
        }
        if (!string.IsNullOrWhiteSpace(StandardOutput))
        {
            return StandardOutput;
        }

        return ExitCode is int exitCode
            ? $"{fallback} Exit code: {exitCode}."
            : fallback;
    }

    public static MemoryBridgeCommandResult NotStarted(string error) =>
        new(false, false, null, string.Empty, string.Empty, error);

    public static MemoryBridgeCommandResult TimedOutResult(string standardOutput, string standardError) =>
        new(true, true, null, standardOutput, standardError, null);

    public static MemoryBridgeCommandResult Completed(int exitCode, string standardOutput, string standardError) =>
        new(true, false, exitCode, standardOutput, standardError, null);
}
