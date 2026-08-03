using TerrariaSplit.Race.Client;
using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.UI;

internal sealed class RaceHostWorldActionCoordinator
{
    private readonly IRacePanelShell shell;

    public RaceHostWorldActionCoordinator(IRacePanelShell shell)
    {
        this.shell = shell;
    }

    public async Task<RaceHostWorldActionResult> ExecuteAsync(
        RaceHostWorldActionRequest request,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        if (request.WorldSource != RacePanelWorldSource.ExistingFile &&
            !RaceWorldSettingsFactory.HasCompatibleJourneyDifficulties(request.WorldSettings))
        {
            return RaceHostWorldActionResult.Failure(
                RaceHostWorldActionFailureKind.InvalidSettings,
                "invalid_world_settings",
                "Journey player difficulty and Journey world difficulty must be selected together.");
        }

        bool generated = request.WorldSource != RacePanelWorldSource.ExistingFile;
        string worldPath = generated ? string.Empty : request.WorldPath;
        try
        {
            if (request.WorldSource == RacePanelWorldSource.Random)
            {
                RacePanelWorldGenerationResult generation = await shell.GenerateRandomWorldAsync(
                    request.WorldSettings,
                    MapProgress(progress, 0, 90));
                cancellationToken.ThrowIfCancellationRequested();
                if (!generation.Succeeded)
                {
                    return RaceHostWorldActionResult.Failure(
                        RaceHostWorldActionFailureKind.Generation,
                        "world_generation_failed",
                        generation.Message);
                }

                worldPath = shell.LocalWorldPath ?? string.Empty;
            }
            else if (request.WorldSource == RacePanelWorldSource.CustomSeed)
            {
                RacePanelWorldGenerationResult generation = await shell.GenerateCustomSeedWorldAsync(
                    request.WorldSettings,
                    request.SeedText,
                    MapProgress(progress, 0, 90));
                cancellationToken.ThrowIfCancellationRequested();
                if (!generation.Succeeded)
                {
                    return RaceHostWorldActionResult.Failure(
                        RaceHostWorldActionFailureKind.Generation,
                        "world_generation_failed",
                        generation.Message);
                }

                worldPath = shell.LocalWorldPath ?? string.Empty;
            }

            if (!RaceWorldFileValidator.IsValidWorldFilePath(worldPath))
            {
                await DiscardGeneratedWorldAsync(generated, worldPath);
                return RaceHostWorldActionResult.Failure(
                    generated
                        ? RaceHostWorldActionFailureKind.Generation
                        : RaceHostWorldActionFailureKind.Upload,
                    generated ? "world_generation_missing_file" : "world_upload_required",
                    generated
                        ? "World generation completed without a world file."
                        : "A valid world file is required.");
            }

            RaceOperationResult<RaceRoomState> upload = await shell.UploadWorldAsync(
                request.ServerUrl,
                request.Nickname,
                worldPath,
                request.WorldSettings,
                request.SeedText,
                generated ? MapProgress(progress, 90, 100) : MapProgress(progress, 0, 100),
                cancellationToken);
            if (upload.Succeeded)
            {
                progress?.Report(100);
                return RaceHostWorldActionResult.Success(worldPath);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await DiscardGeneratedWorldAsync(generated, worldPath);
            return RaceHostWorldActionResult.Failure(
                RaceHostWorldActionFailureKind.Upload,
                upload.ErrorCode,
                upload.Message);
        }
        catch (OperationCanceledException)
        {
            await DiscardGeneratedWorldAsync(generated, ResolveGeneratedWorldPath(worldPath));
            throw;
        }
        catch
        {
            await DiscardGeneratedWorldAsync(generated, ResolveGeneratedWorldPath(worldPath));
            throw;
        }
    }

    public Task CancelAsync()
    {
        return shell.CancelWorldGenerationAsync();
    }

    private string ResolveGeneratedWorldPath(string worldPath)
    {
        return !string.IsNullOrWhiteSpace(worldPath)
            ? worldPath
            : shell.LocalWorldPath ?? string.Empty;
    }

    private async Task DiscardGeneratedWorldAsync(bool generated, string worldPath)
    {
        if (generated && !string.IsNullOrWhiteSpace(worldPath))
        {
            await shell.DiscardLocalWorldAsync(worldPath);
        }
    }

    private static IProgress<int>? MapProgress(IProgress<int>? progress, int minimum, int maximum)
    {
        if (progress is null)
        {
            return null;
        }

        return new Progress<int>(value =>
        {
            int clamped = Math.Clamp(value, 0, 100);
            int mapped = minimum + (int)Math.Round(
                clamped * (maximum - minimum) / 100d,
                MidpointRounding.AwayFromZero);
            progress.Report(Math.Clamp(mapped, minimum, maximum));
        });
    }
}

internal readonly record struct RaceHostWorldActionRequest(
    RacePanelWorldSource WorldSource,
    RaceWorldSettings WorldSettings,
    string ServerUrl,
    string Nickname,
    string SeedText,
    string WorldPath);

internal readonly record struct RaceHostWorldActionResult(
    bool Succeeded,
    RaceHostWorldActionFailureKind FailureKind,
    string WorldPath,
    string ErrorCode,
    string Message)
{
    public static RaceHostWorldActionResult Success(string worldPath)
    {
        return new RaceHostWorldActionResult(
            true,
            RaceHostWorldActionFailureKind.None,
            worldPath,
            string.Empty,
            string.Empty);
    }

    public static RaceHostWorldActionResult Failure(
        RaceHostWorldActionFailureKind failureKind,
        string errorCode,
        string message)
    {
        return new RaceHostWorldActionResult(
            false,
            failureKind,
            string.Empty,
            errorCode,
            message);
    }
}

internal enum RaceHostWorldActionFailureKind
{
    None,
    InvalidSettings,
    Generation,
    Upload
}
