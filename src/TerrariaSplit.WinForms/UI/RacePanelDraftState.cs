using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.UI;

internal enum RacePanelRole
{
    Host,
    Member
}

internal enum RacePanelWorldSource
{
    Random,
    CustomSeed,
    ExistingFile
}

internal sealed record RacePanelDraftState(
    string ServerUrl,
    string Nickname,
    string RoomCode,
    string SeedText,
    string LocalWorldPath,
    string PlayerTemplateCode,
    RacePanelRole Role,
    RacePanelWorldSource WorldSource)
{
    public static RacePanelDraftState CreateDefault()
    {
        return FromSettings(new AppSettings());
    }

    public static RacePanelDraftState FromSettings(AppSettings settings)
    {
        RaceSettings race = settings.Race ?? new RaceSettings();
        string nickname = string.IsNullOrWhiteSpace(race.Nickname)
            ? Environment.UserName
            : race.Nickname;
        return new RacePanelDraftState(
            string.IsNullOrWhiteSpace(race.ServerUrl) ? new RaceSettings().ServerUrl : race.ServerUrl,
            nickname,
            race.LastRoomCode,
            string.Empty,
            string.Empty,
            race.PlayerTemplateCode,
            ToRole(race.PreferredRole),
            ToWorldSource(race.PreferredWorldSource)).Normalize();
    }

    public RacePanelDraftState Normalize()
    {
        return this with
        {
            ServerUrl = ServerUrl.Trim(),
            Nickname = NormalizeNickname(Nickname),
            RoomCode = NormalizeRoomCode(RoomCode),
            SeedText = SeedText.Trim(),
            LocalWorldPath = LocalWorldPath.Trim(),
            PlayerTemplateCode = PlayerTemplateCode.Trim()
        };
    }

    private static string NormalizeNickname(string? value)
    {
        string nickname = value?.Trim() ?? string.Empty;
        return nickname.Length <= RacePlayerNameRules.MaximumLength
            ? nickname
            : nickname[..RacePlayerNameRules.MaximumLength];
    }

    private static string NormalizeRoomCode(string? value)
    {
        string roomCode = value?.Trim() ?? string.Empty;
        return RaceRoomCodeRules.IsValid(roomCode) ? roomCode : string.Empty;
    }

    private static RacePanelRole ToRole(string? value)
    {
        return string.Equals(value, RacePreferredRole.Member, StringComparison.OrdinalIgnoreCase)
            ? RacePanelRole.Member
            : RacePanelRole.Host;
    }

    private static RacePanelWorldSource ToWorldSource(string? value)
    {
        if (string.Equals(value, RacePreferredWorldSource.CustomSeed, StringComparison.OrdinalIgnoreCase))
        {
            return RacePanelWorldSource.CustomSeed;
        }

        return RacePanelWorldSource.Random;
    }
}
