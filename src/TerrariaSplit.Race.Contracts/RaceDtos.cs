namespace TerrariaSplit.Race.Contracts;

public enum RaceRoomStatus
{
    Lobby,
    WorldUploaded,
    Ready,
    Starting,
    Running,
    Closed
}

public enum RacePlayerStatus
{
    Joined,
    WorldReady,
    Running
}

public enum RacePlayerFileStatus
{
    Waiting,
    Creating,
    Ready,
    Failed
}

public enum RaceWorldFileStatus
{
    Waiting,
    Downloading,
    Ready,
    Failed
}

public enum RaceRngControlStatus
{
    Closed,
    Enabling,
    Enabled,
    EnableFailed,
    NotEnabled
}

public enum RaceServerConnectionStatus
{
    Connecting,
    Connected,
    Reconnecting,
    Disconnected,
    ConnectionFailed
}

public enum RaceSeedSource
{
    Fixed,
    HostGenerated
}

public static class RacePlayerNameRules
{
    public const int MaximumLength = 20;
}

public static class RaceRoomCodeRules
{
    public const int Length = 4;

    public static bool IsValid(string? value)
    {
        if (value?.Length != Length)
        {
            return false;
        }

        foreach (char character in value)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }
}

public static class RaceDeathMessageRules
{
    public const int MaximumUtf8Length = 1024;

    public static string Normalize(string? value)
    {
        return (value ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\0', ' ')
            .Trim();
    }

    public static bool IsValid(string value)
    {
        return System.Text.Encoding.UTF8.GetByteCount(value) <= MaximumUtf8Length;
    }
}

public static class RacePlayerDifficultyCodes
{
    private const int JourneyWorldDifficultyCode = 4;

    public const int Softcore = 0;
    public const int Mediumcore = 1;
    public const int Hardcore = 2;
    public const int Journey = 3;

    public static bool IsValid(int value) => value is >= Softcore and <= Journey;

    public static int Normalize(int value) => IsValid(value) ? value : Softcore;

    public static int ForWorldDifficulty(int worldDifficultyCode) =>
        worldDifficultyCode == JourneyWorldDifficultyCode ? Journey : Softcore;
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

public sealed record RaceCheatSettings(
    bool Enabled,
    bool PyramidEnabled,
    int PyramidItemMask,
    bool CrimsonEnabled,
    string CrimsonDistance,
    int ResourceItemMask,
    int LifeCrystalMinimum,
    int SpelunkerPotionMinimum,
    int FeatherfallPotionMinimum,
    string JungleRouteDepth = "0")
{
    public static RaceCheatSettings Disabled { get; } = new(
        false,
        false,
        0,
        false,
        string.Empty,
        0,
        0,
        0,
        0);
}

public sealed record RaceWorldSettings(
    string TerrariaVersion,
    int SizeCode,
    int DifficultyCode,
    bool HasCrimson,
    int SpecialSeedMask,
    RaceCheatSettings Cheats,
    string WorldName = "",
    string SecretSeeds = "",
    int PlayerDifficultyCode = RacePlayerDifficultyCodes.Softcore,
    bool RngControlEnabled = true,
    bool BossFailurePenaltyEnabled = true,
    string BossPenaltySchedule = "")
{
    public RaceCheatSettings EffectiveCheats => Cheats ?? RaceCheatSettings.Disabled;
}

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

public sealed record RaceDeathReport(
    string RoomCode,
    string Nickname,
    DateTimeOffset? ReportedAtUtc = null,
    string DeathMessage = "")
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
    DateTimeOffset LastUpdatedAtUtc)
{
    public RacePlayerFileStatus PlayerFileStatus { get; init; }

    public RaceWorldFileStatus WorldFileStatus { get; init; }

    public RaceRngControlStatus RngControlStatus { get; init; }

    public RaceServerConnectionStatus ServerConnectionStatus { get; init; } = RaceServerConnectionStatus.Connected;

    public bool IsReady { get; init; }
}

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
    RaceDeterminismPackage? Determinism,
    IReadOnlyList<RacePlayerState> Players,
    IReadOnlyList<RaceLeaderboardEntry> Leaderboard,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastUpdatedAtUtc)
{
    public long PackageRevision { get; init; }

    public DateTimeOffset? ScheduledStartUtc { get; init; }

    public int StartCountdownMilliseconds { get; init; }

    public long StartSequence { get; init; }
}

public enum RaceRoomStateUpdateKind
{
    Snapshot,
    RoomCreated,
    PlayerJoined,
    WorldReadyChanged,
    PlayerLeft,
    PlayerKicked,
    PlayerConnectionChanged,
    PlayerReadyChanged,
    RaceStarting,
    RoomClosed,
    RoomResumed
}

public enum RacePackageChangeKind
{
    Published,
    Restarted
}

public sealed record RacePackageChanged(
    RaceRoomState State,
    string ActorNickname,
    string PackageRevision,
    RacePackageChangeKind Kind);

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

public sealed record RaceGroupCompleted(
    string RoomCode,
    long PackageRevision,
    string RunId,
    string Nickname,
    int SplitIndex,
    string SplitId,
    long ElapsedMilliseconds,
    long Sequence);

public sealed record RacePlayerDied(
    string RoomCode,
    long PackageRevision,
    string RunId,
    string Nickname,
    long Sequence,
    string DeathMessage = "");

public sealed record RacePlayerProgressReset(
    string RoomCode,
    long PackageRevision,
    string RunId,
    string Nickname);

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

public sealed record RaceHostActionRequest(
    string RoomCode,
    string Nickname,
    long PackageRevision);

public sealed record RacePreparationStatusRequest(
    string RoomCode,
    string Nickname,
    RacePlayerFileStatus PlayerFileStatus,
    RaceWorldFileStatus WorldFileStatus,
    RaceRngControlStatus RngControlStatus,
    string? Error = null,
    long PackageRevision = 0);

public sealed record RacePlayerReadyRequest(
    string RoomCode,
    string Nickname,
    long PackageRevision,
    bool IsReady);

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
