using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace TerrariaSplit.Diagnostics;

internal static class StartupMetrics
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan GracefulExitTimeout = TimeSpan.FromSeconds(5);

    public static bool TryRun(string[] args)
    {
        if (args.Length == 0 || !string.Equals(args[0], "startup-metrics", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (args.Length < 2)
        {
            throw new ArgumentException(
                "Usage: startup-metrics <published-exe-or-directory> [--runs N] [--cold] [--compare baseline-exe-or-directory] [--csv path]");
        }

        string sourceExecutable = ResolveSourceExecutable(args[1]);
        string? comparisonExecutable = ReadStringOption(args, "--compare") is string comparisonPath
            ? ResolveSourceExecutable(comparisonPath)
            : null;
        int runCount = ReadIntOption(args, "--runs", 20);
        bool cold = args.Any(argument => string.Equals(argument, "--cold", StringComparison.OrdinalIgnoreCase));
        if (cold && comparisonExecutable is not null)
        {
            throw new ArgumentException("--cold and --compare cannot be used together.");
        }

        bool trace = args.Any(argument => string.Equals(argument, "--trace", StringComparison.OrdinalIgnoreCase));
        string csvPath = ReadStringOption(args, "--csv") ?? Path.GetFullPath(Path.Combine(
            "test",
            "Results",
            "Startup",
            $"startup-{(comparisonExecutable is not null ? "compare" : cold ? "cold" : "normal")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv"));
        string sessionRoot = Path.GetFullPath(Path.Combine(
            "test",
            "Temp",
            "startup-metrics",
            Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(sessionRoot);

        var results = new List<StartupMeasurement>();
        try
        {
            if (comparisonExecutable is not null)
            {
                string candidate = StagePublishedApplication(sourceExecutable, sessionRoot, "candidate");
                string baseline = StagePublishedApplication(comparisonExecutable, sessionRoot, "baseline");
                _ = MeasureOnce(baseline, 0, "baseline-warmup", trace: false);
                _ = MeasureOnce(candidate, 0, "candidate-warmup", trace: false);
                for (int index = 0; index < runCount; index++)
                {
                    int run = index + 1;
                    if ((index & 1) == 0)
                    {
                        results.Add(MeasureOnce(baseline, run, "baseline", trace));
                        results.Add(MeasureOnce(candidate, run, "candidate", trace));
                    }
                    else
                    {
                        results.Add(MeasureOnce(candidate, run, "candidate", trace));
                        results.Add(MeasureOnce(baseline, run, "baseline", trace));
                    }
                }
            }
            else if (cold)
            {
                for (int index = 0; index < runCount; index++)
                {
                    string executable = StagePublishedApplication(sourceExecutable, sessionRoot, $"cold-{index + 1}");
                    results.Add(MeasureOnce(executable, index + 1, "cold", trace));
                }
            }
            else
            {
                string executable = StagePublishedApplication(sourceExecutable, sessionRoot, "normal");
                _ = MeasureOnce(executable, 0, "warmup", trace: false);
                for (int index = 0; index < runCount; index++)
                {
                    results.Add(MeasureOnce(executable, index + 1, "normal", trace));
                }
            }

            WriteCsv(csvPath, results);
            PrintSummary(csvPath, results);
        }
        finally
        {
            TryDeleteDirectory(sessionRoot);
        }

        return true;
    }

    private static StartupMeasurement MeasureOnce(string executablePath, int run, string mode, bool trace)
    {
        string eventPrefix = $"Local\\TerrariaSplit.Startup.{Environment.ProcessId}.{Guid.NewGuid():N}";
        string firstFrameEventName = eventPrefix + ".FirstFrame";
        string fullyReadyEventName = eventPrefix + ".FullyReady";
        using var firstFrameEvent = new EventWaitHandle(false, EventResetMode.ManualReset, firstFrameEventName);
        using var fullyReadyEvent = new EventWaitHandle(false, EventResetMode.ManualReset, fullyReadyEventName);
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!
        };
        startInfo.Environment[StartupDiagnostics.FirstFrameEventEnvironmentVariable] = firstFrameEventName;
        startInfo.Environment[StartupDiagnostics.FullyReadyEventEnvironmentVariable] = fullyReadyEventName;
        string? tracePath = trace
            ? Path.Combine(Path.GetDirectoryName(executablePath)!, $"startup-trace-{run}.csv")
            : null;
        if (tracePath is not null)
        {
            startInfo.Environment[StartupDiagnostics.TracePathEnvironmentVariable] = tracePath;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {executablePath}.");
        try
        {
            if (!firstFrameEvent.WaitOne(StartupTimeout))
            {
                throw new TimeoutException($"Run {run} did not present its first frame within {StartupTimeout}.");
            }

            double firstFrameMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            if (!fullyReadyEvent.WaitOne(StartupTimeout))
            {
                throw new TimeoutException($"Run {run} did not become fully ready within {StartupTimeout}.");
            }

            double fullyReadyMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            if (tracePath is not null && TryReadTrace(tracePath, out string traceText))
            {
                Console.WriteLine(traceText);
            }

            return new StartupMeasurement(
                run,
                mode,
                firstFrameMilliseconds,
                fullyReadyMilliseconds,
                process.Id);
        }
        finally
        {
            CloseMeasuredProcess(process);
        }
    }

    private static void CloseMeasuredProcess(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        process.Refresh();
        _ = process.CloseMainWindow();
        if (process.WaitForExit((int)GracefulExitTimeout.TotalMilliseconds))
        {
            return;
        }

        process.Kill(entireProcessTree: true);
        process.WaitForExit();
    }

    private static bool TryReadTrace(string path, out string text)
    {
        var stopwatch = Stopwatch.StartNew();
        do
        {
            try
            {
                if (File.Exists(path))
                {
                    text = File.ReadAllText(path);
                    return true;
                }
            }
            catch (IOException)
            {
            }

            Thread.Sleep(10);
        }
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(2));

        text = string.Empty;
        return false;
    }

    private static string StagePublishedApplication(string sourceExecutable, string sessionRoot, string name)
    {
        string sourceDirectory = Path.GetDirectoryName(sourceExecutable)!;
        string destinationDirectory = Path.Combine(sessionRoot, name);
        CopyDirectory(sourceDirectory, destinationDirectory);
        string stagedExecutable = Path.Combine(destinationDirectory, Path.GetFileName(sourceExecutable));
        if (!File.Exists(stagedExecutable))
        {
            throw new FileNotFoundException("The staged executable was not copied.", stagedExecutable);
        }

        return stagedExecutable;
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (string sourceFile in Directory.EnumerateFiles(sourceDirectory))
        {
            if (string.Equals(Path.GetExtension(sourceFile), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(sourceFile, Path.Combine(destinationDirectory, Path.GetFileName(sourceFile)), overwrite: true);
        }

        foreach (string sourceSubdirectory in Directory.EnumerateDirectories(sourceDirectory))
        {
            string name = Path.GetFileName(sourceSubdirectory);
            if (name is "Settings" or "Data" or "Worlds" or "Logs")
            {
                continue;
            }

            CopyDirectory(sourceSubdirectory, Path.Combine(destinationDirectory, name));
        }
    }

    private static string ResolveSourceExecutable(string value)
    {
        string path = Path.GetFullPath(value);
        if (Directory.Exists(path))
        {
            path = Path.Combine(path, "TerrariaSplit.exe");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Published TerrariaSplit.exe was not found.", path);
        }

        return path;
    }

    private static void WriteCsv(string path, IReadOnlyList<StartupMeasurement> results)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var csv = new StringBuilder("Run,Mode,FirstFrameMs,FullyReadyMs,ProcessId\n");
        foreach (StartupMeasurement result in results)
        {
            csv.Append(result.Run.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(result.Mode).Append(',')
                .Append(result.FirstFrameMilliseconds.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                .Append(result.FullyReadyMilliseconds.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                .Append(result.ProcessId.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        File.WriteAllText(fullPath, csv.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void PrintSummary(string csvPath, IReadOnlyList<StartupMeasurement> results)
    {
        StartupMeasurement[][] groups = results
            .GroupBy(result => result.Mode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.ToArray())
            .ToArray();
        if (groups.Length > 1)
        {
            foreach (StartupMeasurement[] group in groups.OrderBy(group => group[0].Mode, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    $"{group[0].Mode}: first-frame median {Median(group.Select(result => result.FirstFrameMilliseconds)):F3} ms, " +
                    $"P95 {Percentile95(group.Select(result => result.FirstFrameMilliseconds)):F3} ms; " +
                    $"fully-ready median {Median(group.Select(result => result.FullyReadyMilliseconds)):F3} ms, " +
                    $"P95 {Percentile95(group.Select(result => result.FullyReadyMilliseconds)):F3} ms");
            }

            Console.WriteLine($"CSV: {Path.GetFullPath(csvPath)}");
            return;
        }

        double firstFrameP95 = Percentile95(results.Select(result => result.FirstFrameMilliseconds));
        double fullyReadyP95 = Percentile95(results.Select(result => result.FullyReadyMilliseconds));
        Console.WriteLine($"First frame P95: {firstFrameP95:F3} ms");
        Console.WriteLine($"Fully ready P95: {fullyReadyP95:F3} ms");
        Console.WriteLine($"CSV: {Path.GetFullPath(csvPath)}");
    }

    private static double Percentile95(IEnumerable<double> values)
    {
        double[] sorted = values.Order().ToArray();
        if (sorted.Length == 0)
        {
            return 0;
        }

        int index = Math.Clamp((int)Math.Ceiling(sorted.Length * 0.95) - 1, 0, sorted.Length - 1);
        return sorted[index];
    }

    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = values.Order().ToArray();
        if (sorted.Length == 0)
        {
            return 0;
        }

        int middle = sorted.Length / 2;
        return (sorted.Length & 1) == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2d
            : sorted[middle];
    }

    private static int ReadIntOption(string[] args, string name, int fallback)
    {
        string? value = ReadStringOption(args, name);
        if (value is null)
        {
            return fallback;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) || result <= 0)
        {
            throw new ArgumentException($"{name} must be a positive integer.");
        }

        return result;
    }

    private static string? ReadStringOption(string[] args, string name)
    {
        for (int index = 2; index < args.Length; index++)
        {
            if (!string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"{name} requires a value.");
            }

            return args[index + 1];
        }

        return null;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to clean startup metrics directory {path}: {ex.Message}");
        }
    }

    private sealed record StartupMeasurement(
        int Run,
        string Mode,
        double FirstFrameMilliseconds,
        double FullyReadyMilliseconds,
        int ProcessId);
}
