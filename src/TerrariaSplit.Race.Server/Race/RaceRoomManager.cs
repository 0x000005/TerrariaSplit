using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.Race.Server;

public sealed class RaceRoomManager
{
    private const string RoomCodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int MaximumActiveRooms = 10_000;
    private const int MaximumNicknameLength = 64;
    private const int MaximumRouteSplits = 512;
    private const int MaximumRouteIcons = 2_048;
    private const int MaximumSerializedRouteLength = 8 * 1024 * 1024;
    private const int MaximumEmbeddedIconBase64Length = 3 * 1024 * 1024;
    private const long MaximumTotalIconBase64Length = 48L * 1024 * 1024;
    private const long MaximumWorldFileLength = 128L * 1024 * 1024;
    private readonly ConcurrentDictionary<string, RaceRoom> rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly IRaceRecordStore recordStore;
    private readonly ILogger<RaceRoomManager>? logger;

    public RaceRoomManager(IRaceRecordStore recordStore, ILogger<RaceRoomManager>? logger = null)
    {
        this.recordStore = recordStore;
        this.logger = logger;
    }

    public RaceOperationResult<RaceRoomState> CreateRoom(RaceRoomCreateRequest request)
    {
        if (!IsValidNickname(request.Nickname))
        {
            return Failure(RaceErrors.InvalidRequest, $"Nickname must contain 1-{MaximumNicknameLength} characters.");
        }

        if (rooms.Count >= MaximumActiveRooms)
        {
            return Failure(RaceErrors.ServerCapacity, "The Race server has reached its active-room limit.");
        }

        string nickname = NormalizeNickname(request.Nickname);
        for (int attempt = 0; attempt < 256; attempt++)
        {
            string code = CreateRoomCode();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var room = new RaceRoom(code, nickname, now);
            room.Players[nickname] = RacePlayer.Create(nickname, isHost: true, now);
            if (rooms.TryAdd(code, room))
            {
                return RaceOperationResult<RaceRoomState>.Success(room.ToState());
            }
        }

        throw new InvalidOperationException("Could not create a unique race room code.");
    }

    public RaceOperationResult<RaceRoomState> JoinRoom(RaceRoomJoinRequest request)
    {
        if (!TryGetRoom(request.RoomCode, out RaceRoom? room, out RaceOperationResult<RaceRoomState> failure))
        {
            return failure;
        }

        RaceRoom activeRoom = room!;
        string nickname = NormalizeNickname(request.Nickname);
        if (!IsValidNickname(nickname))
        {
            return Failure(RaceErrors.InvalidRequest, $"Nickname must contain 1-{MaximumNicknameLength} characters.");
        }

        lock (activeRoom.Sync)
        {
            if (activeRoom.Status == RaceRoomStatus.Closed)
            {
                return Failure(RaceErrors.RoomClosed, "Room is closed.");
            }

            if (activeRoom.Players.ContainsKey(nickname))
            {
                return Failure(RaceErrors.NicknameTaken, "Nickname already exists in this room.");
            }

            activeRoom.Players[nickname] = RacePlayer.Create(nickname, isHost: false, DateTimeOffset.UtcNow);
            activeRoom.Touch();
            return RaceOperationResult<RaceRoomState>.Success(activeRoom.ToState());
        }
    }

    public RaceOperationResult<RaceRoomState> PublishWorldFile(RaceWorldFilePublishRequest request)
    {
        if (!TryGetHostRoom(request.RoomCode, request.Nickname, out RaceRoom? room, out RaceOperationResult<RaceRoomState> failure))
        {
            return failure;
        }

        if (!IsValidRoutePackage(request.Route))
        {
            return Failure(RaceErrors.RouteRequired, "Route package exceeds the Race server limits.");
        }

        if (request.WorldFile.Length <= 0 || request.WorldFile.Length > MaximumWorldFileLength ||
            string.IsNullOrWhiteSpace(request.WorldFile.FileName) ||
            string.IsNullOrWhiteSpace(request.WorldFile.Sha256))
        {
            return Failure(RaceErrors.WorldUploadRequired, "A valid world file is required.");
        }

        RaceRoom activeRoom = room!;
        lock (activeRoom.Sync)
        {
            if (activeRoom.Status == RaceRoomStatus.Closed)
            {
                return Failure(RaceErrors.RoomClosed, "Room is closed.");
            }

            activeRoom.Route = request.Route;
            activeRoom.WorldSettings = request.WorldSettings;
            activeRoom.Seed = request.Seed;
            activeRoom.WorldFile = request.WorldFile;
            activeRoom.PackageRevision++;
            foreach (RacePlayer player in activeRoom.Players.Values)
            {
                player.ClearProgress();
                player.WorldReady = player.IsHost;
                player.LastError = null;
                player.Status = player.IsHost
                    ? RacePlayerStatus.WorldReady
                    : RacePlayerStatus.Joined;
                player.Touch();
            }

            activeRoom.Status = ResolveRaceStatus(activeRoom);
            activeRoom.Touch();
            return RaceOperationResult<RaceRoomState>.Success(activeRoom.ToState());
        }
    }

    public RaceOperationResult<RaceRoomState> AuthorizeWorldUpload(string roomCode, string nickname)
    {
        if (!TryGetHostRoom(roomCode, nickname, out RaceRoom? room, out RaceOperationResult<RaceRoomState> failure))
        {
            return failure;
        }

        RaceRoom activeRoom = room!;
        lock (activeRoom.Sync)
        {
            if (activeRoom.Status == RaceRoomStatus.Closed)
            {
                return Failure(RaceErrors.RoomClosed, "Room is closed.");
            }

            return RaceOperationResult<RaceRoomState>.Success(activeRoom.ToState());
        }
    }

    public RaceOperationResult<RaceWorldFileInfo> AuthorizeWorldDownload(string roomCode, string nickname)
    {
        if (!TryGetPlayerRoom(roomCode, nickname, out RaceRoom? room, out _, out RaceOperationResult<RaceRoomState> failure))
        {
            return RaceOperationResult<RaceWorldFileInfo>.Failure(failure.ErrorCode, failure.Message);
        }

        RaceRoom activeRoom = room!;
        lock (activeRoom.Sync)
        {
            if (activeRoom.Status == RaceRoomStatus.Closed)
            {
                return RaceOperationResult<RaceWorldFileInfo>.Failure(RaceErrors.RoomClosed, "Room is closed.");
            }

            return activeRoom.WorldFile is RaceWorldFileInfo worldFile
                ? RaceOperationResult<RaceWorldFileInfo>.Success(worldFile)
                : RaceOperationResult<RaceWorldFileInfo>.Failure(RaceErrors.WorldRequired, "The room host has not uploaded a world file.");
        }
    }

    public RaceOperationResult<RaceRoomState> MarkWorldReady(RaceWorldReadyRequest request)
    {
        if (!TryGetPlayerRoom(request.RoomCode, request.Nickname, out RaceRoom? room, out RacePlayer? player, out RaceOperationResult<RaceRoomState> failure))
        {
            return failure;
        }

        RaceRoom activeRoom = room!;
        RacePlayer activePlayer = player!;
        lock (activeRoom.Sync)
        {
            if (activeRoom.Status == RaceRoomStatus.Closed)
            {
                return Failure(RaceErrors.RoomClosed, "Room is closed.");
            }

            if (activeRoom.WorldFile is null && request.Ready)
            {
                return Failure(RaceErrors.WorldRequired, "The room host has not uploaded a world file.");
            }

            activePlayer.WorldReady = request.Ready;
            activePlayer.LastError = string.IsNullOrWhiteSpace(request.Error)
                ? null
                : request.Error.Trim()[..Math.Min(request.Error.Trim().Length, 512)];
            activePlayer.Status = request.Ready
                ? RacePlayerStatus.WorldReady
                : RacePlayerStatus.Joined;
            activePlayer.Touch();
            activeRoom.Status = ResolveRaceStatus(activeRoom);
            activeRoom.Touch();
            return RaceOperationResult<RaceRoomState>.Success(activeRoom.ToState());
        }
    }

    public RaceOperationResult<RaceRoomState> ReportStart(RaceRunStartReport report)
    {
        if (!TryGetPlayerRoom(report.RoomCode, report.Nickname, out RaceRoom? room, out RacePlayer? player, out RaceOperationResult<RaceRoomState> failure))
        {
            return failure;
        }

        RaceRoom activeRoom = room!;
        RacePlayer activePlayer = player!;
        lock (activeRoom.Sync)
        {
            if (activeRoom.Status == RaceRoomStatus.Closed)
            {
                return Failure(RaceErrors.RoomClosed, "Room is closed.");
            }

            if (activeRoom.WorldFile is null)
            {
                return Failure(RaceErrors.WorldRequired, "The room host has not uploaded a world file.");
            }

            if (!TryValidateProgressIdentity(
                    activeRoom,
                    activePlayer,
                    report.PackageRevision,
                    report.RunId,
                    allowRunInitialization: true,
                    out RaceOperationResult<RaceRoomState> identityFailure))
            {
                return identityFailure;
            }

            activePlayer.Status = RacePlayerStatus.Running;
            activePlayer.WorldReady = true;
            activePlayer.LastError = null;
            activePlayer.Touch();
            activeRoom.Status = ResolveRaceStatus(activeRoom);
            activeRoom.Touch();
            return RaceOperationResult<RaceRoomState>.Success(activeRoom.ToState());
        }
    }

    public RaceOperationResult<RaceRoomState> ReportSplit(RaceSplitReport report)
    {
        if (!TryGetPlayerRoom(report.RoomCode, report.Nickname, out RaceRoom? room, out RacePlayer? player, out RaceOperationResult<RaceRoomState> failure))
        {
            return failure;
        }

        if (report.ElapsedMilliseconds < 0 || report.SplitIndex < 0 || report.ConditionIndex < 0)
        {
            return Failure(RaceErrors.InvalidSplit, "Split index, condition index and elapsed time must be non-negative.");
        }

        RaceRoom activeRoom = room!;
        RacePlayer activePlayer = player!;
        lock (activeRoom.Sync)
        {
            if (activeRoom.Status == RaceRoomStatus.Closed)
            {
                return Failure(RaceErrors.RoomClosed, "Room is closed.");
            }

            if (activeRoom.Route is null || report.SplitIndex >= activeRoom.Route.Splits.Count)
            {
                return Failure(RaceErrors.InvalidSplit, "Split index is outside of the room route.");
            }

            RaceSplitDefinition routeSplit = activeRoom.Route.Splits[report.SplitIndex];
            if (!string.Equals(routeSplit.Id, report.SplitId, StringComparison.OrdinalIgnoreCase))
            {
                return Failure(RaceErrors.InvalidSplit, "Split id does not match the room route.");
            }

            if (routeSplit.Conditions.Count > 0 &&
                report.ConditionIndex >= routeSplit.Conditions.Count)
            {
                return Failure(RaceErrors.InvalidSplit, "Split condition index is outside of the route split.");
            }

            if (!TryValidateProgressIdentity(
                    activeRoom,
                    activePlayer,
                    report.PackageRevision,
                    report.RunId,
                    allowRunInitialization: true,
                    out RaceOperationResult<RaceRoomState> identityFailure))
            {
                return identityFailure;
            }

            RaceSplitReport normalizedReport = NormalizeReport(activeRoom, activePlayer, routeSplit, report);
            if (!normalizedReport.IsSplitComplete && !IsMultiIconProgressSplit(routeSplit))
            {
                return RaceOperationResult<RaceRoomState>.Success(activeRoom.ToState());
            }

            activePlayer.AddReport(normalizedReport);
            activePlayer.Status = RacePlayerStatus.Running;
            activePlayer.WorldReady = true;
            activePlayer.Touch();
            activeRoom.Status = ResolveRaceStatus(activeRoom);
            activeRoom.Touch();

            return RaceOperationResult<RaceRoomState>.Success(activeRoom.ToState());
        }
    }

    public RaceOperationResult<RaceRoomState> ResetPlayerProgress(RaceProgressResetRequest request)
    {
        if (!TryGetPlayerRoom(request.RoomCode, request.Nickname, out RaceRoom? room, out RacePlayer? player, out RaceOperationResult<RaceRoomState> failure))
        {
            return failure;
        }

        RaceRoom activeRoom = room!;
        RacePlayer activePlayer = player!;
        lock (activeRoom.Sync)
        {
            if (activeRoom.Status == RaceRoomStatus.Closed)
            {
                return Failure(RaceErrors.RoomClosed, "Room is closed.");
            }

            if (request.PackageRevision != activeRoom.PackageRevision)
            {
                return Failure(RaceErrors.StalePackage, "Race package revision is no longer current.");
            }

            string runId = NormalizeRunId(request.RunId);
            if (string.IsNullOrWhiteSpace(runId))
            {
                return Failure(RaceErrors.InvalidRequest, "Run id is required.");
            }

            activePlayer.ClearProgress();
            activePlayer.RunId = runId;
            activePlayer.LastError = null;
            activePlayer.Status = activePlayer.WorldReady
                ? RacePlayerStatus.WorldReady
                : RacePlayerStatus.Joined;
            activePlayer.Touch();
            activeRoom.Status = ResolveRaceStatus(activeRoom);
            activeRoom.Touch();
            return RaceOperationResult<RaceRoomState>.Success(activeRoom.ToState());
        }
    }

    public RaceOperationResult<RaceRoomState> LeaveRoom(string roomCode, string nickname)
    {
        if (!TryGetPlayerRoom(roomCode, nickname, out RaceRoom? room, out RacePlayer? player, out RaceOperationResult<RaceRoomState> failure))
        {
            return failure;
        }

        RaceRoom activeRoom = room!;
        RacePlayer activePlayer = player!;
        bool removeRoom;
        RaceRoomState state;
        lock (activeRoom.Sync)
        {
            bool wasHost = activePlayer.IsHost;
            activeRoom.Players.Remove(activePlayer.Nickname);
            if (wasHost)
            {
                activeRoom.Status = RaceRoomStatus.Closed;
                SaveRecord(activeRoom);
            }
            else
            {
                activeRoom.Status = ResolveRaceStatus(activeRoom);
            }

            activeRoom.Touch();
            state = activeRoom.ToState();
            removeRoom = wasHost || activeRoom.Players.Count == 0;
        }

        if (removeRoom)
        {
            rooms.TryRemove(activeRoom.RoomCode, out _);
        }

        return RaceOperationResult<RaceRoomState>.Success(state);
    }

    public RaceOperationResult<RaceRoomState> KickPlayer(RacePlayerKickRequest request)
    {
        if (!TryGetHostRoom(request.RoomCode, request.Nickname, out RaceRoom? room, out RaceOperationResult<RaceRoomState> failure))
        {
            return failure;
        }

        string targetNickname = NormalizeNickname(request.TargetNickname);
        if (string.IsNullOrWhiteSpace(targetNickname))
        {
            return Failure(RaceErrors.InvalidRequest, "Target nickname is required.");
        }

        RaceRoom activeRoom = room!;
        lock (activeRoom.Sync)
        {
            if (activeRoom.Status == RaceRoomStatus.Closed)
            {
                return Failure(RaceErrors.RoomClosed, "Room is closed.");
            }

            if (!activeRoom.Players.TryGetValue(targetNickname, out RacePlayer? targetPlayer))
            {
                return Failure(RaceErrors.PlayerNotFound, "Player is not in this room.");
            }

            if (targetPlayer.IsHost)
            {
                return Failure(RaceErrors.CannotKickHost, "The room host cannot be kicked.");
            }

            activeRoom.Players.Remove(targetPlayer.Nickname);
            activeRoom.Status = ResolveRaceStatus(activeRoom);
            activeRoom.Touch();
            return RaceOperationResult<RaceRoomState>.Success(activeRoom.ToState());
        }
    }

    public RaceOperationResult<RaceRoomState> ResumeRoom(string roomCode, string nickname)
    {
        if (!TryGetPlayerRoom(roomCode, nickname, out RaceRoom? room, out RacePlayer? player, out RaceOperationResult<RaceRoomState> failure))
        {
            return failure;
        }

        RaceRoom activeRoom = room!;
        RacePlayer activePlayer = player!;
        lock (activeRoom.Sync)
        {
            if (activeRoom.Status == RaceRoomStatus.Closed)
            {
                return Failure(RaceErrors.RoomClosed, "Room is closed.");
            }

            activePlayer.Touch();
            activeRoom.Touch();
            return RaceOperationResult<RaceRoomState>.Success(activeRoom.ToState());
        }
    }

    public RaceOperationResult<RaceRoomState> CloseRoom(string roomCode, string nickname)
    {
        if (!TryGetHostRoom(roomCode, nickname, out RaceRoom? room, out RaceOperationResult<RaceRoomState> failure))
        {
            return failure;
        }

        RaceRoom activeRoom = room!;
        RaceRoomState state;
        lock (activeRoom.Sync)
        {
            activeRoom.Status = RaceRoomStatus.Closed;
            activeRoom.Touch();
            SaveRecord(activeRoom);
            state = activeRoom.ToState();
        }

        rooms.TryRemove(activeRoom.RoomCode, out _);
        return RaceOperationResult<RaceRoomState>.Success(state);
    }

    public RaceOperationResult<RaceRoomState> GetRoomState(string roomCode)
    {
        if (!TryGetRoom(roomCode, out RaceRoom? room, out RaceOperationResult<RaceRoomState> failure))
        {
            return failure;
        }

        RaceRoom activeRoom = room!;
        lock (activeRoom.Sync)
        {
            return RaceOperationResult<RaceRoomState>.Success(activeRoom.ToState());
        }
    }

    private static RaceSplitReport NormalizeReport(
        RaceRoom room,
        RacePlayer player,
        RaceSplitDefinition routeSplit,
        RaceSplitReport report)
    {
        RaceSplitConditionDefinition? routeCondition = routeSplit.Conditions
            .FirstOrDefault(condition => condition.ConditionIndex == report.ConditionIndex);
        string? factKey = string.IsNullOrWhiteSpace(report.FactKey)
            ? routeCondition?.FactKey
            : report.FactKey.Trim();
        string? targetId = string.IsNullOrWhiteSpace(report.TargetId)
            ? routeCondition?.TargetId
            : report.TargetId.Trim();
        string? iconFileName = ResolveRouteIconFileName(routeSplit, routeCondition, targetId, factKey, report.ConditionIndex) ??
            (string.IsNullOrWhiteSpace(report.IconFileName) ? null : report.IconFileName.Trim());
        string? iconDisplayName = string.IsNullOrWhiteSpace(report.IconDisplayName)
            ? routeCondition?.DisplayName
            : report.IconDisplayName.Trim();

        return report with
        {
            RoomCode = room.RoomCode,
            Nickname = player.Nickname,
            SplitId = routeSplit.Id,
            FactKey = factKey,
            TargetId = targetId,
            IconFileName = iconFileName,
            IconDisplayName = iconDisplayName,
            ReportedAtUtc = report.ReportedAtUtc ?? DateTimeOffset.UtcNow
        };
    }

    public IReadOnlyList<RaceRoomState> CloseInactiveRooms(DateTimeOffset inactiveBeforeUtc)
    {
        var closed = new List<RaceRoomState>();
        foreach (RaceRoom room in rooms.Values)
        {
            RaceRoomState? state = null;
            lock (room.Sync)
            {
                if (room.Status == RaceRoomStatus.Closed || room.LastUpdatedAtUtc >= inactiveBeforeUtc)
                {
                    continue;
                }

                room.Status = RaceRoomStatus.Closed;
                room.Touch();
                SaveRecord(room);
                state = room.ToState();
            }

            if (state is not null && rooms.TryRemove(room.RoomCode, out _))
            {
                closed.Add(state);
            }
        }

        return closed;
    }

    private static bool TryValidateProgressIdentity(
        RaceRoom room,
        RacePlayer player,
        long packageRevision,
        string runId,
        bool allowRunInitialization,
        out RaceOperationResult<RaceRoomState> failure)
    {
        if (packageRevision != room.PackageRevision)
        {
            failure = Failure(RaceErrors.StalePackage, "Race package revision is no longer current.");
            return false;
        }

        string normalizedRunId = NormalizeRunId(runId);
        if (string.IsNullOrWhiteSpace(normalizedRunId))
        {
            failure = Failure(RaceErrors.InvalidRequest, "Run id is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(player.RunId) && allowRunInitialization)
        {
            player.RunId = normalizedRunId;
        }

        if (!string.Equals(player.RunId, normalizedRunId, StringComparison.Ordinal))
        {
            failure = Failure(RaceErrors.StaleRun, "Race run is no longer current.");
            return false;
        }

        failure = null!;
        return true;
    }

    private static string NormalizeRunId(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string? ResolveRouteIconFileName(
        RaceSplitDefinition routeSplit,
        RaceSplitConditionDefinition? routeCondition,
        string? targetId,
        string? factKey,
        int conditionIndex)
    {
        if (!string.IsNullOrWhiteSpace(routeCondition?.IconFileName))
        {
            return routeCondition.IconFileName;
        }

        if (!string.IsNullOrWhiteSpace(targetId))
        {
            for (int index = 0; index < routeSplit.IconKeys.Count && index < routeSplit.IconFileNames.Count; index++)
            {
                if (string.Equals(routeSplit.IconKeys[index], targetId, StringComparison.OrdinalIgnoreCase))
                {
                    return routeSplit.IconFileNames[index];
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(factKey) && routeSplit.Conditions.Count > 0)
        {
            RaceSplitConditionDefinition? matchingCondition = routeSplit.Conditions.FirstOrDefault(condition =>
                string.Equals(condition.FactKey, factKey, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(matchingCondition?.IconFileName))
            {
                return matchingCondition.IconFileName;
            }
        }

        if (routeSplit.IconFileNames.Count == 1)
        {
            return routeSplit.IconFileNames[0];
        }

        return conditionIndex >= 0 && conditionIndex < routeSplit.IconFileNames.Count
            ? routeSplit.IconFileNames[conditionIndex]
            : null;
    }

    private static bool IsMultiIconProgressSplit(RaceSplitDefinition routeSplit)
    {
        return routeSplit.IconFileNames.Count > 1 && routeSplit.IconKeys.Count > 1;
    }

    private static RaceOperationResult<RaceRoomState> Failure(string errorCode, string message)
    {
        return RaceOperationResult<RaceRoomState>.Failure(errorCode, message);
    }

    private bool TryGetRoom(
        string roomCode,
        out RaceRoom? room,
        out RaceOperationResult<RaceRoomState> failure)
    {
        room = null;
        string normalized = NormalizeRoomCode(roomCode);
        if (string.IsNullOrWhiteSpace(normalized) || !rooms.TryGetValue(normalized, out room))
        {
            failure = Failure(RaceErrors.RoomNotFound, "Room was not found.");
            return false;
        }

        failure = null!;
        return true;
    }

    private bool TryGetPlayerRoom(
        string roomCode,
        string nickname,
        out RaceRoom? room,
        out RacePlayer? player,
        out RaceOperationResult<RaceRoomState> failure)
    {
        room = null;
        player = null;
        if (!TryGetRoom(roomCode, out room, out failure))
        {
            return false;
        }

        RaceRoom activeRoom = room!;
        lock (activeRoom.Sync)
        {
            if (!activeRoom.Players.TryGetValue(NormalizeNickname(nickname), out player))
            {
                failure = Failure(RaceErrors.PlayerNotFound, "Player is not in this room.");
                return false;
            }

            return true;
        }
    }

    private bool TryGetHostRoom(
        string roomCode,
        string nickname,
        out RaceRoom? room,
        out RaceOperationResult<RaceRoomState> failure)
    {
        if (!TryGetPlayerRoom(roomCode, nickname, out room, out RacePlayer? player, out failure))
        {
            return false;
        }

        if (!player!.IsHost)
        {
            failure = Failure(RaceErrors.HostOnly, "Only the room host can perform this action.");
            return false;
        }

        return true;
    }

    private static string CreateRoomCode()
    {
        Span<char> chars = stackalloc char[6];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = RoomCodeChars[Random.Shared.Next(RoomCodeChars.Length)];
        }

        return new string(chars);
    }

    private static string NormalizeRoomCode(string? value)
    {
        return value?.Trim().ToUpperInvariant() ?? string.Empty;
    }

    private static string NormalizeNickname(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static bool IsValidNickname(string? value)
    {
        int length = value?.Trim().Length ?? 0;
        return length is >= 1 and <= MaximumNicknameLength;
    }

    private static bool IsValidRoutePackage(RaceRoutePayload? route)
    {
        if (route is null ||
            route.Splits?.Count is null or < 1 or > MaximumRouteSplits ||
            (route.Icons?.Count ?? 0) > MaximumRouteIcons ||
            string.IsNullOrWhiteSpace(route.SerializedRouteJson) ||
            route.SerializedRouteJson.Length > MaximumSerializedRouteLength)
        {
            return false;
        }

        foreach (RaceSplitDefinition? split in route.Splits)
        {
            if (split is null || string.IsNullOrWhiteSpace(split.Id) || split.Id.Length > 512 ||
                split.IconFileNames is null || split.IconKeys is null || split.Conditions is null)
            {
                return false;
            }
        }

        long totalIconBase64Length = 0;
        foreach (RaceRouteIconPayload? icon in route.Icons ?? [])
        {
            if (icon is null || string.IsNullOrWhiteSpace(icon.Key) || string.IsNullOrWhiteSpace(icon.FileName))
            {
                return false;
            }

            int encodedLength = icon.DataBase64?.Length ?? 0;
            if (encodedLength > MaximumEmbeddedIconBase64Length)
            {
                return false;
            }

            totalIconBase64Length += encodedLength;
            if (totalIconBase64Length > MaximumTotalIconBase64Length)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllPlayersReady(RaceRoom room)
    {
        return room.Players.Count > 0 && room.Players.Values.All(static player => player.WorldReady);
    }

    private static RaceRoomStatus ResolveRaceStatus(RaceRoom room)
    {
        if (room.Players.Values.Any(static player => player.Status == RacePlayerStatus.Running))
        {
            return RaceRoomStatus.Running;
        }

        if (room.WorldFile is not null && AllPlayersReady(room))
        {
            return RaceRoomStatus.Ready;
        }

        if (room.WorldFile is not null)
        {
            return RaceRoomStatus.WorldUploaded;
        }

        return RaceRoomStatus.Lobby;
    }

    private void SaveRecord(RaceRoom room)
    {
        if (room.RecordSaved)
        {
            return;
        }

        var splits = room.Players.Values.ToDictionary(
            static player => player.Nickname,
            static player => (IReadOnlyList<RaceSplitReport>)player.ProgressReports
                .OrderBy(static item => item.Key.SplitIndex)
                .ThenBy(static item => item.Key.ConditionIndex)
                .ThenBy(static item => item.Value.ElapsedMilliseconds)
                .Select(static item => item.Value)
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);
        try
        {
            recordStore.Save(new RaceSavedRoomRecord(room.ToState(), splits, DateTimeOffset.UtcNow));
            room.RecordSaved = true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Race room record save failed. Room={RoomCode}", room.RoomCode);
        }
    }

    private sealed class RaceRoom
    {
        public RaceRoom(
            string roomCode,
            string hostNickname,
            DateTimeOffset createdAtUtc)
        {
            RoomCode = roomCode;
            HostNickname = hostNickname;
            CreatedAtUtc = createdAtUtc;
            LastUpdatedAtUtc = createdAtUtc;
        }

        public object Sync { get; } = new();

        public string RoomCode { get; }

        public string HostNickname { get; }

        public RaceRoomStatus Status { get; set; } = RaceRoomStatus.Lobby;

        public RaceRoutePayload? Route { get; set; }

        public RaceWorldSettings? WorldSettings { get; set; }

        public RaceSeedAssignment? Seed { get; set; }

        public RaceWorldFileInfo? WorldFile { get; set; }

        public long PackageRevision { get; set; }

        public Dictionary<string, RacePlayer> Players { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool RecordSaved { get; set; }

        public DateTimeOffset CreatedAtUtc { get; }

        public DateTimeOffset LastUpdatedAtUtc { get; private set; }

        public void Touch()
        {
            LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        public RaceRoomState ToState()
        {
            RacePlayerState[] players = Players.Values
                .Select(player => player.ToState(Route))
                .OrderByDescending(static player => player.IsHost)
                .ThenBy(static player => player.Nickname, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            RaceLeaderboardEntry[] leaderboard = BuildLeaderboard(Players.Values, Route).ToArray();
            return new RaceRoomState(
                RoomCode,
                Status,
                HostNickname,
                Route,
                WorldSettings,
                Seed,
                WorldFile,
                players,
                leaderboard,
                CreatedAtUtc,
                LastUpdatedAtUtc)
            {
                PackageRevision = PackageRevision
            };
        }

        private static IEnumerable<RaceLeaderboardEntry> BuildLeaderboard(
            IEnumerable<RacePlayer> players,
            RaceRoutePayload? route)
        {
            var ordered = players
                .Select(player => new
                {
                    Player = player,
                    State = player.ToState(route),
                    Ranking = player.GetRankingProgress()
                })
                .OrderByDescending(static item => item.Ranking?.SplitIndex ?? -1)
                .ThenBy(static item => item.Ranking?.ElapsedMilliseconds ?? long.MaxValue)
                .ThenBy(static item => item.State.Nickname, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var entries = new List<RaceLeaderboardEntry>(ordered.Length);
            int currentRank = 0;
            int? previousSplitIndex = null;
            long? previousElapsedMilliseconds = null;
            for (int index = 0; index < ordered.Length; index++)
            {
                var item = ordered[index];
                int splitIndex = item.Ranking?.SplitIndex ?? -1;
                long elapsedMilliseconds = item.Ranking?.ElapsedMilliseconds ?? long.MaxValue;
                if (index == 0 ||
                    splitIndex != previousSplitIndex ||
                    elapsedMilliseconds != previousElapsedMilliseconds)
                {
                    currentRank = index + 1;
                    previousSplitIndex = splitIndex;
                    previousElapsedMilliseconds = elapsedMilliseconds;
                }

                entries.Add(new RaceLeaderboardEntry(
                    currentRank,
                    item.State.Nickname,
                    item.State.Status,
                    item.State.CompletedSplitCount,
                    item.State.LastSplitId,
                    item.State.LastSplitIndex,
                    item.State.LastConditionIndex,
                    item.State.LastFactKey,
                    item.State.LastTargetId,
                    item.State.LastIconFileName,
                    item.State.LastIconDisplayName,
                    item.State.LastSplitElapsedMilliseconds));
            }

            return entries;
        }
    }

    private sealed class RacePlayer
    {
        private RacePlayer(string nickname, bool isHost, DateTimeOffset now)
        {
            Nickname = nickname;
            IsHost = isHost;
            JoinedAtUtc = now;
            LastUpdatedAtUtc = now;
        }

        public string Nickname { get; }

        public bool IsHost { get; }

        public RacePlayerStatus Status { get; set; } = RacePlayerStatus.Joined;

        public bool WorldReady { get; set; }

        public string? LastError { get; set; }

        public string RunId { get; set; } = string.Empty;

        public Dictionary<RaceProgressKey, RaceSplitReport> ProgressReports { get; } = new();

        public Dictionary<int, RaceSplitReport> CompletedReports { get; } = new();

        public HashSet<int> CompletedSplitIndexes { get; } = new();

        public DateTimeOffset JoinedAtUtc { get; }

        public DateTimeOffset LastUpdatedAtUtc { get; private set; }

        public static RacePlayer Create(string nickname, bool isHost, DateTimeOffset now)
        {
            return new RacePlayer(nickname, isHost, now);
        }

        public void Touch()
        {
            LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        public void AddReport(RaceSplitReport report)
        {
            RaceProgressKey progressKey = RaceProgressKey.From(report);
            if (!ProgressReports.ContainsKey(progressKey))
            {
                ProgressReports[progressKey] = report;
            }

            if (report.IsSplitComplete && !CompletedReports.ContainsKey(report.SplitIndex))
            {
                CompletedReports[report.SplitIndex] = report;
                CompletedSplitIndexes.Add(report.SplitIndex);
            }
        }

        public void ClearProgress()
        {
            ProgressReports.Clear();
            CompletedReports.Clear();
            CompletedSplitIndexes.Clear();
            RunId = string.Empty;
        }

        public RaceSplitReport? GetDisplayProgress(RaceRoutePayload? route)
        {
            if (ProgressReports.Count == 0)
            {
                return null;
            }

            RaceSplitReport? incompleteProgress = GetLatestIncompleteProgress(route);
            if (incompleteProgress is not null)
            {
                return incompleteProgress;
            }

            return GetRankingProgress() ?? GetLatestProgress(ProgressReports.Values);
        }

        public RaceSplitReport? GetRankingProgress()
        {
            if (CompletedReports.Count == 0)
            {
                return null;
            }

            int splitIndex = CompletedReports.Keys.Max();
            return CompletedReports.TryGetValue(splitIndex, out RaceSplitReport? report)
                ? report
                : null;
        }

        private RaceSplitReport? GetLatestIncompleteProgress(RaceRoutePayload? route)
        {
            if (route is null)
            {
                return null;
            }

            return GetLatestProgress(ProgressReports.Values.Where(report =>
                report.SplitIndex >= 0 &&
                report.SplitIndex < route.Splits.Count &&
                !CompletedSplitIndexes.Contains(report.SplitIndex)));
        }

        private static RaceSplitReport? GetLatestProgress(IEnumerable<RaceSplitReport> reports)
        {
            return reports
                .OrderBy(static report => report.ElapsedMilliseconds)
                .ThenBy(static report => report.ReportedAtUtc ?? DateTimeOffset.MinValue)
                .ThenBy(static report => report.SplitIndex)
                .ThenBy(static report => report.ConditionIndex)
                .LastOrDefault();
        }

        public RacePlayerState ToState(RaceRoutePayload? route)
        {
            RaceSplitReport? last = GetDisplayProgress(route);
            return new RacePlayerState(
                Nickname,
                Status,
                IsHost,
                WorldReady,
                CompletedSplitIndexes.Count,
                last?.SplitIndex ?? -1,
                last?.ConditionIndex ?? -1,
                last?.SplitId,
                last?.FactKey,
                last?.TargetId,
                last?.IconFileName,
                last?.IconDisplayName,
                last?.ElapsedMilliseconds,
                LastError,
                JoinedAtUtc,
                LastUpdatedAtUtc);
        }
    }

    private readonly record struct RaceProgressKey(
        int SplitIndex,
        int ConditionIndex,
        string FactKey)
    {
        public static RaceProgressKey From(RaceSplitReport report)
        {
            return new RaceProgressKey(
                report.SplitIndex,
                report.ConditionIndex,
                (report.FactKey?.Trim() ?? string.Empty).ToUpperInvariant());
        }
    }
}
