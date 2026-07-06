using System.Collections.Concurrent;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using TerrariaSplit.Race.Client;
using TerrariaSplit.Race.Contracts;
using TerrariaSplit.Terraria.Automation;

namespace TerrariaSplit.UI;

internal sealed class RaceShell : IRacePanelShell, IDisposable
{
    private static readonly TimeSpan DisposeRaceSessionTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan CloseWindowTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RemoteRoomExitTimeout = TimeSpan.FromSeconds(2);
    private const int RaceRandomWorldMaxAttempts = 250_000;
    private const int RaceVerifiedGenerationProgressMaximum = 80;
    private const int RaceDirectGenerationProgressMaximum = 90;
    private readonly RaceClientSession session = new();
    private readonly RaceRouteOverrideController routeOverride;
    private readonly RaceLocalPyramidSeedGenerator seedGenerator = new();
    private readonly TerrariaRaceWorldGenerationService worldGeneration = new();
    private readonly ISettingsSnapshotFactory settingsSnapshots;
    private readonly IAppLogger logger;
    private readonly Func<AppSettings> getSettings;
    private readonly Func<AppSettings> getBaseSettings;
    private readonly Func<ApplicationViewState> getViewState;
    private readonly Func<string?> getTerrariaVersion;
    private readonly Action<SettingsRouteOverridePackage> applyRouteOverride;
    private readonly Action clearRouteOverride;
    private readonly Func<AppSettings, OperationResult> saveSettings;
    private readonly Action raceTimerColorChanged;
    private readonly Action resetRaceTimer;
    private readonly Form owner;
    private const string RaceStartProgressKey = "start";
    private readonly ConcurrentQueue<RaceProgressUpload> queuedProgressUploads = new();
    private readonly object progressViewUpdateLock = new();
    private RaceForm? form;
    private RaceLeaderboardForm? leaderboardForm;
    private CancellationTokenSource? worldGenerationCancellation;
    private CancellationTokenSource? memberWorldDownloadCancellation;
    private string? localWorldPath;
    private string? activeWorldRoomCode;
    private string? activeWorldFileName;
    private string? activeWorldRevisionKey;
    private string? pendingWorldFileKey;
    private RacePanelDraftState draftState;
    private RacePanelPersistentPreferences lastPersistedPreferences;
    private readonly HashSet<string> reportedProgressKeys = new(StringComparer.OrdinalIgnoreCase);
    private RaceProgressChanged? pendingProgressViewUpdate;
    private int progressReportDrainActive;
    private int progressViewUpdatePending;
    private bool mouseClickThrough;
    private bool closingWindows;
    private bool disposed;

    public RaceShell(
        ISettingsSnapshotFactory settingsSnapshots,
        IAppLogger logger,
        Func<AppSettings> getSettings,
        Func<AppSettings> getBaseSettings,
        Func<ApplicationViewState> getViewState,
        Func<string?> getTerrariaVersion,
        Action<SettingsRouteOverridePackage> applyRouteOverride,
        Action clearRouteOverride,
        Func<AppSettings, OperationResult> saveSettings,
        Form owner,
        Action raceTimerColorChanged,
        Action resetRaceTimer)
    {
        routeOverride = new RaceRouteOverrideController(settingsSnapshots);
        this.settingsSnapshots = settingsSnapshots;
        this.logger = logger;
        this.getSettings = getSettings;
        this.getBaseSettings = getBaseSettings;
        this.getViewState = getViewState;
        this.getTerrariaVersion = getTerrariaVersion;
        this.applyRouteOverride = applyRouteOverride;
        this.clearRouteOverride = clearRouteOverride;
        this.saveSettings = saveSettings;
        this.owner = owner;
        this.raceTimerColorChanged = raceTimerColorChanged;
        this.resetRaceTimer = resetRaceTimer;
        draftState = RacePanelDraftState.FromSettings(getSettings());
        lastPersistedPreferences = RacePanelPersistentPreferences.FromDraft(draftState);
        session.PackageChanged += HandlePackageChanged;
        session.ProgressChanged += HandleProgressChanged;
        session.RosterChanged += HandleRosterChanged;
    }

    public RaceRoomState? State => session.State;

    public bool IsHostInCurrentRoom => session.State is RaceRoomState state && IsCurrentUserHost(state);

    public bool IsPyramidFilterActive =>
        session.State is RaceRoomState { Status: not RaceRoomStatus.Closed, WorldSettings: RaceWorldSettings worldSettings } &&
        worldSettings.RequiredPyramidItemMask != 0;

    public string? LocalWorldPath => localWorldPath;

    public RacePanelDraftState DraftState => CreateCurrentDraftState();

    public bool IsInRoom => session.IsInRoom;

    public RaceLeaderboardSettings LeaderboardSettings =>
        CloneLeaderboardSettings(getSettings().Race?.Leaderboard ?? new RaceLeaderboardSettings());

    public string Localize(string key)
    {
        return Localizer.Get(key, getSettings());
    }

    public void SaveDraftState(RacePanelDraftState nextDraftState)
    {
        draftState = nextDraftState.Normalize();
        PersistRacePreferences(draftState);
    }

    public void SaveLeaderboardSettings(RaceLeaderboardSettings leaderboardSettings)
    {
        RaceLeaderboardSettings nextLeaderboard = CloneLeaderboardSettings(leaderboardSettings);
        try
        {
            AppSettings nextSettings = settingsSnapshots.CreateSnapshot(getBaseSettings());
            nextSettings.Race ??= new RaceSettings();
            nextSettings.Race.Leaderboard = nextLeaderboard;

            OperationResult result = saveSettings(nextSettings);
            if (result.Succeeded)
            {
                getBaseSettings().Race ??= new RaceSettings();
                getBaseSettings().Race.Leaderboard = CloneLeaderboardSettings(nextLeaderboard);
                getSettings().Race ??= new RaceSettings();
                getSettings().Race.Leaderboard = CloneLeaderboardSettings(nextLeaderboard);
                leaderboardForm?.ApplySettings();
                raceTimerColorChanged();
                return;
            }

            logger.Info("Race leaderboard settings save failed: " + result.Message);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Race leaderboard settings save failed.");
        }
    }

    public void RefreshWindowSettings()
    {
        SyncLeaderboardVisibility();
        ApplyLeaderboardTopMost();
        leaderboardForm?.ApplyMouseClickThrough(mouseClickThrough);
        leaderboardForm?.ApplySettings();
        leaderboardForm?.UpdateState(session.State);
    }

    public void ApplyMouseClickThrough(bool enabled)
    {
        mouseClickThrough = enabled;
        leaderboardForm?.ApplyMouseClickThrough(enabled);
    }

    public void CloseWindows()
    {
        if (closingWindows)
        {
            return;
        }

        closingWindows = true;
        try
        {
            CloseFormIfOpen(form);
            form = null;
            CloseFormIfOpen(leaderboardForm);
            leaderboardForm = null;
        }
        finally
        {
            closingWindows = false;
        }
    }

    public void OpenPanel()
    {
        if (form is { IsDisposed: false })
        {
            form.Show();
            if (form.IsHandleCreated)
            {
                WindowTopMostSync.Apply(false, form.Handle);
            }

            form.Activate();
            return;
        }

        form = new RaceForm(this);
        form.FormClosed += (_, _) => form = null;
        form.Show();
        if (form.IsHandleCreated)
        {
            WindowTopMostSync.Apply(false, form.Handle);
        }

        SyncLeaderboardVisibility();
    }

    public Task CreateRoomAsync(string serverUrl, string nickname)
    {
        SaveDraftState(draftState with
        {
            ServerUrl = serverUrl,
            Nickname = nickname,
            Role = RacePanelRole.Host
        });

        logger.Info("Race room creation is deferred until a world is uploaded.");
        return Task.CompletedTask;
    }

    public async Task<RaceOperationResult<RaceRoomState>> JoinRoomAsync(string serverUrl, string roomCode, string nickname)
    {
        SaveDraftState(draftState with
        {
            ServerUrl = serverUrl,
            Nickname = nickname,
            RoomCode = roomCode,
            Role = RacePanelRole.Member
        });

        try
        {
            RaceOperationResult<RaceRoomState> result = await session.JoinRoomAsync(
                serverUrl,
                new RaceRoomJoinRequest(roomCode, nickname));
            ClearReportedProgress();

            LogRaceOperationFailure(result, "join room");
            return result;
        }
        catch (Exception ex) when (IsRaceConnectionExitException(ex))
        {
            RaceOperationResult<RaceRoomState> result = RaceOperationResult<RaceRoomState>.Failure(
                "connection_failed",
                ex.Message);
            LogRaceOperationFailure(result, "join room");
            return result;
        }
    }

    public async Task CloseRoomAsync()
    {
        try
        {
            using var cancellation = new CancellationTokenSource(RemoteRoomExitTimeout);
            RaceOperationResult<RaceRoomState> result = await session.CloseRoomAsync(cancellation.Token);
            if (result.Succeeded)
            {
                await LeaveLocalRoomStateAsync();
                return;
            }

            LogRaceOperationFailure(result, "close room");
        }
        catch (Exception ex) when (IsRaceConnectionExitException(ex))
        {
            logger.Info("Race close room failed; leaving local room state. " + ex.Message);
        }

        await LeaveLocalRoomStateAsync();
    }

    public Task CopyRoomInfoAsync()
    {
        string roomCode = session.RoomCode ?? draftState.RoomCode;
        if (!string.IsNullOrWhiteSpace(roomCode))
        {
            TryCopyRoomInfo(draftState.ServerUrl, roomCode);
        }

        return Task.CompletedTask;
    }

    public async Task KickPlayerAsync(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
        {
            return;
        }

        RaceOperationResult<RaceRoomState> result = await session.KickPlayerAsync(nickname);
        ApplyOperationState(result);
        LogRaceOperationFailure(result, "kick player");
    }

    public async Task GenerateRandomWorldAsync(RaceWorldSettings worldSettings, IProgress<int>? progress = null)
    {
        SaveDraftState(draftState with { Role = RacePanelRole.Host });
        localWorldPath = null;

        if (worldSettings.RequiredPyramidItemMask == 0)
        {
            string seedText = CreateRandomSeedText();
            await GenerateWorldFromSeedAsync(
                worldSettings,
                new RaceSeedAssignment(seedText, RaceSeedSource.HostGenerated),
                progress,
                RaceDirectGenerationProgressMaximum);
            return;
        }

        await GeneratePrescreenedWorldUntilVerifiedAsync(worldSettings, RaceRandomWorldMaxAttempts, progress);
    }

    public async Task GenerateCustomSeedWorldAsync(
        RaceWorldSettings worldSettings,
        string seedText,
        IProgress<int>? progress = null)
    {
        localWorldPath = null;
        if (string.IsNullOrWhiteSpace(seedText))
        {
            logger.Info("Race custom seed world generation ignored because seed is empty.");
            return;
        }

        SaveDraftState(draftState with { SeedText = seedText.Trim() });

        await GenerateWorldFromSeedAsync(
            worldSettings,
            new RaceSeedAssignment(seedText.Trim(), RaceSeedSource.Fixed),
            progress,
            RaceDirectGenerationProgressMaximum);
    }

    public async Task<RaceOperationResult<RaceRoomState>> UploadWorldAsync(
        string serverUrl,
        string nickname,
        string worldPath,
        RaceWorldSettings worldSettings,
        string seedText,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string normalizedWorldPath = worldPath.Trim();
        if (!RaceWorldFileValidator.IsValidWorldFilePath(normalizedWorldPath))
        {
            progress?.Report(0);
            return RaceOperationResult<RaceRoomState>.Failure(
                "world_upload_required",
                "A valid world file is required.");
        }

        SaveDraftState(draftState with
        {
            ServerUrl = serverUrl,
            Nickname = nickname,
            Role = RacePanelRole.Host,
            LocalWorldPath = normalizedWorldPath,
            SeedText = string.IsNullOrWhiteSpace(seedText) ? draftState.SeedText : seedText
        });

        string effectiveSeedText = string.IsNullOrWhiteSpace(seedText) ? draftState.SeedText : seedText.Trim();
        RaceSeedAssignment? seed = string.IsNullOrWhiteSpace(effectiveSeedText)
            ? session.State?.Seed
            : new RaceSeedAssignment(effectiveSeedText.Trim(), RaceSeedSource.Fixed);
        RaceRoutePayload route = RaceRoutePayloadFactory.Create(getBaseSettings());
        RaceOperationResult<RaceRoomState> result;
        string uploadWorldPath = normalizedWorldPath;
        bool createdRoomDuringUpload = false;
        bool hasOpenRoom = session.IsInRoom && session.State?.Status != RaceRoomStatus.Closed;
        try
        {
            if (!hasOpenRoom)
            {
                result = await session.CreateRoomAsync(
                    serverUrl,
                    new RaceRoomCreateRequest(nickname),
                    cancellationToken);
                if (!result.Succeeded || result.Value is not RaceRoomState)
                {
                    ApplyOperationState(result);
                    LogRaceOperationFailure(result, "create room");
                    return result;
                }

                createdRoomDuringUpload = true;
                uploadWorldPath = PrepareRaceWorldFileForUpload(
                    normalizedWorldPath,
                    DateTimeOffset.Now);
                RaceWorldSettings uploadWorldSettings = worldSettings with
                {
                    WorldName = Path.GetFileNameWithoutExtension(uploadWorldPath)
                };
                result = await session.UploadWorldFileAsync(
                    uploadWorldPath,
                    route,
                    uploadWorldSettings,
                    seed,
                    progress,
                    cancellationToken);
                if (!result.Succeeded)
                {
                    await CloseCreatedRoomAfterFailedUploadAsync();
                }
            }
            else
            {
                uploadWorldPath = PrepareRaceWorldFileForUpload(
                    normalizedWorldPath,
                    DateTimeOffset.Now);
                RaceWorldSettings uploadWorldSettings = worldSettings with
                {
                    WorldName = Path.GetFileNameWithoutExtension(uploadWorldPath)
                };
                result = await session.UploadWorldFileAsync(
                    uploadWorldPath,
                    route,
                    uploadWorldSettings,
                    seed,
                    progress,
                    cancellationToken);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or OperationCanceledException or TimeoutException)
        {
            if (createdRoomDuringUpload)
            {
                await CloseCreatedRoomAfterFailedUploadAsync();
            }

            logger.Error(ex, "Race world upload failed.");
            result = RaceOperationResult<RaceRoomState>.Failure(
                "world_upload_failed",
                string.IsNullOrWhiteSpace(ex.Message) ? "Upload failed." : ex.Message);
        }

        if (result.Succeeded)
        {
            ClearReportedProgress();
            localWorldPath = uploadWorldPath;
            SaveDraftState(draftState with
            {
                RoomCode = result.Value?.RoomCode ?? draftState.RoomCode,
                LocalWorldPath = uploadWorldPath,
                SeedText = effectiveSeedText
            });
            if (result.Value is RaceRoomState state)
            {
                RememberObtainedWorldFile(state.RoomCode, state.WorldFile, resetTimer: true);
                TryCopyRoomInfo(draftState.ServerUrl, state.RoomCode);
            }
        }
        else if (!string.Equals(uploadWorldPath, normalizedWorldPath, StringComparison.OrdinalIgnoreCase))
        {
            DeleteRaceWorldFile(uploadWorldPath);
        }

        ApplyOperationState(result);
        LogRaceOperationFailure(result, "upload world");
        return result;
    }

    private async Task CloseCreatedRoomAfterFailedUploadAsync()
    {
        try
        {
            _ = await session.CloseRoomAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or OperationCanceledException or TimeoutException)
        {
            logger.Error(ex, "Race room cleanup after upload failure failed.");
        }
    }

    public Task CancelWorldGenerationAsync()
    {
        worldGenerationCancellation?.Cancel();
        return Task.CompletedTask;
    }

    public Task DiscardLocalWorldAsync(string worldPath)
    {
        if (string.IsNullOrWhiteSpace(worldPath))
        {
            return Task.CompletedTask;
        }

        DeleteRaceWorldFile(worldPath);
        if (string.Equals(localWorldPath, worldPath, StringComparison.OrdinalIgnoreCase))
        {
            localWorldPath = null;
            activeWorldRoomCode = null;
            activeWorldFileName = null;
            activeWorldRevisionKey = null;
        }

        SaveDraftState(draftState with { LocalWorldPath = string.Empty });
        return Task.CompletedTask;
    }

    private async Task DownloadWorldForStateAsync(
        RaceRoomState state,
        bool force,
        CancellationToken cancellationToken)
    {
        RaceWorldFileInfo? worldFile = state.WorldFile;
        if (worldFile is null)
        {
            logger.Info("Race room has no uploaded world yet.");
            return;
        }

        string serverFileName = NormalizeWorldFileName(worldFile.FileName);
        if (string.IsNullOrWhiteSpace(serverFileName))
        {
            logger.Info("Race room world file has an empty filename.");
            return;
        }

        if (!force && HasCurrentLocalWorld(state.RoomCode, worldFile))
        {
            MarkWorldReadyForAlreadyInstalledWorldIfNeeded(state);
            return;
        }

        string destinationPath = GetUniqueRaceWorldPath(state);
        RaceWorldFileTransferResult download;
        try
        {
            download = await session.DownloadWorldFileAsync(destinationPath, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!download.Succeeded)
        {
            _ = await session.MarkWorldReadyAsync(ready: false, download.Message);
            logger.Info("Race world download failed: " + download.Message);
            return;
        }

        localWorldPath = download.WorldPath;
        SaveDraftState(draftState with { LocalWorldPath = download.WorldPath });
        RememberObtainedWorldFile(state.RoomCode, download.WorldFile, resetTimer: true);
        RaceOperationResult<RaceRoomState> ready = await session.MarkWorldReadyAsync(ready: true);
        ApplyOperationState(ready);
        LogRaceOperationFailure(ready, "mark world ready");
    }

    private async Task GenerateWorldFromSeedAsync(
        RaceWorldSettings worldSettings,
        RaceSeedAssignment seed,
        IProgress<int>? progress,
        int progressMaximum)
    {
        progress?.Report(0);
        RaceLocalWorldGenerationAttempt attempt = await TryGenerateWorldFromSeedAsync(
            worldSettings,
            seed,
            progress,
            progressMaximum);
        if (!attempt.Succeeded)
        {
            logger.Info("Race world generation failed: " + attempt.Message);
            return;
        }

        progress?.Report(90);
    }

    private async Task GeneratePrescreenedWorldUntilVerifiedAsync(
        RaceWorldSettings worldSettings,
        int maxAttempts,
        IProgress<int>? progress)
    {
        int attempts = Math.Clamp(maxAttempts <= 0 ? RaceRandomWorldMaxAttempts : maxAttempts, 1, 5_000_000);
        int verifiedAttempts = 0;
        string lastFailure = string.Empty;
        for (int prescreenAttempts = 1; prescreenAttempts <= attempts; prescreenAttempts++)
        {
            RaceLocalPyramidSeedAttempt seedAttempt = seedGenerator.TryNext(worldSettings);
            if (seedAttempt.Status == RaceLocalPyramidSeedAttemptStatus.Miss)
            {
                continue;
            }

            if (seedAttempt.Status == RaceLocalPyramidSeedAttemptStatus.Fatal ||
                seedAttempt.Seed is not RaceSeedAssignment seed)
            {
                logger.Info("Race pyramid pre-screen is unavailable; falling back to direct world generation and world-file verification. Detail=" + seedAttempt.Message);
                await GenerateRandomWorldUntilVerifiedAsync(worldSettings, attempts, progress);
                return;
            }

            verifiedAttempts++;
            progress?.Report(0);
            RaceLocalWorldGenerationAttempt worldAttempt = await TryGenerateWorldFromSeedAsync(
                worldSettings,
                seed,
                progress,
                RaceVerifiedGenerationProgressMaximum);
            if (worldAttempt.Succeeded)
            {
                logger.Info($"Race world generated after {verifiedAttempts} verified pre-screen candidates: {worldAttempt.WorldPath}");
                progress?.Report(90);
                return;
            }

            if (!worldAttempt.Retryable)
            {
                logger.Info("Race world generation failed: " + worldAttempt.Message);
                return;
            }

            lastFailure = worldAttempt.Message;
        }

        logger.Info(string.IsNullOrWhiteSpace(lastFailure)
            ? $"Race found no verified world after {attempts} pre-screen attempts."
            : $"Race found no verified world after {attempts} pre-screen attempts. Last verification failure: {lastFailure}");
    }

    private async Task GenerateRandomWorldUntilVerifiedAsync(
        RaceWorldSettings worldSettings,
        int maxAttempts,
        IProgress<int>? progress)
    {
        int attempts = Math.Clamp(maxAttempts <= 0 ? RaceRandomWorldMaxAttempts : maxAttempts, 1, 5_000_000);
        string lastFailure = string.Empty;
        for (int generationAttempt = 1; generationAttempt <= attempts; generationAttempt++)
        {
            RaceSeedAssignment seed = new(CreateRandomSeedText(), RaceSeedSource.HostGenerated);
            progress?.Report(0);
            RaceLocalWorldGenerationAttempt worldAttempt = await TryGenerateWorldFromSeedAsync(
                worldSettings,
                seed,
                progress,
                RaceVerifiedGenerationProgressMaximum);
            if (worldAttempt.Succeeded)
            {
                logger.Info($"Race world generated after {generationAttempt} generated-world verification attempts: {worldAttempt.WorldPath}");
                progress?.Report(90);
                return;
            }

            if (!worldAttempt.Retryable)
            {
                logger.Info("Race world generation failed: " + worldAttempt.Message);
                return;
            }

            lastFailure = worldAttempt.Message;
        }

        logger.Info(string.IsNullOrWhiteSpace(lastFailure)
            ? $"Race found no verified world after {attempts} generated worlds."
            : $"Race found no verified world after {attempts} generated worlds. Last verification failure: {lastFailure}");
    }

    private static string CreateRandomSeedText()
    {
        return Random.Shared.Next(0, int.MaxValue).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<RaceLocalWorldGenerationAttempt> TryGenerateWorldFromSeedAsync(
        RaceWorldSettings worldSettings,
        RaceSeedAssignment seed,
        IProgress<int>? progress,
        int progressMaximum)
    {
        worldGenerationCancellation?.Cancel();
        worldGenerationCancellation?.Dispose();
        worldGenerationCancellation = new CancellationTokenSource();
        string worldName = CreateRaceWorldStem(DateTimeOffset.Now);
        try
        {
            TerrariaRaceWorldGenerationResult result = await worldGeneration.GenerateAndInstallAsync(
                RaceWorldSettingsFactory.ToAutoCreateWorldSettings(worldSettings),
                seed.SeedText,
                worldName,
                getSettings().General.Language,
                worldGenerationCancellation.Token,
                progress,
                progressMaximum);
            localWorldPath = result.Succeeded ? result.WorldPath : null;
            if (result.Succeeded)
            {
                SaveDraftState(draftState with
                {
                    SeedText = seed.SeedText,
                    LocalWorldPath = result.WorldPath
                });
            }

            return result.Succeeded
                ? RaceLocalWorldGenerationAttempt.Success(result.WorldPath)
                : RaceLocalWorldGenerationAttempt.Failure(result.Message, IsRetryableWorldGenerationFailure(result.Message));
        }
        catch (OperationCanceledException)
        {
            return RaceLocalWorldGenerationAttempt.Failure(Localize("World generation cancelled."), retryable: false);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Race world generation failed.");
            return RaceLocalWorldGenerationAttempt.Failure(ex.Message, retryable: false);
        }
    }

    private static bool IsRetryableWorldGenerationFailure(string message)
    {
        return string.Equals(
            message,
            "TerrariaServer.exe did not produce a matching world file.",
            StringComparison.Ordinal);
    }

    private void DeleteRaceWorldFile(string worldPath)
    {
        foreach (string path in EnumerateRaceWorldFiles(worldPath))
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

    private static IEnumerable<string> EnumerateRaceWorldFiles(string worldPath)
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

    public async Task LeaveAsync()
    {
        worldGenerationCancellation?.Cancel();
        CancelMemberWorldDownload();
        try
        {
            using var cancellation = new CancellationTokenSource(RemoteRoomExitTimeout);
            await session.LeaveAsync(cancellation.Token);
        }
        catch (Exception ex) when (IsRaceConnectionExitException(ex))
        {
            logger.Info("Race leave room failed; leaving local room state. " + ex.Message);
            await session.LeaveLocalAsync(DisposeRaceSessionTimeout);
        }

        ClearLocalRoomStateAfterLeave();
    }

    private async Task LeaveLocalRoomStateAsync()
    {
        worldGenerationCancellation?.Cancel();
        CancelMemberWorldDownload();
        await session.LeaveLocalAsync(DisposeRaceSessionTimeout);
        ClearLocalRoomStateAfterLeave();
    }

    private void ClearLocalRoomStateAfterLeave()
    {
        RestoreRouteOverride();
        localWorldPath = null;
        activeWorldFileName = null;
        activeWorldRoomCode = null;
        activeWorldRevisionKey = null;
        ClearReportedProgress();
        SaveDraftState(draftState with
        {
            RoomCode = string.Empty,
            SeedText = string.Empty,
            LocalWorldPath = string.Empty
        });
        form?.UpdateRaceState(null);
        CloseLeaderboardForm();
        raceTimerColorChanged();
    }

    private static bool IsRaceConnectionExitException(Exception exception)
    {
        return exception is InvalidOperationException or HttpRequestException or OperationCanceledException or TimeoutException or ObjectDisposedException;
    }

    public void ClearReportedProgress()
    {
        reportedProgressKeys.Clear();
        while (queuedProgressUploads.TryDequeue(out _))
        {
        }
    }

    public void ResetReportedProgress()
    {
        ClearReportedProgress();
        if (!session.IsInRoom)
        {
            return;
        }

        _ = Task.Run(ResetReportedProgressAsync);
    }

    private async Task ResetReportedProgressAsync()
    {
        try
        {
            RaceOperationResult<RaceRoomProgressState> result = await session.ResetProgressAsync().ConfigureAwait(false);
            if (result.Succeeded)
            {
                logger.Info("Race progress reset accepted.");
                return;
            }

            logger.Info($"Race progress reset rejected. Error={result.ErrorCode} Message={result.Message}.");
        }
        catch (Exception ex) when (IsRaceConnectionExitException(ex))
        {
            logger.Info("Race progress reset failed: " + ex.Message);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Race progress reset failed.");
        }
    }

    public void QueueProgressReports(bool runStarted, bool runCompleted)
    {
        _ = runCompleted;
        if (!session.IsInRoom || session.RoomCode is null || session.Nickname is null)
        {
            return;
        }

        if (runStarted && reportedProgressKeys.Add(RaceStartProgressKey))
        {
            queuedProgressUploads.Enqueue(RaceProgressUpload.ForStart(new RaceRunStartReport(
                session.RoomCode,
                session.Nickname,
                DateTimeOffset.UtcNow)));
        }

        ApplicationViewState viewState = getViewState();
        foreach (RaceSplitReport report in RaceSplitReportFactory.CreateProgressReports(
                     session.RoomCode,
                     session.Nickname,
                     viewState.DisplayStatuses))
        {
            string progressKey = RaceSplitReportFactory.CreateProgressKey(report);
            if (!reportedProgressKeys.Add(progressKey))
            {
                continue;
            }

            queuedProgressUploads.Enqueue(RaceProgressUpload.ForSplit(report));
        }

        StartProgressReportDrain();
    }

    private void StartProgressReportDrain()
    {
        if (Interlocked.Exchange(ref progressReportDrainActive, 1) != 0)
        {
            return;
        }

        _ = Task.Run(DrainProgressReportsAsync);
    }

    private async Task DrainProgressReportsAsync()
    {
        try
        {
            while (queuedProgressUploads.TryDequeue(out RaceProgressUpload? upload))
            {
                if (upload is null)
                {
                    continue;
                }

                switch (upload)
                {
                    case RaceProgressUpload.Start start:
                        await SendStartReportAsync(start.Report).ConfigureAwait(false);
                        break;
                    case RaceProgressUpload.Split split:
                        await SendProgressReportAsync(split.Report).ConfigureAwait(false);
                        break;
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref progressReportDrainActive, 0);
            if (!queuedProgressUploads.IsEmpty)
            {
                StartProgressReportDrain();
            }
        }
    }

    private async Task SendProgressReportAsync(RaceSplitReport report)
    {
        try
        {
            RaceOperationResult<RaceRoomProgressState> result = await session.ReportSplitAsync(report).ConfigureAwait(false);
            if (result.Succeeded)
            {
                logger.Info(
                    $"Race split report accepted. Room={report.RoomCode} SplitIndex={report.SplitIndex} ConditionIndex={report.ConditionIndex} SplitId={report.SplitId} ElapsedMs={report.ElapsedMilliseconds}.");
                return;
            }

            logger.Info(
                $"Race split report rejected. Room={report.RoomCode} SplitIndex={report.SplitIndex} ConditionIndex={report.ConditionIndex} SplitId={report.SplitId} Error={result.ErrorCode} Message={result.Message}.");
        }
        catch (Exception ex) when (IsRaceConnectionExitException(ex))
        {
            logger.Info("Race split report failed: " + ex.Message);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Race split report failed.");
        }
    }

    private async Task SendStartReportAsync(RaceRunStartReport report)
    {
        try
        {
            RaceOperationResult<RaceRoomProgressState> result = await session.ReportStartAsync(report).ConfigureAwait(false);
            if (result.Succeeded)
            {
                logger.Info($"Race start report accepted. Room={report.RoomCode}.");
                return;
            }

            logger.Info($"Race start report rejected. Room={report.RoomCode} Error={result.ErrorCode} Message={result.Message}.");
        }
        catch (Exception ex) when (IsRaceConnectionExitException(ex))
        {
            logger.Info("Race start report failed: " + ex.Message);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Race start report failed.");
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        CloseWindows();
        worldGenerationCancellation?.Cancel();
        worldGenerationCancellation?.Dispose();
        CancelMemberWorldDownload();
        worldGeneration.Dispose();
        session.PackageChanged -= HandlePackageChanged;
        session.ProgressChanged -= HandleProgressChanged;
        session.RosterChanged -= HandleRosterChanged;
        DisposeRaceSessionBestEffort();
    }

    private void DisposeRaceSessionBestEffort()
    {
        Task disposeTask = Task.Run(async () => await session.DisposeAsync().ConfigureAwait(false));
        try
        {
            if (!disposeTask.Wait(DisposeRaceSessionTimeout))
            {
                _ = disposeTask.ContinueWith(
                    static task => _ = task.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                logger.Info("Race session disposal timed out during shutdown; continuing application exit.");
            }
        }
        catch (AggregateException ex)
        {
            logger.Error(ex.Flatten(), "Race session disposal failed during shutdown.");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Race session disposal failed during shutdown.");
        }
    }

    private void ApplyRouteOverride(RaceRoutePayload route)
    {
        if (!routeOverride.TryCreatePackage(route, out SettingsRouteOverridePackage package, out string detail))
        {
            logger.Info("Race route override ignored: " + detail);
            return;
        }

        if (!routeOverride.MarkApplied(package))
        {
            logger.Info("Race route override ignored: " + RaceRouteOverrideController.AlreadyAppliedDetail);
            return;
        }

        applyRouteOverride(package);
    }

    private void RestoreRouteOverride()
    {
        if (routeOverride.Clear())
        {
            clearRouteOverride();
        }
    }

    private void CancelMemberWorldDownload()
    {
        memberWorldDownloadCancellation?.Cancel();
        memberWorldDownloadCancellation?.Dispose();
        memberWorldDownloadCancellation = null;
        pendingWorldFileKey = null;
    }

    private RacePanelDraftState CreateCurrentDraftState()
    {
        RacePanelDraftState current = draftState;
        RaceRoomState? state = session.State;
        if (state is not null)
        {
            current = current with
            {
                Nickname = session.Nickname ?? current.Nickname,
                RoomCode = state.RoomCode,
                SeedText = state.Seed?.SeedText ?? current.SeedText,
                LocalWorldPath = localWorldPath ?? current.LocalWorldPath,
                Role = IsCurrentUserHost(state) ? RacePanelRole.Host : RacePanelRole.Member
            };
        }
        else if (!string.IsNullOrWhiteSpace(localWorldPath))
        {
            current = current with { LocalWorldPath = localWorldPath };
        }

        return current.Normalize();
    }

    private void SyncDraftFromRoomState(RaceRoomState state)
    {
        SaveDraftState(draftState with
        {
            Nickname = session.Nickname ?? draftState.Nickname,
            RoomCode = state.RoomCode,
            SeedText = state.Seed?.SeedText ?? draftState.SeedText,
            LocalWorldPath = localWorldPath ?? draftState.LocalWorldPath,
            Role = IsCurrentUserHost(state) ? RacePanelRole.Host : RacePanelRole.Member
        });
    }

    private void HandlePackageChanged(object? sender, RacePackageChanged update)
    {
        if (DispatchOwnerThreadIfRequired(() => HandlePackageChanged(sender, update)))
        {
            return;
        }

        ApplyPackageToViews(update);
    }

    private void HandleProgressChanged(object? sender, RaceProgressChanged update)
    {
        QueueProgressViewUpdate(update);
    }

    private void HandleRosterChanged(object? sender, RaceRosterChanged update)
    {
        if (DispatchOwnerThreadIfRequired(() => HandleRosterChanged(sender, update)))
        {
            return;
        }

        ApplyRosterToViews(update);
    }

    private void ApplyOperationState(RaceOperationResult<RaceRoomState> result)
    {
        if (result.Succeeded && result.Value is RaceRoomState state)
        {
            ApplyRosterToViews(new RaceRosterChanged(RaceRoomStateUpdateKind.Snapshot, state));
        }
    }

    private void QueueProgressViewUpdate(RaceProgressChanged update)
    {
        if (owner.IsDisposed)
        {
            return;
        }

        lock (progressViewUpdateLock)
        {
            pendingProgressViewUpdate = update;
        }

        if (Interlocked.Exchange(ref progressViewUpdatePending, 1) != 0)
        {
            return;
        }

        if (!PostOwnerThread(FlushProgressViewUpdate))
        {
            Interlocked.Exchange(ref progressViewUpdatePending, 0);
        }
    }

    private void FlushProgressViewUpdate()
    {
        RaceProgressChanged? update;
        lock (progressViewUpdateLock)
        {
            update = pendingProgressViewUpdate;
            pendingProgressViewUpdate = null;
        }

        if (update is not null && !owner.IsDisposed)
        {
            ApplyProgressToViews(update);
        }

        Interlocked.Exchange(ref progressViewUpdatePending, 0);
        bool hasPendingUpdate;
        lock (progressViewUpdateLock)
        {
            hasPendingUpdate = pendingProgressViewUpdate is not null;
        }

        if (hasPendingUpdate &&
            Interlocked.Exchange(ref progressViewUpdatePending, 1) == 0 &&
            !PostOwnerThread(FlushProgressViewUpdate))
        {
            Interlocked.Exchange(ref progressViewUpdatePending, 0);
        }
    }

    private void ApplyPackageToViews(RacePackageChanged update)
    {
        RaceRoomState state = update.State;
        SyncDraftFromRoomState(state);
        if (session.IsInRoom &&
            ShouldApplyRoomPayloadForUpdate(state))
        {
            ApplyRouteOverride(state.Route!);
            ClearReportedProgress();
            StartMemberWorldDownloadIfNeeded(state);
        }

        form?.UpdateRaceState(state);
        SyncLeaderboardVisibility();
        leaderboardForm?.UpdateState(state);
        raceTimerColorChanged();
    }

    private void ApplyRosterToViews(RaceRosterChanged update)
    {
        RaceRoomState state = update.State;
        if (ShouldEndLocalRoomForUpdate(update))
        {
            BeginLeaveLocalRoomStateAfterRemoteExit();
            return;
        }

        SyncDraftFromRoomState(state);
        form?.UpdateRaceState(state);
        SyncLeaderboardVisibility();
        leaderboardForm?.UpdateState(state);
        raceTimerColorChanged();
    }

    private void ApplyProgressToViews(RaceProgressChanged update)
    {
        RaceRoomState? state = session.State;
        RaceRoomProgressState progress = update.Progress;
        if (state is null ||
            !string.Equals(state.RoomCode, progress.RoomCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        form?.UpdateRaceState(state);
        SyncLeaderboardVisibility();
        leaderboardForm?.UpdateState(state);
        raceTimerColorChanged();
    }

    private void StartMemberWorldDownloadIfNeeded(RaceRoomState state)
    {
        RaceWorldFileInfo? worldFile = state.WorldFile;
        if (!session.IsInRoom ||
            worldFile is null ||
            state.Status == RaceRoomStatus.Closed ||
            IsCurrentUserHost(state))
        {
            return;
        }

        string serverFileName = NormalizeWorldFileName(worldFile.FileName);
        string worldFileKey = CreateWorldFileKey(state.RoomCode, worldFile);
        if (string.IsNullOrWhiteSpace(serverFileName))
        {
            return;
        }

        if (HasCurrentLocalWorld(state.RoomCode, worldFile))
        {
            MarkWorldReadyForAlreadyInstalledWorldIfNeeded(state);
            return;
        }

        if (string.Equals(pendingWorldFileKey, worldFileKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        memberWorldDownloadCancellation?.Cancel();
        memberWorldDownloadCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        memberWorldDownloadCancellation = cancellation;
        pendingWorldFileKey = worldFileKey;
        _ = DownloadWorldForStateInBackgroundAsync(state, cancellation);
    }

    private void MarkWorldReadyForAlreadyInstalledWorldIfNeeded(RaceRoomState state)
    {
        string localNickname = session.Nickname ?? draftState.Nickname;
        if (string.IsNullOrWhiteSpace(localNickname))
        {
            return;
        }

        RacePlayerState? localPlayer = state.Players.FirstOrDefault(player =>
            string.Equals(player.Nickname, localNickname, StringComparison.OrdinalIgnoreCase));
        if (localPlayer is null || localPlayer.WorldReady)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                RaceOperationResult<RaceRoomState> ready = await session.MarkWorldReadyAsync(ready: true);
                LogRaceOperationFailure(ready, "mark world ready for existing world");
            }
            catch (Exception ex) when (IsRaceConnectionExitException(ex))
            {
                logger.Info("Race mark ready for existing world failed: " + ex.Message);
            }
        });
    }

    private bool ShouldEndLocalRoomForUpdate(RaceRosterChanged update)
    {
        if (update.Kind == RaceRoomStateUpdateKind.RoomClosed)
        {
            return true;
        }

        string localNickname = session.Nickname ?? draftState.Nickname;
        return update.Kind is RaceRoomStateUpdateKind.PlayerKicked or RaceRoomStateUpdateKind.PlayerLeft &&
            !string.IsNullOrWhiteSpace(localNickname) &&
            string.Equals(update.ActorNickname, localNickname, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool ShouldAcquireWorldForPackage(
        RacePackageChanged update,
        bool isCurrentUserHost)
    {
        if (isCurrentUserHost)
        {
            return false;
        }

        return update.State.WorldFile is not null;
    }

    private void BeginLeaveLocalRoomStateAfterRemoteExit()
    {
        worldGenerationCancellation?.Cancel();
        CancelMemberWorldDownload();
        ClearLocalRoomStateAfterLeave();
        _ = Task.Run(async () =>
        {
            try
            {
                await session.LeaveLocalAsync(DisposeRaceSessionTimeout).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsRaceConnectionExitException(ex))
            {
                logger.Info("Race local room cleanup after remote exit failed: " + ex.Message);
            }
        });
    }

    private async Task DownloadWorldForStateInBackgroundAsync(
        RaceRoomState state,
        CancellationTokenSource cancellation)
    {
        try
        {
            await DownloadWorldForStateAsync(state, force: false, cancellation.Token);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or HttpRequestException or TimeoutException)
        {
            logger.Error(ex, "Race automatic world download failed.");
            _ = await session.MarkWorldReadyAsync(ready: false, ex.Message);
        }
        finally
        {
            if (ReferenceEquals(memberWorldDownloadCancellation, cancellation))
            {
                memberWorldDownloadCancellation = null;
                pendingWorldFileKey = null;
            }

            cancellation.Dispose();
        }
    }

    private bool HasCurrentLocalWorld(string roomCode, RaceWorldFileInfo worldFile)
    {
        string serverFileName = NormalizeWorldFileName(worldFile.FileName);
        string revisionKey = CreateWorldFileKey(roomCode, worldFile);
        return string.Equals(activeWorldRoomCode, roomCode, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(activeWorldFileName, serverFileName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(activeWorldRevisionKey, revisionKey, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(localWorldPath) &&
            File.Exists(localWorldPath);
    }

    private void RememberObtainedWorldFile(string roomCode, RaceWorldFileInfo? worldFile, bool resetTimer)
    {
        string serverFileName = NormalizeWorldFileName(worldFile?.FileName);
        if (worldFile is null || string.IsNullOrWhiteSpace(serverFileName))
        {
            return;
        }

        string revisionKey = CreateWorldFileKey(roomCode, worldFile);
        if (string.Equals(activeWorldRevisionKey, revisionKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        activeWorldRoomCode = roomCode;
        activeWorldFileName = serverFileName;
        activeWorldRevisionKey = revisionKey;
        if (resetTimer)
        {
            ResetRaceTimerForNewWorld();
        }
    }

    internal static bool ShouldApplyRoomPayloadForUpdate(RaceRoomState state)
    {
        if (state.Route is null ||
            state.Status == RaceRoomStatus.Closed)
        {
            return false;
        }

        return true;
    }

    private void ResetRaceTimerForNewWorld()
    {
        try
        {
            resetRaceTimer();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Race timer reset after receiving world failed.");
        }
    }

    private void SyncLeaderboardVisibility()
    {
        if (session.IsInRoom && session.State?.Status != RaceRoomStatus.Closed)
        {
            EnsureLeaderboardForm();
            return;
        }

        CloseLeaderboardForm();
    }

    private void EnsureLeaderboardForm()
    {
        if (leaderboardForm is { IsDisposed: false })
        {
            leaderboardForm.Show();
            ApplyLeaderboardTopMost();
            leaderboardForm.ApplyMouseClickThrough(mouseClickThrough);
            return;
        }

        leaderboardForm = new RaceLeaderboardForm(getSettings, Localize, GetLeaderboardLocalNickname);
        leaderboardForm.FormClosed += (_, _) =>
        {
            leaderboardForm = null;
        };
        leaderboardForm.Show(owner);
        ApplyLeaderboardTopMost();
        leaderboardForm.ApplyMouseClickThrough(mouseClickThrough);
    }

    private void CloseLeaderboardForm()
    {
        CloseFormIfOpen(leaderboardForm);
        leaderboardForm = null;
    }

    private static void CloseFormIfOpen(Form? target)
    {
        if (target is null || target.IsDisposed)
        {
            return;
        }

        try
        {
            if (target.InvokeRequired && target.IsHandleCreated)
            {
                using var closed = new ManualResetEventSlim(false);
                target.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (!target.IsDisposed)
                        {
                            target.Close();
                        }
                    }
                    finally
                    {
                        closed.Set();
                    }
                }));
                closed.Wait(CloseWindowTimeout);
                return;
            }

            target.Close();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void ApplyLeaderboardTopMost()
    {
        if (leaderboardForm is not { IsDisposed: false } || !leaderboardForm.IsHandleCreated)
        {
            return;
        }

        WindowTopMostSync.Apply(getSettings().General.AlwaysOnTop, leaderboardForm.Handle);
    }

    private void PersistRacePreferences(RacePanelDraftState state)
    {
        RacePanelPersistentPreferences preferences = RacePanelPersistentPreferences.FromDraft(state);
        if (preferences == lastPersistedPreferences)
        {
            return;
        }

        try
        {
            AppSettings nextSettings = settingsSnapshots.CreateSnapshot(getBaseSettings());
            nextSettings.Race ??= new RaceSettings();
            ApplyPreferencesToSettings(nextSettings.Race, preferences);

            OperationResult result = saveSettings(nextSettings);
            if (result.Succeeded)
            {
                getBaseSettings().Race ??= new RaceSettings();
                ApplyPreferencesToSettings(getBaseSettings().Race, preferences);
                getSettings().Race ??= new RaceSettings();
                ApplyPreferencesToSettings(getSettings().Race, preferences);
                lastPersistedPreferences = preferences;
                return;
            }

            logger.Info("Race preferences save failed: " + result.Message);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Race preferences save failed.");
        }
    }

    private static void ApplyPreferencesToSettings(
        RaceSettings settings,
        RacePanelPersistentPreferences preferences)
    {
        settings.ServerUrl = preferences.ServerUrl;
        settings.Nickname = preferences.Nickname;
        settings.PreferredRole = preferences.PreferredRole;
        settings.PreferredWorldSource = preferences.PreferredWorldSource;
    }

    private static RaceLeaderboardSettings CloneLeaderboardSettings(RaceLeaderboardSettings source)
    {
        return new RaceLeaderboardSettings
        {
            UseRankColorForMainTimer = source.UseRankColorForMainTimer,
            Rank = CloneColumn(source.Rank),
            Player = CloneColumn(source.Player),
            Icon = CloneColumn(source.Icon),
            Time = CloneColumn(source.Time),
            TextEffects = new RaceLeaderboardTextEffectSettings
            {
                Rank = CloneEffect(source.TextEffects?.Rank),
                Player = CloneEffect(source.TextEffects?.Player),
                Icon = CloneEffect(source.TextEffects?.Icon),
                Time = CloneEffect(source.TextEffects?.Time)
            },
            Colors = new RaceLeaderboardColorSettings
            {
                RankGradient = CloneRankGradient(source.Colors?.RankGradient),
                Rank = CloneColor(source.Colors?.Rank),
                Player = CloneColor(source.Colors?.Player),
                PlayerSelf = CloneColor(source.Colors?.PlayerSelf ?? source.Colors?.Player),
                PlayerOther = CloneColor(source.Colors?.PlayerOther ?? source.Colors?.Player),
                Icon = CloneColor(source.Colors?.Icon),
                Time = CloneColor(source.Colors?.Time)
            }
        };
    }

    private string? GetLeaderboardLocalNickname()
    {
        return !string.IsNullOrWhiteSpace(session.Nickname)
            ? session.Nickname
            : draftState.Nickname;
    }

    public Color? GetMainTimerRankColor(
        SplitTimerPhase timerPhase,
        IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        RaceLeaderboardSettings leaderboard = getSettings().Race?.Leaderboard ?? new RaceLeaderboardSettings();
        if (!leaderboard.UseRankColorForMainTimer || !session.IsInRoom)
        {
            return null;
        }

        bool completedRun = statuses.Count > 0 && statuses[^1].Time.HasValue;
        if (timerPhase != SplitTimerPhase.Running &&
            (timerPhase != SplitTimerPhase.Paused || !completedRun))
        {
            return null;
        }

        RaceRoomState? state = session.State;
        IReadOnlyList<RaceLeaderboardEntry> rows = state?.Leaderboard ?? [];
        int rowCount = rows.Count;
        int rank = 1;
        string? localNickname = GetLeaderboardLocalNickname();
        if (!string.IsNullOrWhiteSpace(localNickname))
        {
            RaceLeaderboardEntry? entry = rows.FirstOrDefault(item =>
                string.Equals(item.Nickname, localNickname.Trim(), StringComparison.OrdinalIgnoreCase));
            if (entry is not null)
            {
                rank = entry.Rank;
            }
        }

        return RaceLeaderboardColorMath.GetRankFillColor(
            rank,
            Math.Max(Math.Max(rowCount, rank), 1),
            leaderboard.Colors?.RankGradient);
    }

    private static UiColumnSettings CloneColumn(UiColumnSettings? source)
    {
        UiColumnSettings fallback = new();
        source ??= fallback;
        return new UiColumnSettings
        {
            Show = source.Show,
            Width = source.Width,
            FontFamily = source.FontFamily,
            FontSize = source.FontSize,
            Bold = source.Bold
        };
    }

    private static RaceLeaderboardColumnEffectSettings CloneEffect(RaceLeaderboardColumnEffectSettings? source)
    {
        source ??= new RaceLeaderboardColumnEffectSettings();
        return new RaceLeaderboardColumnEffectSettings
        {
            OpacityPercent = source.OpacityPercent,
            ShadowPercent = source.ShadowPercent,
            OutlineThicknessPercent = source.OutlineThicknessPercent
        };
    }

    private static RaceLeaderboardRankGradientColorSettings CloneRankGradient(RaceLeaderboardRankGradientColorSettings? source)
    {
        source ??= new RaceLeaderboardRankGradientColorSettings();
        return new RaceLeaderboardRankGradientColorSettings
        {
            Start = source.Start,
            Middle = source.Middle,
            End = source.End
        };
    }

    private static RaceLeaderboardColumnColorSettings CloneColor(RaceLeaderboardColumnColorSettings? source)
    {
        source ??= new RaceLeaderboardColumnColorSettings();
        return new RaceLeaderboardColumnColorSettings
        {
            Text = source.Text,
            Outline = source.Outline,
            Shadow = source.Shadow
        };
    }

    private sealed record RacePanelPersistentPreferences(
        string ServerUrl,
        string Nickname,
        string PreferredRole,
        string PreferredWorldSource)
    {
        public static RacePanelPersistentPreferences FromDraft(RacePanelDraftState draft)
        {
            RacePanelDraftState normalized = draft.Normalize();
            return new RacePanelPersistentPreferences(
                string.IsNullOrWhiteSpace(normalized.ServerUrl)
                    ? new RaceSettings().ServerUrl
                    : normalized.ServerUrl,
                normalized.Nickname,
                normalized.Role == RacePanelRole.Member ? RacePreferredRole.Member : RacePreferredRole.Host,
                normalized.WorldSource switch
                {
                    RacePanelWorldSource.CustomSeed => RacePreferredWorldSource.CustomSeed,
                    RacePanelWorldSource.ExistingFile => RacePreferredWorldSource.ExistingFile,
                    _ => RacePreferredWorldSource.Random
                });
        }
    }

    private bool DispatchOwnerThreadIfRequired(Action action)
    {
        if (owner.IsDisposed || !owner.InvokeRequired)
        {
            return false;
        }

        _ = PostOwnerThread(action);
        return true;
    }

    private bool PostOwnerThread(Action action)
    {
        try
        {
            if (owner.IsHandleCreated)
            {
                owner.BeginInvoke(action);
                return true;
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        return false;
    }

    private bool IsCurrentUserHost(RaceRoomState state)
    {
        return state.Players.Any(player =>
            player.IsHost &&
            string.Equals(player.Nickname, session.Nickname, StringComparison.OrdinalIgnoreCase));
    }

    private void TryCopyRoomInfo(string serverUrl, string roomCode)
    {
        try
        {
            Clipboard.SetText(CreateRoomInfoClipboardText(serverUrl, roomCode));
        }
        catch (Exception ex) when (ex is ExternalException or ThreadStateException)
        {
            logger.Info("Race room info clipboard copy failed: " + ex.Message);
        }
    }

    private string CreateRoomInfoClipboardText(string serverUrl, string roomCode)
    {
        return string.Join(
            Environment.NewLine,
            string.Format(CultureInfo.CurrentCulture, Localize("Server: {0}"), serverUrl.Trim()),
            string.Format(CultureInfo.CurrentCulture, Localize("Room code: {0}"), roomCode.Trim()));
    }

    private static string GetUniqueRaceWorldPath(RaceRoomState state)
    {
        string worldsDirectory = Path.Combine(TerrariaSavePaths.SaveRoot(), "Worlds");
        Directory.CreateDirectory(worldsDirectory);
        string fileName = NormalizeWorldFileName(state.WorldFile?.FileName);
        string stem = string.IsNullOrWhiteSpace(fileName)
            ? CreateRaceWorldStem(state.WorldFile?.UploadedAtUtc ?? DateTimeOffset.Now)
            : SanitizeFileStem(Path.GetFileNameWithoutExtension(fileName));
        return GetUniqueWorldPath(worldsDirectory, stem);
    }

    private static string PrepareRaceWorldFileForUpload(
        string sourcePath,
        DateTimeOffset timestamp)
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

        string worldsDirectory = Path.Combine(TerrariaSavePaths.SaveRoot(), "Worlds");
        Directory.CreateDirectory(worldsDirectory);
        string stem = CreateRaceWorldStem(timestamp);
        string targetPath = GetUniqueWorldPath(worldsDirectory, stem);
        File.Copy(sourcePath, targetPath, overwrite: false);
        CopyFileIfPresent(sourcePath + ".bak", targetPath + ".bak");
        return targetPath;
    }

    private static string GetUniqueWorldPath(string directory, string stem)
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

        return Path.Combine(directory, $"{stem}-{DateTime.UtcNow:yyyyMMddHHmmssfff}.wld");
    }

    private static string CreateRaceWorldStem(DateTimeOffset timestamp)
    {
        return SanitizeFileStem(
            $"TerrariaRace-{timestamp.LocalDateTime:yyyyMMddHHmmss}");
    }

    private static bool IsRaceWorldStem(string? stem)
    {
        return !string.IsNullOrWhiteSpace(stem) &&
            stem.Trim().StartsWith("TerrariaRace-", StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateWorldFileKey(string roomCode, RaceWorldFileInfo worldFile)
    {
        return string.Join(
            "|",
            roomCode.Trim(),
            NormalizeWorldFileName(worldFile.FileName),
            (worldFile.Sha256 ?? string.Empty).Trim(),
            worldFile.UploadedAtUtc.UtcDateTime.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToUpperInvariant();
    }

    private static string CreateWorldFileKey(string roomCode, string serverFileName)
    {
        return (roomCode.Trim() + "|" + serverFileName.Trim()).ToUpperInvariant();
    }

    private static string NormalizeWorldFileName(string? fileName)
    {
        string name = Path.GetFileName(fileName ?? string.Empty).Trim();
        return string.Equals(Path.GetExtension(name), ".wld", StringComparison.OrdinalIgnoreCase)
            ? name
            : string.Empty;
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

    private void LogRaceOperationFailure(RaceOperationResult<RaceRoomState> result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        string detail = string.IsNullOrWhiteSpace(result.Message)
            ? result.ErrorCode
            : $"{result.ErrorCode}: {result.Message}";
        logger.Info($"Race {operation} failed: {detail}");
    }

    private abstract record RaceProgressUpload
    {
        public sealed record Start(RaceRunStartReport Report) : RaceProgressUpload;

        public sealed record Split(RaceSplitReport Report) : RaceProgressUpload;

        public static RaceProgressUpload ForStart(RaceRunStartReport report)
        {
            return new Start(report);
        }

        public static RaceProgressUpload ForSplit(RaceSplitReport report)
        {
            return new Split(report);
        }
    }

    private readonly record struct RaceLocalWorldGenerationAttempt(
        bool Succeeded,
        string WorldPath,
        string Message,
        bool Retryable)
    {
        public static RaceLocalWorldGenerationAttempt Success(string worldPath)
        {
            return new RaceLocalWorldGenerationAttempt(true, worldPath, string.Empty, false);
        }

        public static RaceLocalWorldGenerationAttempt Failure(string message, bool retryable)
        {
            return new RaceLocalWorldGenerationAttempt(false, string.Empty, message, retryable);
        }
    }
}
