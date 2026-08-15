using TerrariaSplit.Race.Client;
using TerrariaSplit.Race.Contracts;
using TerrariaSplit.Terraria;

namespace TerrariaSplit.UI;

internal sealed class RaceWorldFileWorkspace
{
    private readonly IAppLogger logger;
    private readonly TimeProvider timeProvider;

    public RaceWorldFileWorkspace(IAppLogger logger, TimeProvider? timeProvider = null)
    {
        this.logger = logger;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string CreateDownloadPath(RaceRoomState state)
    {
        string worldsDirectory = GetWorldsDirectory();
        string fileName = NormalizeFileName(state.WorldFile?.FileName);
        string stem = string.IsNullOrWhiteSpace(fileName)
            ? CreateWorldStem(state.WorldFile?.UploadedAtUtc ?? timeProvider.GetLocalNow())
            : SanitizeFileStem(Path.GetFileNameWithoutExtension(fileName));
        return GetUniqueWorldPath(worldsDirectory, stem);
    }

    public string PrepareForUpload(string sourcePath, DateTimeOffset timestamp)
    {
        if (!RaceWorldFileValidator.IsValidWorldFilePath(sourcePath))
        {
            throw new InvalidOperationException("A valid world file is required.");
        }

        string sourceStem = Path.GetFileNameWithoutExtension(sourcePath);
        if (IsRaceWorldStem(sourceStem))
        {
            return sourcePath;
        }

        string stem = CreateWorldStem(timestamp);
        string targetPath = GetUniqueWorldPath(GetWorldsDirectory(), stem);
        File.Copy(sourcePath, targetPath, overwrite: false);
        CopyFileIfPresent(sourcePath + ".bak", targetPath + ".bak");
        return targetPath;
    }

    public void Delete(string worldPath)
    {
        foreach (string path in EnumerateWorldFiles(worldPath))
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
                logger.Error(ex, "Race generated world cleanup failed.");
            }
        }
    }

    public bool Exists(string? worldPath)
    {
        return !string.IsNullOrWhiteSpace(worldPath) && File.Exists(worldPath);
    }

    public string CreateWorldStem(DateTimeOffset timestamp)
    {
        return SanitizeFileStem($"TerrariaRace-{timestamp.LocalDateTime:yyyyMMddHHmmss}");
    }

    public string CreateRevisionKey(string roomCode, RaceWorldFileInfo worldFile)
    {
        return string.Join(
            "|",
            roomCode.Trim(),
            NormalizeFileName(worldFile.FileName),
            (worldFile.Sha256 ?? string.Empty).Trim(),
            worldFile.UploadedAtUtc.UtcDateTime.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToUpperInvariant();
    }

    public string NormalizeFileName(string? fileName)
    {
        string name = Path.GetFileName(fileName ?? string.Empty).Trim();
        return string.Equals(Path.GetExtension(name), ".wld", StringComparison.OrdinalIgnoreCase)
            ? name
            : string.Empty;
    }

    private static string GetWorldsDirectory()
    {
        string worldsDirectory = Path.Combine(TerrariaSavePaths.SaveRoot(), "Worlds");
        Directory.CreateDirectory(worldsDirectory);
        return worldsDirectory;
    }

    private string GetUniqueWorldPath(string directory, string stem)
    {
        string candidate = Path.Combine(directory, stem + ".wld");
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        for (int index = 1; index < 10_000; index++)
        {
            candidate = Path.Combine(directory, $"{stem}-{index}.wld");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{stem}-{timeProvider.GetUtcNow():yyyyMMddHHmmssfff}.wld");
    }

    private static IEnumerable<string> EnumerateWorldFiles(string worldPath)
    {
        yield return worldPath;
        yield return worldPath + ".bak";

        string? directory = Path.GetDirectoryName(worldPath);
        string stem = Path.GetFileNameWithoutExtension(worldPath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(stem))
        {
            yield break;
        }

        string twldPath = Path.Combine(directory, stem + ".twld");
        yield return twldPath;
        yield return twldPath + ".bak";
    }

    private static bool IsRaceWorldStem(string? stem)
    {
        return !string.IsNullOrWhiteSpace(stem) &&
            stem.Trim().StartsWith("TerrariaRace-", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeFileStem(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string stem = new(value
            .Trim()
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray());
        return string.IsNullOrWhiteSpace(stem) ? "TerrariaRace" : stem;
    }

    private static void CopyFileIfPresent(string sourcePath, string targetPath)
    {
        if (File.Exists(sourcePath))
        {
            File.Copy(sourcePath, targetPath, overwrite: false);
        }
    }
}
