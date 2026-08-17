using System.Drawing;
using System.Globalization;
using TerrariaSplit.Localization;
using TerrariaSplit.Race.Client;
using TerrariaSplit.Race.Contracts;
using TerrariaSplit.Terraria;
using TerrariaSplit.Terraria.Automation;
using TerrariaSplit.UI.Rendering;

namespace TerrariaSplit.UI;

internal sealed partial class RaceShell : IRacePanelShell, IDisposable
{
    private static readonly TimeSpan DisposeRaceSessionTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan DisposeWorldLockTimeout = TimeSpan.FromSeconds(7);
    private static readonly TimeSpan CloseWindowTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RemoteRoomExitTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan HostWorldPreparationTimeout = TimeSpan.FromSeconds(45);
    private const int RaceRandomWorldMaxAttempts = 250_000;
    private const int RaceVerifiedGenerationProgressMaximum = 80;
    private const int RaceDirectGenerationProgressMaximum = 90;
    private readonly RaceClientSession session = new();
    private readonly RaceRouteOverrideController routeOverride;
    private readonly TerrariaRaceWorldGenerationService worldGeneration = new();
    private readonly ITerrariaRaceWorldLockService worldLock;
    private readonly RaceSpeechCoordinator speechCoordinator;
    private readonly SemaphoreSlim worldLockLifecycleGate = new(1, 1);
    private readonly SemaphoreSlim restartLifecycleGate = new(1, 1);
    private readonly object hostWorldPreparationSync = new();
    private readonly IAppLogger logger;
    private readonly RaceWorldFileWorkspace worldFiles;
    private readonly Func<AppSettings> getSettings;
    private readonly Func<AppSettings> getBaseSettings;
    private readonly Func<ApplicationViewState> getViewState;
    private readonly Func<string?> getTerrariaVersion;
    private readonly Action<SettingsRouteOverridePackage> applyRouteOverride;
    private readonly Action clearRouteOverride;
    private readonly RaceSettingsCoordinator settingsCoordinator;
    private readonly Action raceTimerColorChanged;
    private readonly Action resetRaceTimer;
    private readonly Action<SystemEvent> publishSystemEvent;
    private readonly Func<TimeSpan, CancellationToken, Task<bool>> applyRacePenalty;
    private readonly Form owner;
    private readonly RaceProgressTransport progressTransport;
    private Task? completedRunUnlockTask;
    private readonly object progressViewUpdateLock = new();
    private RaceForm? form;
    private RaceLeaderboardForm? leaderboardForm;
    private CancellationTokenSource? worldGenerationCancellation;
    private CancellationTokenSource? memberWorldDownloadCancellation;
    private CancellationTokenSource? hostWorldPreparationCancellation;
    private Task? hostWorldPreparationTask;
    private CancellationTokenSource? worldLockRetryCancellation;
    private CancellationTokenSource? scheduledRaceStartCancellation;
    private CancellationTokenSource? restartCancellation;
    private string? localWorldPath;
    private string? activeWorldRoomCode;
    private string? activeWorldFileName;
    private string? activeWorldRevisionKey;
    private string? pendingWorldFileKey;
    private string? planteraBulbPlanCacheKey;
    private TerrariaPlanteraBulbPlan? planteraBulbPlanCache;
    private RacePanelDraftState draftState;
    private RacePanelPersistentPreferences lastPersistedPreferences;
    private RaceProgressChanged? pendingProgressViewUpdate;
    private int progressViewUpdatePending;
    private int localRoomExitActive;
    private bool mouseClickThrough;
    private bool closingWindows;
    private int raceEnabled;
    private int localPreparationStage;
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
        Action<RaceSettings> updateRaceSettings,
        Action<SystemEvent> publishSystemEvent,
        Func<TimeSpan, CancellationToken, Task<bool>> applyRacePenalty,
        Form owner,
        Action raceTimerColorChanged,
        Action resetRaceTimer,
        ITerrariaRaceWorldLockService? worldLock = null)
    {
        routeOverride = new RaceRouteOverrideController(settingsSnapshots);
        this.logger = logger;
        worldFiles = new RaceWorldFileWorkspace(logger);
        this.getSettings = getSettings;
        this.getBaseSettings = getBaseSettings;
        this.getViewState = getViewState;
        this.getTerrariaVersion = getTerrariaVersion;
        this.applyRouteOverride = applyRouteOverride;
        this.clearRouteOverride = clearRouteOverride;
        settingsCoordinator = new RaceSettingsCoordinator(
            () => getBaseSettings().Race ?? new RaceSettings(),
            updateRaceSettings,
            logger);
        this.publishSystemEvent = publishSystemEvent;
        this.applyRacePenalty = applyRacePenalty;
        this.owner = owner;
        this.raceTimerColorChanged = raceTimerColorChanged;
        this.resetRaceTimer = resetRaceTimer;
        this.worldLock = worldLock ?? new TerrariaRaceWorldLockService();
        progressTransport = new RaceProgressTransport(session, logger.Info, logger.Error);
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
        session.PlayerDied += HandlePlayerDied;
        session.PlayerProgressReset += HandlePlayerProgressReset;
        session.RosterChanged += HandleRosterChanged;
        session.ConnectionStatusChanged += HandleConnectionStatusChanged;
        session.RoomResumeFailed += HandleRoomResumeFailed;
    }

    public RaceRoomState? State => session.State;

    public bool IsRaceEnabled => Volatile.Read(ref raceEnabled) != 0;

    public RaceServerConnectionStatus ServerConnectionStatus => session.ConnectionStatus;

    public string? LocalNickname => session.Nickname;

    public bool IsHostInCurrentRoom => session.State is RaceRoomState state && IsCurrentUserHost(state);

    public bool IsCheatsActive =>
        session.State is RaceRoomState { Status: not RaceRoomStatus.Closed, WorldSettings: RaceWorldSettings worldSettings } &&
        worldSettings.EffectiveCheats.Enabled;

    public CheatFilterIndicatorLevel CheatFilterIndicatorLevel =>
        session.State is RaceRoomState
        {
            Status: not RaceRoomStatus.Closed,
            WorldSettings: RaceWorldSettings worldSettings
        }
            ? CheatFilterIndicator.Resolve(worldSettings.EffectiveCheats)
            : CheatFilterIndicatorLevel.None;

    public string? LocalWorldPath => localWorldPath;

    private RaceLocalPreparationStage LocalPreparationStage =>
        (RaceLocalPreparationStage)Volatile.Read(ref localPreparationStage);

    public RacePanelDraftState DraftState => CreateCurrentDraftState();

    public bool IsInRoom => session.IsInRoom;

    public RaceLeaderboardSettings LeaderboardSettings =>
        CloneLeaderboardSettings(getSettings().Race?.Leaderboard ?? new RaceLeaderboardSettings());

    public RaceBossPenaltySettings BossPenaltySettings =>
        AppSettingsCloner.CloneRaceBossPenaltySettings(
            getSettings().Race?.BossPenalty ?? new RaceBossPenaltySettings());

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

    public void SaveRaceEnabled(bool enabled)
    {
        if (!enabled && IsInRoom)
        {
            return;
        }

        try
        {
            bool changed = Interlocked.Exchange(ref raceEnabled, enabled ? 1 : 0) != (enabled ? 1 : 0);
            if (!enabled)
            {
                CancelWorldGeneration();
                StopInGameMenu();
            }

            if (changed)
            {
                publishSystemEvent(new RaceModeSystemEvent(enabled));
            }

            form?.UpdateRaceState(session.State);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Race enabled setting save failed.");
        }
    }

    public void SaveLeaderboardSettings(RaceLeaderboardSettings leaderboardSettings)
    {
        RaceLeaderboardSettings nextLeaderboard = CloneLeaderboardSettings(leaderboardSettings);
        if (settingsCoordinator.Update(
                "Race leaderboard settings update",
                next => next.Leaderboard = CloneLeaderboardSettings(nextLeaderboard)))
        {
            leaderboardForm?.ApplySettings();
            raceTimerColorChanged();
        }
    }

    private void SaveLeaderboardPosition(Point location)
    {
        RaceLeaderboardSettings current =
            settingsCoordinator.CreateSnapshot().Leaderboard ?? new RaceLeaderboardSettings();
        if (current.WindowPositionX == location.X && current.WindowPositionY == location.Y)
        {
            return;
        }

        settingsCoordinator.Update(
            "Race leaderboard position update",
            next =>
            {
                next.Leaderboard ??= new RaceLeaderboardSettings();
                next.Leaderboard.WindowPositionX = location.X;
                next.Leaderboard.WindowPositionY = location.Y;
            });
    }

    public void SaveVoiceSettings(RaceVoiceSettings voiceSettings)
    {
        RaceVoiceSettings nextVoice = CloneVoiceSettings(voiceSettings);
        if (settingsCoordinator.Update(
                "Race voice settings update",
                next => next.Voice = CloneVoiceSettings(nextVoice)))
        {
            speechCoordinator.ApplySettings(nextVoice);
        }
    }

    public void SaveBossPenaltySettings(RaceBossPenaltySettings bossPenaltySettings)
    {
        RaceBossPenaltySettings nextPenalty =
            AppSettingsCloner.CloneRaceBossPenaltySettings(bossPenaltySettings);
        settingsCoordinator.Update(
            "Race boss penalty settings update",
            next => next.BossPenalty =
                AppSettingsCloner.CloneRaceBossPenaltySettings(nextPenalty));
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
        StopInGameMenu();
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
        roomCode = roomCode.Trim();
        SaveDraftState(draftState with
        {
            ServerUrl = serverUrl,
            Nickname = nickname,
            RoomCode = roomCode,
            Role = RacePanelRole.Member
        });

        if (!RaceRoomCodeRules.IsValid(roomCode))
        {
            return RaceOperationResult<RaceRoomState>.Failure(
                "invalid_request",
                Localize("Room code must be four digits."));
        }

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
            await CancelRestartAndWaitAsync().ConfigureAwait(false);
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

    public async Task<RacePanelWorldGenerationResult> GenerateRandomWorldAsync(
        RaceWorldSettings worldSettings,
        IProgress<int>? progress = null)
    {
        progress = CreateClampedProgress(progress);
        SaveDraftState(draftState with { Role = RacePanelRole.Host });
        localWorldPath = null;

        if (!RaceWorldSettingsFactory.HasActiveFilters(worldSettings))
        {
            string seedText = CreateRandomSeedText();
            return await GenerateWorldFromSeedAsync(
                worldSettings,
                new RaceSeedAssignment(seedText, RaceSeedSource.HostGenerated),
                progress,
                RaceDirectGenerationProgressMaximum);
        }

        return await GenerateRandomWorldUntilVerifiedAsync(
            worldSettings,
            RaceRandomWorldMaxAttempts,
            progress);
    }

    public async Task<RacePanelWorldGenerationResult> GenerateCustomSeedWorldAsync(
        RaceWorldSettings worldSettings,
        string seedText,
        IProgress<int>? progress = null)
    {
        progress = CreateClampedProgress(progress);
        localWorldPath = null;
        if (string.IsNullOrWhiteSpace(seedText))
        {
            logger.Info("Race custom seed world generation ignored because seed is empty.");
            return RacePanelWorldGenerationResult.Failure(Localize("A seed is required."));
        }

        SaveDraftState(draftState with { SeedText = seedText.Trim() });

        return await GenerateWorldFromSeedAsync(
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
        progress = CreateClampedProgress(progress);
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
                uploadWorldPath = worldFiles.PrepareForUpload(
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
                uploadWorldPath = worldFiles.PrepareForUpload(
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
                StartHostWorldPreparation(state, uploadWorldPath);
            }
        }
        else if (!string.Equals(uploadWorldPath, normalizedWorldPath, StringComparison.OrdinalIgnoreCase))
        {
            worldFiles.Delete(uploadWorldPath);
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
        CancelWorldGeneration();
        return Task.CompletedTask;
    }

    private void CancelWorldGeneration()
    {
        CancellationTokenSource? cancellation =
            Volatile.Read(ref worldGenerationCancellation);
        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public Task DiscardLocalWorldAsync(string worldPath)
    {
        if (string.IsNullOrWhiteSpace(worldPath))
        {
            return Task.CompletedTask;
        }

        worldFiles.Delete(worldPath);
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
        await restartLifecycleGate.WaitAsync().ConfigureAwait(false);
        using var cancellation = new CancellationTokenSource();
        Volatile.Write(ref restartCancellation, cancellation);
        Interlocked.Exchange(ref restartActive, 1);
        CancellationToken cancellationToken = cancellation.Token;

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
                GetRngControlStartingStatus(restartState),
                cancellationToken: cancellationToken);
            ApplyOperationState(notReady);
            if (!notReady.Succeeded)
            {
                return notReady;
            }

            Task? pendingCompletedRunUnlock = Volatile.Read(ref completedRunUnlockTask);
            if (pendingCompletedRunUnlock is not null)
            {
                await pendingCompletedRunUnlock.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            TerrariaRaceWorldLockResult prepared;
            await worldLockLifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                prepared = await worldLock.PrepareRestartAsync(cancellationToken).ConfigureAwait(false);
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
            await DownloadWorldForStateAsync(restartState, force: true, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            RaceRoomState? current = session.State;
            if (current is null || current.PackageRevision != restartState.PackageRevision)
            {
                return RaceOperationResult<RaceRoomState>.Failure(
                    "restart_failed",
                    "The Race package changed while the local reset was running.");
            }

            TerrariaRaceWorldLockResult returnedToMenu;
            await worldLockLifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                returnedToMenu = await worldLock.ReturnToMainMenuAsync(cancellationToken).ConfigureAwait(false);
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
                worldFiles.Delete(previousWorldPath);
            }

            return RaceOperationResult<RaceRoomState>.Success(current);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return RaceOperationResult<RaceRoomState>.Failure(
                "restart_canceled",
                "The local Race restart was canceled.");
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
            Interlocked.CompareExchange(ref restartCancellation, null, cancellation);
            Interlocked.Exchange(ref restartActive, 0);
            restartLifecycleGate.Release();
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

        string serverFileName = worldFiles.NormalizeFileName(worldFile.FileName);
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

        string destinationPath = worldFiles.CreateDownloadPath(state);
        ReportLocalPreparationStage(state, RaceLocalPreparationStage.DownloadWorld);
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
            worldFiles.Delete(download.WorldPath);
            return;
        }

        localWorldPath = download.WorldPath;
        SaveDraftState(draftState with { LocalWorldPath = download.WorldPath });
        RememberObtainedWorldFile(state.RoomCode, download.WorldFile, resetTimer: true);
        _ = await MarkWorldLockStartingAsync(cancellationToken);
        TerrariaRaceWorldLockResult worldLockResult = await LockRaceWorldAsync(
            download.WorldPath,
            state.Determinism,
            cancellationToken,
            preparationState: state);
        RaceOperationResult<RaceRoomState> ready = await MarkWorldLockResultAsync(
            worldLockResult,
            cancellationToken,
            preparationState: state);
        ApplyOperationState(ready);
        LogRaceOperationFailure(ready, "mark world ready");
    }

    private async Task<RacePanelWorldGenerationResult> GenerateWorldFromSeedAsync(
        RaceWorldSettings worldSettings,
        RaceSeedAssignment seed,
        IProgress<int>? progress,
        int progressMaximum,
        bool seedFilterAlreadyAccepted = false,
        bool reuseWorldGenerationCancellation = false)
    {
        progress?.Report(0);
        RaceLocalWorldGenerationAttempt attempt = await TryGenerateWorldFromSeedAsync(
            worldSettings,
            seed,
            progress,
            progressMaximum,
            seedFilterAlreadyAccepted,
            reuseWorldGenerationCancellation);
        if (!attempt.Succeeded)
        {
            logger.Info("Race world generation failed: " + attempt.Message);
            return RacePanelWorldGenerationResult.Failure(attempt.Message);
        }

        progress?.Report(90);
        return RacePanelWorldGenerationResult.Success();
    }

    private async Task<RacePanelWorldGenerationResult> GenerateRandomWorldUntilVerifiedAsync(
        RaceWorldSettings worldSettings,
        int maxAttempts,
        IProgress<int>? progress)
    {
        int attempts = Math.Clamp(maxAttempts <= 0 ? RaceRandomWorldMaxAttempts : maxAttempts, 1, 5_000_000);
        int concurrency = TerrariaRaceWorldGenerationService.CalculateSeedFilterConcurrency(
            Environment.ProcessorCount);
        CancellationToken cancellationToken = ResetWorldGenerationCancellation();
        AutoCreateWorldSettings filterSettings =
            RaceWorldSettingsFactory.ToAutoCreateWorldSettings(worldSettings);
        logger.Info(
            $"Race seed pre-filter starting with {concurrency} workers " +
            $"({Environment.ProcessorCount} logical processors, 80% policy).");
        int evaluatedSeeds = 0;
        int consecutiveCandidateFailures = 0;
        string lastFailure = string.Empty;
        while (evaluatedSeeds < attempts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int batchCount = Math.Min(concurrency, attempts - evaluatedSeeds);
            string[] seedTexts = Enumerable.Range(0, batchCount)
                .Select(_ => CreateRandomSeedText())
                .ToArray();
            progress?.Report(0);
            TerrariaRaceSeedFilterBatchResult batch =
                await worldGeneration.FilterSeedBatchAsync(
                    filterSettings,
                    seedTexts,
                    cancellationToken,
                    consecutiveCandidateFailures);
            consecutiveCandidateFailures =
                batch.ConsecutiveCandidateFailures;
            int batchStartAttempt = evaluatedSeeds + 1;
            evaluatedSeeds += batch.EvaluatedCount;
            if (batch.HasFatalError)
            {
                logger.Info("Race seed pre-filter failed: " + batch.FatalError);
                return RacePanelWorldGenerationResult.Failure(batch.FatalError);
            }

            if (batch.AcceptedCandidates.Count == 0)
            {
                lastFailure = batch.Detail;
                continue;
            }

            foreach (TerrariaRaceSeedFilterCandidate candidate in batch.AcceptedCandidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int generationAttempt = batchStartAttempt + candidate.BatchIndex;
                var seed = new RaceSeedAssignment(
                    candidate.SeedText,
                    RaceSeedSource.HostGenerated);
                RaceLocalWorldGenerationAttempt worldAttempt =
                    await TryGenerateWorldFromSeedAsync(
                        worldSettings,
                        seed,
                        progress,
                        RaceVerifiedGenerationProgressMaximum,
                        seedFilterAlreadyAccepted: true,
                        reuseWorldGenerationCancellation: true);
                if (worldAttempt.Succeeded)
                {
                    logger.Info(
                        $"Race world generated after {generationAttempt} parallel pre-filter attempts: " +
                        worldAttempt.WorldPath);
                    progress?.Report(90);
                    return RacePanelWorldGenerationResult.Success();
                }

                if (!worldAttempt.Retryable)
                {
                    logger.Info("Race world generation failed: " + worldAttempt.Message);
                    return RacePanelWorldGenerationResult.Failure(worldAttempt.Message);
                }

                lastFailure = worldAttempt.Message;
            }
        }

        string message = string.IsNullOrWhiteSpace(lastFailure)
            ? $"Race found no verified world after {attempts} generated worlds."
            : $"Race found no verified world after {attempts} generated worlds. Last verification failure: {lastFailure}";
        logger.Info(message);
        return RacePanelWorldGenerationResult.Failure(message);
    }

    private static string CreateRandomSeedText()
    {
        return Random.Shared.Next(0, int.MaxValue).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<RaceLocalWorldGenerationAttempt> TryGenerateWorldFromSeedAsync(
        RaceWorldSettings worldSettings,
        RaceSeedAssignment seed,
        IProgress<int>? progress,
        int progressMaximum,
        bool seedFilterAlreadyAccepted = false,
        bool reuseWorldGenerationCancellation = false)
    {
        CancellationToken cancellationToken = reuseWorldGenerationCancellation
            ? worldGenerationCancellation?.Token ??
                throw new InvalidOperationException(
                    "Race world generation cancellation was not initialized.")
            : ResetWorldGenerationCancellation();
        string worldName = worldFiles.CreateWorldStem(DateTimeOffset.Now);
        try
        {
            TerrariaRaceWorldGenerationResult result = await worldGeneration.GenerateAndInstallAsync(
                RaceWorldSettingsFactory.ToAutoCreateWorldSettings(worldSettings),
                seed.SeedText,
                worldName,
                getSettings().General.Language,
                cancellationToken,
                progress,
                progressMaximum,
                seedFilterAlreadyAccepted);
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
                : RaceLocalWorldGenerationAttempt.Failure(result.Message, result.Retryable);
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

    private CancellationToken ResetWorldGenerationCancellation()
    {
        worldGenerationCancellation?.Cancel();
        worldGenerationCancellation?.Dispose();
        worldGenerationCancellation = new CancellationTokenSource();
        return worldGenerationCancellation.Token;
    }

    public async Task LeaveAsync()
    {
        if (Interlocked.Exchange(ref localRoomExitActive, 1) != 0)
        {
            return;
        }

        try
        {
            await CancelRestartAndWaitAsync().ConfigureAwait(false);
            worldGenerationCancellation?.Cancel();
            CancelMemberWorldDownload();
            await ObserveCanceledPreparationAsync(CancelHostWorldPreparation()).ConfigureAwait(false);
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
        await CancelRestartAndWaitAsync().ConfigureAwait(false);
        worldGenerationCancellation?.Cancel();
        CancelMemberWorldDownload();
        await ObserveCanceledPreparationAsync(CancelHostWorldPreparation()).ConfigureAwait(false);
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
        progressTransport.Clear();
        SetLocalPreparationStage(RaceLocalPreparationStage.None);
        SaveDraftState(draftState with
        {
            RoomCode = string.Empty,
            SeedText = string.Empty,
            LocalWorldPath = string.Empty
        });
        _ = TransitionInGameMenu(RaceInGameTransition.RoomExited);
        publishSystemEvent(new RaceRosterSystemEvent(roomCode, IsInRoom: false));
    }

    private async Task CancelRestartAndWaitAsync()
    {
        CancellationTokenSource? cancellation = Volatile.Read(ref restartCancellation);
        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        await restartLifecycleGate.WaitAsync().ConfigureAwait(false);
        restartLifecycleGate.Release();
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
            progressTransport.Clear();
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
                progressTransport.Clear();
                return;
            }

            if (!reset.Succeeded)
            {
                logger.Info("Race determinism reset failed: " + reset.Message);
                MarkPackageUnavailable(state, reset.Message);
                progressTransport.Clear();
                return;
            }
        }

        progressTransport.Reset(state, session.Nickname ?? string.Empty);
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

        if (progressTransport.RequiresReset(state))
        {
            ResetReportedProgress();
        }

        ApplicationViewState viewState = getViewState();
        progressTransport.QueueReports(
            session.RoomCode,
            session.Nickname,
            viewState.DisplayStatuses,
            runStarted);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        StopInGameMenu();
        Task hostWorldPreparation = CancelHostWorldPreparation();
        try
        {
            hostWorldPreparation.Wait(DisposeWorldLockTimeout);
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static item => item is OperationCanceledException))
        {
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Race host world preparation shutdown failed.");
        }
        UnlockRaceWorldForDispose();
        CloseWindows();
        worldGenerationCancellation?.Cancel();
        worldGenerationCancellation?.Dispose();
        CancelMemberWorldDownload();
        CancelWorldLockRetry();
        CancelScheduledRaceStart();
        progressTransport.Dispose();
        worldGeneration.Dispose();
        worldLock.HealthFailed -= HandleWorldLockHealthFailed;
        if (worldLock is IDisposable disposableWorldLock)
        {
            disposableWorldLock.Dispose();
        }
        session.PackageChanged -= HandlePackageChanged;
        session.ProgressChanged -= HandleProgressChanged;
        session.GroupCompleted -= HandleGroupCompleted;
        session.PlayerDied -= HandlePlayerDied;
        session.PlayerProgressReset -= HandlePlayerProgressReset;
        session.RosterChanged -= HandleRosterChanged;
        session.ConnectionStatusChanged -= HandleConnectionStatusChanged;
        session.RoomResumeFailed -= HandleRoomResumeFailed;
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

        try
        {
            applyRouteOverride(package);
            if (!routeOverride.MarkApplied(package))
            {
                logger.Info("Race route override ignored: " + RaceRouteOverrideController.AlreadyAppliedDetail);
                clearRouteOverride();
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            try
            {
                clearRouteOverride();
            }
            finally
            {
                routeOverride.Clear();
            }

            logger.Error(ex, "Race route override application failed.");
            return false;
        }
    }

    private void RestoreRouteOverride()
    {
        if (routeOverride.HasOverride)
        {
            try
            {
                clearRouteOverride();
            }
            finally
            {
                routeOverride.Clear();
            }
        }
    }

    private void CancelMemberWorldDownload()
    {
        memberWorldDownloadCancellation?.Cancel();
        memberWorldDownloadCancellation?.Dispose();
        memberWorldDownloadCancellation = null;
        pendingWorldFileKey = null;
    }

    private void StartHostWorldPreparation(RaceRoomState state, string worldPath)
    {
        CancelWorldLockRetry();
        Task previous = CancelHostWorldPreparation();
        var cancellation = new CancellationTokenSource();
        Task task;
        lock (hostWorldPreparationSync)
        {
            task = Task.Run(async () =>
            {
                await ObserveCanceledPreparationAsync(previous).ConfigureAwait(false);
                await PrepareHostWorldInBackgroundAsync(state, worldPath, cancellation).ConfigureAwait(false);
            });
            hostWorldPreparationCancellation = cancellation;
            hostWorldPreparationTask = task;
        }
    }

    private async Task PrepareHostWorldInBackgroundAsync(
        RaceRoomState state,
        string worldPath,
        CancellationTokenSource lifetime)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        timeout.CancelAfter(HostWorldPreparationTimeout);
        try
        {
            if (!IsCurrentPackage(state))
            {
                return;
            }

            _ = await MarkWorldLockStartingAsync(
                timeout.Token,
                state.PackageRevision).ConfigureAwait(false);
            TerrariaRaceWorldLockResult worldLockResult = await LockRaceWorldAsync(
                worldPath,
                state.Determinism,
                timeout.Token,
                preparationState: state).ConfigureAwait(false);
            if (!IsCurrentPackage(state))
            {
                return;
            }

            RaceOperationResult<RaceRoomState> preparation = await MarkWorldLockResultAsync(
                worldLockResult,
                timeout.Token,
                state.PackageRevision,
                state).ConfigureAwait(false);
            LogRaceOperationFailure(preparation, "prepare host Race world");
            if (!worldLockResult.Succeeded && IsTerrariaProcessUnavailable(worldLockResult.Message))
            {
                logger.Info("Race world uploaded; waiting for Terraria to start before installing the hook.");
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch (OperationCanceledException)
        {
            await MarkHostWorldPreparationTimedOutAsync(state).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or HttpRequestException or TimeoutException)
        {
            logger.Error(ex, "Race host world preparation failed.");
            if (IsCurrentPackage(state))
            {
                RaceOperationResult<RaceRoomState> failure = await session.UpdatePreparationStatusAsync(
                    RacePlayerFileStatus.Failed,
                    RaceWorldFileStatus.Ready,
                    GetRngControlFailureStatus(state),
                    ex.Message,
                    packageRevision: state.PackageRevision).ConfigureAwait(false);
                LogRaceOperationFailure(failure, "mark host Race world preparation failed");
            }
        }
        finally
        {
            lock (hostWorldPreparationSync)
            {
                if (ReferenceEquals(hostWorldPreparationCancellation, lifetime))
                {
                    hostWorldPreparationCancellation = null;
                    hostWorldPreparationTask = null;
                }
            }

            lifetime.Dispose();
        }
    }

    private async Task MarkHostWorldPreparationTimedOutAsync(RaceRoomState state)
    {
        if (!IsCurrentPackage(state))
        {
            return;
        }

        const string message = "Preparing the local Race environment timed out.";
        RaceOperationResult<RaceRoomState> failure = await session.UpdatePreparationStatusAsync(
            RacePlayerFileStatus.Failed,
            RaceWorldFileStatus.Ready,
            GetRngControlFailureStatus(state),
            message,
            packageRevision: state.PackageRevision).ConfigureAwait(false);
        LogRaceOperationFailure(failure, "mark host Race world preparation timed out");
    }

    private bool IsCurrentPackage(RaceRoomState state)
    {
        RaceRoomState? current = session.State;
        return current is not null &&
            current.Status != RaceRoomStatus.Closed &&
            current.PackageRevision == state.PackageRevision &&
            string.Equals(current.RoomCode, state.RoomCode, StringComparison.OrdinalIgnoreCase);
    }

    private void ReportLocalPreparationStage(
        RaceRoomState? state,
        RaceLocalPreparationStage stage)
    {
        if (state is not null && IsCurrentPackage(state))
        {
            SetLocalPreparationStage(stage);
        }
    }

    private void SetLocalPreparationStage(RaceLocalPreparationStage stage)
    {
        if (Interlocked.Exchange(ref localPreparationStage, (int)stage) == (int)stage)
        {
            return;
        }

        MarkInGameMenuDirty();
    }

    private void SyncLocalPreparationCompletion(RaceRoomState state)
    {
        string localNickname = session.Nickname ?? draftState.Nickname;
        RacePlayerState? localPlayer = state.Players.FirstOrDefault(player =>
            string.Equals(player.Nickname, localNickname, StringComparison.OrdinalIgnoreCase));
        if (localPlayer is null || !IsPreparationReady(state, localPlayer))
        {
            return;
        }

        SetLocalPreparationStage(localPlayer.IsHost || localPlayer.IsReady
            ? RaceLocalPreparationStage.Ready
            : RaceLocalPreparationStage.WaitForManualReady);
    }

    private static RaceLocalPreparationStage MapLocalPreparationStage(
        TerrariaRaceWorldLockPreparationStage stage)
    {
        return stage switch
        {
            TerrariaRaceWorldLockPreparationStage.WaitForGame => RaceLocalPreparationStage.WaitForGame,
            TerrariaRaceWorldLockPreparationStage.PrepareMemoryControl => RaceLocalPreparationStage.PrepareMemoryControl,
            TerrariaRaceWorldLockPreparationStage.CreateRacePlayer => RaceLocalPreparationStage.CreateRacePlayer,
            TerrariaRaceWorldLockPreparationStage.AlmostReady => RaceLocalPreparationStage.AlmostReady,
            _ => RaceLocalPreparationStage.AlmostReady
        };
    }

    private Task CancelHostWorldPreparation()
    {
        CancellationTokenSource? cancellation;
        Task? task;
        lock (hostWorldPreparationSync)
        {
            cancellation = hostWorldPreparationCancellation;
            task = hostWorldPreparationTask;
            hostWorldPreparationCancellation = null;
            hostWorldPreparationTask = null;
        }

        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        return task ?? Task.CompletedTask;
    }

    private static async Task ObserveCanceledPreparationAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            _ = task.Exception;
        }
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

        if (update.Kind == RacePackageChangeKind.Restarted)
        {
            _ = TransitionInGameMenu(RaceInGameTransition.RoomPrepared);
        }

        speechCoordinator.Clear();
        ApplyPackageToViews(update);
        MarkInGameMenuDirty();
    }

    private void HandleProgressChanged(object? sender, RaceProgressChanged update)
    {
        QueueProgressViewUpdate(update);
        MarkInGameMenuDirty();
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

        string displayName = string.IsNullOrWhiteSpace(split.DisplayName) ? split.Id : split.DisplayName;
        bool chinese = LanguageNames.IsChinese(getSettings().General.Language);
        speechCoordinator.Enqueue(new RaceSpeechQueueItem(
            update,
            displayName,
            chinese));
        ShowRaceGameMessage(
            RaceSpeechTextFormatter.FormatGameMessage(
                update.Nickname,
                displayName,
                update.ElapsedMilliseconds,
                chinese),
            TerrariaRaceMessageKind.SplitCompleted);
    }

    private void HandlePlayerDied(object? sender, RacePlayerDied update)
    {
        if (!IsRaceEnabled ||
            string.Equals(update.Nickname, session.Nickname, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string deathMessage = RaceDeathMessageRules.Normalize(update.DeathMessage);
        bool chinese = LanguageNames.IsChinese(getSettings().General.Language);
        ShowRaceGameMessage(
            string.IsNullOrWhiteSpace(deathMessage)
                ? chinese ? $"{update.Nickname} 死亡了" : $"{update.Nickname} died"
                : deathMessage,
            TerrariaRaceMessageKind.PlayerDied);
    }

    private void ShowRaceGameMessage(string message, TerrariaRaceMessageKind kind)
    {
        _ = ShowRaceGameMessageAsync(message, kind);
    }

    private async Task ShowRaceGameMessageAsync(string message, TerrariaRaceMessageKind kind)
    {
        try
        {
            TerrariaRaceWorldLockResult result = await worldLock.ShowInGameMessageAsync(
                message,
                kind).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                logger.Info("Race in-game message was not shown: " + result.Message);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Race in-game message failed.");
        }
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
        MarkInGameMenuDirty();
    }

    private void HandleConnectionStatusChanged(object? sender, EventArgs e)
    {
        if (DispatchOwnerThreadIfRequired(() => HandleConnectionStatusChanged(sender, e)))
        {
            return;
        }

        form?.UpdateRaceState(session.State);
        MarkInGameMenuDirty();
    }

    private void HandleRoomResumeFailed(object? sender, RaceRoomResumeFailed failure)
    {
        if (DispatchOwnerThreadIfRequired(() => HandleRoomResumeFailed(sender, failure)))
        {
            return;
        }

        logger.Info(
            $"Race room resume failed. Room={failure.RoomCode} " +
            $"Error={failure.ErrorCode} Message={failure.Message}.");
        form?.UpdateRaceState(session.State);
        MarkInGameMenuDirty();
        BeginClearLocalRoomStateAfterResumeFailure();
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
        SyncLocalPreparationCompletion(state);
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

        // The preparation page is only for distributing the package and
        // starting the run. Once the synchronized start is accepted, future
        // returns to Terraria's menu must show the Race room home instead.
        _ = TransitionInGameMenu(RaceInGameTransition.RaceStarted);

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

        string serverFileName = worldFiles.NormalizeFileName(worldFile.FileName);
        string worldFileKey = worldFiles.CreateRevisionKey(state.RoomCode, worldFile);
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
                TerrariaRaceWorldLockResult worldLockResult = await LockRaceWorldAsync(
                    localWorldPath,
                    state.Determinism,
                    preparationState: state);
                RaceOperationResult<RaceRoomState> ready = await MarkWorldLockResultAsync(
                    worldLockResult,
                    preparationState: state);
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
        BeginLocalRoomCleanupAfterRemoteExit(disconnectSession: true);
    }

    private void BeginClearLocalRoomStateAfterResumeFailure()
    {
        BeginLocalRoomCleanupAfterRemoteExit(disconnectSession: false);
    }

    private void BeginLocalRoomCleanupAfterRemoteExit(bool disconnectSession)
    {
        if (Interlocked.Exchange(ref localRoomExitActive, 1) != 0)
        {
            return;
        }

        worldGenerationCancellation?.Cancel();
        CancelMemberWorldDownload();
        Task hostWorldPreparation = CancelHostWorldPreparation();
        _ = Task.Run(async () =>
        {
            await CancelRestartAndWaitAsync().ConfigureAwait(false);
            await ObserveCanceledPreparationAsync(hostWorldPreparation).ConfigureAwait(false);
            if (disconnectSession)
            {
                try
                {
                    await session.LeaveLocalAsync(DisposeRaceSessionTimeout).ConfigureAwait(false);
                }
                catch (Exception ex) when (IsRaceConnectionExitException(ex))
                {
                    logger.Info("Race local room cleanup after remote exit failed: " + ex.Message);
                }
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
        string serverFileName = worldFiles.NormalizeFileName(worldFile.FileName);
        string revisionKey = worldFiles.CreateRevisionKey(roomCode, worldFile);
        return string.Equals(activeWorldRoomCode, roomCode, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(activeWorldFileName, serverFileName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(activeWorldRevisionKey, revisionKey, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(localWorldPath) &&
            worldFiles.Exists(localWorldPath);
    }

    private void RememberObtainedWorldFile(string roomCode, RaceWorldFileInfo? worldFile, bool resetTimer)
    {
        string serverFileName = worldFiles.NormalizeFileName(worldFile?.FileName);
        if (worldFile is null || string.IsNullOrWhiteSpace(serverFileName))
        {
            return;
        }

        string revisionKey = worldFiles.CreateRevisionKey(roomCode, worldFile);
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
        bool scheduleRetry = true,
        RaceRoomState? preparationState = null)
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

        ReportLocalPreparationStage(preparationState, RaceLocalPreparationStage.ValidateWorld);
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

        ReportLocalPreparationStage(preparationState, RaceLocalPreparationStage.AnalyzeWorld);
        TerrariaPlanteraBulbPlan planteraBulbPlan = await GetPlanteraBulbPlanAsync(
            worldPath,
            identity,
            determinism,
            cancellationToken).ConfigureAwait(false);

        await worldLockLifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        TerrariaRaceWorldLockResult result;
        try
        {
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
                    IsRaceEntryAllowed(session.State),
                    session.State?.WorldSettings?.BossFailurePenaltyEnabled != false,
                    RaceBossPenaltyConfiguration.NormalizeOrDefault(
                        session.State?.WorldSettings?.BossPenaltySchedule)),
                new TerrariaRaceInitialPlayerConfiguration(
                    session.Nickname ?? draftState.Nickname,
                    draftState.PlayerTemplateCode,
                    session.State?.WorldSettings is RaceWorldSettings roomWorldSettings
                        ? RaceWorldSettingsFactory.ToPlayerDifficultyForWorld(roomWorldSettings)
                        : AutoCreatePlayerDifficulty.Softcore),
                Localize("Only the assigned Race world and player can be used until the run is completed."),
                cancellationToken,
                stage => ReportLocalPreparationStage(
                    preparationState,
                    MapLocalPreparationStage(stage))).ConfigureAwait(false);
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
        Task<TerrariaPlanteraBulbPlan> planning = Task.Run(
            () => new TerrariaPlanteraBulbPlanner().Create(
                fullPath,
                identity.WorldId,
                identity.UniqueId,
                entropySeed,
                determinism.ProtocolVersion));
        TerrariaPlanteraBulbPlan plan;
        try
        {
            plan = await planning.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _ = planning.ContinueWith(
                static task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            throw;
        }
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
                        scheduleRetry: false,
                        preparationState: state).ConfigureAwait(false);
                    if (!retry.Succeeded)
                    {
                        if (IsTerrariaProcessUnavailable(retry.Message))
                        {
                            continue;
                        }

                        MarkPackageUnavailable(state, retry.Message);
                        return;
                    }

                    state = session.State;
                    if (state is null ||
                        state.PackageRevision != packageRevision ||
                        !string.Equals(localWorldPath, worldPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    RaceOperationResult<RaceRoomState> ready = await MarkWorldLockResultAsync(
                        retry,
                        cancellation.Token,
                        packageRevision,
                        state).ConfigureAwait(false);
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
            message.Contains("Race menu is not attached", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Terraria process running the Race hook exited", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("pipe", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("管道", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("connection reset", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("not connected", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Terraria is still starting", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("bootstrap=0x80070015", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("payload=10", StringComparison.OrdinalIgnoreCase);
    }

    private async Task UnlockRaceWorldBestEffortAsync()
    {
        CancelWorldLockRetry();
        bool gateAcquired = false;
        using var timeout = new CancellationTokenSource(DisposeWorldLockTimeout);
        try
        {
            await worldLockLifecycleGate.WaitAsync(timeout.Token).ConfigureAwait(false);
            gateAcquired = true;
            TerrariaRaceWorldLockResult result = await worldLock.UnlockAsync().ConfigureAwait(false);
            if (!result.Succeeded)
            {
                logger.Info("Race world unlock failed: " + result.Message);
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            logger.Info("Race world unlock timed out; local room cleanup will continue.");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or OperationCanceledException or ObjectDisposedException)
        {
            logger.Error(ex, "Race world unlock failed.");
        }
        finally
        {
            if (gateAcquired)
            {
                worldLockLifecycleGate.Release();
            }
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
            if (leaderboardForm.InvokeRequired)
            {
                try
                {
                    leaderboardForm.BeginInvoke(new Action(EnsureLeaderboardForm));
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }

                return;
            }

            leaderboardForm.Show();
            ApplyLeaderboardTopMost();
            leaderboardForm.ApplyMouseClickThrough(mouseClickThrough);
            return;
        }

        leaderboardForm = new RaceLeaderboardForm(
            getSettings,
            Localize,
            GetLeaderboardLocalNickname,
            SaveLeaderboardPosition);
        leaderboardForm.FormClosed += (_, _) =>
        {
            leaderboardForm = null;
        };
        leaderboardForm.Show();
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
        RaceLeaderboardForm? target = leaderboardForm;
        if (target is not { IsDisposed: false } || !target.IsHandleCreated)
        {
            return;
        }

        if (target.InvokeRequired)
        {
            try
            {
                target.BeginInvoke(new Action(() =>
                {
                    if (ReferenceEquals(leaderboardForm, target))
                    {
                        ApplyLeaderboardTopMost();
                    }
                }));
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return;
        }

        WindowTopMostSync.Apply(getSettings().General.AlwaysOnTop, target.Handle);
    }

    private void PersistRacePreferences(RacePanelDraftState state)
    {
        RacePanelPersistentPreferences preferences = RacePanelPersistentPreferences.FromDraft(state);
        if (preferences == lastPersistedPreferences)
        {
            return;
        }

        if (settingsCoordinator.Update(
                "Race preferences update",
                next => ApplyPreferencesToSettings(next, preferences)))
        {
            lastPersistedPreferences = preferences;
        }
    }

    private static void ApplyPreferencesToSettings(
        RaceSettings settings,
        RacePanelPersistentPreferences preferences)
    {
        settings.ServerUrl = preferences.ServerUrl;
        settings.Nickname = preferences.Nickname;
        settings.LastRoomCode = preferences.RoomCode;
        settings.PreferredRole = preferences.PreferredRole;
        settings.PreferredWorldSource = preferences.PreferredWorldSource;
        settings.PlayerTemplateCode = preferences.PlayerTemplateCode;
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
            WindowPositionX = source.WindowPositionX,
            WindowPositionY = source.WindowPositionY,
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
        string RoomCode,
        string PreferredRole,
        string PreferredWorldSource,
        string PlayerTemplateCode)
    {
        public static RacePanelPersistentPreferences FromDraft(RacePanelDraftState draft)
        {
            RacePanelDraftState normalized = draft.Normalize();
            return new RacePanelPersistentPreferences(
                string.IsNullOrWhiteSpace(normalized.ServerUrl)
                    ? new RaceSettings().ServerUrl
                    : normalized.ServerUrl,
                normalized.Nickname,
                normalized.RoomCode,
                normalized.Role == RacePanelRole.Member ? RacePreferredRole.Member : RacePreferredRole.Host,
                normalized.WorldSource switch
                {
                    RacePanelWorldSource.CustomSeed => RacePreferredWorldSource.CustomSeed,
                    _ => RacePreferredWorldSource.Random
                },
                normalized.PlayerTemplateCode);
        }
    }

    private static IProgress<int> CreateClampedProgress(IProgress<int>? inner)
    {
        return new Progress<int>(value =>
        {
            int progress = Math.Clamp(value, 0, 100);
            inner?.Report(progress);
        });
    }

    private bool IsCurrentUserHost(RaceRoomState state)
    {
        return state.Players.Any(player =>
            player.IsHost &&
            string.Equals(player.Nickname, session.Nickname, StringComparison.OrdinalIgnoreCase));
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
        CancellationToken cancellationToken = default,
        long? packageRevision = null)
    {
        return session.UpdatePreparationStatusAsync(
            RacePlayerFileStatus.Creating,
            RaceWorldFileStatus.Ready,
            GetRngControlStartingStatus(session.State),
            cancellationToken: cancellationToken,
            packageRevision: packageRevision);
    }

    private async Task<RaceOperationResult<RaceRoomState>> MarkWorldLockResultAsync(
        TerrariaRaceWorldLockResult result,
        CancellationToken cancellationToken = default,
        long? packageRevision = null,
        RaceRoomState? preparationState = null)
    {
        Task<RaceOperationResult<RaceRoomState>> update;
        if (result.Succeeded)
        {
            ReportLocalPreparationStage(preparationState, RaceLocalPreparationStage.ConnectToServer);
            update = session.UpdatePreparationStatusAsync(
                RacePlayerFileStatus.Ready,
                RaceWorldFileStatus.Ready,
                GetRngControlReadyStatus(session.State),
                cancellationToken: cancellationToken,
                packageRevision: packageRevision);
        }
        else
        {
            bool waitingForTerraria = IsTerrariaProcessUnavailable(result.Message);
            update = session.UpdatePreparationStatusAsync(
                waitingForTerraria ? RacePlayerFileStatus.Waiting : RacePlayerFileStatus.Failed,
                RaceWorldFileStatus.Ready,
                waitingForTerraria
                    ? GetRngControlIdleStatus(session.State)
                    : GetRngControlFailureStatus(session.State),
                waitingForTerraria ? null : result.Message,
                cancellationToken,
                packageRevision);
        }

        RaceOperationResult<RaceRoomState> updated = await update.ConfigureAwait(false);
        if (result.Succeeded && updated.Succeeded && updated.Value is RaceRoomState state)
        {
            SyncLocalPreparationCompletion(state);
        }

        return updated;
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
