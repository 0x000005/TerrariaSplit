using TerrariaSplit.Terraria.WorldGeneration;

namespace TerrariaSplit.Terraria.Automation;

// Background worker that keeps the world pool topped up. While world pooling is enabled,
// it asks TerrariaServer.exe to generate worlds from program-built copied seeds and banks
// the .wld file after seed filtering and generated-file metadata validation.
// It backs off once the pool reaches the target count and resumes when worlds are consumed.
// This is a background task, not a dedicated UI thread; the expensive work happens in a
// separate TerrariaServer.exe process.
public sealed class WorldPoolFillService : IDisposable
{
    private static readonly TimeSpan IdleInterval = TimeSpan.FromSeconds(8);

    private readonly IWorldPoolStore store;
    private readonly HeadlessWorldGenerator generator;
    private readonly WorldPoolSeedFilter seedFilter;
    private readonly Queue<string> acceptedSeeds = new();
    private string? seedFilterSignature;
    private readonly ISettingsSnapshotFactory settingsSnapshots;
    private readonly IAppLogger logger;
    private readonly object sync = new();
    private AppSettings? settings;
    private CancellationTokenSource? cancellation;
    private Task? loop;
    private bool disposed;
    private bool loggedMissingServer;

    public WorldPoolFillService(
        IWorldPoolStore store,
        ISettingsSnapshotFactory settingsSnapshots,
        IAppLogger? logger = null,
        IRuntimeDataPaths? paths = null)
    {
        this.store = store;
        generator = new HeadlessWorldGenerator(paths);
        seedFilter = new WorldPoolSeedFilter(Environment.ProcessorCount);
        this.settingsSnapshots = settingsSnapshots;
        this.logger = logger ?? NullAppLogger.Instance;
    }

    // Called at startup and whenever settings are applied. Refreshing the signature clears
    // the pool when any world-gen setting changed, and starts the loop if needed.
    public void UpdateSettings(AppSettings newSettings)
    {
        AppSettings clone = settingsSnapshots.CreateSnapshot(newSettings);
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            settings = clone;
            loggedMissingServer = false;
        }

        store.EnsureSignature(WorldPoolRuntimeVersion.SignatureFromCurrentRuntime(clone));
        EnsureLoopRunning();
    }

    public int GetPoolCount(AppSettings currentSettings)
    {
        AppSettings clone = settingsSnapshots.CreateSnapshot(currentSettings);
        return store.Count(WorldPoolRuntimeVersion.SignatureFromCurrentRuntime(clone));
    }

    private void EnsureLoopRunning()
    {
        lock (sync)
        {
            if (disposed || loop is not null)
            {
                return;
            }

            cancellation = new CancellationTokenSource();
            CancellationToken token = cancellation.Token;
            loop = Task.Run(() => RunLoopAsync(token));
        }
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            bool generated = false;
            try
            {
                generated = await TryGenerateOnceAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "World pool fill iteration failed.");
            }

            if (generated)
            {
                continue;
            }

            try
            {
                await Task.Delay(IdleInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<bool> TryGenerateOnceAsync(CancellationToken cancellationToken)
    {
        AppSettings? current;
        lock (sync)
        {
            current = settings;
        }

        if (current is null)
        {
            ClearPendingSeeds();
            return false;
        }

        AutoCreateWorldSettings autoCreate = current.Automation.AutoCreate;
        if (!autoCreate.EnableWorldPool)
        {
            ClearPendingSeeds();
            return false;
        }

        TerrariaServerTarget? serverTarget = TerrariaServerLocator.TryResolveTarget();
        if (serverTarget is null)
        {
            lock (sync)
            {
                if (!loggedMissingServer)
                {
                    loggedMissingServer = true;
                    logger.Info("World pool fill is idle because TerrariaServer.exe could not be located.");
                }
            }

            return false;
        }

        string signature = WorldPoolRuntimeVersion.SignatureFromServerTarget(current, serverTarget.Value);
        if (!string.Equals(seedFilterSignature, signature, StringComparison.Ordinal))
        {
            ClearPendingSeeds();
            seedFilterSignature = signature;
        }
        store.EnsureSignature(signature);
        if (store.Count(signature) >= autoCreate.WorldPoolTargetCount)
        {
            return false;
        }

        bool filtering = WorldSeedFilterEvaluator.IsEnabledFor(autoCreate);
        if (filtering && acceptedSeeds.Count == 0)
        {
            TerrariaWorldGenerationVersion version = serverTarget.Value.IsLegacy1449
                ? TerrariaWorldGenerationVersion.Legacy1449 : TerrariaWorldGenerationVersion.Modern1458;
            WorldPoolSeedFilterResult batch = await seedFilter.FilterBatchAsync(autoCreate, version, cancellationToken);
            if (!IsGenerationStillCurrent(signature)) return true;
            if (batch.Failure is { } failure)
            {
                logger.Info($"World pool seed filtering stopped: {failure}");
                return false;
            }
            foreach (string seed in batch.AcceptedSeeds) acceptedSeeds.Enqueue(seed);
            logger.Info($"World pool filtered {seedFilter.Concurrency} candidates with one native thread each; accepted={acceptedSeeds.Count}.");
            if (acceptedSeeds.Count == 0) return true;
        }
        if (!IsGenerationStillCurrent(signature)) return true;
        HeadlessWorldGenResult result = await generator.GenerateAsync(serverTarget.Value, current.General.Language, autoCreate,
            seedOverride: filtering ? acceptedSeeds.Peek() : null, worldNameOverride: null,
            cancellationToken, skipSeedFilter: filtering);
        if (filtering && result.Generated) acceptedSeeds.Dequeue();
        try
        {
            if (result.Keep &&
                IsGenerationStillCurrent(signature) &&
                store.TryAdd(signature, result.WorldPath, result.Metadata, out WorldPoolItem item))
            {
                logger.Info(
                    $"World pool banked world {item.WorldFileName}; pool now holds " +
                    $"{store.Count(signature)}/{autoCreate.WorldPoolTargetCount}.");
            }
        }
        finally
        {
            // A skipped attempt did not acquire the generation lease and must not
            // clear the scratch directory of the generator currently holding it.
            if (result.Generated) generator.ClearScratch();
        }

        return result.Generated;
    }

    private void ClearPendingSeeds()
    {
        acceptedSeeds.Clear();
        seedFilter.Reset();
        seedFilterSignature = null;
    }

    private bool IsGenerationStillCurrent(string signature)
    {
        AppSettings? current;
        lock (sync)
        {
            current = settings;
        }

        if (current is null)
        {
            return false;
        }

        AutoCreateWorldSettings autoCreate = current.Automation.AutoCreate;
        return autoCreate.EnableWorldPool &&
            string.Equals(WorldPoolRuntimeVersion.SignatureFromCurrentRuntime(current), signature, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Task? pending;
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            cancellation?.Cancel();
            pending = loop;
        }

        try
        {
            generator.Dispose();
            seedFilter.Dispose();
            pending?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex) when (ex is AggregateException or OperationCanceledException)
        {
        }

        lock (sync)
        {
            cancellation?.Dispose();
            cancellation = null;
            loop = null;
        }
    }
}
