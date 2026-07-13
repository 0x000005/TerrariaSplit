using System.Text.Json;
using TerrariaSplit.Race.Client;

namespace TerrariaSplit.Tests;

internal static class RaceFlowTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("race room journey covers create, join, upload, ready, run, ranking, reset, kick and close", TestSuite.Flow, CompleteRoomJourney);
        yield return TestCase.Sync("race server rejects invalid identities, stale progress and unauthorized host actions", TestSuite.Flow, PermissionAndStalenessBoundaries);
        yield return TestCase.Sync("race package survives transport serialization with route, world and leaderboard intact", TestSuite.Flow, TransportRoundTrip);
    }

    private static void CompleteRoomJourney()
    {
        var store = new InMemoryRaceRecordStore();
        var manager = new RaceRoomManager(store);
        RaceRoomState created = Success(manager.CreateRoom(new RaceRoomCreateRequest("host")));
        string room = created.RoomCode;
        Success(manager.JoinRoom(new RaceRoomJoinRequest(room, "guest")));
        RaceRoomState uploaded = Success(manager.PublishWorldFile(Publish(room, "host", revisionName: "first")));
        Check.Equal(RaceRoomStatus.WorldUploaded, uploaded.Status);
        Check.Equal(RaceRoomStatus.Ready, Success(manager.MarkWorldReady(new RaceWorldReadyRequest(room, "guest", true))).Status);

        Success(manager.ReportStart(new RaceRunStartReport(room, "guest") { PackageRevision = 1, RunId = "guest-run" }));
        Success(manager.ReportSplit(Report(room, "host", 0, 4_000, "host-run")));
        RaceRoomState first = Success(manager.ReportSplit(Report(room, "guest", 0, 5_000, "guest-run")));
        Check.Equal("host", first.Leaderboard[0].Nickname);
        Success(manager.ReportSplit(Report(room, "guest", 1, 7_000, "guest-run")));
        RaceRoomState ranked = Success(manager.ReportSplit(Report(room, "host", 1, 8_000, "host-run")));
        Check.Equal("guest", ranked.Leaderboard[0].Nickname);

        RaceRoomState reset = Success(manager.ResetPlayerProgress(new RaceProgressResetRequest(room, "guest", 1, "guest-run-2")));
        Check.Equal(0, reset.Players.Single(player => player.Nickname == "guest").CompletedSplitCount);
        RaceRoomState kicked = Success(manager.KickPlayer(new RacePlayerKickRequest(room, "host", "guest")));
        Check.Equal(1, kicked.Players.Count);
        Success(manager.CloseRoom(room, "host"));
        Check.Equal(1, store.Records.Count);
        Check.Equal(RaceErrors.RoomNotFound, manager.GetRoomState(room).ErrorCode);
    }

    private static void PermissionAndStalenessBoundaries()
    {
        var manager = new RaceRoomManager(new InMemoryRaceRecordStore());
        RaceRoomState created = Success(manager.CreateRoom(new RaceRoomCreateRequest("host")));
        string room = created.RoomCode;
        Success(manager.JoinRoom(new RaceRoomJoinRequest(room, "guest")));
        Check.Equal(RaceErrors.NicknameTaken, manager.JoinRoom(new RaceRoomJoinRequest(room, "guest")).ErrorCode);
        Check.False(manager.PublishWorldFile(Publish(room, "guest", "forbidden")).Succeeded);
        Success(manager.PublishWorldFile(Publish(room, "host", "first")));
        Success(manager.ReportSplit(Report(room, "host", 0, 1_000, "run-1")));
        Success(manager.PublishWorldFile(Publish(room, "host", "second")));
        Check.Equal(RaceErrors.StalePackage, manager.ReportSplit(Report(room, "host", 0, 2_000, "run-1")).ErrorCode);
        Check.False(manager.KickPlayer(new RacePlayerKickRequest(room, "guest", "host")).Succeeded);
        Check.False(manager.CloseRoom(room, "guest").Succeeded);
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
        Check.Equal(8, restored.WorldSettings.Cheats.LifeCrystalMinimum);
        Check.Equal(AutoCreateResourceHook.Sapphire, restored.WorldSettings.Cheats.HookMinimum);
        Check.True(RaceWorldSettingsFactory.HasActiveFilters(restored.WorldSettings));
        AutoCreateWorldSettings generatedSettings = RaceWorldSettingsFactory.ToAutoCreateWorldSettings(restored.WorldSettings);
        Check.True(generatedSettings.EnableCheats);
        Check.True(generatedSettings.EnablePyramidFilter);
        Check.True(generatedSettings.RequireCrimsonBetweenDungeonAndSpawn);
        Check.Equal(AutoCreateCrimsonDistance.Near, generatedSettings.CrimsonDistance);
        Check.Equal(AutoCreateResourceFilterItem.BoomstickMask, generatedSettings.ResourceFilterItemMask);
        Check.Equal(8, generatedSettings.ResourceFilterLifeCrystalMinimum);
        Check.Equal(AutoCreateResourceHook.Sapphire, generatedSettings.ResourceFilterHookMinimum);
        Check.Equal(2, generatedSettings.ResourceFilterSpelunkerPotionMinimum);
        Check.Equal(1, generatedSettings.ResourceFilterFeatherfallPotionMinimum);
        Check.False(RaceWorldSettingsFactory.HasActiveFilters(
            restored.WorldSettings with { Cheats = restored.WorldSettings.Cheats with { Enabled = false } }));
        Check.Equal(uploaded.PackageRevision, restored.PackageRevision);
        Check.Equal("host", restored.Leaderboard.Single().Nickname);
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
                    AutoCreateResourceHook.Sapphire,
                    2,
                    1),
                "race"),
            new RaceSeedAssignment("1234", RaceSeedSource.Fixed),
            new RaceWorldFileInfo(revisionName + ".wld", 128, revisionName, DateTimeOffset.UnixEpoch, nickname));

    private static RaceRoutePayload Route() => new("route-hash", "Route", "{}",
    [
        new RaceSplitDefinition(0, "split-0", "First"),
        new RaceSplitDefinition(1, "split-1", "Final")
    ]);

    private static RaceSplitReport Report(string room, string nickname, int index, long elapsed, string runId) =>
        new(room, nickname, index, $"split-{index}", elapsed)
        {
            PackageRevision = 1,
            RunId = runId
        };

    private static T Success<T>(RaceOperationResult<T> result)
    {
        if (!result.Succeeded || result.Value is null) throw new InvalidOperationException($"{result.ErrorCode}: {result.Message}");
        return result.Value;
    }
}
