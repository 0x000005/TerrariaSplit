using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.UI;

internal interface IRacePanelShell
{
    RaceRoomState? State { get; }

    bool IsHostInCurrentRoom { get; }

    string? LocalWorldPath { get; }

    RacePanelDraftState DraftState { get; }

    RaceLeaderboardSettings LeaderboardSettings { get; }

    string Localize(string key);

    void SaveDraftState(RacePanelDraftState draftState);

    void SaveLeaderboardSettings(RaceLeaderboardSettings leaderboardSettings);

    Task CreateRoomAsync(string serverUrl, string nickname);

    Task<RaceOperationResult<RaceRoomState>> JoinRoomAsync(string serverUrl, string roomCode, string nickname);

    Task CloseRoomAsync();

    Task CopyRoomInfoAsync();

    Task KickPlayerAsync(string nickname);

    Task GenerateRandomWorldAsync(RaceWorldSettings worldSettings, IProgress<int>? progress = null);

    Task GenerateCustomSeedWorldAsync(RaceWorldSettings worldSettings, string seedText, IProgress<int>? progress = null);

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

    Task LeaveAsync();
}
