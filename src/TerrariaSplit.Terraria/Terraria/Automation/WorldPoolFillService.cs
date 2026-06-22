namespace TerrariaSplit.Terraria.Automation;

// Background worker that keeps the world pool topped up. While world pooling is enabled,
// it asks TerrariaServer.exe to generate worlds from program-built copied seeds and banks
// the .wld file after metadata and optional pyramid validation pass.
// It backs off once the pool reaches the target count and resumes when worlds are consumed.
// This is a background task, not a dedicated UI thread; the expensive work happens in a
// separate TerrariaServer.exe process.
public sealed class WorldPoolFillService : IDisposable
{
    private static readonly TimeSpan IdleInterval = TimeSpan.FromSeconds(8);

    private readonly WorldPoolStore store;
    private readonly HeadlessWorldGenerator generator;
    private readonly ISettingsSnapshotFactory settingsSnapshots;
    private readonly IAppLogger logger;
    private readonly object sync = new();
    private AppSettings? settings;
    private CancellationTokenSource? cancellation;
    private Task? loop;
    private bool disposed;
    private bool loggedMissingServer;

    public WorldPoolFillService(
        WorldPoolStore store,
        ISettingsSnapshotFactory settingsSnapshots,
        IAppLogger? logger = null,
        IRuntimeDataPaths? paths = null)
    {
        this.store = store;
        generator = new HeadlessWorldGenerator(paths);
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

        store.EnsureSignature(WorldPoolSignature.From(clone));
        EnsureLoopRunning();
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
            return false;
        }

        AutoCreateWorldSettings autoCreate = current.Automation.AutoCreate;
        if (!autoCreate.EnableWorldPool)
        {
            return false;
        }

        string signature = WorldPoolSignature.From(current);
        if (store.Count(signature) >= autoCreate.WorldPoolTargetCount)
        {
            return false;
        }

        string? serverExe = TerrariaServerLocator.TryResolve();
        if (serverExe is null)
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

        HeadlessWorldGenResult result = await generator.GenerateAndScanAsync(serverExe, current.General.Language, autoCreate, cancellationToken);
        try
        {
            if (result.Keep &&
                IsGenerationStillCurrent(signature) &&
                store.TryAdd(signature, result.WorldPath, result.Metadata, out WorldPoolEntry entry))
            {
                logger.Info(
                    $"World pool banked world {entry.WorldFileName}; pool now holds " +
                    $"{store.Count(signature)}/{autoCreate.WorldPoolTargetCount}.");
            }
        }
        finally
        {
            generator.ClearScratch();
        }

        return result.Generated;
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
            string.Equals(WorldPoolSignature.From(current), signature, StringComparison.Ordinal);
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
