using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.Race.Client;

public sealed class RaceClientSession : IAsyncDisposable
{
    private const long MaximumWorldFileLength = 128L * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly TimeSpan DisposeConnectionTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan HubConnectTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan HubInvokeTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan[] UploadRetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(8)
    ];
    private readonly HttpClient httpClient = new();
    private HubConnection? connection;
    private RaceRoomState? state;
    private string serverUrl = string.Empty;
    private string lastPackageRevision = string.Empty;

    public event EventHandler? ConnectionStatusChanged;

    public event EventHandler<RacePackageChanged>? PackageChanged;

    public event EventHandler<RaceRosterChanged>? RosterChanged;

    public event EventHandler<RaceProgressChanged>? ProgressChanged;

    public event EventHandler<RaceGroupCompleted>? GroupCompleted;

    public event EventHandler<RacePlayerProgressReset>? PlayerProgressReset;

    public RaceRoomState? State => state;

    public RaceServerConnectionStatus ConnectionStatus { get; private set; } = RaceServerConnectionStatus.Disconnected;

    public bool IsConnected => connection?.State == HubConnectionState.Connected;

    public bool IsInRoom => state is not null;

    public string? RoomCode => state?.RoomCode;

    public string? Nickname { get; private set; }

    public async Task ConnectAsync(string baseServerUrl, CancellationToken cancellationToken = default)
    {
        string normalized = NormalizeServerUrl(baseServerUrl);
        if (connection is not null && string.Equals(serverUrl, normalized, StringComparison.OrdinalIgnoreCase))
        {
            if (connection.State != HubConnectionState.Connected)
            {
                SetConnectionStatus(RaceServerConnectionStatus.Connecting);
                try
                {
                    await StartConnectionAsync(connection, cancellationToken);
                    SetConnectionStatus(RaceServerConnectionStatus.Connected);
                }
                catch
                {
                    SetConnectionStatus(RaceServerConnectionStatus.ConnectionFailed);
                    throw;
                }
            }

            return;
        }

        if (connection is not null)
        {
            await connection.DisposeAsync();
            SetConnectionStatus(RaceServerConnectionStatus.Disconnected);
        }

        serverUrl = normalized;
        connection = new HubConnectionBuilder()
            .WithUrl(CombineHubUrl(normalized))
            .WithAutomaticReconnect(RaceReconnectRetryPolicy.Instance)
            .Build();
        connection.On<RacePackageChanged>("RacePackageChanged", ApplyPackageChanged);
        connection.On<RaceRosterChanged>("RaceRosterChanged", ApplyRosterChanged);
        connection.On<RaceProgressChanged>("RaceProgressChanged", ApplyProgressChanged);
        connection.On<RaceGroupCompleted>("RaceGroupCompleted", ApplyGroupCompleted);
        connection.On<RacePlayerProgressReset>("RacePlayerProgressReset", ApplyPlayerProgressReset);
        connection.Reconnecting += HandleReconnectingAsync;
        connection.Reconnected += ResumeRoomAfterReconnectAsync;
        connection.Closed += HandleConnectionClosedAsync;
        SetConnectionStatus(RaceServerConnectionStatus.Connecting);
        try
        {
            await StartConnectionAsync(connection, cancellationToken);
            SetConnectionStatus(RaceServerConnectionStatus.Connected);
        }
        catch
        {
            SetConnectionStatus(RaceServerConnectionStatus.ConnectionFailed);
            throw;
        }
    }

    public async Task<RaceOperationResult<RaceRoomState>> CreateRoomAsync(
        string baseServerUrl,
        RaceRoomCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        await ConnectAsync(baseServerUrl, cancellationToken);
        RaceOperationResult<RaceRoomState> result = await InvokeAsync<RaceRoomCreateRequest>(
            "CreateRoom",
            request,
            cancellationToken);
        if (result.Succeeded && result.Value is RaceRoomState next)
        {
            Nickname = request.Nickname.Trim();
            ApplyRoster(next, RaceRoomStateUpdateKind.RoomCreated, request.Nickname);
        }

        return result;
    }

    public async Task<RaceOperationResult<RaceRoomState>> JoinRoomAsync(
        string baseServerUrl,
        RaceRoomJoinRequest request,
        CancellationToken cancellationToken = default)
    {
        await ConnectAsync(baseServerUrl, cancellationToken);
        RaceOperationResult<RaceRoomState> result = await InvokeAsync<RaceRoomJoinRequest>(
            "JoinRoom",
            request,
            cancellationToken);
        if (result.Succeeded && result.Value is RaceRoomState next)
        {
            Nickname = request.Nickname.Trim();
            ApplyStateAfterJoin(next, request.Nickname);
        }

        return result;
    }

    public async Task<RaceOperationResult<RaceRoomState>> UploadWorldFileAsync(
        string worldPath,
        RaceRoutePayload route,
        RaceWorldSettings worldSettings,
        RaceSeedAssignment? seed,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out string roomCode, out string nickname, out RaceOperationResult<RaceRoomState> failure))
        {
            return failure;
        }

        return await UploadWorldFileWithRetriesAsync(
            roomCode,
            nickname,
            worldPath,
            route,
            worldSettings,
            seed,
            progress,
            cancellationToken);
    }

    private async Task<RaceOperationResult<RaceRoomState>> UploadWorldFileWithRetriesAsync(
        string roomCode,
        string nickname,
        string worldPath,
        RaceRoutePayload route,
        RaceWorldSettings worldSettings,
        RaceSeedAssignment? seed,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        if (!RaceWorldFileValidator.IsValidWorldFilePath(worldPath))
        {
            return RaceOperationResult<RaceRoomState>.Failure(
                "world_upload_required",
                "A valid world file is required.");
        }

        RaceOperationResult<RaceRoomState>? lastResult = null;
        for (int attempt = 0; attempt <= UploadRetryDelays.Length; attempt++)
        {
            progress?.Report(0);
            try
            {
                RaceOperationResult<RaceRoomState> result = await UploadWorldFileOnceAsync(
                    roomCode,
                    nickname,
                    worldPath,
                    route,
                    worldSettings,
                    seed,
                    progress,
                    cancellationToken);
                if (result.Succeeded || !ShouldRetryUploadResult(result) || attempt == UploadRetryDelays.Length)
                {
                    if (result.Succeeded)
                    {
                        progress?.Report(100);
                    }

                    return result;
                }

                lastResult = result;
            }
            catch (Exception ex) when (IsRetryableUploadException(ex))
            {
                if (attempt == UploadRetryDelays.Length)
                {
                    return RaceOperationResult<RaceRoomState>.Failure(
                        "world_upload_failed",
                        string.IsNullOrWhiteSpace(ex.Message) ? "Upload failed." : ex.Message);
                }

                lastResult = RaceOperationResult<RaceRoomState>.Failure(
                    "world_upload_failed",
                    string.IsNullOrWhiteSpace(ex.Message) ? "Upload failed." : ex.Message);
            }

            progress?.Report(0);
            await Task.Delay(UploadRetryDelays[attempt], cancellationToken);
        }

        return lastResult ?? RaceOperationResult<RaceRoomState>.Failure(
            "world_upload_failed",
            "Upload failed.");
    }

    private async Task<RaceOperationResult<RaceRoomState>> UploadWorldFileOnceAsync(
        string roomCode,
        string nickname,
        string worldPath,
        RaceRoutePayload route,
        RaceWorldSettings worldSettings,
        RaceSeedAssignment? seed,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        FileInfo fileInfo = new(worldPath);
        FileStream fileStream = File.OpenRead(worldPath);
        using var fileContent = new ProgressStreamContent(fileStream, fileInfo.Length, progress);
        form.Add(fileContent, "world", Path.GetFileName(worldPath));
        form.Add(CreateJsonContent(route), "route");
        form.Add(CreateJsonContent(worldSettings), "worldSettings");
        if (seed is not null)
        {
            form.Add(CreateJsonContent(seed), "seed");
        }

        using HttpResponseMessage response = await httpClient.PostAsync(
            BuildWorldTransferUrl(roomCode, nickname),
            form,
            cancellationToken);
        RaceOperationResult<RaceRoomState> result = await ReadOperationResultAsync<RaceRoomState>(
            response,
            "world_upload_failed",
            "Upload failed.",
            cancellationToken);
        if (result.Succeeded && result.Value is RaceRoomState next)
        {
            ApplyPackage(next, nickname);
        }

        return result;
    }

    private static bool IsRetryableUploadException(Exception exception)
    {
        return exception is IOException or InvalidOperationException or HttpRequestException or TimeoutException ||
            exception is OperationCanceledException;
    }

    private static bool ShouldRetryUploadResult(RaceOperationResult<RaceRoomState> result)
    {
        if (result.Succeeded ||
            !string.Equals(result.ErrorCode, "world_upload_failed", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string message = result.Message ?? string.Empty;
        return message.Contains("HTTP 408", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("HTTP 429", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("HTTP 5", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<RaceWorldFileTransferResult> DownloadWorldFileAsync(
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out string roomCode, out string nickname, out RaceOperationResult<RaceRoomState> failure))
        {
            return RaceWorldFileTransferResult.Failure(failure.Message);
        }

        RaceWorldFileInfo? worldFile = state?.WorldFile;
        if (worldFile is null)
        {
            return RaceWorldFileTransferResult.Failure("The room host has not uploaded a world file.");
        }

        if (worldFile.Length <= 0 || worldFile.Length > MaximumWorldFileLength)
        {
            return RaceWorldFileTransferResult.Failure("The room world file length is outside the supported limit.");
        }

        using HttpResponseMessage response = await httpClient.GetAsync(
            BuildWorldTransferUrl(roomCode, nickname),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            RaceOperationResult<RaceWorldFileInfo> result = await ReadOperationResultAsync<RaceWorldFileInfo>(
                response,
                "world_download_failed",
                "World download failed.",
                cancellationToken);
            return RaceWorldFileTransferResult.Failure(
                result.Message ??
                response.ReasonPhrase ??
                "World download failed.");
        }

        long? contentLength = response.Content.Headers.ContentLength;
        if (contentLength is > MaximumWorldFileLength ||
            (contentLength.HasValue && contentLength.Value != worldFile.Length))
        {
            return RaceWorldFileTransferResult.Failure("The world download response length does not match the room package.");
        }

        string? directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = destinationPath + $".download-{Guid.NewGuid():N}.tmp";
        try
        {
            long transferred = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using (var destination = new FileStream(
                             tempPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                byte[] buffer = new byte[81920];
                while (true)
                {
                    int read = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
                    if (read <= 0)
                    {
                        break;
                    }

                    if (read > worldFile.Length - transferred ||
                        read > MaximumWorldFileLength - transferred)
                    {
                        return RaceWorldFileTransferResult.Failure("The world download exceeded its declared or supported length.");
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    hash.AppendData(buffer, 0, read);
                    transferred += read;
                }

                await destination.FlushAsync(cancellationToken);
            }

            string actualHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            if (transferred != worldFile.Length ||
                !string.Equals(actualHash, worldFile.Sha256?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return RaceWorldFileTransferResult.Failure("Downloaded world file failed length or SHA-256 verification.");
            }

            await using (FileStream validationStream = File.OpenRead(tempPath))
            {
                if (!RaceWorldFileValidator.TryValidateWorldStream(validationStream, out string detail))
                {
                    return RaceWorldFileTransferResult.Failure("Downloaded file is not a valid Terraria world: " + detail);
                }
            }

            File.Move(tempPath, destinationPath, overwrite: true);
            return RaceWorldFileTransferResult.Success(destinationPath, worldFile);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    public async Task<RaceOperationResult<RaceRoomState>> UpdatePreparationStatusAsync(
        RacePlayerFileStatus playerFileStatus,
        RaceWorldFileStatus worldFileStatus,
        RaceRngControlStatus rngControlStatus,
        string? error = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out string roomCode, out string nickname, out RaceOperationResult<RaceRoomState> failure))
        {
            return failure;
        }

        var request = new RacePreparationStatusRequest(
            roomCode,
            nickname,
            playerFileStatus,
            worldFileStatus,
            rngControlStatus,
            error);
        return await InvokeAndApplyAsync(
            "UpdatePreparationStatus",
            request,
            RaceRoomStateUpdateKind.WorldReadyChanged,
            nickname,
            cancellationToken);
    }

    public async Task<RaceOperationResult<RaceRoomProgressState>> ReportSplitAsync(
        RaceSplitReport report,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out _, out _, out RaceOperationResult<RaceRoomState> failure))
        {
            return ConvertFailure<RaceRoomProgressState>(failure);
        }

        RaceOperationResult<RaceRoomProgressState> result =
            await InvokeProgressHubAsync<RaceSplitReport, RaceRoomProgressState>(
                "ReportSplit",
                report,
                cancellationToken);
        if (result.Succeeded && result.Value is RaceRoomProgressState progress)
        {
            ApplyProgressChanged(new RaceProgressChanged(progress));
        }

        return result;
    }

    public async Task<RaceOperationResult<RaceRoomProgressState>> ReportStartAsync(
        RaceRunStartReport report,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out _, out _, out RaceOperationResult<RaceRoomState> failure))
        {
            return ConvertFailure<RaceRoomProgressState>(failure);
        }

        RaceOperationResult<RaceRoomProgressState> result =
            await InvokeProgressHubAsync<RaceRunStartReport, RaceRoomProgressState>(
                "ReportStart",
                report,
                cancellationToken);
        if (result.Succeeded && result.Value is RaceRoomProgressState progress)
        {
            ApplyProgressChanged(new RaceProgressChanged(progress));
        }

        return result;
    }

    public async Task<RaceOperationResult<RaceRoomState>> StartRaceAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out string roomCode, out string nickname, out RaceOperationResult<RaceRoomState> failure))
        {
            return failure;
        }

        RaceRoomState current = state!;
        var request = new RaceHostActionRequest(roomCode, nickname, current.PackageRevision);
        return await InvokeAndApplyAsync(
            "StartRace",
            request,
            RaceRoomStateUpdateKind.RaceStarting,
            nickname,
            cancellationToken);
    }

    public async Task<RaceOperationResult<RaceRoomState>> RestartRaceAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out string roomCode, out string nickname, out RaceOperationResult<RaceRoomState> failure))
        {
            return failure;
        }

        RaceRoomState current = state!;
        var request = new RaceHostActionRequest(roomCode, nickname, current.PackageRevision);
        RaceOperationResult<RaceRoomState> result = await InvokeAsync<RaceHostActionRequest>(
            "RestartRace",
            request,
            cancellationToken);
        if (result.Succeeded && result.Value is RaceRoomState next)
        {
            ApplyPackageChanged(new RacePackageChanged(
                next,
                nickname,
                RacePackageRevisionCalculator.Create(next),
                RacePackageChangeKind.Restarted));
        }

        return result;
    }

    public async Task<RaceOperationResult<RaceRoomProgressState>> ResetProgressAsync(
        long packageRevision,
        string runId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out string roomCode, out string nickname, out RaceOperationResult<RaceRoomState> failure))
        {
            return ConvertFailure<RaceRoomProgressState>(failure);
        }

        var request = new RaceProgressResetRequest(roomCode, nickname, packageRevision, runId);
        RaceOperationResult<RaceRoomProgressState> result =
            await InvokeProgressHubAsync<RaceProgressResetRequest, RaceRoomProgressState>(
                "ResetProgress",
                request,
                cancellationToken);
        if (result.Succeeded && result.Value is RaceRoomProgressState progress)
        {
            ApplyProgressChanged(new RaceProgressChanged(progress));
        }

        return result;
    }

    public async Task<RaceOperationResult<RaceRoomState>> CloseRoomAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out string roomCode, out string nickname, out RaceOperationResult<RaceRoomState> failure))
        {
            return failure;
        }

        RaceOperationResult<RaceRoomState> result = await RequiredConnection.InvokeAsync<RaceOperationResult<RaceRoomState>>(
            "CloseRoom",
            roomCode,
            nickname,
            cancellationToken);
        if (result.Succeeded && result.Value is RaceRoomState next)
        {
            ApplyRoster(next, RaceRoomStateUpdateKind.RoomClosed, nickname);
        }

        return result;
    }

    public async Task<RaceOperationResult<RaceRoomState>> KickPlayerAsync(
        string targetNickname,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out string roomCode, out string nickname, out RaceOperationResult<RaceRoomState> failure))
        {
            return failure;
        }

        var request = new RacePlayerKickRequest(roomCode, nickname, targetNickname);
        return await InvokeAndApplyAsync(
            "KickPlayer",
            request,
            RaceRoomStateUpdateKind.PlayerKicked,
            targetNickname,
            cancellationToken);
    }

    public Task LeaveAsync()
    {
        return LeaveAsync(CancellationToken.None);
    }

    public async Task LeaveAsync(CancellationToken cancellationToken)
    {
        await LeaveAsync(cancellationToken, DisposeConnectionTimeout).ConfigureAwait(false);
    }

    private async Task LeaveAsync(CancellationToken cancellationToken, TimeSpan? connectionDisposeTimeout)
    {
        string roomCode = state?.RoomCode ?? string.Empty;
        string nickname = Nickname ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(roomCode) &&
            !string.IsNullOrWhiteSpace(nickname) &&
            connection is not null &&
            connection.State == HubConnectionState.Connected)
        {
            try
            {
                _ = await connection.InvokeAsync<RaceOperationResult<RaceRoomState>>(
                    IsCurrentUserHost() ? "CloseRoom" : "LeaveRoom",
                    roomCode,
                    nickname,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or OperationCanceledException or TimeoutException)
            {
            }
        }

        await LeaveLocalAsync(connectionDisposeTimeout).ConfigureAwait(false);
    }

    public async Task LeaveLocalAsync(TimeSpan? connectionDisposeTimeout = null)
    {
        state = null;
        Nickname = null;
        lastPackageRevision = string.Empty;
        if (connection is not null)
        {
            await DisposeConnectionAsync(connection, connectionDisposeTimeout).ConfigureAwait(false);
            connection = null;
        }

        SetConnectionStatus(RaceServerConnectionStatus.Disconnected);
    }

    public async ValueTask DisposeAsync()
    {
        await LeaveLocalAsync(DisposeConnectionTimeout).ConfigureAwait(false);
        httpClient.Dispose();
    }

    private static async Task DisposeConnectionAsync(HubConnection target, TimeSpan? timeout)
    {
        Task disposeTask = target.DisposeAsync().AsTask();
        try
        {
            if (timeout is TimeSpan timeoutValue)
            {
                Task completed = await Task.WhenAny(disposeTask, Task.Delay(timeoutValue)).ConfigureAwait(false);
                if (!ReferenceEquals(completed, disposeTask))
                {
                    _ = disposeTask.ContinueWith(
                        static task => _ = task.Exception,
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                    return;
                }
            }

            await disposeTask.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException or ObjectDisposedException or TimeoutException)
        {
        }
    }

    private HubConnection RequiredConnection =>
        connection ?? throw new InvalidOperationException("Race client is not connected.");

    private async Task<RaceOperationResult<RaceRoomState>> InvokeAndApplyAsync<TRequest>(
        string methodName,
        TRequest request,
        RaceRoomStateUpdateKind kind,
        string actorNickname,
        CancellationToken cancellationToken)
    {
        RaceOperationResult<RaceRoomState> result = await InvokeAsync(methodName, request, cancellationToken);
        if (result.Succeeded && result.Value is RaceRoomState next)
        {
            ApplyRoster(next, kind, actorNickname);
        }

        return result;
    }

    private Task<RaceOperationResult<RaceRoomState>> InvokeAsync<TRequest>(
        string methodName,
        TRequest request,
        CancellationToken cancellationToken)
    {
        return InvokeHubAsync(methodName, request, cancellationToken);
    }

    private static async Task StartConnectionAsync(
        HubConnection activeConnection,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(HubConnectTimeout);
        try
        {
            await activeConnection.StartAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Race server connection timed out.");
        }
    }

    private async Task<RaceOperationResult<RaceRoomState>> InvokeHubAsync<TRequest>(
        string methodName,
        TRequest request,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(HubInvokeTimeout);
        try
        {
            return await RequiredConnection.InvokeAsync<RaceOperationResult<RaceRoomState>>(
                methodName,
                request,
                timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Race server request timed out.");
        }
    }

    private async Task<RaceOperationResult<TResult>> InvokeProgressHubAsync<TRequest, TResult>(
        string methodName,
        TRequest request,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(HubInvokeTimeout);
        try
        {
            return await RequiredConnection.InvokeAsync<RaceOperationResult<TResult>>(
                methodName,
                request,
                timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Race server request timed out.");
        }
    }

    private bool TryGetIdentity(
        out string roomCode,
        out string nickname,
        out RaceOperationResult<RaceRoomState> failure)
    {
        roomCode = state?.RoomCode ?? string.Empty;
        nickname = Nickname ?? string.Empty;
        if (string.IsNullOrWhiteSpace(roomCode) || string.IsNullOrWhiteSpace(nickname))
        {
            failure = RaceOperationResult<RaceRoomState>.Failure(
                "not_in_room",
                "Join or create a race room before sending race updates.");
            return false;
        }

        failure = null!;
        return true;
    }

    private void ApplyStateAfterJoin(
        RaceRoomState next,
        string actorNickname,
        RaceRoomStateUpdateKind kind = RaceRoomStateUpdateKind.PlayerJoined)
    {
        if (next.Route is not null || next.WorldFile is not null)
        {
            ApplyPackage(next, actorNickname);
            return;
        }

        ApplyRoster(next, kind, actorNickname);
    }

    private void ApplyPackage(RaceRoomState next, string actorNickname = "")
    {
        ApplyPackageChanged(new RacePackageChanged(
            next,
            actorNickname,
            RacePackageRevisionCalculator.Create(next),
            RacePackageChangeKind.Published));
    }

    private void ApplyRoster(
        RaceRoomState next,
        RaceRoomStateUpdateKind kind = RaceRoomStateUpdateKind.Snapshot,
        string actorNickname = "")
    {
        ApplyRosterChanged(new RaceRosterChanged(kind, next, actorNickname));
    }

    private void ApplyPackageChanged(RacePackageChanged update)
    {
        if (update.State is not RaceRoomState next)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Nickname))
        {
            return;
        }

        bool containsCurrentPlayer = next.Players.Any(player =>
            string.Equals(player.Nickname, Nickname, StringComparison.OrdinalIgnoreCase));
        if (!containsCurrentPlayer)
        {
            if (state?.RoomCode is string currentRoomCode &&
                string.Equals(currentRoomCode, next.RoomCode, StringComparison.OrdinalIgnoreCase))
            {
                state = null;
                Nickname = null;
                lastPackageRevision = string.Empty;
                PackageChanged?.Invoke(this, update);
            }

            return;
        }

        state = next;
        if (string.Equals(lastPackageRevision, update.PackageRevision, StringComparison.Ordinal))
        {
            RosterChanged?.Invoke(
                this,
                new RaceRosterChanged(RaceRoomStateUpdateKind.Snapshot, next, update.ActorNickname));
            return;
        }

        lastPackageRevision = update.PackageRevision;
        PackageChanged?.Invoke(this, update);
    }

    private void ApplyRosterChanged(RaceRosterChanged update)
    {
        if (update.State is not RaceRoomState next)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Nickname))
        {
            return;
        }

        bool containsCurrentPlayer = next.Players.Any(player =>
            string.Equals(player.Nickname, Nickname, StringComparison.OrdinalIgnoreCase));
        if (!containsCurrentPlayer)
        {
            if (state?.RoomCode is string currentRoomCode &&
                string.Equals(currentRoomCode, next.RoomCode, StringComparison.OrdinalIgnoreCase))
            {
                state = null;
                Nickname = null;
                lastPackageRevision = string.Empty;
                RosterChanged?.Invoke(this, update);
            }

            return;
        }

        state = next;
        RosterChanged?.Invoke(this, update);
    }

    private void ApplyProgressChanged(RaceProgressChanged update)
    {
        RaceRoomState? current = state;
        RaceRoomProgressState progress = update.Progress;
        if (current is null ||
            string.IsNullOrWhiteSpace(Nickname) ||
            !string.Equals(current.RoomCode, progress.RoomCode, StringComparison.OrdinalIgnoreCase) ||
            current.PackageRevision != progress.PackageRevision)
        {
            return;
        }

        bool containsCurrentPlayer = progress.Players.Any(player =>
            string.Equals(player.Nickname, Nickname, StringComparison.OrdinalIgnoreCase));
        if (!containsCurrentPlayer)
        {
            return;
        }

        state = current with
        {
            Status = progress.Status,
            Players = progress.Players,
            Leaderboard = progress.Leaderboard,
            LastUpdatedAtUtc = progress.LastUpdatedAtUtc
        };
        ProgressChanged?.Invoke(this, update);
    }

    private void ApplyGroupCompleted(RaceGroupCompleted update)
    {
        RaceRoomState? current = state;
        if (current is null ||
            string.IsNullOrWhiteSpace(Nickname) ||
            !string.Equals(current.RoomCode, update.RoomCode, StringComparison.OrdinalIgnoreCase) ||
            current.PackageRevision != update.PackageRevision ||
            update.SplitIndex < 0 ||
            update.ElapsedMilliseconds < 0)
        {
            return;
        }

        GroupCompleted?.Invoke(this, update);
    }

    private void ApplyPlayerProgressReset(RacePlayerProgressReset update)
    {
        RaceRoomState? current = state;
        if (current is null ||
            string.IsNullOrWhiteSpace(Nickname) ||
            !string.Equals(current.RoomCode, update.RoomCode, StringComparison.OrdinalIgnoreCase) ||
            current.PackageRevision != update.PackageRevision)
        {
            return;
        }

        PlayerProgressReset?.Invoke(this, update);
    }

    private async Task ResumeRoomAfterReconnectAsync(string? connectionId)
    {
        _ = connectionId;
        SetConnectionStatus(RaceServerConnectionStatus.Connected);
        if (state?.RoomCode is not string roomCode ||
            Nickname is not string nickname ||
            connection is null)
        {
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(HubInvokeTimeout);
            RaceOperationResult<RaceRoomState> result =
                await connection.InvokeAsync<RaceOperationResult<RaceRoomState>>(
                    "ResumeRoom",
                    roomCode,
                    nickname,
                    timeout.Token);
            if (result.Succeeded && result.Value is RaceRoomState next)
            {
                ApplyStateAfterJoin(next, nickname, RaceRoomStateUpdateKind.RoomResumed);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or OperationCanceledException or TimeoutException)
        {
        }
    }

    private Task HandleReconnectingAsync(Exception? exception)
    {
        _ = exception;
        SetConnectionStatus(RaceServerConnectionStatus.Reconnecting);
        return Task.CompletedTask;
    }

    private Task HandleConnectionClosedAsync(Exception? exception)
    {
        SetConnectionStatus(exception is null
            ? RaceServerConnectionStatus.Disconnected
            : RaceServerConnectionStatus.ConnectionFailed);
        return Task.CompletedTask;
    }

    private void SetConnectionStatus(RaceServerConnectionStatus next)
    {
        if (ConnectionStatus == next)
        {
            return;
        }

        ConnectionStatus = next;
        ConnectionStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool IsCurrentUserHost()
    {
        if (state is null || string.IsNullOrWhiteSpace(Nickname))
        {
            return false;
        }

        return state.Players.Any(player =>
            player.IsHost &&
            string.Equals(player.Nickname, Nickname, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeServerUrl(string baseServerUrl)
    {
        string normalized = baseServerUrl.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Race server URL is required.", nameof(baseServerUrl));
        }

        return normalized.TrimEnd('/');
    }

    private static string CombineHubUrl(string baseServerUrl)
    {
        return baseServerUrl + "/raceHub";
    }

    private static RaceOperationResult<T> ConvertFailure<T>(RaceOperationResult<RaceRoomState> failure)
    {
        return RaceOperationResult<T>.Failure(failure.ErrorCode, failure.Message);
    }

    private string BuildWorldTransferUrl(string roomCode, string nickname)
    {
        return serverUrl +
            "/api/race/rooms/" +
            Uri.EscapeDataString(roomCode) +
            "/world?nickname=" +
            Uri.EscapeDataString(nickname);
    }

    private static StringContent CreateJsonContent<T>(T value)
    {
        return new StringContent(
            JsonSerializer.Serialize(value, JsonOptions),
            Encoding.UTF8,
            "application/json");
    }

    private static async Task<RaceOperationResult<T>> ReadOperationResultAsync<T>(
        HttpResponseMessage response,
        string fallbackCode,
        string fallbackMessage,
        CancellationToken cancellationToken)
    {
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                RaceOperationResult<T>? result = JsonSerializer.Deserialize<RaceOperationResult<T>>(body, JsonOptions);
                if (result is not null)
                {
                    return result;
                }
            }
            catch (JsonException)
            {
            }
        }

        string detail = FormatHttpFailureDetail(response, body);
        return RaceOperationResult<T>.Failure(
            fallbackCode,
            string.IsNullOrWhiteSpace(detail) ? fallbackMessage : fallbackMessage + " " + detail);
    }

    private static string FormatHttpFailureDetail(HttpResponseMessage response, string body)
    {
        string status = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd();
        string bodyPreview = CreateBodyPreview(body);
        if ((int)response.StatusCode == 404)
        {
            return $"({status}). The world upload/download endpoint was not found.";
        }

        if ((int)response.StatusCode == 413)
        {
            return $"({status}). The world file is larger than the Race server upload limit.";
        }

        return string.IsNullOrWhiteSpace(bodyPreview)
            ? $"({status})."
            : $"({status}): {bodyPreview}";
    }

    private static string CreateBodyPreview(string body)
    {
        string preview = body.Trim();
        if (preview.Length <= 180)
        {
            return preview;
        }

        return preview[..180] + "...";
    }

    private sealed class ProgressStreamContent : HttpContent
    {
        private const int BufferSize = 81920;
        private readonly Stream source;
        private readonly long length;
        private readonly IProgress<int>? progress;
        private int lastReported = -1;

        public ProgressStreamContent(Stream source, long length, IProgress<int>? progress)
        {
            this.source = source;
            this.length = Math.Max(0, length);
            this.progress = progress;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            Report(0);
            byte[] buffer = new byte[BufferSize];
            long transferred = 0;
            while (true)
            {
                int read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                await stream.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                transferred += read;
                Report(transferred);
            }

            Report(length);
        }

        protected override bool TryComputeLength(out long contentLength)
        {
            contentLength = length;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                source.Dispose();
            }

            base.Dispose(disposing);
        }

        private void Report(long transferred)
        {
            if (progress is null)
            {
                return;
            }

            int percent = length <= 0
                ? 100
                : (int)Math.Clamp(Math.Round(transferred * 100d / length, MidpointRounding.AwayFromZero), 0d, 100d);
            if (percent == lastReported)
            {
                return;
            }

            lastReported = percent;
            progress.Report(percent);
        }
    }
}
