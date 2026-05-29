using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace TerrariaSplit;

// Generates a single world headlessly with TerrariaServer.exe (no game window, no
// foreground), then scans the produced .wld for a pyramid and reads the copied seed
// metadata from the world header. Used by the background seed pool to discover seeds
// worth banking. The dedicated server writes the world to a private scratch folder, so
// the user's Worlds folder is never touched.
internal sealed class HeadlessWorldGenerator : IDisposable
{
    private static readonly string ScratchDirectory = Path.Combine(AppContext.BaseDirectory, "seed-pool", "scratch");
    private static readonly string ServerPidPath = Path.Combine(ScratchDirectory, "server.pid");
    private const string GenerationMutexName = @"Local\TerrariaSplit.SeedPool.HeadlessWorldGenerator";
    private const string WorldName = "tspool";
    private const int PyramidWallThreshold = 1;
    private const int PyramidTileThreshold = 1;
    private static readonly TimeSpan GenerationTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan StableFileDuration = TimeSpan.FromMilliseconds(500);

    private readonly TerrariaWorldFilePyramidScanner scanner = new();
    private readonly object currentProcessSync = new();
    private Process? currentProcess;
    private bool disposed;

    public async Task<HeadlessWorldGenResult> GenerateAndScanAsync(
        string serverExePath,
        AutoCreateWorldSettings settings,
        CancellationToken cancellationToken)
    {
        using HeadlessGenerationLease? lease = HeadlessGenerationLease.TryAcquire(GenerationMutexName);
        if (lease is null)
        {
            AppLogger.Info("Seed pool headless generation skipped because another generator is already running.");
            return HeadlessWorldGenResult.Skipped;
        }

        Directory.CreateDirectory(ScratchDirectory);
        StopRecordedServer(serverExePath);
        CleanScratch();

        try
        {
            string configPath = Path.Combine(ScratchDirectory, "server-config.txt");
            File.WriteAllText(configPath, BuildServerConfig(settings));

            Process? process = null;
            int? processId = null;
            string? worldPath;
            try
            {
                process = StartServer(serverExePath, configPath);
                processId = TryGetProcessId(process);
                TrackCurrentProcess(process, serverExePath);
                worldPath = await WaitForStableWorldFileAsync(cancellationToken);
            }
            finally
            {
                if (process is not null)
                {
                    processId ??= TryGetProcessId(process);
                    TryKill(process);
                    ClearCurrentProcess(processId);
                }
            }

            if (worldPath is null)
            {
                AppLogger.Info("Seed pool headless generation produced no world file.");
                return HeadlessWorldGenResult.Miss;
            }

            bool scanned = scanner.TryScanSpeedrunCorridor(
                worldPath,
                settings.WorldSize,
                PyramidWallThreshold,
                PyramidTileThreshold,
                out PyramidEvidenceScanResult evidence,
                out _,
                out _);
            bool pyramidFound = scanned && !evidence.ScanFailed && evidence.MeetsThreshold(PyramidWallThreshold, PyramidTileThreshold);

            bool keep = false;
            string copiedSeed = string.Empty;
            bool worldHasCrimson = false;
            if (pyramidFound)
            {
                if (scanner.TryReadWorldSeedMetadata(worldPath, out TerrariaWorldSeedMetadata metadata, out string detail))
                {
                    copiedSeed = metadata.ToFullSeedText();
                    worldHasCrimson = metadata.HasCrimson;
                    keep = !string.IsNullOrWhiteSpace(copiedSeed) &&
                        EvilMatches(settings.WorldEvil, worldHasCrimson, evilRead: true);
                }
                else
                {
                    AppLogger.Info($"Seed pool could not read generated world seed metadata: {detail}");
                }
            }

            string loggedSeed = string.IsNullOrWhiteSpace(copiedSeed) ? "<unread>" : copiedSeed;
            AppLogger.Info($"Seed pool headless generation seed={loggedSeed}: pyramid={pyramidFound}, keep={keep}.");
            return new HeadlessWorldGenResult(pyramidFound, keep, copiedSeed, Generated: true);
        }
        finally
        {
            CleanScratch();
        }
    }

    private static Process StartServer(string serverExePath, string configPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = serverExePath,
            WorkingDirectory = Path.GetDirectoryName(serverExePath) ?? ScratchDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };
        startInfo.ArgumentList.Add("-config");
        startInfo.ArgumentList.Add(configPath);

        var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.Start();

        // Drain the console pipes so a chatty server cannot block on a full buffer.
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private void TrackCurrentProcess(Process process, string serverExePath)
    {
        lock (currentProcessSync)
        {
            currentProcess = process;
        }

        JsonFileStore.Write(
            ServerPidPath,
            new ServerProcessMarker
            {
                ProcessId = process.Id,
                StartTimeUtcTicks = TryGetProcessStartTimeUtcTicks(process, out long ticks) ? ticks : 0,
                ServerExePath = Path.GetFullPath(serverExePath)
            },
            "seed pool server marker");
    }

    private void ClearCurrentProcess(int? processId)
    {
        lock (currentProcessSync)
        {
            if (currentProcess is not null && ProcessIdMatches(currentProcess, processId))
            {
                currentProcess = null;
            }
        }

        TryDeleteFile(ServerPidPath);
    }

    private static bool ProcessIdMatches(Process process, int? processId)
    {
        if (!processId.HasValue)
        {
            return true;
        }

        try
        {
            return process.Id == processId.Value;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            return true;
        }
    }

    private static int? TryGetProcessId(Process process)
    {
        try
        {
            return process.Id;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            return null;
        }
    }

    private static void StopRecordedServer(string serverExePath)
    {
        ServerProcessMarker? marker = JsonFileStore.Read<ServerProcessMarker>(ServerPidPath, "seed pool server marker");
        if (marker is null || marker.ProcessId <= 0)
        {
            return;
        }

        try
        {
            Process process = Process.GetProcessById(marker.ProcessId);
            if (IsMarkedServerProcess(process, marker, serverExePath))
            {
                AppLogger.Info($"Stopping stale seed pool TerrariaServer.exe process {marker.ProcessId}.");
                TryKill(process);
            }
            else
            {
                process.Dispose();
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
        finally
        {
            TryDeleteFile(ServerPidPath);
        }
    }

    private static bool IsMarkedServerProcess(Process process, ServerProcessMarker marker, string serverExePath)
    {
        if (marker.StartTimeUtcTicks > 0 &&
            (!TryGetProcessStartTimeUtcTicks(process, out long currentTicks) || currentTicks != marker.StartTimeUtcTicks))
        {
            return false;
        }

        string expectedPath = string.IsNullOrWhiteSpace(marker.ServerExePath)
            ? serverExePath
            : marker.ServerExePath;
        return !TryGetProcessPath(process, out string? processPath) || SamePath(processPath, expectedPath);
    }

    private static bool SamePath(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool TryGetProcessStartTimeUtcTicks(Process process, out long ticks)
    {
        try
        {
            ticks = process.StartTime.ToUniversalTime().Ticks;
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            ticks = 0;
            return false;
        }
    }

    private static bool TryGetProcessPath(Process process, out string? path)
    {
        try
        {
            path = process.MainModule?.FileName;
            return !string.IsNullOrWhiteSpace(path);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            path = null;
            return false;
        }
    }

    private static void TryKill(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit((int)TimeSpan.FromSeconds(5).TotalMilliseconds);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException or ObjectDisposedException)
        {
            AppLogger.Error(ex, "Seed pool failed to stop headless Terraria server.");
        }
        finally
        {
            process.Dispose();
        }
    }

    private static async Task<string?> WaitForStableWorldFileAsync(CancellationToken cancellationToken)
    {
        DateTime deadline = DateTime.UtcNow + GenerationTimeout;
        string? stablePath = null;
        long stableLength = -1;
        DateTime stableWriteTime = DateTime.MinValue;
        DateTime stableSince = DateTime.MinValue;

        while (DateTime.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryFindNewestWorldFile(out string? candidatePath, out long length, out DateTime writeTime) &&
                candidatePath is not null)
            {
                if (string.Equals(stablePath, candidatePath, StringComparison.OrdinalIgnoreCase) &&
                    stableLength == length &&
                    stableWriteTime == writeTime &&
                    CanOpenForRead(candidatePath))
                {
                    if (stableSince == DateTime.MinValue)
                    {
                        stableSince = DateTime.UtcNow;
                    }
                    else if (DateTime.UtcNow - stableSince >= StableFileDuration)
                    {
                        return candidatePath;
                    }
                }
                else
                {
                    stablePath = candidatePath;
                    stableLength = length;
                    stableWriteTime = writeTime;
                    stableSince = DateTime.MinValue;
                }
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        return null;
    }

    private static bool TryFindNewestWorldFile(out string? path, out long length, out DateTime writeTimeUtc)
    {
        path = null;
        length = -1;
        writeTimeUtc = DateTime.MinValue;
        if (!Directory.Exists(ScratchDirectory))
        {
            return false;
        }

        foreach (string worldFile in Directory.EnumerateFiles(ScratchDirectory, "*.wld", SearchOption.AllDirectories))
        {
            FileInfo info;
            try
            {
                info = new FileInfo(worldFile);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (info.LastWriteTimeUtc < writeTimeUtc)
            {
                continue;
            }

            path = info.FullName;
            length = info.Length;
            writeTimeUtc = info.LastWriteTimeUtc;
        }

        return path is not null;
    }

    private static bool CanOpenForRead(string path)
    {
        try
        {
            using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return stream.Length > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool EvilMatches(string worldEvil, bool hasCrimson, bool evilRead)
    {
        string evil = AutoCreateWorldEvil.Normalize(worldEvil);
        if (evil == AutoCreateWorldEvil.Random)
        {
            return true;
        }

        if (!evilRead)
        {
            return false;
        }

        return evil == AutoCreateWorldEvil.Crimson ? hasCrimson : !hasCrimson;
    }

    private static string BuildServerConfig(AutoCreateWorldSettings settings)
    {
        var builder = new StringBuilder();
        // autocreate only fires when the world named by `world=` is absent, so point it at the
        // scratch .wld we want generated. Without this line the dedicated server ignores
        // autocreate, drops into its interactive "Choose World" menu, and hangs on stdin until
        // the generation timeout, producing no world file.
        builder.AppendLine("world=" + Path.Combine(ScratchDirectory, WorldName + ".wld"));
        builder.AppendLine("autocreate=" + SizeCode(settings.WorldSize));
        builder.AppendLine("worldname=" + WorldName);
        builder.AppendLine("worldpath=" + ScratchDirectory + Path.DirectorySeparatorChar);
        builder.AppendLine("difficulty=" + DifficultyCode(settings.WorldDifficulty));
        builder.AppendLine("maxplayers=1");
        builder.AppendLine("port=" + Random.Shared.Next(7801, 7999).ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("language=en-US");
        builder.AppendLine("secure=0");
        builder.AppendLine("upnp=0");
        foreach (string key in SpecialSeedConfigKeys(settings.SpecialSeeds))
        {
            builder.AppendLine(key + "=1");
        }

        return builder.ToString();
    }

    private static string SizeCode(string worldSize)
    {
        return AutoCreateWorldSize.Normalize(worldSize) switch
        {
            AutoCreateWorldSize.Small => "1",
            AutoCreateWorldSize.Large => "3",
            _ => "2"
        };
    }

    private static string DifficultyCode(string worldDifficulty)
    {
        return AutoCreateWorldDifficulty.Normalize(worldDifficulty) switch
        {
            AutoCreateWorldDifficulty.Expert => "1",
            AutoCreateWorldDifficulty.Master => "2",
            AutoCreateWorldDifficulty.Journey => "3",
            _ => "0"
        };
    }

    private static IEnumerable<string> SpecialSeedConfigKeys(string? specialSeeds)
    {
        foreach (string seed in AutoCreateSpecialWorldSeed.ParseList(specialSeeds))
        {
            string? key = seed switch
            {
                AutoCreateSpecialWorldSeed.ForTheWorthy => "seed_fortheworthy",
                AutoCreateSpecialWorldSeed.NotTheBees => "seed_notthebees",
                AutoCreateSpecialWorldSeed.Celebration => "seed_celebration",
                AutoCreateSpecialWorldSeed.TheConstant => "seed_theconstant",
                AutoCreateSpecialWorldSeed.NoTraps => "seed_notraps",
                AutoCreateSpecialWorldSeed.Remix => "seed_remix",
                AutoCreateSpecialWorldSeed.Drunk => "seed_drunk",
                AutoCreateSpecialWorldSeed.Zenith => "seed_zenith",
                _ => null
            };
            if (key is not null)
            {
                yield return key;
            }
        }
    }

    private static void CleanScratch()
    {
        if (!Directory.Exists(ScratchDirectory))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(ScratchDirectory, "*", SearchOption.AllDirectories))
        {
            TryDeleteFile(file);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public void Dispose()
    {
        Process? processToKill = null;
        lock (currentProcessSync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            processToKill = currentProcess;
            currentProcess = null;
        }

        TryKill(processToKill);
        TryDeleteFile(ServerPidPath);
    }

    private sealed class ServerProcessMarker
    {
        public int ProcessId { get; set; }

        public long StartTimeUtcTicks { get; set; }

        public string? ServerExePath { get; set; }
    }

    private sealed class HeadlessGenerationLease : IDisposable
    {
        private readonly Mutex mutex;
        private bool ownsMutex;

        private HeadlessGenerationLease(Mutex mutex)
        {
            this.mutex = mutex;
            ownsMutex = true;
        }

        public static HeadlessGenerationLease? TryAcquire(string name)
        {
            Mutex? mutex = null;
            try
            {
                mutex = new Mutex(initiallyOwned: false, name);
                try
                {
                    if (!mutex.WaitOne(0))
                    {
                        mutex.Dispose();
                        return null;
                    }
                }
                catch (AbandonedMutexException)
                {
                }

                return new HeadlessGenerationLease(mutex);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.ComponentModel.Win32Exception)
            {
                mutex?.Dispose();
                AppLogger.Error(ex, "Seed pool failed to acquire headless generation mutex.");
                return null;
            }
        }

        public void Dispose()
        {
            if (ownsMutex)
            {
                ownsMutex = false;
                try
                {
                    mutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                }
            }

            mutex.Dispose();
        }
    }
}

internal readonly record struct HeadlessWorldGenResult(bool PyramidFound, bool Keep, string Seed, bool Generated)
{
    public static HeadlessWorldGenResult Miss => new(false, false, string.Empty, Generated: true);

    public static HeadlessWorldGenResult Skipped => new(false, false, string.Empty, Generated: false);
}
