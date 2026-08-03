namespace TerrariaSplit.Race.Client;

public sealed record RaceRoomResumeFailed(
    string RoomCode,
    string ErrorCode,
    string Message);
