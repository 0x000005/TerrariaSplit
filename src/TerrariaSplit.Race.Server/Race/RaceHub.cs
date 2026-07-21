using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.Race.Server;

public sealed class RaceHub : Hub
{
    private static readonly ConcurrentDictionary<string, RaceConnectionIdentity> Connections = new();
    private static readonly ConcurrentDictionary<string, string> PlayerConnections = new(StringComparer.OrdinalIgnoreCase);
    private readonly RaceRoomManager rooms;
    private readonly RaceWorldFileStore worldFiles;
    private readonly ILogger<RaceHub> logger;

    public RaceHub(RaceRoomManager rooms, RaceWorldFileStore worldFiles, ILogger<RaceHub> logger)
    {
        this.rooms = rooms;
        this.worldFiles = worldFiles;
        this.logger = logger;
    }

    public async Task<RaceOperationResult<RaceRoomState>> CreateRoom(RaceRoomCreateRequest request)
    {
        RaceOperationResult<RaceRoomState> result = rooms.CreateRoom(request);
        if (result.Succeeded && result.Value is RaceRoomState state)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, state.RoomCode);
            AttachConnection(Context.ConnectionId, state.RoomCode, request.Nickname);
            await BroadcastRosterAsync(state, RaceRoomStateUpdateKind.RoomCreated, request.Nickname);
        }

        LogResult("create-room", result, nickname: request.Nickname);
        return result;
    }

    public async Task<RaceOperationResult<RaceRoomState>> JoinRoom(RaceRoomJoinRequest request)
    {
        RaceOperationResult<RaceRoomState> result = rooms.JoinRoom(request);
        if (result.Succeeded && result.Value is RaceRoomState state)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, state.RoomCode);
            AttachConnection(Context.ConnectionId, state.RoomCode, request.Nickname);
            await BroadcastRosterAsync(state, RaceRoomStateUpdateKind.PlayerJoined, request.Nickname);
        }

        LogResult("join-room", result, request.RoomCode, request.Nickname);
        return result;
    }

    public Task<RaceOperationResult<RaceRoomState>> GetRoomState(string roomCode)
    {
        return Task.FromResult(rooms.GetRoomState(roomCode));
    }

    public async Task<RaceOperationResult<RaceRoomState>> UpdatePreparationStatus(RacePreparationStatusRequest request)
    {
        RaceOperationResult<RaceRoomState> result = rooms.UpdatePreparationStatus(request);
        await BroadcastRosterIfSucceededAsync(result, RaceRoomStateUpdateKind.WorldReadyChanged, request.Nickname);
        LogResult(
            "preparation-status",
            result,
            request.RoomCode,
            request.Nickname);
        return result;
    }

    public async Task<RaceOperationResult<RaceRoomState>> SetPlayerReady(RacePlayerReadyRequest request)
    {
        RaceOperationResult<RaceRoomState> result = rooms.SetPlayerReady(request);
        await BroadcastRosterIfSucceededAsync(result, RaceRoomStateUpdateKind.PlayerReadyChanged, request.Nickname);
        LogResult("player-ready", result, request.RoomCode, request.Nickname);
        return result;
    }

    public async Task<RaceOperationResult<RaceRoomProgressState>> ReportSplit(RaceSplitReport report)
    {
        RaceOperationResult<RaceRoomState> result = rooms.ReportSplit(report, out RaceGroupCompleted? completedGroup);
        RaceOperationResult<RaceRoomProgressState> progressResult = CreateProgressResult(result);
        if (progressResult.Succeeded && progressResult.Value is RaceRoomProgressState progress)
        {
            await BroadcastProgressAsync(progress);
            if (completedGroup is not null)
            {
                await Clients.Group(progress.RoomCode).SendAsync("RaceGroupCompleted", completedGroup);
            }
        }

        LogSplitReport(report, result);
        return progressResult;
    }

    public async Task<RaceOperationResult<RaceRoomProgressState>> ReportStart(RaceRunStartReport report)
    {
        RaceOperationResult<RaceRoomState> result = rooms.ReportStart(report);
        RaceOperationResult<RaceRoomProgressState> progressResult = CreateProgressResult(result);
        if (progressResult.Succeeded && progressResult.Value is RaceRoomProgressState progress)
        {
            await BroadcastProgressAsync(progress);
        }

        LogStartReport(report, result);
        return progressResult;
    }

    public async Task<RaceOperationResult<RaceRoomState>> ReportDeath(RaceDeathReport report)
    {
        RaceOperationResult<RaceRoomState> result =
            rooms.ReportDeath(report, out RacePlayerDied? playerDied);
        if (result.Succeeded && playerDied is not null)
        {
            await Clients.Group(playerDied.RoomCode).SendAsync("RacePlayerDied", playerDied);
        }

        LogResult("player-death", result, report.RoomCode, report.Nickname);
        return result;
    }

    public async Task<RaceOperationResult<RaceRoomState>> StartRace(RaceHostActionRequest request)
    {
        RaceOperationResult<RaceRoomState> result = rooms.StartRace(request);
        await BroadcastRosterIfSucceededAsync(result, RaceRoomStateUpdateKind.RaceStarting, request.Nickname);
        LogResult("start-race", result, request.RoomCode, request.Nickname);
        return result;
    }

    public async Task<RaceOperationResult<RaceRoomState>> RestartRace(RaceHostActionRequest request)
    {
        RaceOperationResult<RaceRoomState> result = rooms.RestartRace(request);
        if (result.Succeeded && result.Value is RaceRoomState state)
        {
            await Clients.Group(state.RoomCode).SendAsync(
                "RacePackageChanged",
                new RacePackageChanged(
                    state,
                    request.Nickname,
                    RacePackageRevisionCalculator.Create(state),
                    RacePackageChangeKind.Restarted));
        }

        LogResult("restart-race", result, request.RoomCode, request.Nickname);
        return result;
    }

    public async Task<RaceOperationResult<RaceRoomProgressState>> ResetProgress(RaceProgressResetRequest request)
    {
        RaceOperationResult<RaceRoomState> result = rooms.ResetPlayerProgress(request);
        RaceOperationResult<RaceRoomProgressState> progressResult = CreateProgressResult(result);
        if (progressResult.Succeeded && progressResult.Value is RaceRoomProgressState progress)
        {
            await BroadcastProgressAsync(progress);
            await Clients.Group(progress.RoomCode).SendAsync(
                "RacePlayerProgressReset",
                new RacePlayerProgressReset(
                    progress.RoomCode,
                    progress.PackageRevision,
                    request.RunId,
                    request.Nickname));
        }

        LogResult("reset-progress", result, request.RoomCode, request.Nickname);
        return progressResult;
    }

    public async Task<RaceOperationResult<RaceRoomState>> CloseRoom(string roomCode, string nickname)
    {
        RaceOperationResult<RaceRoomState> result = rooms.CloseRoom(roomCode, nickname);
        await BroadcastRosterIfSucceededAsync(result, RaceRoomStateUpdateKind.RoomClosed, nickname);
        if (result.Succeeded && result.Value is RaceRoomState state)
        {
            await DetachRoomConnectionsAsync(state.RoomCode);
            worldFiles.DeleteRoom(state.RoomCode);
        }

        LogResult("close-room", result, roomCode, nickname);
        return result;
    }

    public async Task<RaceOperationResult<RaceRoomState>> LeaveRoom(string roomCode, string nickname)
    {
        DetachConnection(Context.ConnectionId, out _);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomCode);
        RaceOperationResult<RaceRoomState> result = rooms.LeaveRoom(roomCode, nickname);
        RaceRoomStateUpdateKind kind = result.Value?.Status == RaceRoomStatus.Closed
            ? RaceRoomStateUpdateKind.RoomClosed
            : RaceRoomStateUpdateKind.PlayerLeft;
        await BroadcastRosterIfSucceededAsync(result, kind, nickname);
        if (result.Succeeded && result.Value is RaceRoomState { Status: RaceRoomStatus.Closed } state)
        {
            await DetachRoomConnectionsAsync(state.RoomCode);
            worldFiles.DeleteRoom(state.RoomCode);
        }

        LogResult("leave-room", result, roomCode, nickname);
        return result;
    }

    public async Task<RaceOperationResult<RaceRoomState>> KickPlayer(RacePlayerKickRequest request)
    {
        RaceOperationResult<RaceRoomState> result = rooms.KickPlayer(request);
        if (result.Succeeded && result.Value is RaceRoomState state)
        {
            foreach ((string connectionId, RaceConnectionIdentity identity) in Connections.ToArray())
            {
                if (string.Equals(identity.RoomCode, request.RoomCode, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(identity.Nickname, request.TargetNickname, StringComparison.OrdinalIgnoreCase))
                {
                    await Groups.RemoveFromGroupAsync(connectionId, state.RoomCode);
                    DetachConnection(connectionId, out _);
                }
            }

            await BroadcastRosterAsync(state, RaceRoomStateUpdateKind.PlayerKicked, request.TargetNickname);
        }

        LogResult("kick-player", result, request.RoomCode, request.TargetNickname);
        return result;
    }

    public async Task<RaceOperationResult<RaceRoomState>> ResumeRoom(string roomCode, string nickname)
    {
        RaceOperationResult<RaceRoomState> result = rooms.ResumeRoom(roomCode, nickname);
        if (result.Succeeded && result.Value is RaceRoomState state)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, state.RoomCode);
            AttachConnection(Context.ConnectionId, state.RoomCode, nickname);
            await BroadcastRosterAsync(state, RaceRoomStateUpdateKind.RoomResumed, nickname);
        }

        LogResult("resume-room", result, roomCode, nickname);
        return result;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (DetachConnection(Context.ConnectionId, out RaceConnectionIdentity? identity) && identity is not null)
        {
            logger.LogInformation(
                "Race connection dropped. Room={RoomCode} Nickname={Nickname}",
                NormalizeLogText(identity.RoomCode),
                NormalizeLogText(identity.Nickname));

            RaceOperationResult<RaceRoomState> result = rooms.DisconnectPlayer(identity.RoomCode, identity.Nickname);
            if (result.Succeeded && result.Value is RaceRoomState state)
            {
                await BroadcastRosterAsync(state, RaceRoomStateUpdateKind.PlayerConnectionChanged, identity.Nickname);
            }
            else
            {
                LogResult("disconnect-leave", result, identity.RoomCode, identity.Nickname);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task BroadcastRosterIfSucceededAsync(
        RaceOperationResult<RaceRoomState> result,
        RaceRoomStateUpdateKind kind,
        string actorNickname)
    {
        if (result.Succeeded && result.Value is RaceRoomState state)
        {
            await BroadcastRosterAsync(state, kind, actorNickname);
        }
    }

    private Task BroadcastRosterAsync(
        RaceRoomState state,
        RaceRoomStateUpdateKind kind,
        string actorNickname = "")
    {
        return Clients
            .Group(state.RoomCode)
            .SendAsync("RaceRosterChanged", new RaceRosterChanged(kind, state, NormalizeLogText(actorNickname)));
    }

    private Task BroadcastProgressAsync(RaceRoomProgressState progress)
    {
        return Clients.Group(progress.RoomCode).SendAsync("RaceProgressChanged", new RaceProgressChanged(progress));
    }

    private async Task DetachRoomConnectionsAsync(string roomCode)
    {
        foreach ((string connectionId, RaceConnectionIdentity identity) in Connections.ToArray())
        {
            if (!string.Equals(identity.RoomCode, roomCode, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            await Groups.RemoveFromGroupAsync(connectionId, roomCode);
            DetachConnection(connectionId, out _);
        }
    }

    private static void AttachConnection(string connectionId, string roomCode, string nickname)
    {
        var identity = new RaceConnectionIdentity(roomCode.Trim(), nickname.Trim());
        Connections[connectionId] = identity;
        PlayerConnections[CreatePlayerConnectionKey(identity)] = connectionId;
    }

    private static bool DetachConnection(string connectionId, out RaceConnectionIdentity? identity)
    {
        if (!Connections.TryRemove(connectionId, out identity) || identity is null)
        {
            return false;
        }

        string key = CreatePlayerConnectionKey(identity);
        return ((ICollection<KeyValuePair<string, string>>)PlayerConnections)
            .Remove(new KeyValuePair<string, string>(key, connectionId));
    }

    private static string CreatePlayerConnectionKey(RaceConnectionIdentity identity) =>
        identity.RoomCode + "\n" + identity.Nickname;

    private static RaceOperationResult<RaceRoomProgressState> CreateProgressResult(
        RaceOperationResult<RaceRoomState> result)
    {
        if (!result.Succeeded || result.Value is not RaceRoomState state)
        {
            return RaceOperationResult<RaceRoomProgressState>.Failure(result.ErrorCode, result.Message);
        }

        return RaceOperationResult<RaceRoomProgressState>.Success(CreateProgressState(state));
    }

    private static RaceRoomProgressState CreateProgressState(RaceRoomState state)
    {
        return new RaceRoomProgressState(
            state.RoomCode,
            state.Status,
            state.Players,
            state.Leaderboard,
            state.LastUpdatedAtUtc)
        {
            PackageRevision = state.PackageRevision
        };
    }

    private void LogResult(
        string operation,
        RaceOperationResult<RaceRoomState> result,
        string roomCode = "",
        string nickname = "")
    {
        if (result.Succeeded)
        {
            logger.LogInformation(
                "Race {Operation} succeeded. Room={RoomCode} Nickname={Nickname} Status={Status}",
                operation,
                result.Value?.RoomCode ?? roomCode,
                NormalizeLogText(nickname),
                result.Value?.Status);
            return;
        }

        logger.LogWarning(
            "Race {Operation} failed. Room={RoomCode} Nickname={Nickname} Error={ErrorCode} Message={Message}",
            operation,
            NormalizeLogText(roomCode),
            NormalizeLogText(nickname),
            result.ErrorCode,
            result.Message);
    }

    private void LogSplitReport(RaceSplitReport report, RaceOperationResult<RaceRoomState> result)
    {
        if (result.Succeeded)
        {
            logger.LogInformation(
                "Race split accepted. Room={RoomCode} Nickname={Nickname} SplitIndex={SplitIndex} ConditionIndex={ConditionIndex} SplitId={SplitId} ElapsedMs={ElapsedMs} Status={Status}",
                result.Value?.RoomCode ?? report.RoomCode,
                NormalizeLogText(report.Nickname),
                report.SplitIndex,
                report.ConditionIndex,
                NormalizeLogText(report.SplitId),
                report.ElapsedMilliseconds,
                result.Value?.Status);
            return;
        }

        logger.LogWarning(
            "Race split rejected. Room={RoomCode} Nickname={Nickname} SplitIndex={SplitIndex} ConditionIndex={ConditionIndex} SplitId={SplitId} ElapsedMs={ElapsedMs} Error={ErrorCode} Message={Message}",
            NormalizeLogText(report.RoomCode),
            NormalizeLogText(report.Nickname),
            report.SplitIndex,
            report.ConditionIndex,
            NormalizeLogText(report.SplitId),
            report.ElapsedMilliseconds,
            result.ErrorCode,
            result.Message);
    }

    private void LogStartReport(RaceRunStartReport report, RaceOperationResult<RaceRoomState> result)
    {
        if (result.Succeeded)
        {
            logger.LogInformation(
                "Race start accepted. Room={RoomCode} Nickname={Nickname} Status={Status}",
                result.Value?.RoomCode ?? report.RoomCode,
                NormalizeLogText(report.Nickname),
                result.Value?.Status);
            return;
        }

        logger.LogWarning(
            "Race start rejected. Room={RoomCode} Nickname={Nickname} Error={ErrorCode} Message={Message}",
            NormalizeLogText(report.RoomCode),
            NormalizeLogText(report.Nickname),
            result.ErrorCode,
            result.Message);
    }

    private static string NormalizeLogText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    }

    private sealed record RaceConnectionIdentity(string RoomCode, string Nickname);
}
