namespace TerrariaSplit;

// Background worker that keeps the seed pool topped up. While seed pooling is enabled and
// supported, it repeatedly asks TerrariaServer.exe to generate a random-seed world
// headlessly and banks the copied seed read from the .wld when the world has a pyramid.
// It backs off once the pool reaches the target count and resumes when seeds are consumed.
// This is a background task, not a dedicated UI thread; the expensive work happens in a
// separate TerrariaServer.exe process.
internal sealed class SeedPoolFillService : IDisposable
{
    private static readonly TimeSpan IdleInterval = TimeSpan.FromSeconds(8);

    private readonly SeedPoolStore store;
    private readonly HeadlessWorldGenerator generator = new();
    private readonly object sync = new();
    private AppSettings? settings;
    private CancellationTokenSource? cancellation;
    private Task? loop;
    private bool disposed;
    private bool loggedMissingServer;

    public SeedPoolFillService(SeedPoolStore store)
    {
        this.store = store;
    }

    // Called at startup and whenever settings are applied. Refreshing the signature clears
    // the pool when any world-gen setting changed, and starts the loop if needed.
    public void UpdateSettings(AppSettings newSettings)
    {
        AppSettings clone = AppSettingsStore.Clone(newSettings);
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            settings = clone;
            loggedMissingServer = false;
        }

        store.EnsureSignature(WorldGenSignature.From(clone.AutoCreate));
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
                AppLogger.Error(ex, "Seed pool fill iteration failed.");
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

        AutoCreateWorldSettings autoCreate = current.AutoCreate;
        if (!autoCreate.EnablePyramidFilter ||
            !autoCreate.EnableSeedPool ||
            !SeedPoolSupport.IsSupported(autoCreate))
        {
            return false;
        }

        string signature = WorldGenSignature.From(autoCreate);
        if (store.Count(signature) >= autoCreate.SeedPoolTargetCount)
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
                    AppLogger.Info("Seed pool fill is idle because TerrariaServer.exe could not be located.");
                }
            }

            return false;
        }

        HeadlessWorldGenResult result = await generator.GenerateAndScanAsync(serverExe, autoCreate, cancellationToken);
        if (result.Keep &&
            IsGenerationStillCurrent(signature) &&
            store.TryAdd(signature, result.Seed))
        {
            AppLogger.Info($"Seed pool banked seed {result.Seed}; pool now holds {store.Count(signature)}/{autoCreate.SeedPoolTargetCount}.");
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

        AutoCreateWorldSettings autoCreate = current.AutoCreate;
        return autoCreate.EnablePyramidFilter &&
            autoCreate.EnableSeedPool &&
            SeedPoolSupport.IsSupported(autoCreate) &&
            string.Equals(WorldGenSignature.From(autoCreate), signature, StringComparison.Ordinal);
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
