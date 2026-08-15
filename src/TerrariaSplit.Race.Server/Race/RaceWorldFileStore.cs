using System.Security.Cryptography;
using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.Race.Server;

public sealed record RaceStoredWorldFile(
    RaceWorldFileInfo Info,
    string Path,
    bool WasCreated);

public sealed class RaceWorldFileStore
{
    private readonly string rootDirectory;
    private readonly TimeProvider timeProvider;

    public RaceWorldFileStore(string rootDirectory, TimeProvider? timeProvider = null)
    {
        this.rootDirectory = rootDirectory;
        this.timeProvider = timeProvider ?? TimeProvider.System;
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
        string tempPath = Path.Combine(roomDirectory, $".upload-{Guid.NewGuid():N}.tmp");
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long length = 0;
            await using (var destination = new FileStream(
                             tempPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                byte[] buffer = new byte[81920];
                while (true)
                {
                    int read = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
                    if (read <= 0)
                    {
                        break;
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    hash.AppendData(buffer, 0, read);
                    length += read;
                }

                await destination.FlushAsync(cancellationToken);
            }

            await using (FileStream validationStream = File.OpenRead(tempPath))
            {
                if (!RaceWorldFileValidator.TryValidateWorldStream(validationStream, out string detail))
                {
                    throw new InvalidDataException("Invalid Terraria world file: " + detail);
                }
            }

            string hashText = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            string path = Path.Combine(roomDirectory, $"world-{hashText}.wld");
            bool wasCreated;
            try
            {
                File.Move(tempPath, path);
                wasCreated = true;
            }
            catch (IOException) when (File.Exists(path))
            {
                if (new FileInfo(path).Length != length)
                {
                    throw new IOException("The stored world file has an unexpected length.");
                }

                File.Delete(tempPath);
                wasCreated = false;
            }

            var worldFile = new RaceWorldFileInfo(
                fileName,
                length,
                hashText,
                timeProvider.GetUtcNow(),
                nickname.Trim());
            return new RaceStoredWorldFile(worldFile, path, wasCreated);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    public bool TryGetPath(string roomCode, RaceWorldFileInfo worldFile, out string path)
    {
        string hash = NormalizeSha256(worldFile.Sha256);
        path = string.IsNullOrWhiteSpace(hash)
            ? string.Empty
            : Path.Combine(GetRoomDirectory(roomCode), $"world-{hash}.wld");
        return !string.IsNullOrWhiteSpace(path) &&
            File.Exists(path) &&
            new FileInfo(path).Length == worldFile.Length;
    }

    public void DeleteStoredFile(RaceStoredWorldFile stored)
    {
        TryDeleteFile(stored.Path);
    }

    public void DeleteStoredFile(string roomCode, RaceWorldFileInfo worldFile)
    {
        string hash = NormalizeSha256(worldFile.Sha256);
        if (!string.IsNullOrWhiteSpace(hash))
        {
            TryDeleteFile(Path.Combine(GetRoomDirectory(roomCode), $"world-{hash}.wld"));
        }
    }

    public void DeleteAllRooms()
    {
        try
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public void DeleteRoom(string roomCode)
    {
        string roomDirectory = GetRoomDirectory(roomCode);
        try
        {
            if (Directory.Exists(roomDirectory))
            {
                Directory.Delete(roomDirectory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private string GetRoomDirectory(string roomCode)
    {
        return Path.Combine(rootDirectory, SanitizeRoomCode(roomCode));
    }

    private static string NormalizeSha256(string? value)
    {
        string hash = value?.Trim().ToLowerInvariant() ?? string.Empty;
        return hash.Length == 64 && hash.All(Uri.IsHexDigit)
            ? hash
            : string.Empty;
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

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
