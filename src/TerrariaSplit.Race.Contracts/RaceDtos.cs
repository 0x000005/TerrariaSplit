namespace TerrariaSplit.Race.Contracts;

public enum RaceRoomStatus
{
    Lobby,
    WorldUploaded,
    Ready,
    Running,
    Closed
}

public enum RacePlayerStatus
{
    Joined,
    WorldReady,
    Running
}

public enum RaceSeedSource
{
    Fixed,
    HostGenerated
}

public sealed record RaceSplitDefinition(
    int Index,
    string Id,
    string DisplayName,
    bool IsAttached = false)
{
    public IReadOnlyList<string> IconFileNames { get; init; } = [];

    public IReadOnlyList<string> IconKeys { get; init; } = [];

    public IReadOnlyList<RaceSplitConditionDefinition> Conditions { get; init; } = [];
}

public sealed record RaceRouteIconPayload(
    string Key,
    string FileName,
    string? DataBase64 = null);

public sealed record RaceSplitConditionDefinition(
    int ConditionIndex,
    string FactKey,
    string? TargetId,
    string DisplayName,
    string? IconFileName);

public sealed record RaceRoutePayload(
    string RouteHash,
    string Summary,
    string SerializedRouteJson,
    IReadOnlyList<RaceSplitDefinition> Splits)
{
    public IReadOnlyList<RaceRouteIconPayload> Icons { get; init; } = [];
}

public sealed record RaceWorldSettings(
    string TerrariaVersion,
    int SizeCode,
    int DifficultyCode,
    bool HasCrimson,
    int SpecialSeedMask,
    int RequiredPyramidItemMask,
    string WorldName = "",
    string SecretSeeds = "");

public sealed record RaceSeedAssignment(
    string SeedText,
    RaceSeedSource Source,
    string Detail = "");

public sealed record RaceWorldFileInfo(
    string FileName,
    long Length,
    string Sha256,
    DateTimeOffset UploadedAtUtc,
    string UploadedBy);

public sealed record RaceSplitReport(
    string RoomCode,
    string Nickname,
    int SplitIndex,
    string SplitId,
    long ElapsedMilliseconds,
    DateTimeOffset? ReportedAtUtc = null,
    int ConditionIndex = 0,
    string? FactKey = null,
    string? TargetId = null,
    string? IconFileName = null,
    string? IconDisplayName = null,
    bool IsSplitComplete = true)
{
    public long PackageRevision { get; init; }

    public string RunId { get; init; } = string.Empty;
}

public sealed record RaceRunStartReport(
    string RoomCode,
    string Nickname,
    DateTimeOffset? ReportedAtUtc = null)
{
    public long PackageRevision { get; init; }

    public string RunId { get; init; } = string.Empty;
}

public sealed record RacePlayerState(
    string Nickname,
    RacePlayerStatus Status,
    bool IsHost,
    bool WorldReady,
    int CompletedSplitCount,
    int LastSplitIndex,
    int LastConditionIndex,
    string? LastSplitId,
    string? LastFactKey,
    string? LastTargetId,
    string? LastIconFileName,
    string? LastIconDisplayName,
    long? LastSplitElapsedMilliseconds,
    string? LastError,
    DateTimeOffset JoinedAtUtc,
    DateTimeOffset LastUpdatedAtUtc);

public sealed record RaceLeaderboardEntry(
    int Rank,
    string Nickname,
    RacePlayerStatus Status,
    int CompletedSplitCount,
    string? LastSplitId,
    int LastSplitIndex,
    int LastConditionIndex,
    string? LastFactKey,
    string? LastTargetId,
    string? LastIconFileName,
    string? LastIconDisplayName,
    long? LastSplitElapsedMilliseconds);

public sealed record RaceRoomState(
    string RoomCode,
    RaceRoomStatus Status,
    string HostNickname,
    RaceRoutePayload? Route,
    RaceWorldSettings? WorldSettings,
    RaceSeedAssignment? Seed,
    RaceWorldFileInfo? WorldFile,
    IReadOnlyList<RacePlayerState> Players,
    IReadOnlyList<RaceLeaderboardEntry> Leaderboard,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastUpdatedAtUtc)
{
    public long PackageRevision { get; init; }
}

public enum RaceRoomStateUpdateKind
{
    Snapshot,
    RoomCreated,
    PlayerJoined,
    WorldReadyChanged,
    PlayerLeft,
    PlayerKicked,
    RoomClosed,
    RoomResumed
}

public sealed record RacePackageChanged(
    RaceRoomState State,
    string ActorNickname,
    string PackageRevision);

public sealed record RaceRosterChanged(
    RaceRoomStateUpdateKind Kind,
    RaceRoomState State,
    string ActorNickname = "");

public sealed record RaceRoomProgressState(
    string RoomCode,
    RaceRoomStatus Status,
    IReadOnlyList<RacePlayerState> Players,
    IReadOnlyList<RaceLeaderboardEntry> Leaderboard,
    DateTimeOffset LastUpdatedAtUtc)
{
    public long PackageRevision { get; init; }
}

public sealed record RaceProgressChanged(RaceRoomProgressState Progress);

public sealed record RaceProgressResetRequest(
    string RoomCode,
    string Nickname,
    long PackageRevision = 0,
    string RunId = "");

public static class RacePackageRevisionCalculator
{
    public static string Create(RaceRoomState state)
    {
        return state.PackageRevision.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

public sealed record RaceRoomCreateRequest(string Nickname);

public sealed record RaceRoomJoinRequest(
    string RoomCode,
    string Nickname);

public sealed record RaceWorldReadyRequest(
    string RoomCode,
    string Nickname,
    bool Ready,
    string? Error = null);

public sealed record RacePlayerKickRequest(
    string RoomCode,
    string Nickname,
    string TargetNickname);

public sealed record RaceWorldFilePublishRequest(
    string RoomCode,
    string Nickname,
    RaceRoutePayload Route,
    RaceWorldSettings WorldSettings,
    RaceSeedAssignment? Seed,
    RaceWorldFileInfo WorldFile);

public sealed record RaceOperationResult<T>(
    bool Succeeded,
    T? Value,
    string ErrorCode,
    string Message)
{
    public static RaceOperationResult<T> Success(T value)
    {
        return new RaceOperationResult<T>(true, value, string.Empty, string.Empty);
    }

    public static RaceOperationResult<T> Failure(string errorCode, string message)
    {
        return new RaceOperationResult<T>(false, default, errorCode, message);
    }
}
