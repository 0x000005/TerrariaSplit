using System.Text.Json;
using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.Race.Server;

public sealed record RaceSavedRoomRecord(
    RaceRoomState State,
    IReadOnlyDictionary<string, IReadOnlyList<RaceSplitReport>> SplitsByPlayer,
    DateTimeOffset SavedAtUtc);

public interface IRaceRecordStore
{
    void Save(RaceSavedRoomRecord record);
}

public sealed class FileRaceRecordStore : IRaceRecordStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string directory;

    public FileRaceRecordStore(string directory)
    {
        this.directory = directory;
    }

    public void Save(RaceSavedRoomRecord record)
    {
        Directory.CreateDirectory(directory);
        string fileName = $"{Sanitize(record.State.RoomCode)}-{record.SavedAtUtc:yyyyMMdd-HHmmss}.json";
        string path = Path.Combine(directory, fileName);
        string json = JsonSerializer.Serialize(record, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static string Sanitize(string value)
    {
        return new string(value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
    }
}

public sealed class InMemoryRaceRecordStore : IRaceRecordStore
{
    private readonly List<RaceSavedRoomRecord> records = new();

    public IReadOnlyList<RaceSavedRoomRecord> Records => records;

    public void Save(RaceSavedRoomRecord record)
    {
        records.Add(record);
    }
}
