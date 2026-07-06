using System.Security.Cryptography;
using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.Race.Server;

public sealed record RaceStoredWorldFile(RaceWorldFileInfo Info, string Path);

public sealed class RaceWorldFileStore
{
    private readonly string rootDirectory;

    public RaceWorldFileStore(string rootDirectory)
    {
        this.rootDirectory = rootDirectory;
    }

    public async Task<RaceStoredWorldFile> SaveAsync(
        string roomCode,
        string nickname,
        string originalFileName,
        Stream source,
        CancellationToken cancellationToken)
    {
        string roomDirectory = GetRoomDirectory(roomCode);
        Directory.CreateDirectory(roomDirectory);
        string fileName = SanitizeFileName(originalFileName);
        string path = Path.Combine(roomDirectory, "world.wld");
        string tempPath = path + ".tmp";

        await using (FileStream destination = File.Create(tempPath))
        {
            await source.CopyToAsync(destination, cancellationToken);
        }

        File.Move(tempPath, path, overwrite: true);
        var info = new FileInfo(path);
        string hash = await ComputeSha256Async(path, cancellationToken);
        var worldFile = new RaceWorldFileInfo(
            fileName,
            info.Length,
            hash,
            DateTimeOffset.UtcNow,
            nickname.Trim());
        return new RaceStoredWorldFile(worldFile, path);
    }

    public bool TryGetPath(string roomCode, out string path)
    {
        path = Path.Combine(GetRoomDirectory(roomCode), "world.wld");
        return File.Exists(path);
    }

    private string GetRoomDirectory(string roomCode)
    {
        return Path.Combine(rootDirectory, SanitizeRoomCode(roomCode));
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string SanitizeRoomCode(string value)
    {
        string sanitized = new(value
            .Where(static ch => char.IsLetterOrDigit(ch))
            .ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "room" : sanitized.ToUpperInvariant();
    }

    private static string SanitizeFileName(string value)
    {
        string fileName = Path.GetFileName(value.Trim());
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(Path.GetExtension(fileName), ".wld", StringComparison.OrdinalIgnoreCase))
        {
            fileName = "TerrariaSplitRace.wld";
        }

        char[] invalid = Path.GetInvalidFileNameChars();
        string sanitized = new(fileName
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "TerrariaSplitRace.wld" : sanitized;
    }
}
