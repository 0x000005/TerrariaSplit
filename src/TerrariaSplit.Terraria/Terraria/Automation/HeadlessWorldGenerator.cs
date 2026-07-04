using System.Diagnostics;
using System.Globalization;
using System.Text;
using Process = System.Diagnostics.Process;

namespace TerrariaSplit.Terraria.Automation;

// Generates a single world headlessly with TerrariaServer.exe (no game window, no
// foreground), then reads metadata from the world header and optionally scans for
// candidate item chests. Used by the background world pool to discover world files worth banking.
// The dedicated server writes the world to a private scratch folder, so the user's Worlds
// folder is never touched.
internal sealed class HeadlessWorldGenerator : IDisposable
{
    private const string GenerationMutexName = @"Local\TerrariaSplit.WorldPool.HeadlessWorldGenerator";
    private const string WorldFileStem = "tspool";
    private static readonly TimeSpan GenerationTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan StableFileDuration = TimeSpan.FromMilliseconds(500);

    private readonly TerrariaWorldFilePyramidScanner scanner = new();
    private readonly PyramidFilterWorldFileEvaluator worldFileEvaluator;
    private readonly object currentProcessSync = new();
    private readonly string scratchDirectory;
    private readonly string serverPidPath;
    private Process? currentProcess;
    private bool disposed;

    public HeadlessWorldGenerator(IRuntimeDataPaths? paths = null)
    {
        paths ??= AppContextRuntimeDataPaths.Default;
        scratchDirectory = paths.WorldPoolScratchDirectory;
        serverPidPath = Path.Combine(scratchDirectory, "server.pid");
        worldFileEvaluator = new PyramidFilterWorldFileEvaluator(scanner);
    }

    public async Task<HeadlessWorldGenResult> GenerateAndScanAsync(
        TerrariaServerTarget serverTarget,
        string? appLanguage,
        AutoCreateWorldSettings settings,
        CancellationToken cancellationToken)
    {
        using HeadlessGenerationLease? lease = HeadlessGenerationLease.TryAcquire(GenerationMutexName);
        if (lease is null)
        {
            StaticAppLogger.Instance.Info("World pool headless generation skipped because another generator is already running.");
            return HeadlessWorldGenResult.Skipped;
        }

        StopRecordedServer(serverTarget.ExePath);
        using TemporaryDirectoryScope scratch = TemporaryDirectoryScope.Prepare(scratchDirectory);

        if (!TryBuildGenerationRequest(serverTarget, appLanguage, settings, out HeadlessWorldGenerationRequest request, out string requestDetail))
        {
            StaticAppLogger.Instance.Info($"World pool headless generation skipped: {requestDetail}");
            scratch.Clean();
            return HeadlessWorldGenResult.Skipped;
        }

        List<string> serverArguments = new(request.ServerArguments);
        if (!string.IsNullOrEmpty(request.ServerConfig))
        {
            string configPath = Path.Combine(scratchDirectory, "server-config.txt");
            File.WriteAllText(configPath, request.ServerConfig, new UTF8Encoding(false));
            serverArguments.Add("-config");
            serverArguments.Add(configPath);
        }

        string? worldPath;
        using (StartTrackedServer(serverTarget.ExePath, serverArguments, request.StandardInputLines))
        {
            worldPath = await WaitForStableWorldFileAsync(cancellationToken);
        }

        if (worldPath is null)
        {
            StaticAppLogger.Instance.Info("World pool headless generation produced no world file.");
            scratch.Clean();
            return HeadlessWorldGenResult.Miss;
        }

        bool candidateItemFound = false;
        bool pyramidFilterMatches = true;
        PyramidFilterWorldFileResult pyramidFilterResult = default;
        if (settings.EnablePyramidFilter)
        {
            pyramidFilterResult = worldFileEvaluator.Evaluate(worldPath, settings);
            if (!pyramidFilterResult.ScanSucceeded)
            {
                StaticAppLogger.Instance.Info($"World pool could not scan candidate chest contents: {pyramidFilterResult.Detail}");
            }

            candidateItemFound = pyramidFilterResult.Keep;
            pyramidFilterMatches = candidateItemFound;
        }

        bool keep = false;
        TerrariaWorldSeedMetadata metadata = default;
        string metadataDetail = "<unread>";
        if (scanner.TryReadWorldSeedMetadata(worldPath, out metadata, out string detail))
        {
            metadataDetail = metadata.FormatWorldOptions();
            keep = MetadataMatchesRequest(request, metadata, settings) &&
                (!settings.EnablePyramidFilter || pyramidFilterMatches);
        }
        else
        {
            StaticAppLogger.Instance.Info($"World pool could not read generated world metadata: {detail}");
        }

        string requiredPyramidItems = settings.EnablePyramidFilter
            ? PyramidFilterItemMatcher.FormatRequiredItems(pyramidFilterResult.RequiredItemMask)
            : "disabled";
        string candidateChestsSummary = settings.EnablePyramidFilter
            ? pyramidFilterResult.CandidateChests.FormatSummary()
            : "not scanned";
        StaticAppLogger.Instance.Info(
            $"World pool headless generation world='{Path.GetFileName(worldPath)}': " +
            $"requiredPyramidItems={requiredPyramidItems}, " +
            $"candidateItems={candidateItemFound}, " +
            $"candidateChests={candidateChestsSummary}, " +
            $"metadata={metadataDetail}, expected={request.ExpectedDetail}, " +
            $"mode={request.ModeDetail}, keep={keep}.");
        if (!keep)
        {
            scratch.Clean();
            return new HeadlessWorldGenResult(candidateItemFound, false, string.Empty, default, Generated: true);
        }

        return new HeadlessWorldGenResult(candidateItemFound, true, worldPath, metadata, Generated: true);
    }

    private bool TryBuildGenerationRequest(
        TerrariaServerTarget serverTarget,
        string? appLanguage,
        AutoCreateWorldSettings settings,
        out HeadlessWorldGenerationRequest request,
        out string detail)
    {
        return serverTarget.IsLegacy1449
            ? TryBuildLegacy1449GenerationRequest(appLanguage, settings, out request, out detail)
            : TryBuildModernGenerationRequest(appLanguage, settings, out request, out detail);
    }

    private bool TryBuildModernGenerationRequest(
        string? appLanguage,
        AutoCreateWorldSettings settings,
        out HeadlessWorldGenerationRequest request,
        out string detail)
    {
        TerrariaCopiedSeed copiedSeed = TerrariaCopiedSeedBuilder.Create(settings);
        string worldName = TerrariaWorldNameGenerator.Create(appLanguage);
        string serverLanguage = TerrariaLanguageCodes.FromAppLanguage(appLanguage);

        request = new HeadlessWorldGenerationRequest(
            BuildModernServerConfig(settings, copiedSeed.Text, worldName, serverLanguage),
            Array.Empty<string>(),
            copiedSeed.Metadata,
            Array.Empty<string>(),
            copiedSeed.Metadata.FormatWorldOptions(),
            HeadlessWorldGenerationMetadataPolicy.Exact,
            $"copiedSeed={copiedSeed.Text}");
        detail = string.Empty;
        return true;
    }

    private bool TryBuildLegacy1449GenerationRequest(
        string? appLanguage,
        AutoCreateWorldSettings settings,
        out HeadlessWorldGenerationRequest request,
        out string detail)
    {
        request = default;
        if (!TerrariaLegacy1449SeedText.TryBuild(settings, out string seedText, out detail))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(seedText))
        {
            seedText = TerrariaSeedRandom.NextShared().ToString(CultureInfo.InvariantCulture);
            detail = $"1.4.4.9 world seed field text: {seedText}";
        }

        int sizeCode = TerrariaWorldSeedOptions.SizeCode(settings.WorldSize);
        int difficultyCode = TerrariaWorldSeedOptions.CopiedDifficultyCode(settings.WorldDifficulty);
        int specialSeedMask = TerrariaWorldSeedOptions.SpecialSeedMask(settings.SpecialSeeds);
        string worldName = TerrariaWorldNameGenerator.Create(appLanguage);
        string serverLanguage = TerrariaLanguageCodes.FromAppLanguage(appLanguage);

        request = new HeadlessWorldGenerationRequest(
            BuildLegacy1449ServerConfig(settings, seedText, worldName, serverLanguage),
            Array.Empty<string>(),
            new TerrariaWorldSeedMetadata(
                seedText,
                sizeCode,
                difficultyCode,
                HasCrimson: false,
                specialSeedMask),
            Array.Empty<string>(),
            $"{TerrariaWorldSeedMetadata.FormatExpectedWorldOptions(settings)}, seedText={seedText}",
            HeadlessWorldGenerationMetadataPolicy.MatchWorldOptionsAndSeedText,
            detail);
        return true;
    }

    private static bool MetadataMatchesRequest(
        HeadlessWorldGenerationRequest request,
        TerrariaWorldSeedMetadata metadata,
        AutoCreateWorldSettings settings)
    {
        return request.MetadataPolicy switch
        {
            HeadlessWorldGenerationMetadataPolicy.MatchWorldOptionsAndSeedText =>
                string.Equals(metadata.SeedText, request.ExpectedMetadata.SeedText, StringComparison.Ordinal) &&
                metadata.MatchesWorldOptions(settings),
            _ => metadata.Equals(request.ExpectedMetadata)
        };
    }

    private Process StartServer(
        string serverExePath,
        IReadOnlyList<string> serverArguments,
        IReadOnlyList<string> standardInputLines)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = serverExePath,
            WorkingDirectory = Path.GetDirectoryName(serverExePath) ?? scratchDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };
        foreach (string argument in serverArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.Start();

        // Drain the console pipes so a chatty server cannot block on a full buffer.
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        WriteStandardInput(process, standardInputLines);
        return process;
    }

    private static void WriteStandardInput(Process process, IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        try
        {
            foreach (string line in lines)
            {
                process.StandardInput.WriteLine(line);
            }

            process.StandardInput.Flush();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ObjectDisposedException)
        {
            StaticAppLogger.Instance.Error(ex, "World pool failed to write scripted TerrariaServer input.");
        }
    }

    private ProcessLifecycleGuard StartTrackedServer(
        string serverExePath,
        IReadOnlyList<string> serverArguments,
        IReadOnlyList<string> standardInputLines)
    {
        Process process = StartServer(serverExePath, serverArguments, standardInputLines);
        return new ProcessLifecycleGuard(
            process,
            trackedProcess => TrackCurrentProcess(trackedProcess, serverExePath),
            ClearCurrentProcess,
            "World pool failed to stop headless Terraria server.");
    }

    private void TrackCurrentProcess(Process process, string serverExePath)
    {
        lock (currentProcessSync)
        {
            currentProcess = process;
        }

        JsonFileStore.Write(
            serverPidPath,
            new ServerProcessMarker
            {
                ProcessId = process.Id,
                StartTimeUtcTicks = ProcessLifecycleGuard.TryGetProcessStartTimeUtcTicks(process, out long ticks) ? ticks : 0,
                ServerExePath = Path.GetFullPath(serverExePath)
            },
            "world pool server marker");
    }

    private void ClearCurrentProcess(int? processId)
    {
        lock (currentProcessSync)
        {
            if (currentProcess is not null && ProcessLifecycleGuard.ProcessIdMatches(currentProcess, processId))
            {
                currentProcess = null;
            }
        }

        TemporaryDirectoryScope.TryDeleteFile(serverPidPath);
    }

    private void StopRecordedServer(string serverExePath)
    {
        ServerProcessMarker? marker = JsonFileStore.Read<ServerProcessMarker>(serverPidPath, "world pool server marker");
        if (marker is null || marker.ProcessId <= 0)
        {
            return;
        }

        try
        {
            Process process = Process.GetProcessById(marker.ProcessId);
            if (IsMarkedServerProcess(process, marker, serverExePath))
            {
                StaticAppLogger.Instance.Info($"Stopping stale world pool TerrariaServer.exe process {marker.ProcessId}.");
                ProcessLifecycleGuard.TryKill(process, "World pool failed to stop headless Terraria server.");
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
            TemporaryDirectoryScope.TryDeleteFile(serverPidPath);
        }
    }

    private static bool IsMarkedServerProcess(Process process, ServerProcessMarker marker, string serverExePath)
    {
        if (marker.StartTimeUtcTicks > 0 &&
            (!ProcessLifecycleGuard.TryGetProcessStartTimeUtcTicks(process, out long currentTicks) || currentTicks != marker.StartTimeUtcTicks))
        {
            return false;
        }

        string expectedPath = string.IsNullOrWhiteSpace(marker.ServerExePath)
            ? serverExePath
            : marker.ServerExePath;
        return !ProcessLifecycleGuard.TryGetProcessPath(process, out string? processPath) || SamePath(processPath, expectedPath);
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

    private async Task<string?> WaitForStableWorldFileAsync(CancellationToken cancellationToken)
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
                    FileAccessProbe.CanOpenForRead(candidatePath))
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

    private bool TryFindNewestWorldFile(out string? path, out long length, out DateTime writeTimeUtc)
    {
        path = null;
        length = -1;
        writeTimeUtc = DateTime.MinValue;
        if (!Directory.Exists(scratchDirectory))
        {
            return false;
        }

        foreach (string worldFile in Directory.EnumerateFiles(scratchDirectory, "*.wld", SearchOption.AllDirectories))
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

    private string BuildModernServerConfig(
        AutoCreateWorldSettings settings,
        string copiedSeed,
        string worldName,
        string serverLanguage)
    {
        var builder = new StringBuilder();
        // autocreate only fires when the world named by `world=` is absent, so point it at the
        // scratch .wld we want generated. Without this line the dedicated server ignores
        // autocreate, drops into its interactive "Choose World" menu, and hangs on stdin until
        // the generation timeout, producing no world file.
        builder.AppendLine("world=" + Path.Combine(scratchDirectory, WorldFileStem + ".wld"));
        builder.AppendLine("autocreate=" + TerrariaWorldSeedOptions.SizeCode(settings.WorldSize).ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("worldname=" + worldName);
        builder.AppendLine("worldpath=" + scratchDirectory + Path.DirectorySeparatorChar);
        builder.AppendLine("difficulty=" + TerrariaWorldSeedOptions.ServerDifficultyCode(settings.WorldDifficulty).ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("seed=" + copiedSeed);
        builder.AppendLine("maxplayers=1");
        builder.AppendLine("port=" + Random.Shared.Next(7801, 7999).ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("language=" + serverLanguage);
        builder.AppendLine("secure=0");
        builder.AppendLine("upnp=0");

        return builder.ToString();
    }

    private string BuildLegacy1449ServerConfig(
        AutoCreateWorldSettings settings,
        string seedText,
        string worldName,
        string serverLanguage)
    {
        var builder = new StringBuilder();
        builder.AppendLine("world=" + Path.Combine(scratchDirectory, WorldFileStem + ".wld"));
        builder.AppendLine("autocreate=" + TerrariaWorldSeedOptions.SizeCode(settings.WorldSize).ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("worldname=" + worldName);
        builder.AppendLine("worldpath=" + scratchDirectory + Path.DirectorySeparatorChar);
        builder.AppendLine("difficulty=" + TerrariaWorldSeedOptions.ServerDifficultyCode(settings.WorldDifficulty).ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("seed=" + seedText);
        builder.AppendLine("maxplayers=1");
        builder.AppendLine("port=" + Random.Shared.Next(7801, 7999).ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("language=" + serverLanguage);
        builder.AppendLine("secure=0");
        builder.AppendLine("upnp=0");

        return builder.ToString();
    }

    public void ClearScratch()
    {
        TemporaryDirectoryScope.CleanDirectory(scratchDirectory);
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

        ProcessLifecycleGuard.TryKill(processToKill, "World pool failed to stop headless Terraria server.");
        TemporaryDirectoryScope.TryDeleteFile(serverPidPath);
        ClearScratch();
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
                StaticAppLogger.Instance.Error(ex, "World pool failed to acquire headless generation mutex.");
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

internal enum HeadlessWorldGenerationMetadataPolicy
{
    Exact,
    MatchWorldOptionsAndSeedText
}

internal readonly record struct HeadlessWorldGenerationRequest(
    string ServerConfig,
    IReadOnlyList<string> ServerArguments,
    TerrariaWorldSeedMetadata ExpectedMetadata,
    IReadOnlyList<string> StandardInputLines,
    string ExpectedDetail,
    HeadlessWorldGenerationMetadataPolicy MetadataPolicy,
    string ModeDetail);

internal readonly record struct HeadlessWorldGenResult(
    bool CandidateItemFound,
    bool Keep,
    string WorldPath,
    TerrariaWorldSeedMetadata Metadata,
    bool Generated)
{
    public static HeadlessWorldGenResult Miss => new(false, false, string.Empty, default, Generated: true);

    public static HeadlessWorldGenResult Skipped => new(false, false, string.Empty, default, Generated: false);
}
