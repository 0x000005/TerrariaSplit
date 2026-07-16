using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using TerrariaSplit.Configuration;
using TerrariaSplit.Terraria.Processes;

namespace TerrariaSplit.Terraria;

internal sealed record TerrariaRaceWorldLockTarget(
    string WorldPath,
    int WorldId,
    Guid UniqueId,
    TerrariaRaceDeterminismConfiguration Determinism,
    TerrariaPlanteraBulbPlan PlanteraBulbPlan,
    bool EntryAllowed);

internal sealed record TerrariaRaceInitialPlayerConfiguration(
    string PlayerName,
    string PlayerTemplateCode,
    string PlayerDifficulty);

internal sealed record TerrariaRaceDeterminismConfiguration(
    int ProtocolVersion,
    string EpochId,
    string EntropySeedBase64,
    string TerrariaCompatibilityId,
    int EnabledCapabilities,
    int ChancePolicyVersion,
    string PackageDigest);

internal sealed record TerrariaRaceWorldLockResult(
    bool Succeeded,
    string Message,
    int? ProcessId)
{
    public static TerrariaRaceWorldLockResult Success(int? processId = null, string message = "") =>
        new(true, message, processId);

    public static TerrariaRaceWorldLockResult Failure(string message, int? processId = null) =>
        new(false, message, processId);
}

internal enum TerrariaRaceWorldLockState
{
    Inactive,
    Injecting,
    Configuring,
    Active,
    Stopping,
    Faulted
}

internal interface ITerrariaRaceWorldLockService
{
    bool IsLocked { get; }

    event Action<TerrariaRaceWorldLockResult>? HealthFailed;

    Task<TerrariaRaceWorldLockResult> LockAsync(
        TerrariaRaceWorldLockTarget target,
        TerrariaRaceInitialPlayerConfiguration player,
        string rejectionMessage,
        CancellationToken cancellationToken = default);

    Task<TerrariaRaceWorldLockResult> ResetDeterminismAsync(CancellationToken cancellationToken = default);

    Task<TerrariaRaceWorldLockResult> PrepareRestartAsync(CancellationToken cancellationToken = default);

    Task<TerrariaRaceWorldLockResult> ReturnToMainMenuAsync(CancellationToken cancellationToken = default);

    Task<TerrariaRaceWorldLockResult> StartRaceAsync(
        TimeSpan countdownDuration,
        string countdownFormat,
        CancellationToken cancellationToken = default);

    Task<TerrariaRaceWorldLockResult> UnlockAsync(CancellationToken cancellationToken = default);
}

internal sealed class TerrariaRaceWorldLockService : ITerrariaRaceWorldLockService, IDisposable
{
    private static readonly TimeSpan InjectorTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PipeTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PlayerCreationPipeTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StartupReadinessTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan StartupReadinessPollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan StartupWindowStabilityDuration = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan StartupInjectionRetryInterval = TimeSpan.FromMilliseconds(500);
    private const string MemoryBridgeFileName = "TerrariaSplit.MemoryBridge.exe";
    private const string BootstrapFileName = "TerrariaSplit.WorldGuard.Bootstrap.dll";
    private const string PayloadFileName = "TerrariaSplit.WorldGuard.Payload.dll";
    private readonly SemaphoreSlim commandGate = new(1, 1);
    private readonly string assetsDirectory;
    private CancellationTokenSource? heartbeatCancellation;
    private Task? heartbeatTask;
    private string? activePipeName;
    private string? activePackageDigest;
    private string? activeStagingDirectory;
    private string? activeLockKey;
    private string? activePayloadVersion;
    private string? lastProvisionedLockKey;
    private string? lastProvisionedPlayerPath;
    private int lockedProcessId;
    private int lifecycleState;
    private int healthFailurePublished;
    private bool disposed;

    public TerrariaRaceWorldLockService()
        : this(Path.Combine(AppContext.BaseDirectory, "Runtime", "Injector"))
    {
    }

    internal TerrariaRaceWorldLockService(string assetsDirectory)
    {
        this.assetsDirectory = Path.GetFullPath(assetsDirectory);
    }

    public bool IsLocked => Volatile.Read(ref lockedProcessId) > 0;

    internal TerrariaRaceWorldLockState State => (TerrariaRaceWorldLockState)Volatile.Read(ref lifecycleState);

    public event Action<TerrariaRaceWorldLockResult>? HealthFailed;

    public async Task<TerrariaRaceWorldLockResult> LockAsync(
        TerrariaRaceWorldLockTarget target,
        TerrariaRaceInitialPlayerConfiguration player,
        string rejectionMessage,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(player);
        if (string.IsNullOrWhiteSpace(target.WorldPath) ||
            string.IsNullOrWhiteSpace(player.PlayerName) ||
            string.IsNullOrWhiteSpace(rejectionMessage) ||
            string.IsNullOrWhiteSpace(target.Determinism.PackageDigest) ||
            target.PlanteraBulbPlan is null)
        {
            return TerrariaRaceWorldLockResult.Failure("The Race hook target or package is empty.");
        }

        await commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        bool transitionStarted = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            string lockKey = CreateLockKey(target, player, rejectionMessage);
            int currentProcessId = Volatile.Read(ref lockedProcessId);
            if (State == TerrariaRaceWorldLockState.Active &&
                currentProcessId > 0 &&
                string.Equals(activeLockKey, lockKey, StringComparison.Ordinal) &&
                TryGetLiveProcess(currentProcessId, out Process? currentProcess))
            {
                currentProcess!.Dispose();
                TerrariaRaceWorldLockResult currentStatus = await SendPipeCommandAsync(
                    currentProcessId,
                    activePipeName!,
                    "status",
                    cancellationToken).ConfigureAwait(false);
                if (currentStatus.Succeeded &&
                    string.Equals(currentStatus.Message, activePackageDigest, StringComparison.Ordinal))
                {
                    return TerrariaRaceWorldLockResult.Success(currentProcessId, currentStatus.Message);
                }
            }

            StopHeartbeat();
            transitionStarted = true;
            SetState(TerrariaRaceWorldLockState.Stopping);
            TerrariaRaceWorldLockResult shutdown = await ShutdownActivePayloadAsync(cancellationToken).ConfigureAwait(false);
            if (!shutdown.Succeeded)
            {
                SetState(TerrariaRaceWorldLockState.Active);
                StartHeartbeat();
                return shutdown;
            }
            using Process? terraria = TerrariaProcessFinder.FindNewest();
            if (terraria is null)
            {
                SetState(TerrariaRaceWorldLockState.Inactive);
                return TerrariaRaceWorldLockResult.Failure(
                    "Terraria.exe must be running before the Race package can become ready.");
            }

            TerrariaRaceWorldLockResult startupReady = await WaitForTerrariaStartupAsync(
                terraria,
                cancellationToken).ConfigureAwait(false);
            if (!startupReady.Succeeded)
            {
                SetState(TerrariaRaceWorldLockState.Inactive);
                return startupReady;
            }

            string pipeName = CreatePipeName(terraria.Id);
            string startCommand = "start\n" + Convert.ToBase64String(Encoding.UTF8.GetBytes(pipeName));
            string stagingDirectory;
            try
            {
                stagingDirectory = PrepareStagingDirectory(terraria.Id);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                SetState(TerrariaRaceWorldLockState.Faulted);
                return TerrariaRaceWorldLockResult.Failure(
                    "The Race hook staging directory could not be prepared: " + ex.Message,
                    terraria.Id);
            }

            activeStagingDirectory = stagingDirectory;
            activePipeName = pipeName;
            activePackageDigest = target.Determinism.PackageDigest;
            try
            {
                activePayloadVersion = ReadPayloadVersion(stagingDirectory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException)
            {
                TryDeleteDirectory(stagingDirectory);
                ClearActiveState();
                SetState(TerrariaRaceWorldLockState.Faulted);
                return TerrariaRaceWorldLockResult.Failure(
                    "The Race hook payload version could not be read: " + ex.Message,
                    terraria.Id);
            }
            Volatile.Write(ref lockedProcessId, terraria.Id);
            SetState(TerrariaRaceWorldLockState.Injecting);
            TerrariaRaceWorldLockResult start = await RunInjectorWhenReadyAsync(
                terraria,
                Path.Combine(stagingDirectory, BootstrapFileName),
                startCommand,
                cancellationToken).ConfigureAwait(false);
            if (!start.Succeeded)
            {
                QueueStagingCleanup(terraria.Id, stagingDirectory);
                ClearActiveState();
                SetState(TerrariaRaceWorldLockState.Faulted);
                return start;
            }

            SetState(TerrariaRaceWorldLockState.Configuring);
            TerrariaRaceWorldLockResult version = await SendPipeCommandAsync(
                terraria.Id,
                pipeName,
                "version",
                cancellationToken).ConfigureAwait(false);
            if (!version.Succeeded ||
                !string.Equals(version.Message, activePayloadVersion, StringComparison.Ordinal))
            {
                return await FailLockTransitionAsync(
                    terraria.Id,
                    "A different Race hook payload is already loaded. Restart Terraria before continuing.").ConfigureAwait(false);
            }

            TerrariaRaceWorldLockResult createdPlayer;
            if (string.Equals(lastProvisionedLockKey, lockKey, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(lastProvisionedPlayerPath) &&
                File.Exists(lastProvisionedPlayerPath))
            {
                createdPlayer = TerrariaRaceWorldLockResult.Success(terraria.Id, lastProvisionedPlayerPath);
            }
            else
            {
                createdPlayer = await CreatePlayerWhenReadyAsync(
                    terraria.Id,
                    pipeName,
                    BuildCreatePlayerCommand(player),
                    cancellationToken).ConfigureAwait(false);
                if (createdPlayer.Succeeded && !string.IsNullOrWhiteSpace(createdPlayer.Message))
                {
                    lastProvisionedLockKey = lockKey;
                    lastProvisionedPlayerPath = Path.GetFullPath(createdPlayer.Message);
                }
            }
            if (!createdPlayer.Succeeded || string.IsNullOrWhiteSpace(createdPlayer.Message))
            {
                TerrariaRaceWorldLockResult failure = createdPlayer.Succeeded
                    ? TerrariaRaceWorldLockResult.Failure("The Race hook did not return the created player path.", terraria.Id)
                    : createdPlayer;
                return await FailLockTransitionAsync(terraria.Id, failure.Message).ConfigureAwait(false);
            }

            TerrariaRaceWorldLockResult configured = await SendPipeCommandAsync(
                terraria.Id,
                pipeName,
                BuildLockCommand(target, createdPlayer.Message, rejectionMessage),
                cancellationToken).ConfigureAwait(false);
            if (!configured.Succeeded ||
                !string.Equals(configured.Message, activePackageDigest, StringComparison.Ordinal))
            {
                TerrariaRaceWorldLockResult failure = configured.Succeeded
                    ? TerrariaRaceWorldLockResult.Failure("The Race hook returned the wrong package digest.", terraria.Id)
                    : configured;
                return await FailLockTransitionAsync(terraria.Id, failure.Message).ConfigureAwait(false);
            }

            TerrariaRaceWorldLockResult status = await SendPipeCommandAsync(
                terraria.Id,
                pipeName,
                "status",
                cancellationToken).ConfigureAwait(false);
            if (!status.Succeeded ||
                !string.Equals(status.Message, activePackageDigest, StringComparison.Ordinal))
            {
                TerrariaRaceWorldLockResult failure = status.Succeeded
                    ? TerrariaRaceWorldLockResult.Failure("The Race hook handshake digest did not match.", terraria.Id)
                    : status;
                return await FailLockTransitionAsync(terraria.Id, failure.Message).ConfigureAwait(false);
            }

            activeLockKey = lockKey;
            Interlocked.Exchange(ref healthFailurePublished, 0);
            SetState(TerrariaRaceWorldLockState.Active);
            StartHeartbeat();
            return TerrariaRaceWorldLockResult.Success(terraria.Id, status.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            int processId = Volatile.Read(ref lockedProcessId);
            if (transitionStarted && processId > 0)
            {
                _ = await FailLockTransitionAsync(processId, "The Race hook transition was canceled.").ConfigureAwait(false);
            }
            else
            {
                ClearActiveState();
            }

            throw;
        }
        catch (Exception ex)
        {
            int processId = Volatile.Read(ref lockedProcessId);
            if (transitionStarted && processId > 0)
            {
                return await FailLockTransitionAsync(
                    processId,
                    "The Race hook transition failed: " + ex.Message).ConfigureAwait(false);
            }

            ClearActiveState();
            SetState(TerrariaRaceWorldLockState.Faulted);
            return TerrariaRaceWorldLockResult.Failure("The Race hook transition failed: " + ex.Message);
        }
        finally
        {
            commandGate.Release();
        }
    }

    public async Task<TerrariaRaceWorldLockResult> UnlockAsync(CancellationToken cancellationToken = default)
    {
        if (disposed)
        {
            return TerrariaRaceWorldLockResult.Success();
        }

        await commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopHeartbeat();
            SetState(TerrariaRaceWorldLockState.Stopping);
            try
            {
                TerrariaRaceWorldLockResult result = await ShutdownActivePayloadAsync(cancellationToken).ConfigureAwait(false);
                if (!result.Succeeded && IsLocked)
                {
                    SetState(TerrariaRaceWorldLockState.Active);
                    StartHeartbeat();
                }

                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (IsLocked)
                {
                    SetState(TerrariaRaceWorldLockState.Active);
                    StartHeartbeat();
                }

                throw;
            }
        }
        finally
        {
            commandGate.Release();
        }
    }

    public async Task<TerrariaRaceWorldLockResult> ResetDeterminismAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopHeartbeat();
            int processId = Volatile.Read(ref lockedProcessId);
            string? pipeName = activePipeName;
            string? digest = activePackageDigest;
            Process? process = null;
            if (processId <= 0 || string.IsNullOrWhiteSpace(pipeName) || string.IsNullOrWhiteSpace(digest))
            {
                SetState(TerrariaRaceWorldLockState.Faulted);
                return TerrariaRaceWorldLockResult.Failure("The Race hook is not active.", processId > 0 ? processId : null);
            }

            if (!TryGetLiveProcess(processId, out process))
            {
                process?.Dispose();
                ClearActiveState();
                SetState(TerrariaRaceWorldLockState.Faulted);
                return TerrariaRaceWorldLockResult.Failure("The Terraria process running the Race hook exited.", processId);
            }

            process!.Dispose();
            TerrariaRaceWorldLockResult reset = await SendPipeCommandAsync(
                processId,
                pipeName,
                "reset",
                cancellationToken).ConfigureAwait(false);
            if (!reset.Succeeded || !string.Equals(reset.Message, digest, StringComparison.Ordinal))
            {
                SetState(TerrariaRaceWorldLockState.Active);
                StartHeartbeat();
                return reset.Succeeded
                    ? TerrariaRaceWorldLockResult.Failure("The Race hook returned the wrong package digest after reset.", processId)
                    : reset;
            }

            Interlocked.Exchange(ref healthFailurePublished, 0);
            SetState(TerrariaRaceWorldLockState.Active);
            StartHeartbeat();
            return reset;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (IsLocked)
            {
                SetState(TerrariaRaceWorldLockState.Active);
                StartHeartbeat();
            }

            throw;
        }
        finally
        {
            commandGate.Release();
        }
    }

    public async Task<TerrariaRaceWorldLockResult> PrepareRestartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopHeartbeat();
            int processId = Volatile.Read(ref lockedProcessId);
            string? pipeName = activePipeName;
            Process? process = null;
            if (processId <= 0 || string.IsNullOrWhiteSpace(pipeName))
            {
                SetState(TerrariaRaceWorldLockState.Faulted);
                return TerrariaRaceWorldLockResult.Failure(
                    "The Race hook is not active.",
                    processId > 0 ? processId : null);
            }

            if (!TryGetLiveProcess(processId, out process))
            {
                process?.Dispose();
                ClearActiveState();
                SetState(TerrariaRaceWorldLockState.Faulted);
                return TerrariaRaceWorldLockResult.Failure(
                    "The Terraria process running the Race hook exited.",
                    processId);
            }

            process!.Dispose();
            TerrariaRaceWorldLockResult prepared = await SendPipeCommandAsync(
                processId,
                pipeName,
                "prepare-restart",
                cancellationToken,
                PlayerCreationPipeTimeout).ConfigureAwait(false);
            SetState(TerrariaRaceWorldLockState.Active);
            StartHeartbeat();
            if (!prepared.Succeeded)
            {
                return prepared;
            }

            activeLockKey = null;
            lastProvisionedLockKey = null;
            lastProvisionedPlayerPath = null;
            Interlocked.Exchange(ref healthFailurePublished, 0);
            return prepared;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (IsLocked)
            {
                SetState(TerrariaRaceWorldLockState.Active);
                StartHeartbeat();
            }

            throw;
        }
        finally
        {
            commandGate.Release();
        }
    }

    public Task<TerrariaRaceWorldLockResult> ReturnToMainMenuAsync(CancellationToken cancellationToken = default)
    {
        return SendLifecycleCommandAsync("return-menu", clearProvisionedPlayer: false, cancellationToken);
    }

    public Task<TerrariaRaceWorldLockResult> StartRaceAsync(
        TimeSpan countdownDuration,
        string countdownFormat,
        CancellationToken cancellationToken = default)
    {
        string command = BuildStartRaceCommand(countdownDuration, countdownFormat);
        return SendLifecycleCommandAsync(command, clearProvisionedPlayer: false, cancellationToken);
    }

    internal static string BuildStartRaceCommand(TimeSpan countdownDuration, string countdownFormat)
    {
        return string.Join(
            '\n',
            "start-race",
            checked((long)countdownDuration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(Encoding.UTF8.GetBytes(countdownFormat)));
    }

    private async Task<TerrariaRaceWorldLockResult> SendLifecycleCommandAsync(
        string command,
        bool clearProvisionedPlayer,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopHeartbeat();
            int processId = Volatile.Read(ref lockedProcessId);
            string? pipeName = activePipeName;
            if (processId <= 0 || string.IsNullOrWhiteSpace(pipeName))
            {
                SetState(TerrariaRaceWorldLockState.Faulted);
                return TerrariaRaceWorldLockResult.Failure("The Race hook is not active.", processId > 0 ? processId : null);
            }

            if (!TryGetLiveProcess(processId, out Process? process))
            {
                process?.Dispose();
                ClearActiveState();
                SetState(TerrariaRaceWorldLockState.Faulted);
                return TerrariaRaceWorldLockResult.Failure("The Terraria process running the Race hook exited.", processId);
            }

            process!.Dispose();
            TerrariaRaceWorldLockResult result = await SendPipeCommandAsync(
                processId,
                pipeName,
                command,
                cancellationToken,
                PlayerCreationPipeTimeout).ConfigureAwait(false);
            SetState(TerrariaRaceWorldLockState.Active);
            StartHeartbeat();
            if (!result.Succeeded)
            {
                return result;
            }

            activeLockKey = null;
            if (clearProvisionedPlayer)
            {
                lastProvisionedLockKey = null;
                lastProvisionedPlayerPath = null;
            }

            Interlocked.Exchange(ref healthFailurePublished, 0);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (IsLocked)
            {
                SetState(TerrariaRaceWorldLockState.Active);
                StartHeartbeat();
            }

            throw;
        }
        finally
        {
            commandGate.Release();
        }
    }

    internal static string BuildCreatePlayerCommand(TerrariaRaceInitialPlayerConfiguration player)
    {
        return string.Join(
            '\n',
            "create-player",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(player.PlayerName.Trim())),
            Convert.ToBase64String(Encoding.UTF8.GetBytes(player.PlayerTemplateCode ?? string.Empty)),
            AutoCreatePlayerDifficulty.Normalize(player.PlayerDifficulty));
    }

    internal static string BuildLockCommand(
        TerrariaRaceWorldLockTarget target,
        string playerPath,
        string rejectionMessage)
    {
        string fullPath = Path.GetFullPath(target.WorldPath);
        string path = Convert.ToBase64String(Encoding.UTF8.GetBytes(fullPath));
        string player = Convert.ToBase64String(Encoding.UTF8.GetBytes(playerPath));
        string message = Convert.ToBase64String(Encoding.UTF8.GetBytes(rejectionMessage));
        return string.Join(
            '\n',
            "configure",
            path,
            target.WorldId.ToString(CultureInfo.InvariantCulture),
            target.UniqueId.ToString("D"),
            player,
            message,
            target.Determinism.ProtocolVersion.ToString(CultureInfo.InvariantCulture),
            target.Determinism.EpochId,
            target.Determinism.EntropySeedBase64,
            Convert.ToBase64String(Encoding.UTF8.GetBytes(target.Determinism.TerrariaCompatibilityId)),
            target.Determinism.EnabledCapabilities.ToString(CultureInfo.InvariantCulture),
            target.Determinism.ChancePolicyVersion.ToString(CultureInfo.InvariantCulture),
            target.PlanteraBulbPlan.Encode(),
            target.EntryAllowed ? "1" : "0",
            target.Determinism.PackageDigest);
    }

    internal static string CreatePipeName(int processId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
        return $"TerrariaSplit.RaceHook.{processId}";
    }

    public void Dispose()
    {
        disposed = true;
        StopHeartbeat();
    }

    private void StartHeartbeat()
    {
        var cancellation = new CancellationTokenSource();
        heartbeatCancellation = cancellation;
        heartbeatTask = Task.Run(() => RunHeartbeatAsync(cancellation.Token));
    }

    private void StopHeartbeat()
    {
        CancellationTokenSource? cancellation = Interlocked.Exchange(ref heartbeatCancellation, null);
        cancellation?.Cancel();
        cancellation?.Dispose();
        heartbeatTask = null;
    }

    private async Task RunHeartbeatAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(HeartbeatInterval, cancellationToken).ConfigureAwait(false);
                await commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                TerrariaRaceWorldLockResult status;
                try
                {
                    int processId = Volatile.Read(ref lockedProcessId);
                    string? pipeName = activePipeName;
                    string? digest = activePackageDigest;
                    Process? process = null;
                    if (processId <= 0 || string.IsNullOrWhiteSpace(pipeName) ||
                        !TryGetLiveProcess(processId, out process))
                    {
                        process?.Dispose();
                        status = TerrariaRaceWorldLockResult.Failure(
                            "The Terraria process running the Race hook exited.",
                            processId > 0 ? processId : null);
                    }
                    else
                    {
                        process!.Dispose();
                        status = await SendPipeCommandAsync(
                            processId,
                            pipeName,
                            "status",
                            cancellationToken).ConfigureAwait(false);
                        if (status.Succeeded && !string.Equals(status.Message, digest, StringComparison.Ordinal))
                        {
                            status = TerrariaRaceWorldLockResult.Failure(
                                "The Race hook heartbeat returned the wrong package digest.",
                                processId);
                        }
                    }
                }
                finally
                {
                    commandGate.Release();
                }

                if (!status.Succeeded)
                {
                    PublishHealthFailure(status);
                    return;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private void PublishHealthFailure(TerrariaRaceWorldLockResult failure)
    {
        SetState(TerrariaRaceWorldLockState.Faulted);
        if (Interlocked.Exchange(ref healthFailurePublished, 1) == 0)
        {
            HealthFailed?.Invoke(failure);
        }
    }

    private async Task<TerrariaRaceWorldLockResult> ShutdownActivePayloadAsync(CancellationToken cancellationToken)
    {
        int processId = Volatile.Read(ref lockedProcessId);
        string? pipeName = activePipeName;
        if (processId <= 0 || string.IsNullOrWhiteSpace(pipeName))
        {
            ClearActiveState();
            return TerrariaRaceWorldLockResult.Success(processId > 0 ? processId : null);
        }

        Process? process = null;
        if (!TryGetLiveProcess(processId, out process))
        {
            process?.Dispose();
            TryDeleteDirectory(activeStagingDirectory);
            ClearActiveState();
            return TerrariaRaceWorldLockResult.Success(processId);
        }

        process!.Dispose();
        TerrariaRaceWorldLockResult result = await SendPipeCommandAsync(
            processId,
            pipeName,
            "shutdown",
            cancellationToken).ConfigureAwait(false);
        if (result.Succeeded)
        {
            QueueStagingCleanup(processId, activeStagingDirectory);
            ClearActiveState();
        }

        return result;
    }

    private void ClearActiveState()
    {
        activePipeName = null;
        activePackageDigest = null;
        activeStagingDirectory = null;
        activeLockKey = null;
        activePayloadVersion = null;
        Volatile.Write(ref lockedProcessId, 0);
        SetState(TerrariaRaceWorldLockState.Inactive);
    }

    private async Task<TerrariaRaceWorldLockResult> FailLockTransitionAsync(int processId, string message)
    {
        string? stagingDirectory = activeStagingDirectory;
        try
        {
            _ = await ShutdownActivePayloadAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
        }

        QueueStagingCleanup(processId, stagingDirectory);
        ClearActiveState();
        SetState(TerrariaRaceWorldLockState.Faulted);
        return TerrariaRaceWorldLockResult.Failure(message, processId);
    }

    private static string CreateLockKey(
        TerrariaRaceWorldLockTarget target,
        TerrariaRaceInitialPlayerConfiguration player,
        string rejectionMessage)
    {
        return string.Join(
            "\n",
            Path.GetFullPath(target.WorldPath),
            target.WorldId.ToString(CultureInfo.InvariantCulture),
            target.UniqueId.ToString("D"),
            target.Determinism.PackageDigest,
            target.PlanteraBulbPlan.CreateDigest(),
            target.EntryAllowed ? "1" : "0",
            player.PlayerName.Trim(),
            player.PlayerTemplateCode ?? string.Empty,
            AutoCreatePlayerDifficulty.Normalize(player.PlayerDifficulty),
            rejectionMessage);
    }

    private static string ReadPayloadVersion(string stagingDirectory)
    {
        Version? version = AssemblyName.GetAssemblyName(Path.Combine(stagingDirectory, PayloadFileName)).Version;
        return version?.ToString() ?? string.Empty;
    }

    private void SetState(TerrariaRaceWorldLockState state)
    {
        Volatile.Write(ref lifecycleState, (int)state);
    }

    private async Task<TerrariaRaceWorldLockResult> SendPipeCommandAsync(
        int processId,
        string pipeName,
        string command,
        CancellationToken cancellationToken,
        TimeSpan? commandTimeout = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(commandTimeout ?? PipeTimeout);
        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true)
            {
                AutoFlush = true
            };
            using var reader = new StreamReader(pipe, new UTF8Encoding(false), leaveOpen: true);
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(command));
            await writer.WriteLineAsync(encoded.AsMemory(), timeout.Token).ConfigureAwait(false);
            string? response = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(response))
            {
                return TerrariaRaceWorldLockResult.Failure("The Race hook returned an empty response.", processId);
            }

            int separator = response.IndexOf('|');
            if (separator <= 0 ||
                !int.TryParse(response[..separator], NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
            {
                return TerrariaRaceWorldLockResult.Failure("The Race hook returned an invalid response.", processId);
            }

            string message;
            try
            {
                message = Encoding.UTF8.GetString(Convert.FromBase64String(response[(separator + 1)..]));
            }
            catch (FormatException)
            {
                return TerrariaRaceWorldLockResult.Failure("The Race hook response was not valid UTF-8 data.", processId);
            }

            return code == 0
                ? TerrariaRaceWorldLockResult.Success(processId, message)
                : TerrariaRaceWorldLockResult.Failure(
                    string.IsNullOrWhiteSpace(message) ? $"The Race hook rejected command {code}." : message,
                    processId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return TerrariaRaceWorldLockResult.Failure("The Race hook pipe timed out.", processId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return TerrariaRaceWorldLockResult.Failure(ex.Message, processId);
        }
    }

    private async Task<TerrariaRaceWorldLockResult> RunInjectorAsync(
        int processId,
        string bootstrapPath,
        string command,
        CancellationToken cancellationToken)
    {
        string injectorPath = Path.Combine(AppContext.BaseDirectory, "TerrariaSplit.MemoryBridge", MemoryBridgeFileName);
        if (!File.Exists(injectorPath))
        {
            injectorPath = Path.Combine(AppContext.BaseDirectory, MemoryBridgeFileName);
        }

        string? missing = RequiredFiles(assetsDirectory).FirstOrDefault(path => !File.Exists(path));
        if (!File.Exists(injectorPath))
        {
            missing = injectorPath;
        }
        if (missing is not null)
        {
            return TerrariaRaceWorldLockResult.Failure(
                "Race hook component is missing: " + Path.GetFileName(missing),
                processId);
        }

        var startInfo = new ProcessStartInfo(injectorPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("inject");
        startInfo.ArgumentList.Add(processId.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(bootstrapPath);
        startInfo.ArgumentList.Add(command);

        try
        {
            using Process? injector = Process.Start(startInfo);
            if (injector is null)
            {
                return TerrariaRaceWorldLockResult.Failure("The Race hook injector could not be started.", processId);
            }

            Task<string> stdout = injector.StandardOutput.ReadToEndAsync();
            Task<string> stderr = injector.StandardError.ReadToEndAsync();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(InjectorTimeout);
            try
            {
                await injector.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(injector);
                await injector.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                _ = await stdout.ConfigureAwait(false);
                _ = await stderr.ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return TerrariaRaceWorldLockResult.Failure("The Race hook injector timed out.", processId);
            }

            string output = (await stdout.ConfigureAwait(false)).Trim();
            string error = (await stderr.ConfigureAwait(false)).Trim();
            if (injector.ExitCode == 0)
            {
                return TerrariaRaceWorldLockResult.Success(processId);
            }

            string detail = !string.IsNullOrWhiteSpace(error) ? error : output;
            return TerrariaRaceWorldLockResult.Failure(
                string.IsNullOrWhiteSpace(detail)
                    ? $"The Race hook injector failed with exit code {injector.ExitCode}."
                    : detail,
                processId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return TerrariaRaceWorldLockResult.Failure(ex.Message, processId);
        }
    }

    private async Task<TerrariaRaceWorldLockResult> RunInjectorWhenReadyAsync(
        Process process,
        string bootstrapPath,
        string command,
        CancellationToken cancellationToken)
    {
        int processId = process.Id;
        DateTimeOffset deadline = DateTimeOffset.UtcNow + StartupReadinessTimeout;
        TerrariaRaceWorldLockResult lastResult;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                process.Refresh();
                if (process.HasExited)
                {
                    return TerrariaRaceWorldLockResult.Failure(
                        "Terraria process running the Race hook exited before startup completed.",
                        processId);
                }
            }
            catch (InvalidOperationException)
            {
                return TerrariaRaceWorldLockResult.Failure(
                    "Terraria process running the Race hook exited before startup completed.",
                    processId);
            }

            lastResult = await RunInjectorAsync(
                processId,
                bootstrapPath,
                command,
                cancellationToken).ConfigureAwait(false);
            if (lastResult.Succeeded || !IsTransientStartupInjectionFailure(lastResult.Message))
            {
                return lastResult;
            }

            await Task.Delay(StartupInjectionRetryInterval, cancellationToken).ConfigureAwait(false);
        }
        while (DateTimeOffset.UtcNow < deadline);

        return TerrariaRaceWorldLockResult.Failure(
            "Terraria is still starting; the Race hook will retry.",
            processId);
    }

    private async Task<TerrariaRaceWorldLockResult> CreatePlayerWhenReadyAsync(
        int processId,
        string pipeName,
        string command,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + StartupReadinessTimeout;
        do
        {
            TerrariaRaceWorldLockResult result = await SendPipeCommandAsync(
                processId,
                pipeName,
                command,
                cancellationToken,
                PlayerCreationPipeTimeout).ConfigureAwait(false);
            if (result.Succeeded ||
                !result.Message.Contains(
                    "Terraria is still starting; Race player creation will retry.",
                    StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }

            await Task.Delay(StartupInjectionRetryInterval, cancellationToken).ConfigureAwait(false);
        }
        while (DateTimeOffset.UtcNow < deadline);

        return TerrariaRaceWorldLockResult.Failure(
            "Terraria is still starting; the Race hook will retry.",
            processId);
    }

    internal static bool IsTransientStartupInjectionFailure(string message)
    {
        return message.Contains("bootstrap=0x80070015", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("payload=10", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<TerrariaRaceWorldLockResult> WaitForTerrariaStartupAsync(
        Process process,
        CancellationToken cancellationToken)
    {
        int processId = process.Id;
        DateTimeOffset deadline = DateTimeOffset.UtcNow + StartupReadinessTimeout;
        DateTimeOffset? windowReadySince = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                process.Refresh();
                if (process.HasExited)
                {
                    return TerrariaRaceWorldLockResult.Failure(
                        "Terraria process running the Race hook exited before startup completed.",
                        processId);
                }

                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    windowReadySince ??= DateTimeOffset.UtcNow;
                    if (DateTimeOffset.UtcNow - windowReadySince >= StartupWindowStabilityDuration)
                    {
                        return TerrariaRaceWorldLockResult.Success(processId);
                    }
                }
                else
                {
                    windowReadySince = null;
                }
            }
            catch (InvalidOperationException)
            {
                return TerrariaRaceWorldLockResult.Failure(
                    "Terraria process running the Race hook exited before startup completed.",
                    processId);
            }

            await Task.Delay(StartupReadinessPollInterval, cancellationToken).ConfigureAwait(false);
        }

        return TerrariaRaceWorldLockResult.Failure(
            "Terraria is still starting; the Race hook will retry.",
            processId);
    }

    private static IEnumerable<string> RequiredFiles(string directory)
    {
        yield return Path.Combine(directory, BootstrapFileName);
        yield return Path.Combine(directory, PayloadFileName);
        yield return Path.Combine(directory, "0Harmony.dll");
        yield return Path.Combine(directory, "TerrariaSplit.Race.Determinism.dll");
        yield return Path.Combine(directory, "terraria-compatibility.json");
    }

    private string PrepareStagingDirectory(int processId)
    {
        string root = Path.Combine(Path.GetTempPath(), "TerrariaSplit", "RaceHook");
        CleanupExitedProcessStagingDirectories(root);
        string directory = Path.Combine(root, $"{processId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        foreach (string source in RequiredFiles(assetsDirectory))
        {
            File.Copy(source, Path.Combine(directory, Path.GetFileName(source)), overwrite: false);
        }

        return directory;
    }

    private static void QueueStagingCleanup(int processId, string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                using Process process = Process.GetProcessById(processId);
                process.WaitForExit();
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            TryDeleteDirectory(directory);
        });
    }

    private static void CleanupExitedProcessStagingDirectories(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (string directory in Directory.EnumerateDirectories(root))
        {
            string name = Path.GetFileName(directory);
            int separator = name.IndexOf('-');
            if (separator <= 0 ||
                !int.TryParse(name[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out int processId))
            {
                continue;
            }

            if (TryGetLiveProcess(processId, out Process? process))
            {
                process!.Dispose();
                continue;
            }

            process?.Dispose();

            TryDeleteDirectory(directory);
        }
    }

    private static void TryDeleteDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool TryGetLiveProcess(int processId, out Process? process)
    {
        process = null;
        try
        {
            process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            process?.Dispose();
            process = null;
            return false;
        }
        catch (InvalidOperationException)
        {
            process?.Dispose();
            process = null;
            return false;
        }
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
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }
}
