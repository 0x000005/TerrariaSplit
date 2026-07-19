using System.Text.Json;
using TerrariaSplit.Race.Client;
using TerrariaSplit.Race.Determinism;

namespace TerrariaSplit.Tests;

internal static class RaceFlowTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("race room journey covers preparation, synchronized start, reconnect, host restart and a fresh run", TestSuite.Flow, CompleteRoomJourney);
        yield return TestCase.Sync("race server rejects invalid identities, stale progress and unauthorized host actions", TestSuite.Flow, PermissionAndStalenessBoundaries);
        yield return TestCase.Sync("race package survives transport serialization with route, world and leaderboard intact", TestSuite.Flow, TransportRoundTrip);
        yield return TestCase.Sync("race deterministic core derives stable domains, counts events, rolls independent chances and accumulates fixed chances", TestSuite.Core, DeterministicCore);
        yield return TestCase.Async("race voice announces main and attached groups once, queues players in order and clears obsolete work", TestSuite.Flow, VoiceAnnouncementJourney);
    }

    private static void CompleteRoomJourney()
    {
        var store = new InMemoryRaceRecordStore();
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero));
        var manager = new RaceRoomManager(store, timeProvider: clock);
        RaceRoomState created = Success(manager.CreateRoom(new RaceRoomCreateRequest("host")));
        string room = created.RoomCode;
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
        Success(manager.UpdatePreparationStatus(Ready(room, "host")));
        RaceRoomState ready = Success(manager.UpdatePreparationStatus(Ready(room, "guest")));
        Check.Equal(RaceRoomStatus.Ready, ready.Status);
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
            RaceErrors.RaceNotStarted,
            manager.ReportStart(new RaceRunStartReport(room, "guest") { PackageRevision = 1, RunId = "guest-run" }).ErrorCode);
        clock.Advance(TimeSpan.FromSeconds(7));
        Success(manager.ReportStart(new RaceRunStartReport(room, "guest") { PackageRevision = 1, RunId = "guest-run" }));
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

        Success(manager.UpdatePreparationStatus(Ready(room, "host")));
        Success(manager.UpdatePreparationStatus(Ready(room, "guest")));
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
        Success(manager.UpdatePreparationStatus(Ready(room, "host")));
        RaceRoomState rngDisabledReady = Success(manager.UpdatePreparationStatus(Ready(room, "late")));
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
        Success(manager.UpdatePreparationStatus(Ready(room, "host")));
        Success(manager.UpdatePreparationStatus(Ready(room, "guest")));
        Success(manager.StartRace(new RaceHostActionRequest(room, "host", 1)));
        clock.Advance(TimeSpan.FromSeconds(7));
        Success(manager.ReportSplit(Report(room, "host", 0, 1_000, "run-1")));
        Success(manager.PublishWorldFile(Publish(room, "host", "second"), out RaceWorldFileInfo? replacedWorld));
        Check.Equal("first.wld", replacedWorld!.FileName);
        Success(manager.UpdatePreparationStatus(Ready(room, "host")));
        Success(manager.UpdatePreparationStatus(Ready(room, "guest")));
        Success(manager.StartRace(new RaceHostActionRequest(room, "host", 2)));
        clock.Advance(TimeSpan.FromSeconds(7));
        Check.Equal(RaceErrors.StalePackage, manager.ReportSplit(Report(room, "host", 0, 2_000, "run-1")).ErrorCode);
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
        Check.Equal(RacePlayerDifficultyCodes.Hardcore, restored.WorldSettings.PlayerDifficultyCode);
        Check.Equal(
            AutoCreatePlayerDifficulty.Hardcore,
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
        Check.Equal(8, generatedSettings.ResourceFilterLifeCrystalMinimum);
        Check.Equal(2, generatedSettings.ResourceFilterSpelunkerPotionMinimum);
        Check.Equal(1, generatedSettings.ResourceFilterFeatherfallPotionMinimum);
        Check.False(RaceWorldSettingsFactory.HasActiveFilters(
            restored.WorldSettings with { Cheats = restored.WorldSettings.Cheats with { Enabled = false } }));
        Check.Equal(uploaded.PackageRevision, restored.PackageRevision);
        Check.Equal(uploaded.Determinism!.CreateDigest(), restored.Determinism!.CreateDigest());
        Check.Equal("host", restored.Leaderboard.Single().Nickname);
    }

    private static RacePreparationStatusRequest Ready(string roomCode, string nickname)
    {
        return new RacePreparationStatusRequest(
            roomCode,
            nickname,
            RacePlayerFileStatus.Ready,
            RaceWorldFileStatus.Ready,
            RaceRngControlStatus.Enabled);
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
