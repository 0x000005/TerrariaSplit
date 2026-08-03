using System.Text.Json;
using TerrariaSplit.Race.Client;
using TerrariaSplit.Race.Determinism;
using TerrariaSplit.Race.InGame;
using TerrariaSplit.UI;

namespace TerrariaSplit.Tests;

internal static class RaceFlowTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("race room journey covers preparation, synchronized start, reconnect, host restart and a fresh run", TestSuite.Flow, CompleteRoomJourney);
        yield return TestCase.Sync("race server rejects invalid identities, stale progress and unauthorized host actions", TestSuite.Flow, PermissionAndStalenessBoundaries);
        yield return TestCase.Sync("race package survives transport serialization with route, world and leaderboard intact", TestSuite.Flow, TransportRoundTrip);
        yield return TestCase.Sync("race route carries host all-icon customizations to the member and cleans its cache", TestSuite.Flow, HostAllIconsReachMember);
        yield return TestCase.Sync("in-game race protocol preserves bounded multilingual snapshots and ordered actions", TestSuite.Core, InGameProtocolRoundTrip);
        yield return TestCase.Sync("in-game race navigation follows host, member and room lifecycle journeys", TestSuite.Flow, InGameNavigationJourney);
        yield return TestCase.Sync("race client rejects late updates from a room left before joining another room", TestSuite.Flow, CrossRoomUpdateIsolation);
        yield return TestCase.Sync("race deterministic core derives stable domains, counts events, rolls independent chances and accumulates fixed chances", TestSuite.Core, DeterministicCore);
        yield return TestCase.Async("race voice announces main and attached groups once, queues players in order and clears obsolete work", TestSuite.Flow, VoiceAnnouncementJourney);
    }

    private static void InGameProtocolRoundTrip()
    {
        string longValue = "玩家 name with spaces " + new string('界', 12_000);
        var snapshot = new RaceInGameSnapshot(
            42,
            true,
            RaceInGamePageKind.Progress,
            "Race 设置",
            "正在生成 world",
            "返回",
            [
                new RaceInGameControl(
                    "nickname",
                    RaceInGameControlKind.TextField,
                    "用户名",
                    longValue,
                    true,
                    false,
                    0,
                    20,
                    false,
                    "identity",
                    "Images/UI/WorldCreation/IconRandomSeed",
                    "支持中文 description"),
                new RaceInGameControl(
                    "progress",
                    RaceInGameControlKind.Progress,
                    "进度",
                    string.Empty,
                    false,
                    false,
                    73,
                    0,
                    true,
                    string.Empty)
            ]);

        RaceInGameSnapshot decoded = RaceInGameProtocol.DecodeSnapshot(
            RaceInGameProtocol.EncodeSnapshot(snapshot));
        Check.Equal(42L, decoded.Revision);
        Check.Equal(RaceInGamePageKind.Progress, decoded.PageKind);
        Check.Equal("Race 设置", decoded.Title);
        Check.Equal("返回", decoded.CloseLabel);
        Check.Equal(longValue, decoded.Controls[0].Value);
        Check.Equal("identity", decoded.Controls[0].LayoutGroup);
        Check.Equal(
            "Images/UI/WorldCreation/IconRandomSeed",
            decoded.Controls[0].IconPath);
        Check.Equal("支持中文 description", decoded.Controls[0].Description);
        Check.Equal(73, decoded.Controls[1].ProgressValue);

        RaceInGameAction[] actions = RaceInGameProtocol.DecodeActions(
            RaceInGameProtocol.EncodeActions(
            [
                new RaceInGameAction(7, 42, "nickname", RaceInGameActionKind.TextSubmitted, "新 玩家"),
                new RaceInGameAction(8, 42, "join", RaceInGameActionKind.Activate, string.Empty)
            ]));
        Check.Sequence([7L, 8L], actions.Select(action => action.ActionId));
        Check.True(actions.All(action => action.SnapshotRevision == 42));
        Check.Throws<InvalidDataException>(() => RaceInGameProtocol.DecodeSnapshot("not-base64"));
        Check.Throws<InvalidDataException>(() => RaceInGameProtocol.EncodeSnapshot(
            new RaceInGameSnapshot(
                43,
                true,
                RaceInGamePageKind.WorldFilters,
                "Race",
                string.Empty,
                "Back",
                [
                    new RaceInGameControl(
                        "too-long",
                        RaceInGameControlKind.Label,
                        new string('x', 70_000),
                        string.Empty,
                        false,
                        false,
                        0,
                        0,
                        true,
                        string.Empty)
                ])));
    }

    private static void CrossRoomUpdateIsolation()
    {
        Check.True(RaceClientSession.IsRoomUpdateForCurrentRoom(null, "OLD1"));
        Check.True(RaceClientSession.IsRoomUpdateForCurrentRoom("NEW2", "new2"));
        Check.False(RaceClientSession.IsRoomUpdateForCurrentRoom("NEW2", "OLD1"));

        var manager = new RaceRoomManager(new InMemoryRaceRecordStore());
        RaceRoomState room = Success(manager.CreateRoom(new RaceRoomCreateRequest("host")));
        RaceOperationResult<RaceRoomState> missingRoom =
            RaceOperationResult<RaceRoomState>.Failure(
                "room_not_found",
                "Room no longer exists.");
        Check.True(RaceClientSession.ShouldClearRoomAfterResumeFailure(
            room,
            "host",
            room.RoomCode,
            "host",
            missingRoom));
        Check.False(RaceClientSession.ShouldClearRoomAfterResumeFailure(
            room,
            "host",
            "OTHER",
            "host",
            missingRoom));
        Check.False(RaceClientSession.ShouldClearRoomAfterResumeFailure(
            room,
            "host",
            room.RoomCode,
            "host",
            RaceOperationResult<RaceRoomState>.Success(room)));
    }

    private static void HostAllIconsReachMember()
    {
        using var directory = new TestDirectory();
        string hostIconDirectory = directory.Combine("房主 图标");
        Directory.CreateDirectory(hostIconDirectory);
        string eyeIconPath = Path.Combine(hostIconDirectory, "眼球 自定义.png");
        string eaterIconPath = Path.Combine(hostIconDirectory, "世界吞噬者 自定义.png");
        byte[] eyeIconData = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        byte[] eaterIconData = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Z1pEAAAAASUVORK5CYII=");
        File.WriteAllBytes(eyeIconPath, eyeIconData);
        File.WriteAllBytes(eaterIconPath, eaterIconData);

        var hostSettings = new AppSettings
        {
            Route = new RouteSettings
            {
                SplitRoute =
                [
                    new SplitRouteEntry
                    {
                        Id = "pre-hardmode-bosses",
                        Enabled = true,
                        DisplayName = "Pre-hardmode bosses",
                        Condition = SplitCondition.All(
                        [
                            SplitCondition.Fact("boss:eye-of-cthulhu:defeated"),
                            SplitCondition.Fact("boss:eater-of-worlds:defeated")
                        ]),
                        IconTargetIds = ["boss:eye-of-cthulhu", "boss:eater-of-worlds"],
                        IconOverride = new SplitIconOverride
                        {
                            Source = SplitIconOverrideSource.All,
                            FilePath = directory.Combine("hidden-host-only.png"),
                            AllIconFilePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["boss:eye-of-cthulhu"] = eyeIconPath,
                                ["boss:eater-of-worlds"] = eaterIconPath
                            }
                        }
                    }
                ]
            }
        };

        RaceRoutePayload payload = RaceRoutePayloadFactory.Create(hostSettings);
        Check.False(payload.SerializedRouteJson.Contains(hostIconDirectory, StringComparison.OrdinalIgnoreCase));
        Check.False(payload.SerializedRouteJson.Contains("hidden-host-only.png", StringComparison.OrdinalIgnoreCase));
        Check.Equal(
            Convert.ToBase64String(eyeIconData),
            payload.Icons.Single(icon => icon.Key == "boss:eye-of-cthulhu").DataBase64);
        Check.Equal(
            Convert.ToBase64String(eaterIconData),
            payload.Icons.Single(icon => icon.Key == "boss:eater-of-worlds").DataBase64);

        string memberCache = directory.Combine("member", "Data", "RaceIcons");
        var controller = new RaceRouteOverrideController(new CloningSettingsSnapshotFactory(), memberCache);
        bool applied = controller.TryApply(
            new AppSettings(),
            payload,
            out AppSettings memberSettings,
            out string detail);
        if (!applied)
        {
            throw new InvalidOperationException(detail);
        }

        SplitRouteEntry memberEntry = memberSettings.Route.SplitRoute.Single();
        string memberEyePath = memberEntry.IconOverride.AllIconFilePaths["boss:eye-of-cthulhu"];
        string memberEaterPath = memberEntry.IconOverride.AllIconFilePaths["boss:eater-of-worlds"];
        Check.True(memberEyePath.StartsWith(memberCache, StringComparison.OrdinalIgnoreCase));
        Check.True(memberEaterPath.StartsWith(memberCache, StringComparison.OrdinalIgnoreCase));
        Check.Sequence(eyeIconData, File.ReadAllBytes(memberEyePath));
        Check.Sequence(eaterIconData, File.ReadAllBytes(memberEaterPath));
        Check.Sequence(
            [memberEyePath, memberEaterPath],
            SplitCatalog.Build(memberSettings).Single().IconFileNames);

        string routeCacheDirectory = Path.GetDirectoryName(memberEyePath)!;
        Check.True(controller.Clear());
        Check.False(Directory.Exists(routeCacheDirectory));
    }

    private static void InGameNavigationJourney()
    {
        var navigation = new RaceInGameNavigator();
        Check.Equal(RaceInGamePage.Entry, navigation.Current);
        Check.True(navigation.TryMove(RaceInGameTransition.SelectHost, isHost: false));
        Check.True(navigation.TryMove(RaceInGameTransition.SelectRandomWorld, isHost: false));
        Check.True(navigation.TryMove(RaceInGameTransition.OpenSeedSettings, isHost: false));
        Check.True(navigation.TryMove(RaceInGameTransition.BackToWorldSettings, isHost: false));
        Check.True(navigation.TryMove(RaceInGameTransition.OpenFilterSettings, isHost: false));
        Check.True(navigation.TryMove(RaceInGameTransition.RoomPrepared, isHost: true));
        Check.Equal(
            RaceInGamePage.RoomPreparation,
            navigation.Resolve(RacePanelRole.Host, roomOpen: true, isHost: true));
        Check.True(navigation.TryMove(RaceInGameTransition.RaceStarted, isHost: true));
        Check.True(navigation.TryMove(RaceInGameTransition.OpenRoomManagement, isHost: true));
        Check.True(navigation.TryMove(RaceInGameTransition.RoomPrepared, isHost: true));
        Check.True(navigation.TryMove(RaceInGameTransition.RoomExited, isHost: true));
        Check.Equal(RaceInGamePage.Entry, navigation.Current);

        navigation.Reset(roomOpen: false);
        Check.True(navigation.TryMove(RaceInGameTransition.SelectMember, isHost: false));
        Check.True(navigation.TryMove(RaceInGameTransition.RoomPrepared, isHost: false));
        Check.True(navigation.TryMove(RaceInGameTransition.RaceStarted, isHost: false));
        Check.False(navigation.TryMove(RaceInGameTransition.OpenRoomManagement, isHost: false));
        Check.Equal(
            RaceInGamePage.RoomHome,
            navigation.Resolve(RacePanelRole.Member, roomOpen: true, isHost: false));
        Check.True(navigation.TryMove(RaceInGameTransition.RoomExited, isHost: false));
        Check.Equal(
            RaceInGamePage.Entry,
            navigation.Resolve(RacePanelRole.Member, roomOpen: false, isHost: false));

        navigation.Reset(roomOpen: true, raceStarted: false);
        Check.Equal(RaceInGamePage.RoomPreparation, navigation.Current);
        navigation.Reset(roomOpen: true, raceStarted: true);
        Check.Equal(RaceInGamePage.RoomHome, navigation.Current);
    }

    private static void CompleteRoomJourney()
    {
        var store = new InMemoryRaceRecordStore();
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero));
        var manager = new RaceRoomManager(store, timeProvider: clock);
        RaceRoomState created = Success(manager.CreateRoom(new RaceRoomCreateRequest("host")));
        string room = created.RoomCode;
        Check.Equal(RaceRoomCodeRules.Length, room.Length);
        Check.True(room.All(character => character is >= '0' and <= '9'));
        Success(manager.JoinRoom(new RaceRoomJoinRequest(room, "guest")));
        RaceRoomState uploaded = Success(manager.PublishWorldFile(Publish(room, "host", revisionName: "first")));
        Check.Equal(RaceRoomStatus.WorldUploaded, uploaded.Status);
        Check.True(uploaded.Determinism!.TryValidate(out _));
        Check.Equal(
            RaceDeterminismCapability.WorldLock |
            RaceDeterminismCapability.NpcDirectDrops |
            RaceDeterminismCapability.PlayerTriggeredResults |
            RaceDeterminismCapability.AlchemyAndLuck |
            RaceDeterminismCapability.WorldTransitions |
            RaceDeterminismCapability.StardustTownAndNaturalEvents,
            uploaded.Determinism.EnabledCapabilities);
        RacePlayerState uploadedHost = uploaded.Players.Single(player => player.IsHost);
        Check.Equal(RaceWorldFileStatus.Ready, uploadedHost.WorldFileStatus);
        Check.Equal(RaceRngControlStatus.Closed, uploadedHost.RngControlStatus);
        Success(manager.UpdatePreparationStatus(Ready(room, "host", 1)));
        RaceRoomState technicallyReady = Success(manager.UpdatePreparationStatus(Ready(room, "guest", 1)));
        Check.Equal(RaceRoomStatus.WorldUploaded, technicallyReady.Status);
        Check.False(technicallyReady.Players.Single(player => player.Nickname == "guest").IsReady);
        Check.Equal(
            RaceErrors.PlayersNotReady,
            manager.StartRace(new RaceHostActionRequest(room, "host", 1)).ErrorCode);
        RaceRoomState ready = Success(manager.SetPlayerReady(PlayerReady(room, "guest", 1, true)));
        Check.Equal(RaceRoomStatus.Ready, ready.Status);
        Check.True(ready.Players.Single(player => player.Nickname == "guest").IsReady);
        RaceRoomState notReady = Success(manager.SetPlayerReady(PlayerReady(room, "guest", 1, false)));
        Check.Equal(RaceRoomStatus.WorldUploaded, notReady.Status);
        Check.False(notReady.Players.Single(player => player.Nickname == "guest").IsReady);
        Success(manager.SetPlayerReady(PlayerReady(room, "guest", 1, true)));
        RaceRoomState disconnectedBeforeStart = Success(manager.DisconnectPlayer(room, "guest"));
        Check.False(disconnectedBeforeStart.Players.Single(player => player.Nickname == "guest").IsReady);
        RaceRoomState resumedBeforeStart = Success(manager.ResumeRoom(room, "guest"));
        Check.False(resumedBeforeStart.Players.Single(player => player.Nickname == "guest").IsReady);
        Check.Equal(
            RaceErrors.InvalidRequest,
            manager.SetPlayerReady(PlayerReady(room, "host", 1, true)).ErrorCode);
        ready = Success(manager.SetPlayerReady(PlayerReady(room, "guest", 1, true)));
        Check.True(ready.Players.All(player => player.WorldReady));
        Check.True(ready.Players.All(player => player.PlayerFileStatus == RacePlayerFileStatus.Ready));
        Check.True(ready.Players.All(player => player.WorldFileStatus == RaceWorldFileStatus.Ready));
        Check.True(ready.Players.All(player => player.RngControlStatus == RaceRngControlStatus.Enabled));

        Check.Equal(
            RaceErrors.RaceNotStarted,
            manager.ReportStart(new RaceRunStartReport(room, "guest") { PackageRevision = 1, RunId = "guest-run" }).ErrorCode);
        Check.False(manager.StartRace(new RaceHostActionRequest(room, "guest", 1)).Succeeded);
        RaceRoomState starting = Success(manager.StartRace(new RaceHostActionRequest(room, "host", 1)));
        Check.Equal(RaceRoomStatus.Starting, starting.Status);
        Check.Equal(clock.GetUtcNow() + TimeSpan.FromSeconds(7), starting.ScheduledStartUtc);
        Check.Equal(7000, starting.StartCountdownMilliseconds);
        Check.Equal(1L, starting.StartSequence);
        Check.Equal(
            RaceErrors.RaceAlreadyStarted,
            manager.JoinRoom(new RaceRoomJoinRequest(room, "late-during-race")).ErrorCode);
        Check.Equal(
            RaceErrors.RaceNotStarted,
            manager.ReportStart(new RaceRunStartReport(room, "guest") { PackageRevision = 1, RunId = "guest-run" }).ErrorCode);
        Check.Equal(
            RaceErrors.RaceNotStarted,
            manager.ReportDeath(Death(room, "guest", "guest-run"), out _).ErrorCode);
        clock.Advance(TimeSpan.FromSeconds(7));
        Success(manager.ReportStart(new RaceRunStartReport(room, "guest") { PackageRevision = 1, RunId = "guest-run" }));
        Success(manager.ReportDeath(
            Death(
                room,
                "guest",
                "guest-run",
                deathMessage: "guest was slain by Zombie."),
            out RacePlayerDied? guestDeath));
        Check.Equal("guest", guestDeath!.Nickname);
        Check.Equal(1L, guestDeath.PackageRevision);
        Check.Equal("guest-run", guestDeath.RunId);
        Check.Equal("guest was slain by Zombie.", guestDeath.DeathMessage);
        RaceRoomState hostFirstState = Success(manager.ReportSplit(
            Report(room, "host", 0, 4_000, "host-run"),
            out RaceGroupCompleted? hostFirst));
        Check.Equal("host", hostFirst!.Nickname);
        Check.Equal(0, hostFirst.SplitIndex);
        Check.Equal(4_000L, hostFirst.ElapsedMilliseconds);
        Success(manager.ReportSplit(
            Report(room, "host", 0, 4_000, "host-run"),
            out RaceGroupCompleted? duplicateHostFirst));
        Check.True(duplicateHostFirst is null);
        Check.Equal(1, hostFirstState.Players.Single(player => player.Nickname == "host").CompletedSplitCount);
        RaceRoomState first = Success(manager.ReportSplit(Report(room, "guest", 0, 5_000, "guest-run")));
        Check.Equal("host", first.Leaderboard[0].Nickname);
        Success(manager.ReportSplit(
            Report(room, "guest", 1, 7_000, "guest-run"),
            out RaceGroupCompleted? guestAttached));
        Check.True(guestAttached is not null);
        Check.Equal(1, guestAttached!.SplitIndex);
        RaceRoomState ranked = Success(manager.ReportSplit(Report(room, "host", 1, 8_000, "host-run")));
        Check.Equal("guest", ranked.Leaderboard[0].Nickname);

        RaceRoomState disconnected = Success(manager.DisconnectPlayer(room, "guest"));
        RacePlayerState disconnectedGuest = disconnected.Players.Single(player => player.Nickname == "guest");
        Check.Equal(RaceServerConnectionStatus.Disconnected, disconnectedGuest.ServerConnectionStatus);
        Check.Equal(2, disconnectedGuest.CompletedSplitCount);
        Check.Equal(RaceErrors.NicknameTaken, manager.JoinRoom(new RaceRoomJoinRequest(room, "guest")).ErrorCode);
        RaceRoomState resumed = Success(manager.ResumeRoom(room, "guest"));
        RacePlayerState resumedGuest = resumed.Players.Single(player => player.Nickname == "guest");
        Check.Equal(RaceServerConnectionStatus.Connected, resumedGuest.ServerConnectionStatus);
        Check.Equal(2, resumedGuest.CompletedSplitCount);

        string firstDigest = ranked.Determinism!.CreateDigest();
        Check.False(manager.RestartRace(new RaceHostActionRequest(room, "guest", 1)).Succeeded);
        RaceRoomState restarted = Success(manager.RestartRace(new RaceHostActionRequest(room, "host", 1)));
        Check.Equal(RaceRoomStatus.WorldUploaded, restarted.Status);
        Check.Equal(2L, restarted.PackageRevision);
        Check.True(restarted.ScheduledStartUtc is null);
        Check.Equal(0, restarted.StartCountdownMilliseconds);
        Check.False(string.Equals(firstDigest, restarted.Determinism!.CreateDigest(), StringComparison.Ordinal));
        Check.True(restarted.Players.All(player => player.CompletedSplitCount == 0));
        Check.True(restarted.Players.All(player => player.PlayerFileStatus == RacePlayerFileStatus.Waiting));
        Check.True(restarted.Players.All(player => player.WorldFileStatus == RaceWorldFileStatus.Waiting));
        Success(manager.JoinRoom(new RaceRoomJoinRequest(room, "post-restart")));
        Success(manager.KickPlayer(new RacePlayerKickRequest(room, "host", "post-restart")));

        Success(manager.UpdatePreparationStatus(Ready(room, "host", 2)));
        Success(manager.UpdatePreparationStatus(Ready(room, "guest", 2)));
        Success(manager.SetPlayerReady(PlayerReady(room, "guest", 2, true)));
        RaceRoomState secondStarting = Success(manager.StartRace(new RaceHostActionRequest(room, "host", 2)));
        Check.Equal(2L, secondStarting.StartSequence);
        clock.Advance(TimeSpan.FromSeconds(7));
        RaceRoomState secondRun = Success(manager.ReportSplit(Report(room, "host", 0, 3_500, "host-run-2", 2)));
        Check.Equal(RaceRoomStatus.Running, secondRun.Status);
        Check.Equal(1, secondRun.Players.Single(player => player.Nickname == "host").CompletedSplitCount);

        RaceRoomState kicked = Success(manager.KickPlayer(new RacePlayerKickRequest(room, "host", "guest")));
        Check.Equal(1, kicked.Players.Count);

        RaceWorldFilePublishRequest rngDisabledRequest = Publish(room, "host", "rng-disabled") with
        {
            WorldSettings = Publish(room, "host", "rng-disabled").WorldSettings with
            {
                RngControlEnabled = false
            }
        };
        RaceRoomState rngDisabled = Success(manager.PublishWorldFile(rngDisabledRequest));
        Check.Equal(RaceDeterminismCapability.WorldLock, rngDisabled.Determinism!.EnabledCapabilities);
        Check.Equal(RaceRngControlStatus.NotEnabled, rngDisabled.Players.Single().RngControlStatus);
        RaceRoomState lateJoin = Success(manager.JoinRoom(new RaceRoomJoinRequest(room, "late")));
        Check.Equal(
            RaceRngControlStatus.NotEnabled,
            lateJoin.Players.Single(player => player.Nickname == "late").RngControlStatus);
        Success(manager.UpdatePreparationStatus(Ready(room, "host", 3)));
        RaceRoomState rngDisabledReady = Success(manager.UpdatePreparationStatus(Ready(room, "late", 3)));
        rngDisabledReady = Success(manager.SetPlayerReady(PlayerReady(room, "late", 3, true)));
        Check.Equal(RaceRoomStatus.Ready, rngDisabledReady.Status);
        Check.True(rngDisabledReady.Players.All(player => player.RngControlStatus == RaceRngControlStatus.NotEnabled));

        Success(manager.CloseRoom(room, "host"));
        Check.Equal(1, store.Records.Count);
        Check.Equal(RaceErrors.RoomNotFound, manager.GetRoomState(room).ErrorCode);
    }

    private static void PermissionAndStalenessBoundaries()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero));
        var manager = new RaceRoomManager(new InMemoryRaceRecordStore(), timeProvider: clock);
        RaceRoomState created = Success(manager.CreateRoom(new RaceRoomCreateRequest("host")));
        string room = created.RoomCode;
        Success(manager.JoinRoom(new RaceRoomJoinRequest(room, "guest")));
        Check.Equal(RaceErrors.NicknameTaken, manager.JoinRoom(new RaceRoomJoinRequest(room, "guest")).ErrorCode);
        Check.Equal(RaceErrors.NicknameTaken, manager.ResumeRoom(room, "guest").ErrorCode);
        Success(manager.LeaveRoom(room, "guest"));
        Success(manager.JoinRoom(new RaceRoomJoinRequest(room, "guest")));
        Check.False(manager.PublishWorldFile(Publish(room, "guest", "forbidden")).Succeeded);
        RaceWorldFilePublishRequest invalidDifficulty = Publish(room, "host", "invalid-difficulty") with
        {
            WorldSettings = Publish(room, "host", "invalid-difficulty").WorldSettings with
            {
                PlayerDifficultyCode = 99
            }
        };
        Check.Equal(RaceErrors.InvalidRequest, manager.PublishWorldFile(invalidDifficulty).ErrorCode);
        Success(manager.PublishWorldFile(Publish(room, "host", "first"), out RaceWorldFileInfo? initialWorld));
        Check.True(initialWorld is null);
        Check.Equal(RaceErrors.RaceNotStarted, manager.ReportSplit(Report(room, "host", 0, 1_000, "run-1")).ErrorCode);
        Check.Equal(RaceErrors.PlayersNotReady, manager.StartRace(new RaceHostActionRequest(room, "host", 1)).ErrorCode);
        Success(manager.UpdatePreparationStatus(Ready(room, "host", 1)));
        Success(manager.UpdatePreparationStatus(Ready(room, "guest", 1)));
        Success(manager.SetPlayerReady(PlayerReady(room, "guest", 1, true)));
        Success(manager.StartRace(new RaceHostActionRequest(room, "host", 1)));
        clock.Advance(TimeSpan.FromSeconds(7));
        Success(manager.ReportSplit(Report(room, "host", 0, 1_000, "run-1")));
        Success(manager.PublishWorldFile(Publish(room, "host", "second"), out RaceWorldFileInfo? replacedWorld));
        Check.Equal("first.wld", replacedWorld!.FileName);
        Success(manager.UpdatePreparationStatus(Ready(room, "host", 2)));
        Success(manager.UpdatePreparationStatus(Ready(room, "guest", 2)));
        Success(manager.SetPlayerReady(PlayerReady(room, "guest", 2, true)));
        Success(manager.StartRace(new RaceHostActionRequest(room, "host", 2)));
        clock.Advance(TimeSpan.FromSeconds(7));
        Check.Equal(RaceErrors.StalePackage, manager.ReportSplit(Report(room, "host", 0, 2_000, "run-1")).ErrorCode);
        Check.Equal(
            RaceErrors.StalePackage,
            manager.ReportDeath(Death(room, "host", "run-1"), out _).ErrorCode);
        Check.False(manager.KickPlayer(new RacePlayerKickRequest(room, "guest", "host")).Succeeded);
        Check.False(manager.CloseRoom(room, "guest").Succeeded);

        Success(manager.DisconnectPlayer(room, "guest"));
        Check.Equal(RaceErrors.NicknameTaken, manager.JoinRoom(new RaceRoomJoinRequest(room, "guest")).ErrorCode);
        RaceRoomState rejoined = Success(manager.ResumeRoom(room, "guest"));
        RacePlayerState rejoinedGuest = rejoined.Players.Single(player => player.Nickname == "guest");
        Check.Equal(0, rejoinedGuest.CompletedSplitCount);
        Check.Equal(RaceServerConnectionStatus.Connected, rejoinedGuest.ServerConnectionStatus);

        Success(manager.DisconnectPlayer(room, "host"));
        RaceRoomState hostRejoined = Success(manager.ResumeRoom(room, "host"));
        Check.True(hostRejoined.Players.Single(player => player.Nickname == "host").IsHost);
        Check.Equal("second.wld", hostRejoined.WorldFile!.FileName);
    }

    private static void TransportRoundTrip()
    {
        var manager = new RaceRoomManager(new InMemoryRaceRecordStore());
        RaceRoomState created = Success(manager.CreateRoom(new RaceRoomCreateRequest("host")));
        RaceRoomState uploaded = Success(manager.PublishWorldFile(Publish(created.RoomCode, "host", "transport")));
        string json = JsonSerializer.Serialize(uploaded);
        RaceRoomState restored = JsonSerializer.Deserialize<RaceRoomState>(json)!;
        Check.Equal(uploaded.RoomCode, restored.RoomCode);
        Check.Equal(2, restored.Route!.Splits.Count);
        Check.Equal("transport.wld", restored.WorldFile!.FileName);
        Check.True(restored.WorldSettings!.Cheats.Enabled);
        Check.Equal(RacePlayerDifficultyCodes.Softcore, restored.WorldSettings.PlayerDifficultyCode);
        Check.Equal(
            AutoCreatePlayerDifficulty.Softcore,
            RaceWorldSettingsFactory.ToPlayerDifficulty(restored.WorldSettings.PlayerDifficultyCode));
        Check.Equal(8, restored.WorldSettings.Cheats.LifeCrystalMinimum);
        Check.Equal(AutoCreateJungleRouteDepth.VeryDeep, restored.WorldSettings.Cheats.JungleRouteDepth);
        Check.True(restored.WorldSettings.RngControlEnabled);
        Check.True(RaceWorldSettingsFactory.HasCompatibleJourneyDifficulties(restored.WorldSettings));
        Check.False(RaceWorldSettingsFactory.HasCompatibleJourneyDifficulties(
            restored.WorldSettings with { PlayerDifficultyCode = RacePlayerDifficultyCodes.Journey }));
        Check.True(RaceWorldSettingsFactory.HasCompatibleJourneyDifficulties(
            restored.WorldSettings with
            {
                DifficultyCode = 4,
                PlayerDifficultyCode = RacePlayerDifficultyCodes.Journey
            }));
        Check.True(RaceWorldSettingsFactory.HasActiveFilters(restored.WorldSettings));
        AutoCreateWorldSettings generatedSettings = RaceWorldSettingsFactory.ToAutoCreateWorldSettings(restored.WorldSettings);
        Check.True(generatedSettings.EnableCheats);
        Check.True(generatedSettings.EnablePyramidFilter);
        Check.True(generatedSettings.RequireCrimsonBetweenDungeonAndSpawn);
        Check.Equal(AutoCreateCrimsonDistance.Near, generatedSettings.CrimsonDistance);
        Check.Equal(AutoCreateJungleRouteDepth.VeryDeep, generatedSettings.JungleRouteDepth);
        Check.Equal(AutoCreateResourceFilterItem.BoomstickMask, generatedSettings.ResourceFilterItemMask);
        Check.Equal(5, generatedSettings.ResourceFilterLifeCrystalMinimum);
        Check.Equal(2, generatedSettings.ResourceFilterSpelunkerPotionMinimum);
        Check.Equal(1, generatedSettings.ResourceFilterFeatherfallPotionMinimum);
        RaceWorldSettings unsupportedAdvancedFilters = restored.WorldSettings with
        {
            SizeCode = 2,
            Cheats = restored.WorldSettings.Cheats with { PyramidEnabled = false }
        };
        Check.False(RaceWorldSettingsFactory.HasActiveFilters(unsupportedAdvancedFilters));
        AutoCreateWorldSettings unsupportedGeneratedSettings =
            RaceWorldSettingsFactory.ToAutoCreateWorldSettings(unsupportedAdvancedFilters);
        Check.False(unsupportedGeneratedSettings.RequireCrimsonBetweenDungeonAndSpawn);
        Check.Equal(AutoCreateJungleRouteDepth.None, unsupportedGeneratedSettings.JungleRouteDepth);
        Check.Equal(0, unsupportedGeneratedSettings.ResourceFilterItemMask);
        Check.Equal(0, unsupportedGeneratedSettings.ResourceFilterLifeCrystalMinimum);
        Check.True(RaceWorldSettingsFactory.HasActiveFilters(
            unsupportedAdvancedFilters with
            {
                Cheats = unsupportedAdvancedFilters.Cheats with { PyramidEnabled = true }
            }));
        Check.False(RaceWorldSettingsFactory.HasActiveFilters(
            restored.WorldSettings with { Cheats = restored.WorldSettings.Cheats with { Enabled = false } }));
        Check.Equal(uploaded.PackageRevision, restored.PackageRevision);
        Check.Equal(uploaded.Determinism!.CreateDigest(), restored.Determinism!.CreateDigest());
        Check.Equal("host", restored.Leaderboard.Single().Nickname);

        RaceWorldFilePublishRequest journeyRequest = Publish(
            created.RoomCode,
            "host",
            "journey") with
        {
            WorldSettings = Publish(created.RoomCode, "host", "journey").WorldSettings with
            {
                DifficultyCode = 4,
                PlayerDifficultyCode = RacePlayerDifficultyCodes.Softcore
            }
        };
        RaceRoomState journey = Success(manager.PublishWorldFile(journeyRequest));
        Check.Equal(
            RacePlayerDifficultyCodes.Journey,
            journey.WorldSettings!.PlayerDifficultyCode);
        Check.Equal(
            AutoCreatePlayerDifficulty.Journey,
            RaceWorldSettingsFactory.ToPlayerDifficultyForWorld(journey.WorldSettings));
    }

    private static RacePreparationStatusRequest Ready(
        string roomCode,
        string nickname,
        long packageRevision)
    {
        return new RacePreparationStatusRequest(
            roomCode,
            nickname,
            RacePlayerFileStatus.Ready,
            RaceWorldFileStatus.Ready,
            RaceRngControlStatus.Enabled,
            PackageRevision: packageRevision);
    }

    private static RacePlayerReadyRequest PlayerReady(
        string roomCode,
        string nickname,
        long packageRevision,
        bool isReady)
    {
        return new RacePlayerReadyRequest(roomCode, nickname, packageRevision, isReady);
    }

    private static void DeterministicCore()
    {
        byte[] entropy = Enumerable.Range(0, 32).Select(static value => (byte)value).ToArray();
        byte[] seed = DeterministicDomainSeed.Derive(entropy, 1, "npc-direct-drop", "13|1");
        Check.Equal(
            "C3A020B01EE944BF262DA2FBFB3663C50F6DA0451C4EAD337E4E82546AC9D237",
            Convert.ToHexString(seed));

        var counters = new DeterministicEventCounter();
        Check.Equal(1L, counters.Next("npc-direct-drop", "13"));
        Check.Equal(2L, counters.Next("npc-direct-drop", "13"));
        Check.Equal(1L, counters.Next("npc-direct-drop", "50"));

        string bossSource = DeterministicEventIdentity.NpcDropCounterSource(126, isBossDrop: true);
        string[] lootCategories = ["npc-direct-drop", "npc-boss-supplies", "npc-money", "npc-heals"];
        foreach (string category in lootCategories)
        {
            Check.Equal(1L, counters.Next(category, bossSource));
        }
        Check.Equal(2L, counters.Next("npc-boss-supplies", bossSource));

        string skeletronHandSource = DeterministicEventIdentity.NpcDropCounterSource(36, isBossDrop: false);
        string skeletronHeadSource = DeterministicEventIdentity.NpcDropCounterSource(35, isBossDrop: true);
        Check.False(string.Equals(skeletronHandSource, skeletronHeadSource, StringComparison.Ordinal));
        Check.Equal("npc|36|1", DeterministicEventIdentity.NpcDropEventKey(36, isBossDrop: false, 1));
        Check.Equal("boss|35|1", DeterministicEventIdentity.NpcDropEventKey(35, isBossDrop: true, 1));
        Check.Equal("boss|125|1", DeterministicEventIdentity.NpcDropEventKey(125, isBossDrop: true, 1));
        Check.Equal("boss|125|1", DeterministicEventIdentity.NpcDropEventKey(126, isBossDrop: true, 1));
        Check.Equal("boss|13|1", DeterministicEventIdentity.NpcDropEventKey(13, isBossDrop: true, 1));
        Check.Equal("boss|13|1", DeterministicEventIdentity.NpcDropEventKey(14, isBossDrop: true, 1));
        Check.Equal("boss|13|1", DeterministicEventIdentity.NpcDropEventKey(15, isBossDrop: true, 1));
        int[][] multipartBossGroups =
        [
            [13, 14, 15],
            [35, 36],
            [113, 114],
            [125, 126],
            [127, 128, 129, 130, 131],
            [134, 135, 136],
            [245, 246, 247, 248, 249],
            [396, 397, 398, 400, 401]
        ];
        foreach (int[] group in multipartBossGroups)
        {
            Check.Equal(1, group
                .Select(type => DeterministicEventIdentity.NpcDropCounterSource(type, isBossDrop: true))
                .Distinct(StringComparer.Ordinal)
                .Count());
            Check.Equal(group.Length, group
                .Select(type => DeterministicEventIdentity.NpcDropCounterSource(type, isBossDrop: false))
                .Distinct(StringComparer.Ordinal)
                .Count());
            Check.Equal(1, group
                .Select(type => DeterministicEventIdentity.NpcDropEventKey(type, isBossDrop: true, 1))
                .Distinct(StringComparer.Ordinal)
                .Count());
        }
        Check.False(string.Equals(
            DeterministicEventIdentity.NpcDropCounterSource(127, isBossDrop: true),
            DeterministicEventIdentity.NpcDropCounterSource(128, isBossDrop: false),
            StringComparison.Ordinal));
        Check.Equal(
            "world-identity",
            DeterministicEventIdentity.HardmodeAltarCounterSource("world-identity"));
        string destroyerContext = "npc-direct-drop|boss|134|1";
        string destroyerGlobalRule = DeterministicEventIdentity.NpcDropRuleEventKey(destroyerContext, 0);
        string destroyerHallowedBarsRule = DeterministicEventIdentity.NpcDropRuleEventKey(destroyerContext, 17);
        Check.Equal("npc-direct-drop|boss|134|1|rule|0", destroyerGlobalRule);
        Check.Equal("npc-direct-drop|boss|134|1|rule|17", destroyerHallowedBarsRule);
        Check.False(DeterministicDomainSeed.Derive(entropy, RaceDeterminismProtocol.CurrentVersion, "npc-drop-rule/main", destroyerGlobalRule)
            .SequenceEqual(DeterministicDomainSeed.Derive(entropy, RaceDeterminismProtocol.CurrentVersion, "npc-drop-rule/main", destroyerHallowedBarsRule)));
        Check.Equal(5, RaceDeterminismProtocol.CurrentVersion);
        Check.Equal(3, RaceDeterminismProtocol.CurrentChancePolicyVersion);

        bool repeatedRoll = DeterministicChanceRoller.Roll(entropy, 1, "alchemy-craft", "1|28|9|1", 1, 3);
        Check.Equal(
            repeatedRoll,
            DeterministicChanceRoller.Roll(entropy, 1, "alchemy-craft", "1|28|9|1", 1, 3));
        int independentSuccesses = 0;
        int nonGuaranteedBlocks = 0;
        for (int check = 0; check < 3_000; check++)
        {
            if (DeterministicChanceRoller.Roll(entropy, 1, "alchemy-craft", $"1|28|9|{check + 1}", 1, 3))
            {
                independentSuccesses++;
            }

            if (check % 3 == 2)
            {
                int blockSuccesses = Enumerable.Range(check - 2, 3).Count(index =>
                    DeterministicChanceRoller.Roll(entropy, 1, "alchemy-craft", $"block|{index + 1}", 1, 3));
                if (blockSuccesses != 1)
                {
                    nonGuaranteedBlocks++;
                }
            }
        }
        Check.True(independentSuccesses is > 900 and < 1_100);
        Check.True(nonGuaranteedBlocks > 0);

        var accumulator = new IntegerChanceAccumulator(0);
        int successes = 0;
        for (int tick = 1; tick <= 300; tick++)
        {
            bool success = accumulator.Step(1, 300);
            successes += success ? 1 : 0;
            Check.Equal(tick == 300, success);
        }

        Check.Equal(1, successes);

        accumulator = new IntegerChanceAccumulator(17);
        successes = 0;
        for (int check = 0; check < 300; check++)
        {
            if (accumulator.Step(1, 3))
            {
                successes++;
            }
        }
        Check.Equal(100, successes);
    }

    private static async Task VoiceAnnouncementJourney(CancellationToken cancellationToken)
    {
        Check.Equal(
            "玩家完成分段：月亮领主，用时一小时一分二秒。",
            RaceSpeechTextFormatter.FormatPreview(isChinese: true));
        Check.Equal(
            "Player completed split: Moon Lord. Time: one hour, one minute, two seconds.",
            RaceSpeechTextFormatter.FormatPreview(isChinese: false));
        Check.Equal(
            "玩家完成分段：月亮领主，用时1:01:02.03。",
            RaceSpeechTextFormatter.FormatGameMessage(
                "玩家",
                "月亮领主",
                3_662_030,
                isChinese: true));
        Check.Equal(
            "Player completed split: Moon Lord. Time: 1:01:02.03.",
            RaceSpeechTextFormatter.FormatGameMessage(
                "Player",
                "Moon Lord",
                3_662_030,
                isChinese: false));

        var engine = new ControlledRaceSpeechEngine();
        using var speech = new RaceSpeechCoordinator(engine);
        speech.ApplySettings(new RaceVoiceSettings
        {
            Enabled = true,
            SpeedPercent = 125,
            Volume = 80
        });

        RaceSpeechQueueItem playerA = VoiceItem("room", 1, "run-a", "Player A", 0, "Eye", 10_010, 1);
        RaceSpeechQueueItem playerB = VoiceItem("room", 1, "run-b", "Player B", 0, "Eye", 10_020, 2);
        Check.True(speech.Enqueue(playerA));
        Check.True(speech.Enqueue(playerB));
        Check.False(speech.Enqueue(playerA));

        await engine.WaitForCallCountAsync(1, cancellationToken);
        engine.CompleteNext();
        await engine.WaitForCallCountAsync(2, cancellationToken);
        Check.Sequence(
            new[]
            {
                "Player A completed split: Eye. Time: ten seconds.",
                "Player B completed split: Eye. Time: ten seconds."
            },
            engine.Calls.Select(static call => call.Text));

        RaceSpeechQueueItem obsolete = VoiceItem("room", 1, "old-run", "Player C", 1, "Bee", 20_000, 3);
        RaceSpeechQueueItem retained = VoiceItem("room", 1, "run-d", "Player D", 1, "Bee", 21_000, 4);
        Check.True(speech.Enqueue(obsolete));
        Check.True(speech.Enqueue(retained));
        speech.RemovePendingForPlayer(new RacePlayerProgressReset("room", 1, "new-run", "Player C"));
        engine.CompleteNext();
        await engine.WaitForCallCountAsync(3, cancellationToken);
        Check.True(engine.Calls[2].Text.StartsWith("Player D completed split", StringComparison.Ordinal));
        Check.Equal(125, engine.Calls[2].Settings.SpeedPercent);
        Check.Equal(80, engine.Calls[2].Settings.Volume);

        speech.Clear();
        speech.Preview(new RaceVoiceSettings
        {
            Enabled = true,
            SpeedPercent = 150,
            Volume = 70
        }, isChinese: true);
        await engine.WaitForCallCountAsync(4, cancellationToken);
        Check.Equal("玩家完成分段：月亮领主，用时一小时一分二秒。", engine.Calls[3].Text);
        Check.Equal(150, engine.Calls[3].Settings.SpeedPercent);

        RaceSpeechQueueItem priority = VoiceItem("room", 1, "run-e", "Player E", 2, "Wall", 30_000, 5);
        Check.True(speech.Enqueue(priority));
        await engine.WaitForCallCountAsync(5, cancellationToken);
        Check.True(engine.Calls[4].Text.StartsWith("Player E completed split", StringComparison.Ordinal));
        Check.Equal(125, engine.Calls[4].Settings.SpeedPercent);
        speech.Clear();
    }

    private static RaceSpeechQueueItem VoiceItem(
        string room,
        long revision,
        string runId,
        string nickname,
        int splitIndex,
        string splitName,
        long elapsed,
        long sequence)
    {
        return new RaceSpeechQueueItem(
            new RaceGroupCompleted(
                room,
                revision,
                runId,
                nickname,
                splitIndex,
                $"split-{splitIndex}",
                elapsed,
                sequence),
            splitName,
            IsChinese: false);
    }

    private static RaceWorldFilePublishRequest Publish(string room, string nickname, string revisionName) =>
        new(room, nickname, Route(), new RaceWorldSettings(
                "1.4.4.9",
                1,
                1,
                true,
                0,
                new RaceCheatSettings(
                    true,
                    true,
                    AutoCreatePyramidFilterItem.FlyingCarpetMask,
                    true,
                    AutoCreateCrimsonDistance.Near,
                    AutoCreateResourceFilterItem.BoomstickMask,
                    8,
                    2,
                    1,
                    AutoCreateJungleRouteDepth.VeryDeep),
                "race",
                PlayerDifficultyCode: RacePlayerDifficultyCodes.Hardcore),
            new RaceSeedAssignment("1234", RaceSeedSource.Fixed),
            new RaceWorldFileInfo(revisionName + ".wld", 128, revisionName, DateTimeOffset.UnixEpoch, nickname));

    private static RaceRoutePayload Route() => new("route-hash", "Route", "{}",
    [
        new RaceSplitDefinition(0, "split-0", "First"),
        new RaceSplitDefinition(1, "split-1", "Final", IsAttached: true)
    ]);

    private static RaceSplitReport Report(
        string room,
        string nickname,
        int index,
        long elapsed,
        string runId,
        long packageRevision = 1) =>
        new(room, nickname, index, $"split-{index}", elapsed)
        {
            PackageRevision = packageRevision,
            RunId = runId
        };

    private static RaceDeathReport Death(
        string room,
        string nickname,
        string runId,
        long packageRevision = 1,
        string deathMessage = "") =>
        new(room, nickname, DateTimeOffset.UnixEpoch, deathMessage)
        {
            PackageRevision = packageRevision,
            RunId = runId
        };

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow;

        public MutableTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan elapsed) => utcNow += elapsed;
    }

    private sealed class CloningSettingsSnapshotFactory : ISettingsSnapshotFactory
    {
        public AppSettings CreateSnapshot(AppSettings settings) => AppSettingsCloner.Clone(settings);
    }

    private static T Success<T>(RaceOperationResult<T> result)
    {
        if (!result.Succeeded || result.Value is null) throw new InvalidOperationException($"{result.ErrorCode}: {result.Message}");
        return result.Value;
    }

    private sealed class ControlledRaceSpeechEngine : IRaceSpeechEngine
    {
        private readonly SemaphoreSlim completions = new(0);
        private readonly List<SpeechCall> calls = [];

        public IReadOnlyList<SpeechCall> Calls
        {
            get
            {
                lock (calls)
                {
                    return calls.ToArray();
                }
            }
        }

        public IReadOnlyList<RaceVoiceOption> GetInstalledVoices() => [];

        public async Task SpeakAsync(string text, RaceVoiceSettings settings, CancellationToken cancellationToken)
        {
            lock (calls)
            {
                calls.Add(new SpeechCall(text, new RaceVoiceSettings
                {
                    Enabled = settings.Enabled,
                    VoiceName = settings.VoiceName,
                    SpeedPercent = settings.SpeedPercent,
                    Volume = settings.Volume
                }));
            }

            await completions.WaitAsync(cancellationToken);
        }

        public void CompleteNext()
        {
            completions.Release();
        }

        public async Task WaitForCallCountAsync(int count, CancellationToken cancellationToken)
        {
            while (Calls.Count < count)
            {
                await Task.Delay(5, cancellationToken);
            }
        }

        public sealed record SpeechCall(string Text, RaceVoiceSettings Settings);
    }
}
