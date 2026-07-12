using TerrariaSplit.Terraria.Processes;

namespace TerrariaSplit.Terraria.Automation;

public sealed record TerrariaRaceWorldGenerationResult(
    bool Succeeded,
    string WorldPath,
    string Message)
{
    public static TerrariaRaceWorldGenerationResult Success(string worldPath)
    {
        return new TerrariaRaceWorldGenerationResult(true, worldPath, string.Empty);
    }

    public static TerrariaRaceWorldGenerationResult Failure(string message)
    {
        return new TerrariaRaceWorldGenerationResult(false, string.Empty, message);
    }
}

public sealed class TerrariaRaceWorldGenerationService : IDisposable
{
    private readonly HeadlessWorldGenerator generator;

    public TerrariaRaceWorldGenerationService(IRuntimeDataPaths? paths = null)
    {
        generator = new HeadlessWorldGenerator(paths);
    }

    public async Task<TerrariaRaceWorldGenerationResult> GenerateAndInstallAsync(
        AutoCreateWorldSettings settings,
        string seedText,
        string worldName,
        string? appLanguage,
        CancellationToken cancellationToken,
        IProgress<int>? progress = null,
        int progressMaximum = 80)
    {
        TerrariaServerTarget? serverTarget = TerrariaServerLocator.TryResolveTarget();
        if (serverTarget is null)
        {
            return TerrariaRaceWorldGenerationResult.Failure("TerrariaServer.exe was not found.");
        }

        AutoCreateWorldSettings generationSettings = CloneRaceSettings(settings);
        HeadlessWorldGenResult result = await generator.GenerateAndScanAsync(
            serverTarget.Value,
            appLanguage,
            generationSettings,
            seedText,
            worldName,
            cancellationToken,
            CreateRaceProgressMapper(progress, progressMaximum));
        try
        {
            if (!result.Generated)
            {
                return TerrariaRaceWorldGenerationResult.Failure("World generation was skipped because another generator is running.");
            }

            if (!result.Keep || string.IsNullOrWhiteSpace(result.WorldPath) || !File.Exists(result.WorldPath))
            {
                return TerrariaRaceWorldGenerationResult.Failure("TerrariaServer.exe did not produce a matching world file.");
            }

            string installedPath = InstallWorld(result.WorldPath, worldName);
            return TerrariaRaceWorldGenerationResult.Success(installedPath);
        }
        finally
        {
            generator.ClearScratch();
        }
    }

    public void Dispose()
    {
        generator.Dispose();
    }

    private static AutoCreateWorldSettings CloneRaceSettings(AutoCreateWorldSettings settings)
    {
        return new AutoCreateWorldSettings
        {
            WorldSize = settings.WorldSize,
            WorldDifficulty = settings.WorldDifficulty,
            WorldEvil = settings.WorldEvil,
            SpecialSeeds = settings.SpecialSeeds,
            SecretSeeds = settings.SecretSeeds,
            EnablePyramidFilter = settings.EnablePyramidFilter,
            PyramidFilterItemMask = settings.PyramidFilterItemMask,
            RequireCrimsonBetweenDungeonAndSpawn = settings.RequireCrimsonBetweenDungeonAndSpawn,
            PreserveExistingSaves = true
        };
    }

    private static IProgress<int>? CreateRaceProgressMapper(IProgress<int>? progress, int progressMaximum)
    {
        int maximum = Math.Clamp(progressMaximum, 0, 100);
        return progress is null
            ? null
            : new Progress<int>(percent =>
            {
                int clamped = Math.Clamp(percent, 0, 100);
                progress.Report(Math.Clamp((int)Math.Round(clamped * maximum / 100d), 0, maximum));
            });
    }

    private static string InstallWorld(string sourcePath, string worldName)
    {
        string worldsDirectory = Path.Combine(TerrariaSavePaths.SaveRoot(), "Worlds");
        Directory.CreateDirectory(worldsDirectory);
        string stem = SanitizeFileStem(string.IsNullOrWhiteSpace(worldName)
            ? "TerrariaRace"
            : worldName);
        string targetPath = GetUniquePath(worldsDirectory, stem);
        File.Copy(sourcePath, targetPath, overwrite: false);
        CopyBackupIfPresent(sourcePath, targetPath);
        return targetPath;
    }

    private static string GetUniquePath(string directory, string stem)
    {
        string candidate = Path.Combine(directory, stem + ".wld");
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        for (int i = 1; i < 10_000; i++)
        {
            candidate = Path.Combine(directory, $"{stem}-{i}.wld");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{stem}-{DateTime.UtcNow:yyyyMMddHHmmssfff}.wld");
    }

    private static string SanitizeFileStem(string value)
    {
        string stem = new(value
            .Trim()
            .Select(static ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)
            .ToArray());
        return string.IsNullOrWhiteSpace(stem) ? "TerrariaRace" : stem;
    }

    private static void CopyBackupIfPresent(string sourcePath, string targetPath)
    {
        string backup = sourcePath + ".bak";
        if (File.Exists(backup))
        {
            File.Copy(backup, targetPath + ".bak", overwrite: false);
        }
    }
}
