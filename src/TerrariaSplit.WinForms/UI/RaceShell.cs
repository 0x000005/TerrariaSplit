using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using TerrariaSplit.Localization;
using TerrariaSplit.Race.Client;
using TerrariaSplit.Race.Contracts;
using TerrariaSplit.Terraria;
using TerrariaSplit.Terraria.Automation;

namespace TerrariaSplit.UI;

internal sealed class RaceShell : IRacePanelShell, IDisposable
{
    private static readonly TimeSpan DisposeRaceSessionTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan DisposeWorldLockTimeout = TimeSpan.FromSeconds(7);
    private static readonly TimeSpan CloseWindowTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RemoteRoomExitTimeout = TimeSpan.FromSeconds(2);
    private const int RaceRandomWorldMaxAttempts = 250_000;
    private const int RaceVerifiedGenerationProgressMaximum = 80;
    private const int RaceDirectGenerationProgressMaximum = 90;
    private readonly RaceClientSession session = new();
    private readonly RaceRouteOverrideController routeOverride;
    private readonly RaceLocalPyramidSeedGenerator seedGenerator = new();
    private readonly TerrariaRaceWorldGenerationService worldGeneration = new();
    private readonly ITerrariaRaceWorldLockService worldLock;
    private readonly RaceSpeechCoordinator speechCoordinator;
    private readonly SemaphoreSlim worldLockLifecycleGate = new(1, 1);
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
    private readonly Action<SystemEvent> publishSystemEvent;
    private readonly Form owner;
    private const string RaceStartProgressKey = "start";
    private readonly Channel<RaceProgressUpload> progressUploads = Channel.CreateUnbounded<RaceProgressUpload>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly CancellationTokenSource progressUploadCancellation = new();
    private readonly Task progressUploadPump;
    private Task? completedRunUnlockTask;
    private readonly object progressViewUpdateLock = new();
    private RaceForm? form;
    private RaceLeaderboardForm? leaderboardForm;
    private CancellationTokenSource? worldGenerationCancellation;
    private CancellationTokenSource? memberWorldDownloadCancellation;
    private CancellationTokenSource? worldLockRetryCancellation;
    private CancellationTokenSource? scheduledRaceStartCancellation;
    private string? localWorldPath;
    private string? activeWorldRoomCode;
    private string? activeWorldFileName;
    private string? activeWorldRevisionKey;
    private string? pendingWorldFileKey;
    private string? planteraBulbPlanCacheKey;
    private TerrariaPlanteraBulbPlan? planteraBulbPlanCache;
    private RacePanelDraftState draftState;
    private RacePanelPersistentPreferences lastPersistedPreferences;
    private readonly HashSet<string> reportedProgressKeys = new(StringComparer.OrdinalIgnoreCase);
    private RaceProgressChanged? pendingProgressViewUpdate;
    private int progressViewUpdatePending;
    private int localRoomExitActive;
    private long activePackageRevision;
    private string activeRunId = string.Empty;
    private bool mouseClickThrough;
    private bool closingWindows;
    private int worldLockReleasedForCompletedRun;
    private int restartActive;
    private long handledStartPackageRevision;
    private long handledStartSequence;
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
        Action<SystemEvent> publishSystemEvent,
        Form owner,
        Action raceTimerColorChanged,
        Action resetRaceTimer,
        ITerrariaRaceWorldLockService? worldLock = null)
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
        this.publishSystemEvent = publishSystemEvent;
        this.owner = owner;
        this.raceTimerColorChanged = raceTimerColorChanged;
        this.resetRaceTimer = resetRaceTimer;
        this.worldLock = worldLock ?? new TerrariaRaceWorldLockService();
        speechCoordinator = new RaceSpeechCoordinator(
            new WindowsRaceSpeechEngine(),
            ex => logger.Error(ex, "Race voice announcement failed."));
        speechCoordinator.ApplySettings(getSettings().Race?.Voice ?? new RaceVoiceSettings());
        this.worldLock.HealthFailed += HandleWorldLockHealthFailed;
        draftState = RacePanelDraftState.FromSettings(getSettings());
        lastPersistedPreferences = RacePanelPersistentPreferences.FromDraft(draftState);
        session.PackageChanged += HandlePackageChanged;
        session.ProgressChanged += HandleProgressChanged;
        session.GroupCompleted += HandleGroupCompleted;
        session.PlayerProgressReset += HandlePlayerProgressReset;
        session.RosterChanged += HandleRosterChanged;
        session.ConnectionStatusChanged += HandleConnectionStatusChanged;
        progressUploadPump = Task.Run(() => DrainProgressReportsAsync(progressUploadCancellation.Token));
    }

    public RaceRoomState? State => session.State;

    public RaceServerConnectionStatus ServerConnectionStatus => session.ConnectionStatus;

    public string? LocalNickname => session.Nickname;

    public bool IsHostInCurrentRoom => session.State is RaceRoomState state && IsCurrentUserHost(state);

    public bool IsCheatsActive =>
        session.State is RaceRoomState { Status: not RaceRoomStatus.Closed, WorldSettings: RaceWorldSettings worldSettings } &&
        worldSettings.EffectiveCheats.Enabled;

    public string? LocalWorldPath => localWorldPath;

    public RacePanelDraftState DraftState => CreateCurrentDraftState();

    public bool IsInRoom => session.IsInRoom;

    public RaceLeaderboardSettings LeaderboardSettings =>
        CloneLeaderboardSettings(getSettings().Race?.Leaderboard ?? new RaceLeaderboardSettings());

    public RaceVoiceSettings VoiceSettings => CloneVoiceSettings(getSettings().Race?.Voice);

    public IReadOnlyList<RaceVoiceOption> InstalledVoices => speechCoordinator.InstalledVoices;

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

    public void SaveVoiceSettings(RaceVoiceSettings voiceSettings)
    {
        RaceVoiceSettings nextVoice = CloneVoiceSettings(voiceSettings);
        try
        {
            AppSettings nextSettings = settingsSnapshots.CreateSnapshot(getBaseSettings());
            nextSettings.Race ??= new RaceSettings();
            nextSettings.Race.Voice = CloneVoiceSettings(nextVoice);

            OperationResult result = saveSettings(nextSettings);
            if (result.Succeeded)
            {
                getBaseSettings().Race ??= new RaceSettings();
                getBaseSettings().Race.Voice = CloneVoiceSettings(nextVoice);
                getSettings().Race ??= new RaceSettings();
                getSettings().Race.Voice = CloneVoiceSettings(nextVoice);
                speechCoordinator.ApplySettings(nextVoice);
                return;
            }

            logger.Info("Race voice settings save failed: " + result.Message);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Race voice settings save failed.");
        }
    }

    public void PreviewVoice(RaceVoiceSettings voiceSettings)
    {
        RaceVoiceSettings preview = CloneVoiceSettings(voiceSettings);
        speechCoordinator.Preview(preview, LanguageNames.IsChinese(getSettings().General.Language));
    }

    public void RefreshWindowSettings()
    {
        RefreshDisplay(DisplayRefreshLevel.DisplaySettings);
    }

    public void RefreshDisplay(DisplayRefreshLevel level)
    {
        speechCoordinator.ApplySettings(getSettings().Race?.Voice ?? new RaceVoiceSettings());
        form?.UpdateRaceState(session.State);
        SyncLeaderboardVisibility();
        ApplyLeaderboardTopMost();
        leaderboardForm?.ApplyMouseClickThrough(mouseClickThrough);
        if (level is DisplayRefreshLevel.DisplaySettings or
            DisplayRefreshLevel.RoutePackage or
            DisplayRefreshLevel.RunReset or
            DisplayRefreshLevel.FullRebuild)
        {
            leaderboardForm?.ApplySettings();
        }

        leaderboardForm?.UpdateState(session.State);
        raceTimerColorChanged();
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
        if (Interlocked.Exchange(ref localRoomExitActive, 1) != 0)
        {
            return;
        }

        try
        {
            try
            {
                using var cancellation = new CancellationTokenSource(RemoteRoomExitTimeout);
                RaceOperationResult<RaceRoomState> result = await session.CloseRoomAsync(cancellation.Token);
                LogRaceOperationFailure(result, "close room");
            }
            catch (Exception ex) when (IsRaceConnectionExitException(ex))
            {
                logger.Info("Race close room failed; leaving local room state. " + ex.Message);
            }

            await LeaveLocalRoomStateAsync();
        }
        finally
        {
            Interlocked.Exchange(ref localRoomExitActive, 0);
        }
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
        progress = CreateJobProgress("race-world-generation", progress);
        SaveDraftState(draftState with { Role = RacePanelRole.Host });
        localWorldPath = null;

        if (!RaceWorldSettingsFactory.HasActiveFilters(worldSettings))
        {
            string seedText = CreateRandomSeedText();
            await GenerateWorldFromSeedAsync(
                worldSettings,
                new RaceSeedAssignment(seedText, RaceSeedSource.HostGenerated),
                progress,
                RaceDirectGenerationProgressMaximum);
            return;
        }

        if (RaceWorldSettingsFactory.IsPyramidFilterEnabled(worldSettings))
        {
            await GeneratePrescreenedWorldUntilVerifiedAsync(worldSettings, RaceRandomWorldMaxAttempts, progress);
            return;
        }

        await GenerateRandomWorldUntilVerifiedAsync(worldSettings, RaceRandomWorldMaxAttempts, progress);
    }

    public async Task GenerateCustomSeedWorldAsync(
        RaceWorldSettings worldSettings,
        string seedText,
        IProgress<int>? progress = null)
    {
        progress = CreateJobProgress("race-world-generation", progress);
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
        progress = CreateJobProgress("race-world-upload", progress);
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
        catch (Exception ex) when (ex is IOException or InvalidOperationException or HttpRequestException or OperationCanceledException or TimeoutException)
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
                _ = await MarkWorldLockStartingAsync(cancellationToken);
                TerrariaRaceWorldLockResult worldLockResult = await LockRaceWorldAsync(uploadWorldPath, state.Determinism, cancellationToken);
                RaceOperationResult<RaceRoomState> preparation = await MarkWorldLockResultAsync(
                    worldLockResult,
                    cancellationToken);
                if (worldLockResult.Succeeded)
                {
                    result = preparation;
                    TryCopyRoomInfo(draftState.ServerUrl, state.RoomCode);
                }
                else
                {
                    if (IsTerrariaProcessUnavailable(worldLockResult.Message) && preparation.Succeeded)
                    {
                        result = preparation;
                        TryCopyRoomInfo(draftState.ServerUrl, state.RoomCode);
                        logger.Info("Race world uploaded; waiting for Terraria to start before installing the hook.");
                    }
                    else
                    {
                        result = preparation.Succeeded
                            ? RaceOperationResult<RaceRoomState>.Failure("world_lock_failed", worldLockResult.Message)
                            : preparation;
                    }
                }
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

    public async Task<RaceOperationResult<RaceRoomState>> StartAsync()
    {
        if (!IsHostInCurrentRoom)
        {
            return RaceOperationResult<RaceRoomState>.Failure("host_only", "Only the room host can start the Race.");
        }

        RaceOperationResult<RaceRoomState> result = await session.StartRaceAsync();
        if (result.Succeeded && result.Value is RaceRoomState startingState)
        {
            // The host must not depend on the SignalR roster notification being
            // dispatched before the request result. Start directly from the
            // authoritative response and let the sequence guard deduplicate the
            // matching broadcast.
            ApplyScheduledRaceStart(new RaceRosterChanged(
                RaceRoomStateUpdateKind.RaceStarting,
                startingState,
                session.Nickname ?? string.Empty));
        }

        ApplyOperationState(result);
        LogRaceOperationFailure(result, "start Race");
        return result;
    }

    public async Task<RaceOperationResult<RaceRoomState>> RestartAsync()
    {
        if (!IsHostInCurrentRoom)
        {
            return RaceOperationResult<RaceRoomState>.Failure("host_only", "Only the room host can restart the Race.");
        }

        RaceOperationResult<RaceRoomState> result = await session.RestartRaceAsync();
        ApplyOperationState(result);
        LogRaceOperationFailure(result, "restart Race");
        return result;
    }

    private async Task<RaceOperationResult<RaceRoomState>> RebuildLocalRacePackageAsync(RaceRoomState? restartState)
    {
        if (Interlocked.Exchange(ref restartActive, 1) != 0)
        {
            return RaceOperationResult<RaceRoomState>.Failure(
                "restart_in_progress",
                "A Race restart is already in progress.");
        }

        try
        {
            if (!session.IsInRoom || restartState is null || restartState.Status == RaceRoomStatus.Closed)
            {
                return RaceOperationResult<RaceRoomState>.Failure(
                    "room_required",
                    "Join or create a Race room before restarting.");
            }

            if (restartState.WorldFile is null || restartState.Determinism is null)
            {
                return RaceOperationResult<RaceRoomState>.Failure(
                    "world_required",
                    "The room world or determinism package is unavailable.");
            }

            CancelMemberWorldDownload();
            RaceOperationResult<RaceRoomState> notReady = await session.UpdatePreparationStatusAsync(
                RacePlayerFileStatus.Creating,
                RaceWorldFileStatus.Downloading,
                GetRngControlStartingStatus(restartState));
            ApplyOperationState(notReady);
            if (!notReady.Succeeded)
            {
                return notReady;
            }

            Task? pendingCompletedRunUnlock = Volatile.Read(ref completedRunUnlockTask);
            if (pendingCompletedRunUnlock is not null)
            {
                await pendingCompletedRunUnlock;
            }

            TerrariaRaceWorldLockResult prepared;
            await worldLockLifecycleGate.WaitAsync();
            try
            {
                prepared = await worldLock.PrepareRestartAsync();
            }
            finally
            {
                worldLockLifecycleGate.Release();
            }
            if (!prepared.Succeeded && !IsTerrariaProcessUnavailable(prepared.Message))
            {
                await MarkRestartUnavailableAsync(restartState.PackageRevision, prepared.Message);
                return RaceOperationResult<RaceRoomState>.Failure("restart_prepare_failed", prepared.Message);
            }

            string? previousWorldPath = localWorldPath;
            ResetRaceTimerForNewWorld();
            await DownloadWorldForStateAsync(restartState, force: true, CancellationToken.None);

            RaceRoomState? current = session.State;
            if (current is null || current.PackageRevision != restartState.PackageRevision)
            {
                return RaceOperationResult<RaceRoomState>.Failure(
                    "restart_failed",
                    "The Race package changed while the local reset was running.");
            }

            TerrariaRaceWorldLockResult returnedToMenu;
            await worldLockLifecycleGate.WaitAsync();
            try
            {
                returnedToMenu = await worldLock.ReturnToMainMenuAsync();
            }
            finally
            {
                worldLockLifecycleGate.Release();
            }
            if (!returnedToMenu.Succeeded && !IsTerrariaProcessUnavailable(returnedToMenu.Message))
            {
                await MarkRestartUnavailableAsync(restartState.PackageRevision, returnedToMenu.Message);
                return RaceOperationResult<RaceRoomState>.Failure("restart_return_to_menu_failed", returnedToMenu.Message);
            }

            if (!string.IsNullOrWhiteSpace(previousWorldPath) &&
                !string.Equals(previousWorldPath, localWorldPath, StringComparison.OrdinalIgnoreCase))
            {
                DeleteRaceWorldFile(previousWorldPath);
            }

            return RaceOperationResult<RaceRoomState>.Success(current);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or HttpRequestException or OperationCanceledException or TimeoutException)
        {
            logger.Error(ex, "Race restart failed.");
            if (restartState is not null)
            {
                await MarkRestartUnavailableAsync(restartState.PackageRevision, ex.Message);
            }

            return RaceOperationResult<RaceRoomState>.Failure("restart_failed", ex.Message);
        }
        finally
        {
            Interlocked.Exchange(ref restartActive, 0);
        }
    }

    private async Task MarkRestartUnavailableAsync(long packageRevision, string error)
    {
        if (session.State?.PackageRevision != packageRevision)
        {
            return;
        }

        try
        {
            RaceOperationResult<RaceRoomState> result = await session.UpdatePreparationStatusAsync(
                RacePlayerFileStatus.Failed,
                RaceWorldFileStatus.Ready,
                GetRngControlFailureStatus(session.State),
                error);
            ApplyOperationState(result);
            LogRaceOperationFailure(result, "mark restart unavailable");
        }
        catch (Exception ex) when (IsRaceConnectionExitException(ex))
        {
            logger.Info("Race restart unavailable update failed: " + ex.Message);
        }
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
        _ = await session.UpdatePreparationStatusAsync(
            RacePlayerFileStatus.Waiting,
            RaceWorldFileStatus.Downloading,
            GetRngControlIdleStatus(state),
            cancellationToken: cancellationToken);
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
            if (session.State?.PackageRevision == state.PackageRevision)
            {
                _ = await session.UpdatePreparationStatusAsync(
                    RacePlayerFileStatus.Waiting,
                    RaceWorldFileStatus.Failed,
                    GetRngControlIdleStatus(state),
                    download.Message,
                    cancellationToken);
            }

            logger.Info("Race world download failed: " + download.Message);
            return;
        }

        if (session.State?.PackageRevision != state.PackageRevision || cancellationToken.IsCancellationRequested)
        {
            DeleteRaceWorldFile(download.WorldPath);
            return;
        }

        localWorldPath = download.WorldPath;
        SaveDraftState(draftState with { LocalWorldPath = download.WorldPath });
        RememberObtainedWorldFile(state.RoomCode, download.WorldFile, resetTimer: true);
        _ = await MarkWorldLockStartingAsync(cancellationToken);
        TerrariaRaceWorldLockResult worldLockResult = await LockRaceWorldAsync(download.WorldPath, state.Determinism, cancellationToken);
        RaceOperationResult<RaceRoomState> ready = await MarkWorldLockResultAsync(worldLockResult, cancellationToken);
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
        if (Interlocked.Exchange(ref localRoomExitActive, 1) != 0)
        {
            return;
        }

        try
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

            await UnlockRaceWorldBestEffortAsync();
            ClearLocalRoomStateAfterLeave();
        }
        finally
        {
            Interlocked.Exchange(ref localRoomExitActive, 0);
        }
    }

    private async Task LeaveLocalRoomStateAsync()
    {
        worldGenerationCancellation?.Cancel();
        CancelMemberWorldDownload();
        await session.LeaveLocalAsync(DisposeRaceSessionTimeout);
        await UnlockRaceWorldBestEffortAsync();
        ClearLocalRoomStateAfterLeave();
    }

    private void ClearLocalRoomStateAfterLeave()
    {
        speechCoordinator.Clear();
        CancelScheduledRaceStart();
        handledStartPackageRevision = 0;
        handledStartSequence = 0;
        string roomCode = session.RoomCode ?? draftState.RoomCode;
        RestoreRouteOverride();
        localWorldPath = null;
        activeWorldFileName = null;
        activeWorldRoomCode = null;
        activeWorldRevisionKey = null;
        Volatile.Write(ref worldLockReleasedForCompletedRun, 0);
        ClearProgressTransportState();
        SaveDraftState(draftState with
        {
            RoomCode = string.Empty,
            SeedText = string.Empty,
            LocalWorldPath = string.Empty
        });
        publishSystemEvent(new RaceRosterSystemEvent(roomCode, IsInRoom: false));
    }

    private static bool IsRaceConnectionExitException(Exception exception)
    {
        return exception is InvalidOperationException or HttpRequestException or OperationCanceledException or TimeoutException or ObjectDisposedException;
    }

    public void ResetReportedProgress()
    {
        RaceRoomState? state = session.State;
        if (!session.IsInRoom || state is null || state.Status == RaceRoomStatus.Closed)
        {
            ClearProgressTransportState();
            return;
        }

        bool rearmCompletedRun = Interlocked.Exchange(ref worldLockReleasedForCompletedRun, 0) == 1;
        bool restartHandlesHookReset = Volatile.Read(ref restartActive) != 0;
        if (!restartHandlesHookReset && (rearmCompletedRun || worldLock.IsLocked))
        {
            TerrariaRaceWorldLockResult reset;
            try
            {
                if (rearmCompletedRun)
                {
                    if (string.IsNullOrWhiteSpace(localWorldPath))
                    {
                        reset = TerrariaRaceWorldLockResult.Failure("The Race world path is unavailable.");
                    }
                    else
                    {
                        reset = LockRaceWorldAsync(localWorldPath, state.Determinism).GetAwaiter().GetResult();
                    }
                }
                else
                {
                    worldLockLifecycleGate.Wait();
                    try
                    {
                        reset = worldLock.ResetDeterminismAsync().GetAwaiter().GetResult();
                    }
                    finally
                    {
                        worldLockLifecycleGate.Release();
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or OperationCanceledException or ObjectDisposedException)
            {
                logger.Error(ex, "Race determinism reset failed.");
                MarkPackageUnavailable(state, ex.Message);
                ClearProgressTransportState();
                return;
            }

            if (!reset.Succeeded)
            {
                logger.Info("Race determinism reset failed: " + reset.Message);
                MarkPackageUnavailable(state, reset.Message);
                ClearProgressTransportState();
                return;
            }
        }

        Volatile.Write(ref activePackageRevision, state.PackageRevision);
        Volatile.Write(ref activeRunId, Guid.NewGuid().ToString("N"));
        reportedProgressKeys.Clear();
        long packageRevision = Volatile.Read(ref activePackageRevision);
        string runId = Volatile.Read(ref activeRunId);
        progressUploads.Writer.TryWrite(RaceProgressUpload.ForReset(
            new RaceProgressResetRequest(
                state.RoomCode,
                session.Nickname ?? string.Empty,
                packageRevision,
                runId)));

    }

    private void ClearProgressTransportState()
    {
        Volatile.Write(ref activePackageRevision, 0);
        Volatile.Write(ref activeRunId, string.Empty);
        reportedProgressKeys.Clear();
    }

    public void QueueProgressReports(bool runStarted, bool runCompleted)
    {
        if (runCompleted && Interlocked.Exchange(ref worldLockReleasedForCompletedRun, 1) == 0)
        {
            Volatile.Write(ref completedRunUnlockTask, UnlockRaceWorldBestEffortAsync());
        }

        RaceRoomState? state = session.State;
        if (!session.IsInRoom || state is null || session.RoomCode is null || session.Nickname is null)
        {
            return;
        }

        long currentPackageRevision = Volatile.Read(ref activePackageRevision);
        string currentRunId = Volatile.Read(ref activeRunId);
        if (currentPackageRevision != state.PackageRevision || string.IsNullOrWhiteSpace(currentRunId))
        {
            ResetReportedProgress();
        }

        long packageRevision = Volatile.Read(ref activePackageRevision);
        string runId = Volatile.Read(ref activeRunId);

        if (runStarted && reportedProgressKeys.Add(RaceStartProgressKey))
        {
            progressUploads.Writer.TryWrite(RaceProgressUpload.ForStart(new RaceRunStartReport(
                session.RoomCode,
                session.Nickname,
                DateTimeOffset.UtcNow)
            {
                PackageRevision = packageRevision,
                RunId = runId
            }));
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

            progressUploads.Writer.TryWrite(RaceProgressUpload.ForSplit(report with
            {
                PackageRevision = packageRevision,
                RunId = runId
            }));
        }
    }

    private async Task DrainProgressReportsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (RaceProgressUpload upload in progressUploads.Reader.ReadAllAsync(cancellationToken))
            {
                await SendProgressUploadWithRetryAsync(upload, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task SendProgressUploadWithRetryAsync(
        RaceProgressUpload upload,
        CancellationToken cancellationToken)
    {
        int retryCount = 0;
        while (!cancellationToken.IsCancellationRequested && IsCurrentProgressUpload(upload))
        {
            try
            {
                RaceProgressSendResult result = await SendProgressUploadAsync(upload, cancellationToken).ConfigureAwait(false);
                if (result == RaceProgressSendResult.Accepted || result == RaceProgressSendResult.Obsolete)
                {
                    return;
                }
            }
            catch (Exception ex) when (IsRaceConnectionExitException(ex))
            {
                logger.Info("Race progress upload will retry: " + ex.Message);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Race progress upload failed and will retry.");
            }

            int delaySeconds = Math.Min(1 << Math.Min(retryCount, 5), 30);
            retryCount++;
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<RaceProgressSendResult> SendProgressUploadAsync(
        RaceProgressUpload upload,
        CancellationToken cancellationToken)
    {
        switch (upload)
        {
            case RaceProgressUpload.Reset reset:
            {
                RaceOperationResult<RaceRoomProgressState> result = await session.ResetProgressAsync(
                    reset.Request.PackageRevision,
                    reset.Request.RunId,
                    cancellationToken).ConfigureAwait(false);
                return ClassifyProgressResult(result, "reset progress");
            }
            case RaceProgressUpload.Start start:
            {
                RaceOperationResult<RaceRoomProgressState> result = await session.ReportStartAsync(
                    start.Report,
                    cancellationToken).ConfigureAwait(false);
                return ClassifyProgressResult(result, "start report");
            }
            case RaceProgressUpload.Split split:
            {
                RaceOperationResult<RaceRoomProgressState> result = await session.ReportSplitAsync(
                    split.Report,
                    cancellationToken).ConfigureAwait(false);
                return ClassifyProgressResult(result, "split report");
            }
            default:
                throw new NotSupportedException($"Unsupported Race progress upload {upload.GetType().Name}.");
        }
    }

    private RaceProgressSendResult ClassifyProgressResult(
        RaceOperationResult<RaceRoomProgressState> result,
        string operation)
    {
        if (result.Succeeded)
        {
            return RaceProgressSendResult.Accepted;
        }

        logger.Info($"Race {operation} rejected. Error={result.ErrorCode} Message={result.Message}.");
        return RaceProgressSendResult.Obsolete;
    }

    private bool IsCurrentProgressUpload(RaceProgressUpload upload)
    {
        return session.IsInRoom &&
            upload.PackageRevision == Volatile.Read(ref activePackageRevision) &&
            string.Equals(upload.RunId, Volatile.Read(ref activeRunId), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        UnlockRaceWorldForDispose();
        CloseWindows();
        worldGenerationCancellation?.Cancel();
        worldGenerationCancellation?.Dispose();
        CancelMemberWorldDownload();
        CancelWorldLockRetry();
        CancelScheduledRaceStart();
        progressUploads.Writer.TryComplete();
        progressUploadCancellation.Cancel();
        try
        {
            progressUploadPump.Wait(DisposeRaceSessionTimeout);
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static item => item is OperationCanceledException))
        {
        }

        progressUploadCancellation.Dispose();
        worldGeneration.Dispose();
        worldLock.HealthFailed -= HandleWorldLockHealthFailed;
        if (worldLock is IDisposable disposableWorldLock)
        {
            disposableWorldLock.Dispose();
        }
        session.PackageChanged -= HandlePackageChanged;
        session.ProgressChanged -= HandleProgressChanged;
        session.GroupCompleted -= HandleGroupCompleted;
        session.PlayerProgressReset -= HandlePlayerProgressReset;
        session.RosterChanged -= HandleRosterChanged;
        session.ConnectionStatusChanged -= HandleConnectionStatusChanged;
        speechCoordinator.Dispose();
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

    private bool ApplyRouteOverride(RaceRoutePayload route)
    {
        if (!routeOverride.TryCreatePackage(route, out SettingsRouteOverridePackage package, out string detail))
        {
            logger.Info("Race route override ignored: " + detail);
            return false;
        }

        if (string.Equals(routeOverride.ActiveKey, package.Key, StringComparison.Ordinal))
        {
            return true;
        }

        if (!routeOverride.MarkApplied(package))
        {
            logger.Info("Race route override ignored: " + RaceRouteOverrideController.AlreadyAppliedDetail);
            return false;
        }

        try
        {
            applyRouteOverride(package);
            return true;
        }
        catch (Exception ex)
        {
            routeOverride.Clear();
            logger.Error(ex, "Race route override application failed.");
            return false;
        }
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

        speechCoordinator.Clear();
        ApplyPackageToViews(update);
    }

    private void HandleProgressChanged(object? sender, RaceProgressChanged update)
    {
        QueueProgressViewUpdate(update);
    }

    private void HandleGroupCompleted(object? sender, RaceGroupCompleted update)
    {
        RaceRoomState? state = session.State;
        if (state?.Route is not RaceRoutePayload route ||
            string.Equals(update.Nickname, session.Nickname, StringComparison.OrdinalIgnoreCase) ||
            update.SplitIndex < 0 ||
            update.SplitIndex >= route.Splits.Count)
        {
            return;
        }

        RaceSplitDefinition split = route.Splits[update.SplitIndex];
        if (!string.Equals(split.Id, update.SplitId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        speechCoordinator.Enqueue(new RaceSpeechQueueItem(
            update,
            string.IsNullOrWhiteSpace(split.DisplayName) ? split.Id : split.DisplayName,
            LanguageNames.IsChinese(getSettings().General.Language)));
    }

    private void HandlePlayerProgressReset(object? sender, RacePlayerProgressReset update)
    {
        speechCoordinator.RemovePendingForPlayer(update);
    }

    private void HandleRosterChanged(object? sender, RaceRosterChanged update)
    {
        if (DispatchOwnerThreadIfRequired(() => HandleRosterChanged(sender, update)))
        {
            return;
        }

        ApplyRosterToViews(update);
    }

    private void HandleConnectionStatusChanged(object? sender, EventArgs e)
    {
        if (DispatchOwnerThreadIfRequired(() => HandleConnectionStatusChanged(sender, e)))
        {
            return;
        }

        form?.UpdateRaceState(session.State);
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
            if (ApplyRouteOverride(state.Route!))
            {
                if (update.Kind == RacePackageChangeKind.Restarted)
                {
                    CancelScheduledRaceStart();
                    _ = RebuildLocalRacePackageAsync(state);
                }
                else
                {
                    StartMemberWorldDownloadIfNeeded(state);
                }
            }
            else
            {
                MarkPackageUnavailable(state, "The host route could not be applied.");
            }
        }

        publishSystemEvent(new RacePackageSystemEvent(
            state.RoomCode,
            update.PackageRevision,
            session.IsInRoom));
    }

    private void ApplyRosterToViews(RaceRosterChanged update)
    {
        RaceRoomState state = update.State;
        if (ShouldEndLocalRoomForUpdate(update))
        {
            speechCoordinator.Clear();
            BeginLeaveLocalRoomStateAfterRemoteExit();
            return;
        }

        if (state.Status == RaceRoomStatus.Closed)
        {
            speechCoordinator.Clear();
        }

        SyncDraftFromRoomState(state);
        ApplyScheduledRaceStart(update);
        publishSystemEvent(new RaceRosterSystemEvent(state.RoomCode, session.IsInRoom));
    }

    private void ApplyScheduledRaceStart(RaceRosterChanged update)
    {
        RaceRoomState state = update.State;
        if (state.ScheduledStartUtc is null ||
            state.StartCountdownMilliseconds <= 0 ||
            state.StartSequence <= 0)
        {
            CancelScheduledRaceStart();
            return;
        }

        // Only the live host Start broadcast may auto-enter Terraria. A later
        // snapshot or reconnect observes the run but must not replay its start.
        if (update.Kind != RaceRoomStateUpdateKind.RaceStarting)
        {
            return;
        }

        if (handledStartPackageRevision == state.PackageRevision && handledStartSequence == state.StartSequence)
        {
            return;
        }

        CancelScheduledRaceStart();
        handledStartPackageRevision = state.PackageRevision;
        handledStartSequence = state.StartSequence;
        var cancellation = new CancellationTokenSource();
        scheduledRaceStartCancellation = cancellation;
        _ = RunScheduledRaceStartAsync(
            TimeSpan.FromMilliseconds(state.StartCountdownMilliseconds),
            cancellation);
    }

    private async Task RunScheduledRaceStartAsync(
        TimeSpan countdownDuration,
        CancellationTokenSource cancellation)
    {
        try
        {
            TerrariaRaceWorldLockResult started;
            await worldLockLifecycleGate.WaitAsync(cancellation.Token).ConfigureAwait(false);
            try
            {
                started = await worldLock.StartRaceAsync(
                    countdownDuration,
                    Localize("Race Starting in {0}"),
                    cancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                worldLockLifecycleGate.Release();
            }

            if (!started.Succeeded)
            {
                logger.Info("Race synchronized start could not return to the menu, show the countdown, and enter the assigned world: " + started.Message);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
        {
            logger.Error(ex, "Race synchronized start failed.");
        }
        finally
        {
            if (ReferenceEquals(scheduledRaceStartCancellation, cancellation))
            {
                scheduledRaceStartCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void CancelScheduledRaceStart()
    {
        CancellationTokenSource? cancellation = Interlocked.Exchange(ref scheduledRaceStartCancellation, null);
        cancellation?.Cancel();
        cancellation?.Dispose();
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

        publishSystemEvent(new RaceProgressSystemEvent(progress.RoomCode));
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
        if (localPlayer is null || IsPreparationReady(state, localPlayer))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                if (session.State?.PackageRevision != state.PackageRevision)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(localWorldPath))
                {
                    return;
                }

                _ = await MarkWorldLockStartingAsync();
                TerrariaRaceWorldLockResult worldLockResult = await LockRaceWorldAsync(localWorldPath, state.Determinism);
                RaceOperationResult<RaceRoomState> ready = await MarkWorldLockResultAsync(worldLockResult);
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

    private void BeginLeaveLocalRoomStateAfterRemoteExit()
    {
        if (Interlocked.Exchange(ref localRoomExitActive, 1) != 0)
        {
            return;
        }

        worldGenerationCancellation?.Cancel();
        CancelMemberWorldDownload();
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

            await UnlockRaceWorldBestEffortAsync().ConfigureAwait(false);

            if (!PostOwnerThread(() =>
                {
                    try
                    {
                        ClearLocalRoomStateAfterLeave();
                    }
                    finally
                    {
                        Interlocked.Exchange(ref localRoomExitActive, 0);
                    }
                }))
            {
                Interlocked.Exchange(ref localRoomExitActive, 0);
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
            if (session.State?.PackageRevision == state.PackageRevision)
            {
                _ = await session.UpdatePreparationStatusAsync(
                    RacePlayerFileStatus.Waiting,
                    RaceWorldFileStatus.Failed,
                    GetRngControlIdleStatus(state),
                    ex.Message);
            }
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

    private async Task<TerrariaRaceWorldLockResult> LockRaceWorldAsync(
        string worldPath,
        RaceDeterminismPackage? determinism,
        CancellationToken cancellationToken = default,
        bool scheduleRetry = true)
    {
        if (determinism is null)
        {
            return TerrariaRaceWorldLockResult.Failure("The Race determinism package is missing.");
        }

        if (!determinism.TryValidate(out string determinismError))
        {
            return TerrariaRaceWorldLockResult.Failure(
                string.IsNullOrWhiteSpace(determinismError)
                    ? "The Race determinism package is invalid."
                    : determinismError);
        }

        if (!RaceWorldFileValidator.TryReadWorldIdentity(
                worldPath,
                out RaceWorldIdentity? identity,
                out string detail) ||
            identity is null)
        {
            string error = string.IsNullOrWhiteSpace(detail)
                ? "The Race world identity could not be read."
                : detail;
            logger.Info("Race world lock rejected the world file: " + error);
            return TerrariaRaceWorldLockResult.Failure(error);
        }

        await worldLockLifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        TerrariaRaceWorldLockResult result;
        try
        {
            TerrariaPlanteraBulbPlan planteraBulbPlan = await GetPlanteraBulbPlanAsync(
                worldPath,
                identity,
                determinism,
                cancellationToken).ConfigureAwait(false);
            result = await worldLock.LockAsync(
                new TerrariaRaceWorldLockTarget(
                    Path.GetFullPath(worldPath),
                    identity.WorldId,
                    identity.UniqueId,
                    new TerrariaRaceDeterminismConfiguration(
                        determinism.ProtocolVersion,
                        determinism.EpochId,
                        determinism.EntropySeedBase64,
                        determinism.TerrariaCompatibilityId,
                        (int)determinism.EnabledCapabilities,
                        determinism.ChancePolicyVersion,
                        determinism.CreateDigest()),
                    planteraBulbPlan,
                    IsRaceEntryAllowed(session.State)),
                new TerrariaRaceInitialPlayerConfiguration(
                    session.Nickname ?? draftState.Nickname,
                    draftState.PlayerTemplateCode,
                    RaceWorldSettingsFactory.ToPlayerDifficulty(
                        session.State?.WorldSettings?.PlayerDifficultyCode ?? RacePlayerDifficultyCodes.Softcore)),
                Localize("Only the assigned Race world and player can be used until the run is completed."),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            logger.Info("Race Plantera bulb plan failed: " + ex.Message);
            return TerrariaRaceWorldLockResult.Failure("The Race world could not be analyzed for Plantera bulb placement: " + ex.Message);
        }
        finally
        {
            worldLockLifecycleGate.Release();
        }
        if (!result.Succeeded)
        {
            logger.Info("Race world lock failed: " + result.Message);
            if (scheduleRetry && IsTerrariaProcessUnavailable(result.Message))
            {
                ScheduleWorldLockRetry(worldPath, determinism);
            }
        }
        else
        {
            if (scheduleRetry)
            {
                CancelWorldLockRetry();
            }
            Volatile.Write(ref worldLockReleasedForCompletedRun, 0);
        }

        return result;
    }

    private static bool IsRaceEntryAllowed(RaceRoomState? state)
    {
        return state is not null &&
            (state.Status == RaceRoomStatus.Running ||
                state.ScheduledStartUtc is DateTimeOffset startUtc && DateTimeOffset.UtcNow >= startUtc);
    }

    private async Task<TerrariaPlanteraBulbPlan> GetPlanteraBulbPlanAsync(
        string worldPath,
        RaceWorldIdentity identity,
        RaceDeterminismPackage determinism,
        CancellationToken cancellationToken)
    {
        if ((determinism.EnabledCapabilities & RaceDeterminismCapability.WorldTransitions) == 0)
        {
            return TerrariaPlanteraBulbPlan.Empty;
        }

        string fullPath = Path.GetFullPath(worldPath);
        var file = new FileInfo(fullPath);
        string cacheKey = string.Join(
            "|",
            fullPath,
            file.Length.ToString(CultureInfo.InvariantCulture),
            file.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture),
            determinism.CreateDigest());
        if (string.Equals(planteraBulbPlanCacheKey, cacheKey, StringComparison.Ordinal) &&
            planteraBulbPlanCache is not null)
        {
            return planteraBulbPlanCache;
        }

        byte[] entropySeed = determinism.GetEntropySeed();
        TerrariaPlanteraBulbPlan plan = await Task.Run(
            () => new TerrariaPlanteraBulbPlanner().Create(
                fullPath,
                identity.WorldId,
                identity.UniqueId,
                entropySeed,
                determinism.ProtocolVersion),
            cancellationToken).ConfigureAwait(false);
        planteraBulbPlanCacheKey = cacheKey;
        planteraBulbPlanCache = plan;
        return plan;
    }

    private void HandleWorldLockHealthFailed(TerrariaRaceWorldLockResult failure)
    {
        logger.Info("Race hook heartbeat failed: " + failure.Message);
        if (!disposed && session.State is RaceRoomState state)
        {
            if (IsTerrariaProcessUnavailable(failure.Message) &&
                !string.IsNullOrWhiteSpace(localWorldPath) &&
                state.Determinism is not null)
            {
                ScheduleWorldLockRetry(localWorldPath, state.Determinism);
                return;
            }

            MarkPackageUnavailable(state, failure.Message);
        }
    }

    private void ScheduleWorldLockRetry(string worldPath, RaceDeterminismPackage determinism)
    {
        CancelWorldLockRetry();
        var cancellation = new CancellationTokenSource();
        worldLockRetryCancellation = cancellation;
        long packageRevision = session.State?.PackageRevision ?? 0;
        _ = Task.Run(async () =>
        {
            bool enablingReported = false;
            while (!cancellation.IsCancellationRequested && !disposed)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellation.Token).ConfigureAwait(false);
                    RaceRoomState? state = session.State;
                    if (state is null || state.PackageRevision != packageRevision ||
                        !string.Equals(localWorldPath, worldPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    if (!enablingReported)
                    {
                        _ = await MarkWorldLockStartingAsync(cancellation.Token).ConfigureAwait(false);
                        enablingReported = true;
                    }

                    TerrariaRaceWorldLockResult retry = await LockRaceWorldAsync(
                        worldPath,
                        determinism,
                        cancellation.Token,
                        scheduleRetry: false).ConfigureAwait(false);
                    if (!retry.Succeeded)
                    {
                        if (IsTerrariaProcessUnavailable(retry.Message))
                        {
                            continue;
                        }

                        MarkPackageUnavailable(state, retry.Message);
                        return;
                    }

                    RaceOperationResult<RaceRoomState> ready = await MarkWorldLockResultAsync(
                        retry,
                        cancellation.Token).ConfigureAwait(false);
                    ApplyOperationState(ready);
                    LogRaceOperationFailure(ready, "mark world ready after Terraria started");
                    CancelWorldLockRetry();
                    return;
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex) when (IsRaceConnectionExitException(ex))
                {
                    logger.Info("Race hook retry stopped: " + ex.Message);
                    return;
                }
            }
        });
    }

    private void CancelWorldLockRetry()
    {
        CancellationTokenSource? cancellation = Interlocked.Exchange(ref worldLockRetryCancellation, null);
        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    private static bool IsTerrariaProcessUnavailable(string message)
    {
        return message.Contains("Terraria.exe must be running", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Race hook is not active", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Terraria process running the Race hook exited", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Terraria is still starting", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("bootstrap=0x80070015", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("payload=10", StringComparison.OrdinalIgnoreCase);
    }

    private async Task UnlockRaceWorldBestEffortAsync()
    {
        CancelWorldLockRetry();
        await worldLockLifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            TerrariaRaceWorldLockResult result = await worldLock.UnlockAsync().ConfigureAwait(false);
            if (!result.Succeeded)
            {
                logger.Info("Race world unlock failed: " + result.Message);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or OperationCanceledException or ObjectDisposedException)
        {
            logger.Error(ex, "Race world unlock failed.");
        }
        finally
        {
            worldLockLifecycleGate.Release();
        }
    }

    private void UnlockRaceWorldForDispose()
    {
        try
        {
            Task unlock = worldLock.UnlockAsync();
            if (!unlock.Wait(DisposeWorldLockTimeout))
            {
                logger.Info("Race world unlock timed out during shutdown; restarting Terraria clears the lock.");
            }
        }
        catch (Exception ex) when (ex is AggregateException or IOException or InvalidOperationException or ObjectDisposedException)
        {
            logger.Error(ex, "Race world unlock failed during shutdown.");
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
        settings.PlayerTemplateCode = preferences.PlayerTemplateCode;
        settings.HostPlayerDifficulty = preferences.PlayerDifficulty;
    }

    private static RaceVoiceSettings CloneVoiceSettings(RaceVoiceSettings? source)
    {
        source ??= new RaceVoiceSettings();
        return new RaceVoiceSettings
        {
            Enabled = source.Enabled,
            VoiceName = source.VoiceName?.Trim() ?? string.Empty,
            SpeedPercent = Math.Clamp(source.SpeedPercent, 50, 200),
            Volume = Math.Clamp(source.Volume, 0, 100)
        };
    }

    private static RaceLeaderboardSettings CloneLeaderboardSettings(RaceLeaderboardSettings source)
    {
        return new RaceLeaderboardSettings
        {
            UseRankColorForMainTimer = source.UseRankColorForMainTimer,
            RankPlayerGap = source.RankPlayerGap,
            PlayerIconGap = source.PlayerIconGap,
            IconTimeGap = source.IconTimeGap,
            RankAlignment = source.RankAlignment,
            PlayerAlignment = source.PlayerAlignment,
            IconAlignment = source.IconAlignment,
            TimeAlignment = source.TimeAlignment,
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
            Bold = source.Bold,
            Italic = source.Italic
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
        string PreferredWorldSource,
        string PlayerTemplateCode,
        string PlayerDifficulty)
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
                },
                normalized.PlayerTemplateCode,
                AutoCreatePlayerDifficulty.Normalize(normalized.HostPlayerDifficulty));
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

    private IProgress<int> CreateJobProgress(string jobKey, IProgress<int>? inner)
    {
        return new Progress<int>(value =>
        {
            int progress = Math.Clamp(value, 0, 100);
            publishSystemEvent(new JobProgressSystemEvent(jobKey, progress));
            inner?.Report(progress);
        });
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
        public abstract long PackageRevision { get; }

        public abstract string RunId { get; }

        public sealed record Reset(RaceProgressResetRequest Request) : RaceProgressUpload
        {
            public override long PackageRevision => Request.PackageRevision;

            public override string RunId => Request.RunId;
        }

        public sealed record Start(RaceRunStartReport Report) : RaceProgressUpload
        {
            public override long PackageRevision => Report.PackageRevision;

            public override string RunId => Report.RunId;
        }

        public sealed record Split(RaceSplitReport Report) : RaceProgressUpload
        {
            public override long PackageRevision => Report.PackageRevision;

            public override string RunId => Report.RunId;
        }

        public static RaceProgressUpload ForReset(RaceProgressResetRequest request)
        {
            return new Reset(request);
        }

        public static RaceProgressUpload ForStart(RaceRunStartReport report)
        {
            return new Start(report);
        }

        public static RaceProgressUpload ForSplit(RaceSplitReport report)
        {
            return new Split(report);
        }
    }

    private void MarkPackageUnavailable(RaceRoomState state, string error)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (session.State?.PackageRevision != state.PackageRevision)
                {
                    return;
                }

                RaceOperationResult<RaceRoomState> result = await session.UpdatePreparationStatusAsync(
                    IsRngControlEnabled(state) ? RacePlayerFileStatus.Ready : RacePlayerFileStatus.Failed,
                    RaceWorldFileStatus.Ready,
                    GetRngControlFailureStatus(state),
                    error).ConfigureAwait(false);
                LogRaceOperationFailure(result, "mark package unavailable");
            }
            catch (Exception ex) when (IsRaceConnectionExitException(ex))
            {
                logger.Info("Race package-unavailable update failed: " + ex.Message);
            }
        });
    }

    private Task<RaceOperationResult<RaceRoomState>> MarkWorldLockStartingAsync(
        CancellationToken cancellationToken = default)
    {
        return session.UpdatePreparationStatusAsync(
            RacePlayerFileStatus.Creating,
            RaceWorldFileStatus.Ready,
            GetRngControlStartingStatus(session.State),
            cancellationToken: cancellationToken);
    }

    private Task<RaceOperationResult<RaceRoomState>> MarkWorldLockResultAsync(
        TerrariaRaceWorldLockResult result,
        CancellationToken cancellationToken = default)
    {
        if (result.Succeeded)
        {
            return session.UpdatePreparationStatusAsync(
                RacePlayerFileStatus.Ready,
                RaceWorldFileStatus.Ready,
                GetRngControlReadyStatus(session.State),
                cancellationToken: cancellationToken);
        }

        bool waitingForTerraria = IsTerrariaProcessUnavailable(result.Message);
        return session.UpdatePreparationStatusAsync(
            waitingForTerraria ? RacePlayerFileStatus.Waiting : RacePlayerFileStatus.Failed,
            RaceWorldFileStatus.Ready,
            waitingForTerraria
                ? GetRngControlIdleStatus(session.State)
                : GetRngControlFailureStatus(session.State),
            waitingForTerraria ? null : result.Message,
            cancellationToken);
    }

    private static bool IsRngControlEnabled(RaceRoomState? state)
    {
        return state?.WorldSettings?.RngControlEnabled != false;
    }

    private static RaceRngControlStatus GetRngControlIdleStatus(RaceRoomState? state)
    {
        return IsRngControlEnabled(state)
            ? RaceRngControlStatus.Closed
            : RaceRngControlStatus.NotEnabled;
    }

    private static RaceRngControlStatus GetRngControlStartingStatus(RaceRoomState? state)
    {
        return IsRngControlEnabled(state)
            ? RaceRngControlStatus.Enabling
            : RaceRngControlStatus.NotEnabled;
    }

    private static RaceRngControlStatus GetRngControlReadyStatus(RaceRoomState? state)
    {
        return IsRngControlEnabled(state)
            ? RaceRngControlStatus.Enabled
            : RaceRngControlStatus.NotEnabled;
    }

    private static RaceRngControlStatus GetRngControlFailureStatus(RaceRoomState? state)
    {
        return IsRngControlEnabled(state)
            ? RaceRngControlStatus.EnableFailed
            : RaceRngControlStatus.NotEnabled;
    }

    private static bool IsPreparationReady(RaceRoomState? state, RacePlayerState? player)
    {
        return player is not null &&
            player.PlayerFileStatus == RacePlayerFileStatus.Ready &&
            player.WorldFileStatus == RaceWorldFileStatus.Ready &&
            player.RngControlStatus == GetRngControlReadyStatus(state);
    }

    private enum RaceProgressSendResult
    {
        Accepted,
        Obsolete
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
