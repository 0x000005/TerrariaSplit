using System.Globalization;
using TerrariaSplit.Race.Client;
using TerrariaSplit.Race.Contracts;
using TerrariaSplit.Race.InGame;
using TerrariaSplit.Terraria;
using TerrariaSplit.Terraria.Automation;

namespace TerrariaSplit.UI;

internal sealed partial class RaceShell
{
    private static readonly TimeSpan InGameMenuPollInterval = TimeSpan.FromMilliseconds(25);
    private static readonly TimeSpan InGameMenuReconnectInterval = TimeSpan.FromSeconds(1);
    private CancellationTokenSource? inGameMenuCancellation;
    private CancellationTokenSource? inGameOperationCancellation;
    private Task? inGameMenuPump;
    private RaceWorldSetupSettings? inGameWorldSetup;
    private string inGameMenuStatus = string.Empty;
    private int inGameMenuProgress;
    private int inGameMenuBusy;
    private int inGameMenuDedicatedProgress;
    private int inGameMenuDirty;
    private int inGameMenuOpening;
    private int inGameMenuFailureReported;
    private int inGameMenuAttachedOnce;
    private long inGameMenuRevision;
    private long inGameMenuSentRevision;
    private long inGameMenuLastActionId;
    private long inGameOperationId;
    private readonly object inGameActionSnapshotsLock = new();
    private readonly Dictionary<long, RaceInGameSnapshot> inGameActionSnapshots = [];
    private readonly Queue<long> inGameActionSnapshotOrder = [];
    private RaceInGameSnapshot? currentInGameActionSnapshot;

    public void OpenInGameMenu()
    {
        if (disposed)
        {
            return;
        }

        if (inGameMenuPump is { IsCompleted: false })
        {
            MarkInGameMenuDirty();
            _ = ReopenInGameMenuAsync();
            return;
        }

        if (Interlocked.Exchange(ref inGameMenuOpening, 1) != 0)
        {
            return;
        }

        inGameWorldSetup = CloneWorldSetup(getSettings().Race?.WorldSetup);
        ResetInGameNavigation();
        inGameMenuStatus = string.Empty;
        inGameMenuProgress = 0;
        Interlocked.Exchange(ref inGameMenuFailureReported, 0);
        ResetInGameActionSnapshots();
        inGameMenuCancellation = new CancellationTokenSource();
        inGameMenuPump = RunInGameMenuAsync(inGameMenuCancellation.Token);
    }

    public void OpenRaceEntryPoint()
    {
        OpenPanel();
    }

    private async Task ReopenInGameMenuAsync()
    {
        try
        {
            RaceInGameSnapshot snapshot = BuildInGameSnapshot(NextInGameMenuRevision());
            TerrariaRaceMenuExchangeResult result = await worldLock.OpenRaceMenuAsync(snapshot);
            if (result.Succeeded)
            {
                Interlocked.Exchange(ref inGameMenuAttachedOnce, 1);
                RecordSentInGameSnapshot(snapshot);
            }
            else
            {
                if (ShouldRecoverInGameMenu(result.Message))
                {
                    PrepareInGameMenuForHookRecovery(result.Message);
                }
                else
                {
                    HandleInGameMenuFailure(result.Message);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or OperationCanceledException)
        {
            if (ShouldRecoverInGameMenu(ex.Message))
            {
                PrepareInGameMenuForHookRecovery(ex.Message);
            }
            else
            {
                HandleInGameMenuFailure(ex.Message);
            }
        }
    }

    private async Task RunInGameMenuAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    RaceInGameSnapshot initial = BuildInGameSnapshot(NextInGameMenuRevision());
                    TerrariaRaceMenuExchangeResult opened = await worldLock.OpenRaceMenuAsync(
                        initial,
                        cancellationToken).ConfigureAwait(false);
                    if (!opened.Succeeded)
                    {
                        if (!ShouldRecoverInGameMenu(opened.Message))
                        {
                            HandleInGameMenuFailure(opened.Message);
                            return;
                        }

                        PrepareInGameMenuForHookRecovery(opened.Message);
                        await Task.Delay(InGameMenuReconnectInterval, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    Interlocked.Exchange(ref inGameMenuAttachedOnce, 1);
                    Interlocked.Exchange(ref inGameMenuFailureReported, 0);
                    RecordSentInGameSnapshot(initial);
                    await HandleInGameActionsAsync(opened.Actions, cancellationToken).ConfigureAwait(false);

                    bool reconnect = false;
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        RaceInGameSnapshot? snapshot = null;
                        if (Interlocked.Exchange(ref inGameMenuDirty, 0) != 0)
                        {
                            snapshot = BuildInGameSnapshot(NextInGameMenuRevision());
                        }

                        TerrariaRaceMenuExchangeResult exchange = await worldLock.ExchangeRaceMenuAsync(
                            Volatile.Read(ref inGameMenuSentRevision),
                            snapshot,
                            cancellationToken).ConfigureAwait(false);
                        if (!exchange.Succeeded)
                        {
                            if (!ShouldRecoverInGameMenu(exchange.Message))
                            {
                                HandleInGameMenuFailure(exchange.Message);
                                return;
                            }

                            PrepareInGameMenuForHookRecovery(exchange.Message);
                            reconnect = true;
                            break;
                        }

                        if (snapshot is not null)
                        {
                            RecordSentInGameSnapshot(snapshot);
                        }

                        await HandleInGameActionsAsync(exchange.Actions, cancellationToken).ConfigureAwait(false);
                        await Task.Delay(InGameMenuPollInterval, cancellationToken).ConfigureAwait(false);
                    }

                    if (reconnect)
                    {
                        await Task.Delay(InGameMenuReconnectInterval, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (
                    ex is IOException or InvalidOperationException or ObjectDisposedException)
                {
                    if (!ShouldRecoverInGameMenu(ex.Message))
                    {
                        HandleInGameMenuFailure(ex.Message);
                        return;
                    }

                    PrepareInGameMenuForHookRecovery(ex.Message);
                    await Task.Delay(InGameMenuReconnectInterval, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            Interlocked.Exchange(ref inGameMenuOpening, 0);
            CancellationTokenSource? cancellation = Interlocked.Exchange(ref inGameMenuCancellation, null);
            cancellation?.Dispose();
        }
    }

    private static bool IsDetachedRaceMenu(string message)
    {
        return message.Contains(
            "Race menu is not attached",
            StringComparison.OrdinalIgnoreCase);
    }

    private bool ShouldRecoverInGameMenu(string message)
    {
        if (!IsRaceEnabled ||
            (Volatile.Read(ref inGameMenuAttachedOnce) == 0 && !IsInRoom))
        {
            return false;
        }

        return IsTerrariaProcessUnavailable(message) ||
            IsDetachedRaceMenu(message) ||
            message.Contains("pipe", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("connection reset", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("not connected", StringComparison.OrdinalIgnoreCase);
    }

    private void PrepareInGameMenuForHookRecovery(string message)
    {
        logger.Info("Terraria Race hook is offline; waiting to reattach. " + message);
        ResetInGameNavigation();
        ResetInGameActionSnapshots();
        Volatile.Write(ref inGameMenuLastActionId, 0);
        Volatile.Write(ref inGameMenuSentRevision, 0);
        Interlocked.Exchange(ref inGameMenuDirty, 1);
    }

    private async Task HandleInGameActionsAsync(
        IReadOnlyList<RaceInGameAction> actions,
        CancellationToken cancellationToken)
    {
        foreach (RaceInGameAction action in actions.OrderBy(item => item.ActionId))
        {
            if (action.ActionId <= Volatile.Read(ref inGameMenuLastActionId))
            {
                continue;
            }

            Volatile.Write(ref inGameMenuLastActionId, action.ActionId);
            if (action.Kind == RaceInGameActionKind.Close)
            {
                SaveRaceEnabled(false);
                return;
            }

            if (action.Kind == RaceInGameActionKind.Activate &&
                string.Equals(action.ControlId, "race-player-died", StringComparison.Ordinal))
            {
                QueueLocalDeathReport(action.Value);
                continue;
            }

            if (!IsActionValidForCurrentSnapshot(action))
            {
                continue;
            }

            await HandleInGameActionAsync(action, cancellationToken).ConfigureAwait(false);
        }
    }

    private void QueueLocalDeathReport(string deathMessage)
    {
        long packageRevision = Volatile.Read(ref activePackageRevision);
        string runId = Volatile.Read(ref activeRunId);
        if (!session.IsInRoom ||
            packageRevision <= 0 ||
            string.IsNullOrWhiteSpace(runId) ||
            string.IsNullOrWhiteSpace(session.RoomCode) ||
            string.IsNullOrWhiteSpace(session.Nickname))
        {
            return;
        }

        progressUploads.Writer.TryWrite(RaceProgressUpload.ForDeath(new RaceDeathReport(
            session.RoomCode,
            session.Nickname,
            DateTimeOffset.UtcNow,
            RaceDeathMessageRules.Normalize(deathMessage))
        {
            PackageRevision = packageRevision,
            RunId = runId
        }));
    }

    private void RecordSentInGameSnapshot(RaceInGameSnapshot snapshot)
    {
        const int retainedSnapshotCount = 64;
        lock (inGameActionSnapshotsLock)
        {
            currentInGameActionSnapshot = snapshot;
            inGameActionSnapshots[snapshot.Revision] = snapshot;
            inGameActionSnapshotOrder.Enqueue(snapshot.Revision);
            while (inGameActionSnapshotOrder.Count > retainedSnapshotCount)
            {
                long expiredRevision = inGameActionSnapshotOrder.Dequeue();
                inGameActionSnapshots.Remove(expiredRevision);
            }
        }

        Volatile.Write(ref inGameMenuSentRevision, snapshot.Revision);
    }

    private bool IsActionValidForCurrentSnapshot(RaceInGameAction action)
    {
        lock (inGameActionSnapshotsLock)
        {
            RaceInGameSnapshot? current = currentInGameActionSnapshot;
            if (current is null ||
                !inGameActionSnapshots.TryGetValue(
                    action.SnapshotRevision,
                    out RaceInGameSnapshot? source) ||
                source.PageKind != current.PageKind)
            {
                return false;
            }

            RaceInGameControl? sourceControl = source.Controls.FirstOrDefault(
                control => string.Equals(
                    control.Id,
                    action.ControlId,
                    StringComparison.Ordinal));
            RaceInGameControl? currentControl = current.Controls.FirstOrDefault(
                control => string.Equals(
                    control.Id,
                    action.ControlId,
                    StringComparison.Ordinal));
            if (sourceControl is null ||
                currentControl is null ||
                !sourceControl.Enabled ||
                !currentControl.Enabled ||
                sourceControl.Kind != currentControl.Kind ||
                sourceControl.Kind == RaceInGameControlKind.Toggle &&
                sourceControl.Selected != currentControl.Selected)
            {
                return false;
            }

            if (action.Kind == RaceInGameActionKind.TextSubmitted)
            {
                return currentControl.Kind == RaceInGameControlKind.TextField;
            }

            return action.Kind == RaceInGameActionKind.Activate &&
                currentControl.Kind is
                    RaceInGameControlKind.Button or
                    RaceInGameControlKind.Toggle;
        }
    }

    private void ResetInGameActionSnapshots()
    {
        lock (inGameActionSnapshotsLock)
        {
            currentInGameActionSnapshot = null;
            inGameActionSnapshots.Clear();
            inGameActionSnapshotOrder.Clear();
        }
    }

    private async Task HandleInGameActionAsync(
        RaceInGameAction action,
        CancellationToken cancellationToken)
    {
        if (action.Kind == RaceInGameActionKind.TextSubmitted)
        {
            HandleInGameText(action.ControlId, action.Value);
            MarkInGameMenuDirty();
            return;
        }

        if (action.Kind != RaceInGameActionKind.Activate)
        {
            return;
        }

        string id = action.ControlId;
        if (id == "flow-host")
        {
            SaveDraftState(draftState with
            {
                Role = RacePanelRole.Host
            });
            _ = TransitionInGameMenu(RaceInGameTransition.SelectHost);
        }
        else if (id == "nav-home")
        {
            _ = TransitionInGameMenu(RaceInGameTransition.BackToEntry);
        }
        else if (id == "room-management")
        {
            _ = TransitionInGameMenu(RaceInGameTransition.OpenRoomManagement);
        }
        else if (id == "room-back")
        {
            _ = TransitionInGameMenu(RaceInGameTransition.BackToRoomHome);
        }
        else if (id == "host-world-next")
        {
            _ = TransitionInGameMenu(RaceInGameTransition.OpenFilterSettings);
        }
        else if (id == "host-world-seeds")
        {
            _ = TransitionInGameMenu(RaceInGameTransition.OpenSeedSettings);
        }
        else if (id == "nav-host-source")
        {
            _ = TransitionInGameMenu(RaceInGameTransition.BackToWorldSource);
        }
        else if (id == "nav-host-world")
        {
            _ = TransitionInGameMenu(RaceInGameTransition.BackToWorldSettings);
        }
        else if (id == "host-seeds-apply")
        {
            _ = TransitionInGameMenu(RaceInGameTransition.BackToWorldSettings);
        }
        else if (id == "source-random")
        {
            RaceWorldSetupSettings setup = EnsureInGameWorldSetup();
            setup.Source = RacePreferredWorldSource.Random;
            setup.SeedText = string.Empty;
            PersistInGameWorldSetup();
            _ = TransitionInGameMenu(RaceInGameTransition.SelectRandomWorld);
        }
        else if (id.StartsWith("world-size:", StringComparison.Ordinal))
        {
            EnsureInGameWorldSetup().WorldSize = id["world-size:".Length..];
            PersistInGameWorldSetup();
        }
        else if (id.StartsWith("world-difficulty:", StringComparison.Ordinal))
        {
            EnsureInGameWorldSetup().WorldDifficulty = id["world-difficulty:".Length..];
            PersistInGameWorldSetup();
        }
        else if (id.StartsWith("world-evil:", StringComparison.Ordinal))
        {
            EnsureInGameWorldSetup().WorldEvil = id["world-evil:".Length..];
            PersistInGameWorldSetup();
        }
        else if (id.StartsWith("special-seed:", StringComparison.Ordinal))
        {
            ToggleSpecialSeed(id["special-seed:".Length..]);
        }
        else if (id == "rng")
        {
            RaceWorldSetupSettings setup = EnsureInGameWorldSetup();
            setup.RngControlEnabled = !setup.RngControlEnabled;
            PersistInGameWorldSetup();
        }
        else if (id == "pyramid")
        {
            RaceWorldSetupSettings setup = EnsureInGameWorldSetup();
            setup.PyramidEnabled = !setup.PyramidEnabled;
            PersistInGameWorldSetup();
        }
        else if (id.StartsWith("pyramid-item:", StringComparison.Ordinal))
        {
            ToggleMask(
                AutoCreatePyramidFilterItem.Mask(id["pyramid-item:".Length..]),
                static setup => setup.PyramidItemMask,
                static (setup, value) => setup.PyramidItemMask = value);
        }
        else if (id == "crimson")
        {
            RaceWorldSetupSettings setup = EnsureInGameWorldSetup();
            if (AutoCreateAdvancedFilterEligibility.IsEligible(setup))
            {
                setup.CrimsonEnabled = !setup.CrimsonEnabled;
            }
            PersistInGameWorldSetup();
        }
        else if (id.StartsWith("crimson-distance:", StringComparison.Ordinal))
        {
            RaceWorldSetupSettings setup = EnsureInGameWorldSetup();
            if (AutoCreateAdvancedFilterEligibility.IsEligible(setup))
            {
                setup.CrimsonDistance = id["crimson-distance:".Length..];
            }
            PersistInGameWorldSetup();
        }
        else if (id.StartsWith("jungle-depth:", StringComparison.Ordinal))
        {
            RaceWorldSetupSettings setup = EnsureInGameWorldSetup();
            if (AutoCreateAdvancedFilterEligibility.IsEligible(setup))
            {
                setup.JungleRouteDepth = id["jungle-depth:".Length..];
            }
            PersistInGameWorldSetup();
        }
        else if (id == "jungle-route")
        {
            RaceWorldSetupSettings setup = EnsureInGameWorldSetup();
            if (AutoCreateAdvancedFilterEligibility.IsEligible(setup))
            {
                setup.JungleRouteDepth =
                    AutoCreateJungleRouteDepth.Normalize(setup.JungleRouteDepth) ==
                    AutoCreateJungleRouteDepth.None
                        ? AutoCreateJungleRouteDepth.Medium
                        : AutoCreateJungleRouteDepth.None;
            }
            PersistInGameWorldSetup();
        }
        else if (id == "host-generate")
        {
            _ = RunHostWorldOperationAsync();
        }
        else if (id == "join")
        {
            _ = RunJoinOperationAsync();
        }
        else if (id == "cancel")
        {
            CancelInGameOperation();
            await CancelWorldGenerationAsync().ConfigureAwait(false);
        }
        else if (id.StartsWith("kick:", StringComparison.Ordinal))
        {
            _ = RunSimpleOperationAsync(
                Localize("Kicking player..."),
                async () =>
                {
                    await KickPlayerAsync(id["kick:".Length..]).ConfigureAwait(false);
                    return null;
                });
        }
        else if (id == "start")
        {
            _ = RunSimpleOperationAsync(Localize("Starting Race..."), async () => await StartAsync());
        }
        else if (id == "ready")
        {
            RacePlayerState? localPlayer = session.State?.Players.FirstOrDefault(player =>
                string.Equals(player.Nickname, session.Nickname, StringComparison.OrdinalIgnoreCase));
            bool nextReady = localPlayer?.IsReady != true;
            _ = RunSimpleOperationAsync(
                Localize(nextReady ? "Ready" : "Not Ready"),
                async () =>
                {
                    RaceOperationResult<RaceRoomState> result =
                        await session.SetReadyAsync(nextReady).ConfigureAwait(false);
                    ApplyOperationState(result);
                    LogRaceOperationFailure(result, nextReady ? "become ready" : "cancel ready");
                    return result;
                });
        }
        else if (id is "restart" or "room-restart")
        {
            _ = RunSimpleOperationAsync(
                Localize("Restarting..."),
                async () =>
                {
                    RaceOperationResult<RaceRoomState> result = await RestartAsync().ConfigureAwait(false);
                    if (result.Succeeded)
                    {
                        _ = TransitionInGameMenu(RaceInGameTransition.RoomPrepared);
                    }

                    return result;
                });
        }
        else if (id == "room-close")
        {
            _ = RunSimpleOperationAsync(
                Localize("Closing room..."),
                async () =>
                {
                    await CloseRoomAsync().ConfigureAwait(false);
                    _ = TransitionInGameMenu(RaceInGameTransition.RoomExited);
                    return null;
                });
        }
        else if (id == "leave-room")
        {
            _ = RunSimpleOperationAsync(
                IsHostInCurrentRoom ? Localize("Closing room...") : Localize("Leaving room..."),
                async () =>
                {
                    if (IsHostInCurrentRoom)
                    {
                        await CloseRoomAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        await LeaveAsync().ConfigureAwait(false);
                    }

                    _ = TransitionInGameMenu(RaceInGameTransition.RoomExited);
                    return null;
                });
        }

        MarkInGameMenuDirty();
    }

    private void HandleInGameText(string id, string value)
    {
        switch (id)
        {
            case "flow-member":
                SaveDraftState(draftState with
                {
                    Role = RacePanelRole.Member,
                    RoomCode = value
                });
                if (!RaceRoomCodeRules.IsValid(value))
                {
                    inGameMenuStatus = Localize("Room code must be four digits.");
                    break;
                }

                inGameMenuStatus = string.Empty;
                _ = RunJoinOperationAsync();
                break;
            case "room-code":
                SaveDraftState(draftState with { RoomCode = value });
                break;
            case "seed":
                RaceWorldSetupSettings setup = EnsureInGameWorldSetup();
                setup.SeedText = value;
                setup.Source = string.IsNullOrWhiteSpace(value)
                    ? RacePreferredWorldSource.Random
                    : RacePreferredWorldSource.CustomSeed;
                PersistInGameWorldSetup();
                break;
            case "fixed-seed":
                if (string.IsNullOrWhiteSpace(value))
                {
                    inGameMenuStatus = Localize("A fixed seed is required.");
                    break;
                }

                RaceWorldSetupSettings fixedSetup = EnsureInGameWorldSetup();
                fixedSetup.Source = RacePreferredWorldSource.CustomSeed;
                fixedSetup.SeedText = value.Trim();
                fixedSetup.SpecialSeeds = string.Empty;
                fixedSetup.SecretSeeds = string.Empty;
                PersistInGameWorldSetup();
                inGameMenuStatus = string.Empty;
                _ = RunHostWorldOperationAsync();
                break;
            case "secret-seeds":
                EnsureInGameWorldSetup().SecretSeeds = value;
                PersistInGameWorldSetup();
                break;
        }
    }

    private async Task RunHostWorldOperationAsync()
    {
        RaceWorldSetupSettings setup = CloneWorldSetup(EnsureInGameWorldSetup());
        RaceWorldSettings worldSettings = BuildInGameWorldSettings(setup);
        bool filtersSeeds =
            string.Equals(
                setup.Source,
                RacePreferredWorldSource.Random,
                StringComparison.OrdinalIgnoreCase) &&
            RaceWorldSettingsFactory.HasActiveFilters(worldSettings);
        string initialStatus = filtersSeeds
            ? Localize("Filtering seeds")
            : FormatInGameProgressStatus("Generating world", 0);
        if (!TryBeginInGameOperation(
                initialStatus,
                out long operationId,
                out CancellationToken token,
                true))
        {
            return;
        }

        // Publish the first stage before filtering or world generation can
        // replace it with a later progress update.
        await Task.Delay(
            InGameMenuPollInterval + TimeSpan.FromMilliseconds(25),
            token).ConfigureAwait(false);

        string? generatedPath = null;
        try
        {
            if (!RaceWorldSettingsFactory.HasCompatibleJourneyDifficulties(worldSettings))
            {
                SetInGameOperationFailure(
                    operationId,
                    Localize("Journey characters can only be used with Journey worlds."));
                return;
            }

            var progress = new Progress<int>(value =>
            {
                if (operationId != Volatile.Read(ref inGameOperationId))
                {
                    return;
                }

                int generationPercent = Math.Clamp(
                    (int)Math.Round(Math.Clamp(value, 0, 90) * 100d / 90d),
                    0,
                    100);
                Volatile.Write(ref inGameMenuProgress, generationPercent);
                inGameMenuStatus = filtersSeeds && value <= 0
                    ? Localize("Filtering seeds")
                    : FormatInGameProgressStatus("Generating world", generationPercent);
                MarkInGameMenuDirty();
            });
            RacePanelWorldGenerationResult generation =
                string.Equals(setup.Source, RacePreferredWorldSource.CustomSeed, StringComparison.OrdinalIgnoreCase)
                    ? await GenerateCustomSeedWorldAsync(worldSettings, setup.SeedText, progress).ConfigureAwait(false)
                    : await GenerateRandomWorldAsync(worldSettings, progress).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (!generation.Succeeded || string.IsNullOrWhiteSpace(LocalWorldPath))
            {
                SetInGameOperationFailure(
                    operationId,
                    generation.Succeeded
                        ? Localize("World generation completed without a world file.")
                        : generation.Message);
                return;
            }

            generatedPath = LocalWorldPath;
            SetInGameOperationState(
                operationId,
                FormatInGameProgressStatus("Uploading", 0),
                0);
            var uploadProgress = new Progress<int>(value =>
            {
                if (operationId != Volatile.Read(ref inGameOperationId))
                {
                    return;
                }

                int uploadPercent = Math.Clamp(value, 0, 100);
                Volatile.Write(ref inGameMenuProgress, uploadPercent);
                inGameMenuStatus = FormatInGameProgressStatus("Uploading", uploadPercent);
                MarkInGameMenuDirty();
            });
            RaceOperationResult<RaceRoomState> upload = await UploadWorldAsync(
                draftState.ServerUrl,
                draftState.Nickname,
                generatedPath,
                worldSettings,
                setup.SeedText,
                uploadProgress,
                token).ConfigureAwait(false);
            if (!upload.Succeeded)
            {
                SetInGameOperationFailure(operationId, upload.Message);
                return;
            }

            _ = TransitionInGameMenu(RaceInGameTransition.RoomPrepared);
            SetInGameOperationState(
                operationId,
                Localize("World uploaded. Preparing Race environment..."),
                100);
        }
        catch (OperationCanceledException)
        {
            SetInGameOperationFailure(operationId, Localize("Canceled."));
            if (!string.IsNullOrWhiteSpace(generatedPath))
            {
                await DiscardLocalWorldAsync(generatedPath).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or HttpRequestException or TimeoutException)
        {
            logger.Error(ex, "In-game Race world preparation failed.");
            SetInGameOperationFailure(operationId, ex.Message);
        }
        finally
        {
            EndInGameOperation(operationId);
        }
    }

    private string FormatInGameProgressStatus(string key, int percent)
    {
        return Localize(key) +
            " " +
            Math.Clamp(percent, 0, 100).ToString(CultureInfo.InvariantCulture) +
            "%";
    }

    private async Task RunJoinOperationAsync()
    {
        if (!TryBeginInGameOperation(
                Localize("Joining room..."),
                out long operationId,
                out CancellationToken token,
                true))
        {
            return;
        }

        try
        {
            RaceOperationResult<RaceRoomState> result = await JoinRoomAsync(
                draftState.ServerUrl,
                draftState.RoomCode,
                draftState.Nickname).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (result.Succeeded)
            {
                _ = TransitionInGameMenu(
                    HasRaceStarted(result.Value)
                        ? RaceInGameTransition.RaceStarted
                        : RaceInGameTransition.RoomPrepared);
            }

            SetInGameOperationState(
                operationId,
                result.Succeeded ? Localize("Joined room.") : result.Message,
                result.Succeeded ? 100 : 0);
        }
        catch (OperationCanceledException)
        {
            SetInGameOperationFailure(operationId, Localize("Canceled."));
        }
        finally
        {
            EndInGameOperation(operationId);
        }
    }

    private async Task RunSimpleOperationAsync(
        string status,
        Func<Task<RaceOperationResult<RaceRoomState>?>> operation)
    {
        if (!TryBeginInGameOperation(status, out long operationId, out _))
        {
            return;
        }

        try
        {
            RaceOperationResult<RaceRoomState>? result = await operation().ConfigureAwait(false);
            SetInGameOperationState(
                operationId,
                result is { Succeeded: false } ? result.Message : string.Empty,
                result is { Succeeded: false } ? 0 : 100);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or HttpRequestException or OperationCanceledException or TimeoutException)
        {
            SetInGameOperationFailure(operationId, ex.Message);
        }
        finally
        {
            EndInGameOperation(operationId);
        }
    }
}
