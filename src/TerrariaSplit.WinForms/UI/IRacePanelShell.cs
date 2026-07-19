using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.UI;

internal interface IRacePanelShell
{
    RaceRoomState? State { get; }

    bool IsRaceEnabled { get; }

    RacePanelDraftState DraftState { get; }

    RaceServerConnectionStatus ServerConnectionStatus { get; }

    string? LocalNickname { get; }

    bool IsHostInCurrentRoom { get; }

    string? LocalWorldPath { get; }

    RaceLeaderboardSettings LeaderboardSettings { get; }

    RaceVoiceSettings VoiceSettings { get; }

    IReadOnlyList<RaceVoiceOption> InstalledVoices { get; }

    string Localize(string key);

    void SaveRaceEnabled(bool enabled);

    void SaveDraftState(RacePanelDraftState draftState);

    void OpenInGameMenu();

    void SaveLeaderboardSettings(RaceLeaderboardSettings leaderboardSettings);

    void SaveVoiceSettings(RaceVoiceSettings voiceSettings);

    void PreviewVoice(RaceVoiceSettings voiceSettings);

    Task CreateRoomAsync(string serverUrl, string nickname);

    Task<RaceOperationResult<RaceRoomState>> JoinRoomAsync(
        string serverUrl,
        string roomCode,
        string nickname);

    Task CloseRoomAsync();

    Task KickPlayerAsync(string nickname);

    Task<RacePanelWorldGenerationResult> GenerateRandomWorldAsync(
        RaceWorldSettings worldSettings,
        IProgress<int>? progress = null);

    Task<RacePanelWorldGenerationResult> GenerateCustomSeedWorldAsync(
        RaceWorldSettings worldSettings,
        string seedText,
        IProgress<int>? progress = null);

    Task<RaceOperationResult<RaceRoomState>> UploadWorldAsync(
        string serverUrl,
        string nickname,
        string worldPath,
        RaceWorldSettings worldSettings,
        string seedText,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);

    Task CancelWorldGenerationAsync();

    Task DiscardLocalWorldAsync(string worldPath);

    Task<RaceOperationResult<RaceRoomState>> StartAsync();

    Task<RaceOperationResult<RaceRoomState>> RestartAsync();

    Task LeaveAsync();
}

internal readonly record struct RacePanelWorldGenerationResult(
    bool Succeeded,
    string Message)
{
    public static RacePanelWorldGenerationResult Success()
    {
        return new RacePanelWorldGenerationResult(true, string.Empty);
    }

    public static RacePanelWorldGenerationResult Failure(string message)
    {
        return new RacePanelWorldGenerationResult(false, message);
    }
}
