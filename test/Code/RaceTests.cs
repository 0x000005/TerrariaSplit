using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using TerrariaSplit.Configuration;
using TerrariaSplit.Models;
using TerrariaSplit.Race.Client;
using TerrariaSplit.Race.Contracts;
using TerrariaSplit.Race.Server;
using TerrariaSplit.Storage;
using TerrariaSplit.UI;

namespace TerrariaSplit.Tests;

internal static class RaceTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("Race contracts round-trip room state JSON", RaceContractsRoundTripRoomStateJson);
        yield return ("Race contracts round-trip package and roster JSON", RaceContractsRoundTripPackageAndRosterJson);
        yield return ("Race route payload embeds host icon data", RaceRoutePayloadEmbedsHostIconData);
        yield return ("Race split report factory emits condition progress", RaceSplitReportFactoryEmitsConditionProgress);
        yield return ("Race split report factory skips single-icon partial progress", RaceSplitReportFactorySkipsSingleIconPartialProgress);
        yield return ("Race server enforces room rules and ranking", RaceServerEnforcesRoomRulesAndRanking);
        yield return ("Race server clears progress on world reupload", RaceServerClearsProgressOnWorldReupload);
        yield return ("Race server clears one player progress on reset", RaceServerClearsOnePlayerProgressOnReset);
        yield return ("Race server rejects stale package and run reports", RaceServerRejectsStalePackageAndRunReports);
        yield return ("Race server marks player running on start signal", RaceServerMarksPlayerRunningOnStartSignal);
        yield return ("Race server gives tied ranks for equal progress", RaceServerGivesTiedRanksForEqualProgress);
        yield return ("Race server ranks by completed splits and displays latest lit icon", RaceServerRanksByCompletedSplitsAndDisplaysLatestLitIcon);
        yield return ("Race server ignores single-icon partial progress", RaceServerIgnoresSingleIconPartialProgress);
        yield return ("Race server removes players on leave", RaceServerRemovesPlayersOnLeave);
        yield return ("Race server treats closed rooms as terminal", RaceServerTreatsClosedRoomsAsTerminal);
        yield return ("Race server lets host kick members", RaceServerLetsHostKickMembers);
        yield return ("Race client reconnect uses exponential backoff", RaceClientReconnectUsesExponentialBackoff);
        yield return ("Race world file validator requires an existing wld file", RaceWorldFileValidatorRequiresExistingWldFile);
        yield return ("Race world file store commits validated content by hash", RaceWorldFileStoreCommitsValidatedContentByHash);
        yield return ("Race client applies route override package", RaceClientAppliesRouteOverridePackage);
        yield return ("Race client materializes host custom route icons", RaceClientMaterializesHostCustomRouteIcons);
        yield return ("Race client ignores duplicate route override package", RaceClientIgnoresDuplicateRouteOverridePackage);
        yield return ("Race room applies payload on world publish", RaceRoomAppliesPayloadOnWorldPublish);
    }

    private static void RaceContractsRoundTripRoomStateJson()
    {
        RaceRoutePayload route = CreateRoutePayload("route-a", "Route A", splitCount: 2);
        var state = new RaceRoomState(
            "ABC123",
            RaceRoomStatus.Lobby,
            "host",
            route,
            new RaceWorldSettings("1.4.4.9", 1, 1, true, 0, 3, "race"),
            new RaceSeedAssignment("12345", RaceSeedSource.Fixed),
            null,
            [CreatePlayer("host", isHost: true)],
            [new RaceLeaderboardEntry(1, "host", RacePlayerStatus.Joined, 0, null, -1, -1, null, null, null, null, null)],
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);

        string json = JsonSerializer.Serialize(state);
        RaceRoomState? roundTrip = JsonSerializer.Deserialize<RaceRoomState>(json);

        TestAssert.Equal("ABC123", roundTrip?.RoomCode);
        TestAssert.Equal("12345", roundTrip?.Seed?.SeedText);
        TestAssert.Equal(2, roundTrip?.Route?.Splits.Count);
    }

    private static void RaceContractsRoundTripPackageAndRosterJson()
    {
        RaceRoomState state = CreateRoomStateWithWorld();
        var package = new RacePackageChanged(
            state,
            "host",
            RacePackageRevisionCalculator.Create(state));
        string packageJson = JsonSerializer.Serialize(package);
        RacePackageChanged? packageRoundTrip = JsonSerializer.Deserialize<RacePackageChanged>(packageJson);

        TestAssert.Equal("host", packageRoundTrip?.ActorNickname);
        TestAssert.Equal("ROOM", packageRoundTrip?.State.RoomCode);
        TestAssert.Equal(package.PackageRevision, packageRoundTrip?.PackageRevision);

        var roster = new RaceRosterChanged(RaceRoomStateUpdateKind.WorldReadyChanged, state, "guest");
        string rosterJson = JsonSerializer.Serialize(roster);
        RaceRosterChanged? rosterRoundTrip = JsonSerializer.Deserialize<RaceRosterChanged>(rosterJson);

        TestAssert.Equal(RaceRoomStateUpdateKind.WorldReadyChanged, rosterRoundTrip?.Kind);
        TestAssert.Equal("guest", rosterRoundTrip?.ActorNickname);
        TestAssert.Equal("ROOM", rosterRoundTrip?.State.RoomCode);
    }

    private static void RaceRoutePayloadEmbedsHostIconData()
    {
        string directory = Path.Combine(
            "test",
            "Temp",
            "race-route-icons-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string iconPath = Path.Combine(directory, "host-icon.png");
            byte[] iconBytes = [137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3, 4];
            File.WriteAllBytes(iconPath, iconBytes);

            AppSettings settings = AppSettingsDefaults.Create();
            string targetId = SplitCatalog.CreateItemTargetId(50);
            settings.Route.SplitRoute =
            [
                new SplitRouteEntry
                {
                    Id = "split:host-custom-icon",
                    DisplayName = "Host Custom Icon",
                    Enabled = true,
                    Condition = SplitCondition.All([SplitCatalog.CreateItemEverOwnedCondition(50, 1)]),
                    IconTargetIds = [targetId],
                    IconOverride = new SplitIconOverride
                    {
                        Source = SplitIconOverrideSource.CustomFile,
                        FilePath = iconPath
                    }
                }
            ];

            RaceRoutePayload payload = RaceRoutePayloadFactory.Create(settings);
            string expected = Convert.ToBase64String(iconBytes);

            TestAssert.Equal(true, payload.Icons.Count >= 1);
            RaceRouteIconPayload? splitIcon = payload.Icons.FirstOrDefault(icon =>
                string.Equals(icon.FileName, "host-icon.png", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(icon.DataBase64, expected, StringComparison.Ordinal));
            RaceRouteIconPayload? targetIcon = payload.Icons.FirstOrDefault(icon =>
                string.Equals(icon.Key, targetId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(icon.FileName, "host-icon.png", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(icon.DataBase64, expected, StringComparison.Ordinal));

            TestAssert.Equal(true, splitIcon is not null);
            TestAssert.Equal(true, targetIcon is not null);
            TestAssert.Equal(false, payload.SerializedRouteJson.Contains(directory, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void RaceServerEnforcesRoomRulesAndRanking()
    {
        var store = new InMemoryRaceRecordStore();
        var manager = new RaceRoomManager(store);
        RaceRoutePayload route = CreateRoutePayload("race-route", "Race Route", splitCount: 3);
        RaceWorldSettings world = new("1.4.4.9", 1, 1, true, 0, 3, "race");

        RaceOperationResult<RaceRoomState> created = manager.CreateRoom(new RaceRoomCreateRequest("host"));
        RequireSuccess(created);
        string roomCode = created.Value!.RoomCode;

        RequireSuccess(manager.JoinRoom(new RaceRoomJoinRequest(roomCode, "guest")));
        RaceOperationResult<RaceRoomState> duplicate = manager.JoinRoom(new RaceRoomJoinRequest(roomCode, "guest"));
        TestAssert.Equal(false, duplicate.Succeeded);
        TestAssert.Equal(RaceErrors.NicknameTaken, duplicate.ErrorCode);

        RaceOperationResult<RaceRoomState> uploaded = manager.PublishWorldFile(
            new RaceWorldFilePublishRequest(
                roomCode,
                "host",
                route,
                world,
                new RaceSeedAssignment("1234", RaceSeedSource.Fixed),
                new RaceWorldFileInfo("race.wld", 128, "abc", DateTimeOffset.UnixEpoch, "host")));
        RequireSuccess(uploaded);
        TestAssert.Equal(RaceRoomStatus.WorldUploaded, uploaded.Value!.Status);
        RaceOperationResult<RaceRoomState> ready = manager.MarkWorldReady(new RaceWorldReadyRequest(roomCode, "guest", true));
        RequireSuccess(ready);
        TestAssert.Equal(RaceRoomStatus.Ready, ready.Value!.Status);

        RequireSuccess(manager.ReportSplit(CreateReport(roomCode, "guest", 0, 5_000)));
        RaceOperationResult<RaceRoomState> hostFirst = manager.ReportSplit(CreateReport(roomCode, "host", 0, 4_000));
        RequireSuccess(hostFirst);
        TestAssert.Equal("host", hostFirst.Value!.Leaderboard[0].Nickname);

        RaceOperationResult<RaceRoomState> duplicateSplit = manager.ReportSplit(CreateReport(roomCode, "guest", 0, 3_000));
        RequireSuccess(duplicateSplit);
        RaceLeaderboardEntry guestEntry = duplicateSplit.Value!.Leaderboard.First(entry => entry.Nickname == "guest");
        TestAssert.Equal(5_000L, guestEntry.LastSplitElapsedMilliseconds);

        RequireSuccess(manager.ReportSplit(CreateReport(roomCode, "guest", 1, 7_000)));
        RaceOperationResult<RaceRoomState> hostSecond = manager.ReportSplit(CreateReport(roomCode, "host", 1, 8_000));
        RequireSuccess(hostSecond);
        TestAssert.Equal("guest", hostSecond.Value!.Leaderboard[0].Nickname);

        RequireSuccess(manager.ReportSplit(CreateReport(roomCode, "host", 2, 10_000)));
        RaceOperationResult<RaceRoomState> finalSplit = manager.ReportSplit(CreateReport(roomCode, "guest", 2, 9_000));
        RequireSuccess(finalSplit);

        TestAssert.Equal(RaceRoomStatus.Running, finalSplit.Value!.Status);
        TestAssert.Equal("guest", finalSplit.Value.Leaderboard[0].Nickname);
        TestAssert.Equal(9_000L, finalSplit.Value.Leaderboard[0].LastSplitElapsedMilliseconds);
        RequireSuccess(manager.CloseRoom(roomCode, "host"));
        TestAssert.Equal(1, store.Records.Count);
    }

    private static void RaceServerClearsProgressOnWorldReupload()
    {
        var manager = new RaceRoomManager(new InMemoryRaceRecordStore());
        RaceRoutePayload route = CreateRoutePayload("race-route", "Race Route", splitCount: 2);
        RaceWorldSettings world = new("1.4.5.6", 1, 1, true, 0, 0, "race");

        RaceOperationResult<RaceRoomState> created = manager.CreateRoom(new RaceRoomCreateRequest("host"));
        RequireSuccess(created);
        string roomCode = created.Value!.RoomCode;
        RequireSuccess(manager.JoinRoom(new RaceRoomJoinRequest(roomCode, "guest")));
        RequireSuccess(manager.PublishWorldFile(
            new RaceWorldFilePublishRequest(
                roomCode,
                "host",
                route,
                world,
                new RaceSeedAssignment("1234", RaceSeedSource.Fixed),
                new RaceWorldFileInfo("race-old.wld", 128, "old", DateTimeOffset.UnixEpoch, "host"))));

        RequireSuccess(manager.ReportSplit(CreateReport(roomCode, "host", 0, 4_000)));
        RequireSuccess(manager.ReportSplit(CreateReport(roomCode, "guest", 0, 5_000)));

        RaceOperationResult<RaceRoomState> reuploaded = manager.PublishWorldFile(
            new RaceWorldFilePublishRequest(
                roomCode,
                "host",
                route,
                world,
                new RaceSeedAssignment("5678", RaceSeedSource.Fixed),
                new RaceWorldFileInfo("race-new.wld", 256, "new", DateTimeOffset.UnixEpoch.AddSeconds(1), "host")));
        RequireSuccess(reuploaded);

        RaceRoomState state = reuploaded.Value!;
        TestAssert.Equal(RaceRoomStatus.WorldUploaded, state.Status);
        TestAssert.Equal(true, state.Leaderboard.All(entry => entry.Rank == 1));
        TestAssert.Equal(true, state.Leaderboard.All(entry => entry.CompletedSplitCount == 0));
        TestAssert.Equal(true, state.Leaderboard.All(entry => entry.LastSplitIndex == -1));
        TestAssert.Equal(true, state.Leaderboard.All(entry => entry.LastSplitElapsedMilliseconds is null));
        TestAssert.Equal(true, state.Players.All(player => player.CompletedSplitCount == 0));
        TestAssert.Equal(true, state.Players.All(player => player.LastSplitElapsedMilliseconds is null));
    }

    private static void RaceServerClearsOnePlayerProgressOnReset()
    {
        var manager = new RaceRoomManager(new InMemoryRaceRecordStore());
        RaceRoutePayload route = CreateRoutePayload("race-route", "Race Route", splitCount: 2);
        RaceWorldSettings world = new("1.4.5.6", 1, 1, true, 0, 0, "race");

        RaceOperationResult<RaceRoomState> created = manager.CreateRoom(new RaceRoomCreateRequest("host"));
        RequireSuccess(created);
        string roomCode = created.Value!.RoomCode;
        RequireSuccess(manager.JoinRoom(new RaceRoomJoinRequest(roomCode, "guest")));
        RequireSuccess(manager.PublishWorldFile(
            new RaceWorldFilePublishRequest(
                roomCode,
                "host",
                route,
                world,
                new RaceSeedAssignment("1234", RaceSeedSource.Fixed),
                new RaceWorldFileInfo("race.wld", 128, "abc", DateTimeOffset.UnixEpoch, "host"))));

        RequireSuccess(manager.ReportSplit(CreateReport(roomCode, "host", 0, 4_000)));
        RequireSuccess(manager.ReportSplit(CreateReport(roomCode, "guest", 0, 5_000)));

        RaceOperationResult<RaceRoomState> reset = manager.ResetPlayerProgress(
            new RaceProgressResetRequest(roomCode, "guest", PackageRevision: 1, RunId: "run-2"));
        RequireSuccess(reset);

        RaceRoomState state = reset.Value!;
        RaceLeaderboardEntry guest = state.Leaderboard.Single(entry => entry.Nickname == "guest");
        RaceLeaderboardEntry host = state.Leaderboard.Single(entry => entry.Nickname == "host");
        TestAssert.Equal(0, guest.CompletedSplitCount);
        TestAssert.Equal(-1, guest.LastSplitIndex);
        TestAssert.Equal(null, guest.LastSplitElapsedMilliseconds);
        TestAssert.Equal(1, host.CompletedSplitCount);
        TestAssert.Equal(0, host.LastSplitIndex);
        TestAssert.Equal(4_000L, host.LastSplitElapsedMilliseconds);
        RacePlayerState guestState = state.Players.Single(player => player.Nickname == "guest");
        TestAssert.Equal(RacePlayerStatus.WorldReady, guestState.Status);
        TestAssert.Equal(true, guestState.WorldReady);
    }

    private static void RaceServerRejectsStalePackageAndRunReports()
    {
        var manager = new RaceRoomManager(new InMemoryRaceRecordStore());
        RaceRoutePayload route = CreateRoutePayload("race-route", "Race Route", splitCount: 2);
        RaceWorldSettings world = new("1.4.5.6", 1, 1, true, 0, 0);
        RaceRoomState created = manager.CreateRoom(new RaceRoomCreateRequest("host")).Value!;
        string roomCode = created.RoomCode;
        RequireSuccess(manager.PublishWorldFile(
            new RaceWorldFilePublishRequest(
                roomCode,
                "host",
                route,
                world,
                new RaceSeedAssignment("1234", RaceSeedSource.Fixed),
                new RaceWorldFileInfo("race.wld", 128, "abc", DateTimeOffset.UnixEpoch, "host"))));

        RequireSuccess(manager.ReportSplit(CreateReport(roomCode, "host", 0, 4_000)));
        RequireSuccess(manager.PublishWorldFile(
            new RaceWorldFilePublishRequest(
                roomCode,
                "host",
                route,
                world,
                new RaceSeedAssignment("5678", RaceSeedSource.Fixed),
                new RaceWorldFileInfo("race-2.wld", 128, "def", DateTimeOffset.UnixEpoch.AddSeconds(1), "host"))));

        RaceOperationResult<RaceRoomState> stalePackage = manager.ReportSplit(
            CreateReport(roomCode, "host", 0, 5_000));
        TestAssert.Equal(false, stalePackage.Succeeded);
        TestAssert.Equal(RaceErrors.StalePackage, stalePackage.ErrorCode);

        RequireSuccess(manager.ResetPlayerProgress(
            new RaceProgressResetRequest(roomCode, "host", PackageRevision: 2, RunId: "run-2")));
        RaceOperationResult<RaceRoomState> staleRun = manager.ReportSplit(
            CreateReport(roomCode, "host", 0, 5_000) with
            {
                PackageRevision = 2,
                RunId = "run-1"
            });
        TestAssert.Equal(false, staleRun.Succeeded);
        TestAssert.Equal(RaceErrors.StaleRun, staleRun.ErrorCode);

        RequireSuccess(manager.ReportSplit(
            CreateReport(roomCode, "host", 0, 5_000) with
            {
                PackageRevision = 2,
                RunId = "run-2"
            }));
    }

    private static void RaceServerMarksPlayerRunningOnStartSignal()
    {
        var manager = new RaceRoomManager(new InMemoryRaceRecordStore());
        RaceRoutePayload route = CreateRoutePayload("race-route", "Race Route", splitCount: 2);
        RaceWorldSettings world = new("1.4.5.6", 1, 1, true, 0, 0, "race");

        RaceOperationResult<RaceRoomState> created = manager.CreateRoom(new RaceRoomCreateRequest("host"));
        RequireSuccess(created);
        string roomCode = created.Value!.RoomCode;
        RequireSuccess(manager.JoinRoom(new RaceRoomJoinRequest(roomCode, "guest")));
        RequireSuccess(manager.PublishWorldFile(
            new RaceWorldFilePublishRequest(
                roomCode,
                "host",
                route,
                world,
                new RaceSeedAssignment("1234", RaceSeedSource.Fixed),
                new RaceWorldFileInfo("race.wld", 128, "abc", DateTimeOffset.UnixEpoch, "host"))));

        RaceOperationResult<RaceRoomState> started = manager.ReportStart(new RaceRunStartReport(
            roomCode,
            "guest",
            DateTimeOffset.UnixEpoch)
        {
            PackageRevision = 1,
            RunId = "run-1"
        });
        RequireSuccess(started);

        RaceRoomState state = started.Value!;
        TestAssert.Equal(RaceRoomStatus.Running, state.Status);
        RacePlayerState guest = state.Players.Single(player => player.Nickname == "guest");
        TestAssert.Equal(RacePlayerStatus.Running, guest.Status);
        TestAssert.Equal(true, guest.WorldReady);
        TestAssert.Equal(0, guest.CompletedSplitCount);
        RaceLeaderboardEntry guestEntry = state.Leaderboard.Single(entry => entry.Nickname == "guest");
        TestAssert.Equal(0, guestEntry.CompletedSplitCount);
        TestAssert.Equal(-1, guestEntry.LastSplitIndex);
        TestAssert.Equal(null, guestEntry.LastSplitElapsedMilliseconds);
        TestAssert.Equal(true, state.Leaderboard.All(entry => entry.Rank == 1));
    }

    private static void RaceSplitReportFactoryEmitsConditionProgress()
    {
        SplitTargetDefinition destroyer = RequireTarget(SplitCatalog.Destroyer);
        SplitTargetDefinition twins = RequireTarget(SplitCatalog.Twins);
        var definition = new SplitDefinition(
            "split-mechs",
            "Mechs",
            SplitCondition.AtLeast(
            [
                SplitCatalog.CreateBossFactCondition(SplitCatalog.Destroyer),
                SplitCatalog.CreateBossFactCondition(SplitCatalog.Twins)
            ], 2),
            ["host-destroyer.png", "host-twins.png"],
            [destroyer.Id, twins.Id],
            [destroyer.Id, twins.Id]);
        var status = new SplitStatusSnapshot(
            definition,
            TimeSpan.FromSeconds(12),
            IsSkipped: false,
            [destroyer.FactKey, twins.FactKey],
            new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
            {
                [destroyer.FactKey] = TimeSpan.FromSeconds(10),
                [twins.FactKey] = TimeSpan.FromSeconds(12)
            });

        IReadOnlyList<RaceSplitReport> reports = RaceSplitReportFactory.CreateProgressReports(
            "ABC123",
            "runner",
            [status]);

        TestAssert.Equal(2, reports.Count);
        TestAssert.Equal(0, reports[0].ConditionIndex);
        TestAssert.Equal(10_000L, reports[0].ElapsedMilliseconds);
        TestAssert.Equal(false, reports[0].IsSplitComplete);
        TestAssert.Equal(destroyer.Id, reports[0].TargetId);
        TestAssert.Equal("host-destroyer.png", reports[0].IconFileName);
        TestAssert.Equal(1, reports[1].ConditionIndex);
        TestAssert.Equal(12_000L, reports[1].ElapsedMilliseconds);
        TestAssert.Equal(true, reports[1].IsSplitComplete);
        TestAssert.Equal("host-twins.png", reports[1].IconFileName);
    }

    private static void RaceServerGivesTiedRanksForEqualProgress()
    {
        var manager = new RaceRoomManager(new InMemoryRaceRecordStore());
        RaceRoutePayload route = CreateRoutePayload("race-route", "Race Route", splitCount: 2);
        RaceWorldSettings world = new("1.4.5.6", 1, 1, true, 0, 0, "race");
        RaceOperationResult<RaceRoomState> created = manager.CreateRoom(new RaceRoomCreateRequest("host"));
        RequireSuccess(created);
        string roomCode = created.Value!.RoomCode;

        RaceOperationResult<RaceRoomState> joined = manager.JoinRoom(new RaceRoomJoinRequest(roomCode, "guest"));
        RequireSuccess(joined);
        TestAssert.Equal(true, joined.Value!.Leaderboard.All(entry => entry.Rank == 1));
        RequireSuccess(manager.PublishWorldFile(
            new RaceWorldFilePublishRequest(
                roomCode,
                "host",
                route,
                world,
                new RaceSeedAssignment("1234", RaceSeedSource.Fixed),
                new RaceWorldFileInfo("race.wld", 128, "abc", DateTimeOffset.UnixEpoch, "host"))));

        RequireSuccess(manager.ReportSplit(CreateReport(roomCode, "host", 0, 5_000)));
        RaceOperationResult<RaceRoomState> tied = manager.ReportSplit(CreateReport(roomCode, "guest", 0, 5_000));
        RequireSuccess(tied);
        TestAssert.Equal(true, tied.Value!.Leaderboard.All(entry => entry.Rank == 1));

        RaceOperationResult<RaceRoomState> hostAhead = manager.ReportSplit(CreateReport(roomCode, "host", 1, 8_000));
        RequireSuccess(hostAhead);
        RaceLeaderboardEntry host = hostAhead.Value!.Leaderboard.First(entry => entry.Nickname == "host");
        RaceLeaderboardEntry guest = hostAhead.Value.Leaderboard.First(entry => entry.Nickname == "guest");
        TestAssert.Equal(1, host.Rank);
        TestAssert.Equal(2, guest.Rank);
    }

    private static void RaceSplitReportFactorySkipsSingleIconPartialProgress()
    {
        SplitTargetDefinition destroyer = RequireTarget(SplitCatalog.Destroyer);
        SplitTargetDefinition twins = RequireTarget(SplitCatalog.Twins);
        var definition = new SplitDefinition(
            "split-mechs-single",
            "Mechs",
            SplitCondition.AtLeast(
            [
                SplitCatalog.CreateBossFactCondition(SplitCatalog.Destroyer),
                SplitCatalog.CreateBossFactCondition(SplitCatalog.Twins)
            ], 2),
            ["single.gif"],
            ["custom-icon:mechs"],
            [destroyer.Id, twins.Id]);
        var partialStatus = new SplitStatusSnapshot(
            definition,
            Time: null,
            IsSkipped: false,
            [destroyer.FactKey],
            new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
            {
                [destroyer.FactKey] = TimeSpan.FromSeconds(10)
            });

        IReadOnlyList<RaceSplitReport> partialReports = RaceSplitReportFactory.CreateProgressReports(
            "ABC123",
            "runner",
            [partialStatus]);

        TestAssert.Equal(0, partialReports.Count);

        var completedStatus = partialStatus with
        {
            Time = TimeSpan.FromSeconds(12),
            CompletedFactKeys = [destroyer.FactKey, twins.FactKey],
            FactCompletionTimes = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
            {
                [destroyer.FactKey] = TimeSpan.FromSeconds(10),
                [twins.FactKey] = TimeSpan.FromSeconds(12)
            }
        };
        IReadOnlyList<RaceSplitReport> completedReports = RaceSplitReportFactory.CreateProgressReports(
            "ABC123",
            "runner",
            [completedStatus]);

        TestAssert.Equal(1, completedReports.Count);
        TestAssert.Equal(true, completedReports[0].IsSplitComplete);
        TestAssert.Equal("single.gif", completedReports[0].IconFileName);
        TestAssert.Equal(12_000L, completedReports[0].ElapsedMilliseconds);
    }

    private static void RaceServerRanksByCompletedSplitsAndDisplaysLatestLitIcon()
    {
        var manager = new RaceRoomManager(new InMemoryRaceRecordStore());
        RaceRoutePayload route = CreateRoutePayloadWithProgressSplit();
        RaceWorldSettings world = new("1.4.5.6", 1, 1, true, 0, 0, "race");
        RaceOperationResult<RaceRoomState> created = manager.CreateRoom(new RaceRoomCreateRequest("host"));
        RequireSuccess(created);
        string roomCode = created.Value!.RoomCode;
        RequireSuccess(manager.JoinRoom(new RaceRoomJoinRequest(roomCode, "guest")));
        RequireSuccess(manager.PublishWorldFile(
            new RaceWorldFilePublishRequest(
                roomCode,
                "host",
                route,
                world,
                new RaceSeedAssignment("1234", RaceSeedSource.Fixed),
                new RaceWorldFileInfo("race.wld", 128, "abc", DateTimeOffset.UnixEpoch, "host"))));

        RequireSuccess(manager.ReportSplit(CreateReport(roomCode, "host", 0, 5_000)));
        RequireSuccess(manager.ReportSplit(CreateReport(roomCode, "guest", 0, 4_000)));

        RaceOperationResult<RaceRoomState> guestAhead = manager.ReportSplit(CreateReport(
            roomCode,
            "host",
            1,
            6_000,
            conditionIndex: 1,
            factKey: "fact-b",
            isSplitComplete: false));
        RequireSuccess(guestAhead);
        TestAssert.Equal("guest", guestAhead.Value!.Leaderboard[0].Nickname);
        RaceLeaderboardEntry hostPartial = guestAhead.Value.Leaderboard.First(entry => entry.Nickname == "host");
        TestAssert.Equal(1, hostPartial.CompletedSplitCount);
        TestAssert.Equal(1, hostPartial.LastSplitIndex);
        TestAssert.Equal(1, hostPartial.LastConditionIndex);
        TestAssert.Equal("host-b.png", hostPartial.LastIconFileName);
        TestAssert.Equal(6_000L, hostPartial.LastSplitElapsedMilliseconds);

        RaceOperationResult<RaceRoomState> hostCompletesNextSplit = manager.ReportSplit(CreateReport(
            roomCode,
            "host",
            1,
            7_000,
            conditionIndex: 0,
            factKey: "fact-a",
            isSplitComplete: true));
        RequireSuccess(hostCompletesNextSplit);
        TestAssert.Equal("host", hostCompletesNextSplit.Value!.Leaderboard[0].Nickname);
        RaceLeaderboardEntry hostLaterIcon = hostCompletesNextSplit.Value.Leaderboard.First(entry => entry.Nickname == "host");
        TestAssert.Equal(0, hostLaterIcon.LastConditionIndex);
        TestAssert.Equal("host-a.png", hostLaterIcon.LastIconFileName);
        TestAssert.Equal(7_000L, hostLaterIcon.LastSplitElapsedMilliseconds);

        RaceOperationResult<RaceRoomState> lateCompletedGroupProgress = manager.ReportSplit(CreateReport(
            roomCode,
            "host",
            1,
            7_500,
            conditionIndex: 2,
            factKey: "fact-e",
            isSplitComplete: false));
        RequireSuccess(lateCompletedGroupProgress);
        RaceLeaderboardEntry hostAfterLateSameGroupIcon = lateCompletedGroupProgress.Value!.Leaderboard.First(entry => entry.Nickname == "host");
        TestAssert.Equal(0, hostAfterLateSameGroupIcon.LastConditionIndex);
        TestAssert.Equal("host-a.png", hostAfterLateSameGroupIcon.LastIconFileName);
        TestAssert.Equal(7_000L, hostAfterLateSameGroupIcon.LastSplitElapsedMilliseconds);

        RaceOperationResult<RaceRoomState> laterUnfinishedProgress = manager.ReportSplit(CreateReport(
            roomCode,
            "host",
            2,
            8_000,
            conditionIndex: 1,
            factKey: "fact-d",
            isSplitComplete: false));
        RequireSuccess(laterUnfinishedProgress);
        TestAssert.Equal("host", laterUnfinishedProgress.Value!.Leaderboard[0].Nickname);
        RaceLeaderboardEntry hostUnfinishedIcon = laterUnfinishedProgress.Value.Leaderboard.First(entry => entry.Nickname == "host");
        TestAssert.Equal(2, hostUnfinishedIcon.LastSplitIndex);
        TestAssert.Equal(1, hostUnfinishedIcon.LastConditionIndex);
        TestAssert.Equal("host-d.png", hostUnfinishedIcon.LastIconFileName);
        TestAssert.Equal(8_000L, hostUnfinishedIcon.LastSplitElapsedMilliseconds);
    }

    private static void RaceServerIgnoresSingleIconPartialProgress()
    {
        var manager = new RaceRoomManager(new InMemoryRaceRecordStore());
        RaceRoutePayload route = CreateRoutePayloadWithSingleIconProgressSplit();
        RaceWorldSettings world = new("1.4.5.6", 1, 1, true, 0, 0, "race");
        RaceOperationResult<RaceRoomState> created = manager.CreateRoom(new RaceRoomCreateRequest("host"));
        RequireSuccess(created);
        string roomCode = created.Value!.RoomCode;
        RequireSuccess(manager.PublishWorldFile(
            new RaceWorldFilePublishRequest(
                roomCode,
                "host",
                route,
                world,
                new RaceSeedAssignment("1234", RaceSeedSource.Fixed),
                new RaceWorldFileInfo("race.wld", 128, "abc", DateTimeOffset.UnixEpoch, "host"))));

        RequireSuccess(manager.ReportSplit(CreateReport(roomCode, "host", 0, 5_000)));
        RaceOperationResult<RaceRoomState> partial = manager.ReportSplit(CreateReport(
            roomCode,
            "host",
            1,
            6_000,
            conditionIndex: 1,
            factKey: "fact-b",
            isSplitComplete: false));
        RequireSuccess(partial);

        RaceLeaderboardEntry entry = partial.Value!.Leaderboard.First(item => item.Nickname == "host");
        TestAssert.Equal(1, entry.CompletedSplitCount);
        TestAssert.Equal(0, entry.LastSplitIndex);
        TestAssert.Equal(5_000L, entry.LastSplitElapsedMilliseconds);
    }

    private static void RaceServerRemovesPlayersOnLeave()
    {
        var manager = new RaceRoomManager(new InMemoryRaceRecordStore());
        RaceOperationResult<RaceRoomState> created = manager.CreateRoom(new RaceRoomCreateRequest("host"));
        RequireSuccess(created);
        string roomCode = created.Value!.RoomCode;
        RequireSuccess(manager.JoinRoom(new RaceRoomJoinRequest(roomCode, "guest")));

        RaceOperationResult<RaceRoomState> left = manager.LeaveRoom(roomCode, "guest");
        RequireSuccess(left);

        TestAssert.Equal(false, left.Value!.Players.Any(player => player.Nickname == "guest"));
        TestAssert.Equal(false, left.Value.Leaderboard.Any(entry => entry.Nickname == "guest"));
        TestAssert.Equal(true, left.Value.Players.Any(player => player.Nickname == "host"));

        RaceOperationResult<RaceRoomState> hostLeft = manager.LeaveRoom(roomCode, "host");
        RequireSuccess(hostLeft);
        TestAssert.Equal(RaceRoomStatus.Closed, hostLeft.Value!.Status);
        TestAssert.Equal(false, hostLeft.Value.Players.Any(player => player.Nickname == "host"));
    }

    private static void RaceServerLetsHostKickMembers()
    {
        var manager = new RaceRoomManager(new InMemoryRaceRecordStore());
        RaceRoutePayload route = CreateRoutePayload("race-route", "Race Route", splitCount: 2);
        RaceWorldSettings world = new("1.4.5.6", 1, 1, true, 0, 0, "race");
        RaceOperationResult<RaceRoomState> created = manager.CreateRoom(new RaceRoomCreateRequest("host"));
        RequireSuccess(created);
        string roomCode = created.Value!.RoomCode;
        RequireSuccess(manager.JoinRoom(new RaceRoomJoinRequest(roomCode, "guest")));
        RequireSuccess(manager.PublishWorldFile(
            new RaceWorldFilePublishRequest(
                roomCode,
                "host",
                route,
                world,
                new RaceSeedAssignment("1234", RaceSeedSource.Fixed),
                new RaceWorldFileInfo("race.wld", 128, "abc", DateTimeOffset.UnixEpoch, "host"))));
        RequireSuccess(manager.ReportSplit(CreateReport(roomCode, "guest", 0, 5_000)));

        RaceOperationResult<RaceRoomState> kicked = manager.KickPlayer(
            new RacePlayerKickRequest(roomCode, "host", "guest"));
        RequireSuccess(kicked);

        TestAssert.Equal(false, kicked.Value!.Players.Any(player => player.Nickname == "guest"));
        TestAssert.Equal(false, kicked.Value.Leaderboard.Any(entry => entry.Nickname == "guest"));
        TestAssert.Equal(true, kicked.Value.Players.Any(player => player.Nickname == "host"));

        RaceOperationResult<RaceRoomState> kickHost = manager.KickPlayer(
            new RacePlayerKickRequest(roomCode, "host", "host"));
        TestAssert.Equal(false, kickHost.Succeeded);
        TestAssert.Equal(RaceErrors.CannotKickHost, kickHost.ErrorCode);
    }

    private static void RaceClientReconnectUsesExponentialBackoff()
    {
        var policy = new RaceReconnectRetryPolicy();

        TestAssert.Equal(TimeSpan.FromSeconds(1), policy.NextRetryDelay(new RetryContext { PreviousRetryCount = 0 }));
        TestAssert.Equal(TimeSpan.FromSeconds(2), policy.NextRetryDelay(new RetryContext { PreviousRetryCount = 1 }));
        TestAssert.Equal(TimeSpan.FromSeconds(4), policy.NextRetryDelay(new RetryContext { PreviousRetryCount = 2 }));
        TestAssert.Equal(TimeSpan.FromSeconds(8), policy.NextRetryDelay(new RetryContext { PreviousRetryCount = 3 }));
    }

    private static void RaceWorldFileValidatorRequiresExistingWldFile()
    {
        string directory = Path.Combine(
            "test",
            "Temp",
            "race-world-file-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string textPath = Path.Combine(directory, "not-world.txt");
            string invalidWorldPath = Path.Combine(directory, "invalid.wld");
            string worldPath = Path.Combine(directory, "world.wld");
            string missingWorldPath = Path.Combine(directory, "missing.wld");
            File.WriteAllText(textPath, "not a world file");
            File.WriteAllText(invalidWorldPath, "not a world file");
            CreateMinimalWorldFile(worldPath);

            TestAssert.Equal(false, RaceWorldFileValidator.IsValidWorldFilePath(null));
            TestAssert.Equal(false, RaceWorldFileValidator.IsValidWorldFilePath(string.Empty));
            TestAssert.Equal(false, RaceWorldFileValidator.IsValidWorldFilePath(textPath));
            TestAssert.Equal(false, RaceWorldFileValidator.IsValidWorldFilePath(invalidWorldPath));
            TestAssert.Equal(false, RaceWorldFileValidator.IsValidWorldFilePath(missingWorldPath));
            TestAssert.Equal(true, RaceWorldFileValidator.IsValidWorldFilePath(worldPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void RaceClientAppliesRouteOverridePackage()
    {
        AppSettings local = AppSettingsDefaults.Create();
        int originalCount = local.Route.SplitRoute.Count;
        local.Route.ExpandSplitDetails = false;
        local.Route.EnableVisibleGroupCountLimit = false;
        local.Route.VisibleGroupCountLimit = 3;
        local.Overlay.ShowEarlyDeltaTime = false;
        local.Overlay.Columns.ScalePercent = 64;
        AppSettings host = AppSettingsDefaults.Create();
        host.Route.SplitRoute = host.Route.SplitRoute.Take(2).ToList();
        host.Route.SplitRoute[0].DisplayName = "Race First";
        host.Route.ExpandSplitDetails = true;
        host.Route.EnableVisibleGroupCountLimit = true;
        host.Route.VisibleGroupCountLimit = 9;
        host.Overlay.ShowEarlyDeltaTime = true;
        host.Overlay.Columns.ScalePercent = 180;
        string hostReferenceKey = SplitConditionDataRows.Build(host)[0].Key;
        host.Comparison.UsePersonalBestAsReferenceTime = true;
        host.Comparison.PersonalBestTimes[hostReferenceKey] = "1:23.45";
        RaceRoutePayload payload = RaceRoutePayloadFactory.Create(host);
        var overrides = new RaceRouteOverrideController(new StoredSettingsSnapshotFactory());

        bool applied = overrides.TryApply(local, payload, out AppSettings raceSettings, out string detail);
        TestAssert.Equal(true, applied);
        TestAssert.Equal(string.Empty, detail);
        TestAssert.Equal(2, raceSettings.Route.SplitRoute.Count);
        TestAssert.Equal("Race First", raceSettings.Route.SplitRoute[0].DisplayName);
        TestAssert.Equal(false, raceSettings.Route.ExpandSplitDetails);
        TestAssert.Equal(false, raceSettings.Route.EnableVisibleGroupCountLimit);
        TestAssert.Equal(3, raceSettings.Route.VisibleGroupCountLimit);
        TestAssert.Equal(false, raceSettings.Overlay.ShowEarlyDeltaTime);
        TestAssert.Equal(64, raceSettings.Overlay.Columns.ScalePercent);
        TestAssert.Equal(false, raceSettings.Comparison.UsePersonalBestAsReferenceTime);
        TestAssert.Equal("Race Reference", raceSettings.Comparison.ActiveReferenceSplitSet);
        TestAssert.Equal("1:23.45", ReferenceSplitSetService.GetReferenceText(raceSettings, hostReferenceKey));

        bool cleared = overrides.Clear();
        TestAssert.Equal(true, cleared);

        AppSettings changedLocal = AppSettingsDefaults.Create();
        changedLocal.Route.SplitRoute = changedLocal.Route.SplitRoute.Take(originalCount).ToList();
        changedLocal.Route.ExpandSplitDetails = true;
        changedLocal.Overlay.ShowEarlyDeltaTime = true;
        TestAssert.Equal(true, overrides.TryCreatePackage(payload, out SettingsRouteOverridePackage package, out detail));
        AppSettings rebasedRaceSettings = SettingsRouteOverrideService.Apply(
            changedLocal,
            package,
            new StoredSettingsSnapshotFactory());
        TestAssert.Equal(2, rebasedRaceSettings.Route.SplitRoute.Count);
        TestAssert.Equal("Race First", rebasedRaceSettings.Route.SplitRoute[0].DisplayName);
        TestAssert.Equal(true, rebasedRaceSettings.Route.ExpandSplitDetails);
        TestAssert.Equal(true, rebasedRaceSettings.Overlay.ShowEarlyDeltaTime);
    }

    private static void RaceClientIgnoresDuplicateRouteOverridePackage()
    {
        AppSettings local = AppSettingsDefaults.Create();
        AppSettings host = AppSettingsDefaults.Create();
        host.Route.SplitRoute = host.Route.SplitRoute.Take(2).ToList();
        host.Route.SplitRoute[0].DisplayName = "Race First";
        RaceRoutePayload payload = RaceRoutePayloadFactory.Create(host);
        var overrides = new RaceRouteOverrideController(new StoredSettingsSnapshotFactory());

        bool firstApplied = overrides.TryApply(local, payload, out AppSettings raceSettings, out string firstDetail);
        TestAssert.Equal(true, firstApplied);
        TestAssert.Equal(string.Empty, firstDetail);

        bool duplicateApplied = overrides.TryApply(raceSettings, payload, out AppSettings duplicateSettings, out string duplicateDetail);
        TestAssert.Equal(false, duplicateApplied);
        TestAssert.Equal(RaceRouteOverrideController.AlreadyAppliedDetail, duplicateDetail);
        TestAssert.Equal(true, ReferenceEquals(raceSettings, duplicateSettings));

        host.Route.SplitRoute[0].DisplayName = "Race First Changed";
        RaceRoutePayload changedPayload = RaceRoutePayloadFactory.Create(host);
        bool changedApplied = overrides.TryApply(raceSettings, changedPayload, out AppSettings changedSettings, out string changedDetail);
        TestAssert.Equal(true, changedApplied);
        TestAssert.Equal(string.Empty, changedDetail);
        TestAssert.Equal("Race First Changed", changedSettings.Route.SplitRoute[0].DisplayName);

        bool cleared = overrides.Clear();
        TestAssert.Equal(true, cleared);
    }

    private static void RaceClientMaterializesHostCustomRouteIcons()
    {
        string directory = Path.Combine("test", "Temp", "race-route-icon-cache-" + Guid.NewGuid().ToString("N"));
        string hostDirectory = Path.Combine(directory, "host");
        string cacheDirectory = Path.Combine(directory, "cache");
        Directory.CreateDirectory(hostDirectory);
        try
        {
            string hostIconPath = Path.Combine(hostDirectory, "host-custom.gif");
            byte[] iconBytes = [71, 73, 70, 56, 57, 97, 1, 2, 3, 4, 5, 6];
            File.WriteAllBytes(hostIconPath, iconBytes);

            AppSettings local = AppSettingsDefaults.Create();
            AppSettings host = AppSettingsDefaults.Create();
            string targetId = SplitCatalog.CreateItemTargetId(50);
            host.Route.SplitRoute =
            [
                new SplitRouteEntry
                {
                    Id = "split:host-custom-icon",
                    DisplayName = "Host Custom Icon",
                    Enabled = true,
                    Condition = SplitCondition.All([SplitCatalog.CreateItemEverOwnedCondition(50, 1)]),
                    IconTargetIds = [targetId],
                    IconOverride = new SplitIconOverride
                    {
                        Source = SplitIconOverrideSource.CustomFile,
                        FilePath = hostIconPath
                    }
                }
            ];
            RaceRoutePayload payload = RaceRoutePayloadFactory.Create(host);
            var overrides = new RaceRouteOverrideController(new StoredSettingsSnapshotFactory(), cacheDirectory);

            bool applied = overrides.TryApply(local, payload, out AppSettings raceSettings, out string detail);

            TestAssert.Equal(true, applied);
            TestAssert.Equal(string.Empty, detail);
            string localIconPath = raceSettings.Route.SplitRoute.Single().IconOverride.FilePath;
            TestAssert.Equal(false, string.Equals(hostIconPath, localIconPath, StringComparison.OrdinalIgnoreCase));
            TestAssert.Equal(true, localIconPath.StartsWith(cacheDirectory, StringComparison.OrdinalIgnoreCase));
            TestAssert.Equal(true, File.Exists(localIconPath));
            TestAssert.Equal(Convert.ToBase64String(iconBytes), Convert.ToBase64String(File.ReadAllBytes(localIconPath)));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void RaceRoomAppliesPayloadOnWorldPublish()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        var state = new RaceRoomState(
            "ROOM",
            RaceRoomStatus.WorldUploaded,
            "host",
            CreateRoutePayload("route-a", "Route A", splitCount: 2),
            new RaceWorldSettings("1.4.5.6", 1, 1, true, 0, 0, "TerrariaRace-20260705010101"),
            new RaceSeedAssignment("12345", RaceSeedSource.Fixed),
            new RaceWorldFileInfo("TerrariaRace-20260705010101.wld", 128, "abc", now, "host"),
            [CreatePlayer("host", isHost: true), CreatePlayer("guest", isHost: false)],
            [],
            now,
            now);

        TestAssert.Equal(true, RaceShell.ShouldApplyRoomPayloadForUpdate(state));

        TestAssert.Equal(false, RaceShell.ShouldApplyRoomPayloadForUpdate(
            state with { Status = RaceRoomStatus.Closed }));

        TestAssert.Equal(false, RaceShell.ShouldApplyRoomPayloadForUpdate(
            state with { Route = null }));
    }

    private static RaceRoomState CreateRoomStateWithWorld()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        return new RaceRoomState(
            "ROOM",
            RaceRoomStatus.WorldUploaded,
            "host",
            CreateRoutePayload("route-a", "Route A", splitCount: 2),
            new RaceWorldSettings("1.4.5.6", 1, 1, true, 0, 0, "TerrariaRace-20260705010101"),
            new RaceSeedAssignment("12345", RaceSeedSource.Fixed),
            new RaceWorldFileInfo("TerrariaRace-20260705010101.wld", 128, "abc", now, "host"),
            [CreatePlayer("host", isHost: true), CreatePlayer("guest", isHost: false)],
            [],
            now,
            now);
    }

    private static RacePlayerState CreatePlayer(string nickname, bool isHost)
    {
        return new RacePlayerState(
            nickname,
            RacePlayerStatus.Joined,
            isHost,
            WorldReady: false,
            CompletedSplitCount: 0,
            LastSplitIndex: -1,
            LastConditionIndex: -1,
            LastSplitId: null,
            LastFactKey: null,
            LastTargetId: null,
            LastIconFileName: null,
            LastIconDisplayName: null,
            LastSplitElapsedMilliseconds: null,
            LastError: null,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);
    }

    private static RaceRoutePayload CreateRoutePayload(string hash, string summary, int splitCount)
    {
        RaceSplitDefinition[] splits = Enumerable.Range(0, splitCount)
            .Select(index => new RaceSplitDefinition(index, $"split-{index}", $"Split {index}"))
            .ToArray();
        return new RaceRoutePayload(hash, summary, "{}", splits);
    }

    private static RaceRoutePayload CreateRoutePayloadWithProgressSplit()
    {
        RaceSplitDefinition completedSplit = new RaceSplitDefinition(0, "split-0", "Split 0");
        RaceSplitDefinition progressSplit = new RaceSplitDefinition(1, "split-1", "Split 1")
        {
            IconFileNames = ["host-a.png", "host-b.png", "host-e.png"],
            IconKeys = ["target-a", "target-b", "target-e"],
            Conditions =
            [
                new RaceSplitConditionDefinition(0, "fact-a", "target-a", "Target A", "host-a.png"),
                new RaceSplitConditionDefinition(1, "fact-b", "target-b", "Target B", "host-b.png"),
                new RaceSplitConditionDefinition(2, "fact-e", "target-e", "Target E", "host-e.png")
            ]
        };
        RaceSplitDefinition laterProgressSplit = new RaceSplitDefinition(2, "split-2", "Split 2")
        {
            IconFileNames = ["host-c.png", "host-d.png"],
            IconKeys = ["target-c", "target-d"],
            Conditions =
            [
                new RaceSplitConditionDefinition(0, "fact-c", "target-c", "Target C", "host-c.png"),
                new RaceSplitConditionDefinition(1, "fact-d", "target-d", "Target D", "host-d.png")
            ]
        };
        return new RaceRoutePayload("race-route", "Race Route", "{}", [completedSplit, progressSplit, laterProgressSplit]);
    }

    private static RaceRoutePayload CreateRoutePayloadWithSingleIconProgressSplit()
    {
        RaceSplitDefinition completedSplit = new RaceSplitDefinition(0, "split-0", "Split 0")
        {
            IconFileNames = ["completed.png"],
            IconKeys = ["custom-icon:completed"]
        };
        RaceSplitDefinition progressSplit = new RaceSplitDefinition(1, "split-1", "Split 1")
        {
            IconFileNames = ["single.gif"],
            IconKeys = ["custom-icon:single"],
            Conditions =
            [
                new RaceSplitConditionDefinition(0, "fact-a", "target-a", "Target A", "single.gif"),
                new RaceSplitConditionDefinition(1, "fact-b", "target-b", "Target B", "single.gif")
            ]
        };
        return new RaceRoutePayload("race-route", "Race Route", "{}", [completedSplit, progressSplit]);
    }

    private static RaceSplitReport CreateReport(
        string roomCode,
        string nickname,
        int splitIndex,
        long elapsed,
        int conditionIndex = 0,
        string? factKey = null,
        bool isSplitComplete = true)
    {
        return new RaceSplitReport(
            roomCode,
            nickname,
            splitIndex,
            $"split-{splitIndex}",
            elapsed,
            DateTimeOffset.UnixEpoch,
            ConditionIndex: conditionIndex,
            FactKey: factKey,
            IsSplitComplete: isSplitComplete)
        {
            PackageRevision = 1,
            RunId = "run-1"
        };
    }

    private static void RaceWorldFileStoreCommitsValidatedContentByHash()
    {
        string directory = Path.Combine(
            "test",
            "Temp",
            "race-world-store-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string sourcePath = Path.Combine(directory, "source.wld");
            CreateMinimalWorldFile(sourcePath);
            byte[] bytes = File.ReadAllBytes(sourcePath);
            var store = new RaceWorldFileStore(Path.Combine(directory, "store"));
            using var source = new MemoryStream(bytes, writable: false);
            RaceStoredWorldFile stored = store.SaveAsync(
                    "ROOM",
                    "host",
                    "race.wld",
                    source,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            string expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
            TestAssert.Equal(expectedHash, stored.Info.Sha256);
            TestAssert.Equal((long)bytes.Length, stored.Info.Length);
            TestAssert.Equal(true, store.TryGetPath("ROOM", stored.Info, out string storedPath));
            TestAssert.Equal(stored.Path, storedPath);

            store.DeleteRoom("ROOM");
            TestAssert.Equal(false, File.Exists(stored.Path));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void RaceServerTreatsClosedRoomsAsTerminal()
    {
        var manager = new RaceRoomManager(new InMemoryRaceRecordStore());
        RaceRoomState created = manager.CreateRoom(new RaceRoomCreateRequest("host")).Value!;
        RaceOperationResult<RaceRoomState> closed = manager.CloseRoom(created.RoomCode, "host");
        RequireSuccess(closed);
        TestAssert.Equal(RaceRoomStatus.Closed, closed.Value!.Status);

        RaceOperationResult<RaceRoomState> missing = manager.GetRoomState(created.RoomCode);
        TestAssert.Equal(false, missing.Succeeded);
        TestAssert.Equal(RaceErrors.RoomNotFound, missing.ErrorCode);
    }

    private static void CreateMinimalWorldFile(string path)
    {
        const ulong reLogicMagic = 27981915666277746UL;
        using var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(279);
        writer.Write(reLogicMagic | (2UL << 56));
        writer.Write(0U);
        writer.Write(0UL);
        writer.Write((short)1);
        long pointerPosition = stream.Position;
        writer.Write(0);
        writer.Write((short)0);
        int headerPosition = checked((int)stream.Position);
        writer.Write("Race Test World");
        long endPosition = stream.Position;
        stream.Position = pointerPosition;
        writer.Write(headerPosition);
        stream.Position = endPosition;
    }

    private static SplitTargetDefinition RequireTarget(string targetId)
    {
        if (!SplitCatalog.TryGetTarget(targetId, out SplitTargetDefinition target))
        {
            throw new InvalidOperationException("Missing target " + targetId);
        }

        return target;
    }

    private static void RequireSuccess(RaceOperationResult<RaceRoomState> result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.Message);
        }
    }

}
