using System.Globalization;
using TerrariaSplit.Race.Contracts;
using TerrariaSplit.Race.InGame;
using TerrariaSplit.Terraria.Automation;

namespace TerrariaSplit.UI;

internal sealed partial class RaceShell
{
    private RaceInGameSnapshot BuildInGameSnapshot(long revision)
    {
        RaceRoomState? state = State;
        bool busy = Volatile.Read(ref inGameMenuBusy) != 0;
        if (Volatile.Read(ref localRoomExitActive) != 0)
        {
            var exiting = new List<RaceInGameControl>();
            AddProgressControls(exiting);
            return Snapshot(
                revision,
                RaceInGamePageKind.Progress,
                string.Empty,
                state,
                exiting);
        }

        if (busy && Volatile.Read(ref inGameMenuDedicatedProgress) != 0)
        {
            var progress = new List<RaceInGameControl>();
            AddProgressControls(progress);
            return Snapshot(
                revision,
                RaceInGamePageKind.Progress,
                string.Empty,
                state,
                progress);
        }

        if (state is not null && state.Status != RaceRoomStatus.Closed)
        {
            var roomControls = new List<RaceInGameControl>();
            RaceInGamePage roomPage = ResolveInGamePage(CreateCurrentDraftState().Role, state);
            if (roomPage == RaceInGamePage.RoomHome)
            {
                BuildRoomHomeControls(roomControls, busy);
                return Snapshot(
                    revision,
                    RaceInGamePageKind.Home,
                    "Race",
                    state,
                    roomControls);
            }

            if (roomPage == RaceInGamePage.RoomPreparation)
            {
                BuildRoomPreparationControls(roomControls, state);
            }
            else
            {
                BuildRoomManagementControls(roomControls, state);
            }

            return Snapshot(
                revision,
                RaceInGamePageKind.Lobby,
                roomPage == RaceInGamePage.RoomPreparation ? "Race room" : "Room management",
                state,
                roomControls);
        }

        RacePanelDraftState draft = CreateCurrentDraftState();
        RaceWorldSetupSettings setup = EnsureInGameWorldSetup();
        RaceInGamePage page = ResolveInGamePage(draft.Role, state);
        var controls = new List<RaceInGameControl>();
        RaceInGamePageKind kind;
        string title;
        switch (page)
        {
            case RaceInGamePage.MemberJoin:
                kind = RaceInGamePageKind.MemberJoin;
                title = "Join room";
                BuildMemberJoinControls(controls, draft, busy);
                break;
            case RaceInGamePage.HostWorldSource:
                kind = RaceInGamePageKind.WorldSource;
                title = "Choose world seed";
                BuildHostSourceControls(controls, busy);
                break;
            case RaceInGamePage.HostWorldSettings:
                kind = RaceInGamePageKind.WorldCreation;
                title = "World settings";
                BuildHostWorldControls(controls, setup, busy);
                break;
            case RaceInGamePage.HostSeedSettings:
                kind = RaceInGamePageKind.SpecialSeeds;
                title = "Seed settings";
                BuildHostSeedControls(controls, setup, busy);
                break;
            case RaceInGamePage.HostFilterSettings:
                kind = RaceInGamePageKind.WorldFilters;
                title = "Filter settings";
                BuildHostFilterControls(controls, setup, busy);
                break;
            default:
                kind = RaceInGamePageKind.Home;
                title = "Race";
                BuildHomeControls(controls, draft, busy);
                break;
        }

        return Snapshot(revision, kind, title, null, controls);
    }

    private RaceInGameSnapshot Snapshot(
        long revision,
        RaceInGamePageKind pageKind,
        string title,
        RaceRoomState? state,
        List<RaceInGameControl> controls)
    {
        return new RaceInGameSnapshot(
            revision,
            true,
            pageKind,
            Localize(title),
            BuildInGameStatus(state),
            Localize("Back"),
            controls);
    }

    private void BuildHomeControls(
        List<RaceInGameControl> controls,
        RacePanelDraftState draft,
        bool busy)
    {
        AddButton(controls, "flow-host", Localize("I am the host / Create room"), !busy, "menu");
        AddText(
            controls,
            "flow-member",
            Localize("I am a member / Join room"),
            draft.RoomCode,
            !busy,
            RaceRoomCodeRules.Length,
            false,
            "menu",
            Localize("Room code"));
    }

    private void BuildRoomHomeControls(List<RaceInGameControl> controls, bool busy)
    {
        AddButton(controls, "terraria-single-player", Localize("Single Player"), !busy, "menu");
        if (IsHostInCurrentRoom)
        {
            AddButton(controls, "room-management", Localize("Room management"), !busy, "menu-lower");
        }

        AddButton(
            controls,
            "leave-room",
            Localize("Leave room"),
            !busy,
            "menu-lower",
            description: Localize(
                IsHostInCurrentRoom
                    ? "Are you sure you want to close the room?"
                    : "Are you sure you want to leave the room?"));
    }

    private void BuildMemberJoinControls(
        List<RaceInGameControl> controls,
        RacePanelDraftState draft,
        bool busy)
    {
        AddText(
            controls,
            "room-code",
            Localize("Room code"),
            draft.RoomCode,
            !busy,
            RaceRoomCodeRules.Length,
            false,
            "field",
            Localize("Enter the room code provided by the host."));
        AddButton(controls, "nav-home", Localize("Back"), !busy, "footer");
        AddButton(
            controls,
            "join",
            Localize("Join"),
            !busy &&
            !string.IsNullOrWhiteSpace(draft.ServerUrl) &&
            RaceRoomCodeRules.IsValid(draft.RoomCode),
            "footer");
    }

    private void BuildHostSourceControls(List<RaceInGameControl> controls, bool busy)
    {
        AddText(
            controls,
            "fixed-seed",
            Localize("Fixed seed"),
            string.Empty,
            !busy,
            256,
            false,
            "menu",
            Localize("Enter the fixed world seed."));
        AddButton(
            controls,
            "source-random",
            Localize("Random seed"),
            !busy,
            "menu",
            string.Empty,
            Localize("Configure a randomly generated world."));
        AddButton(controls, "nav-home", Localize("Back"), !busy, "menu");
    }

    private void BuildHostWorldControls(
        List<RaceInGameControl> controls,
        RaceWorldSetupSettings setup,
        bool busy)
    {
        if (string.Equals(
                setup.Source,
                RacePreferredWorldSource.Random,
                StringComparison.OrdinalIgnoreCase))
        {
            AddButton(
                controls,
                "host-world-seeds",
                Localize("Special and secret seeds"),
                !busy,
                "world-advanced",
                "Images/UI/WorldCreation/IconRandomSeed",
                Localize("Configure special and secret world seeds."));
        }

        AddChoiceControls(
            controls,
            "world-size:",
            AutoCreateWorldSize.All,
            setup.WorldSize,
            !busy,
            "world-size",
            WorldSizeIcon);
        AddChoiceControls(
            controls,
            "world-difficulty:",
            AutoCreateWorldDifficulty.All,
            setup.WorldDifficulty,
            !busy,
            "world-difficulty",
            WorldDifficultyIcon);
        AddChoiceControls(
            controls,
            "world-evil:",
            AutoCreateWorldEvil.All,
            setup.WorldEvil,
            !busy,
            "world-evil",
            WorldEvilIcon);
        AddButton(controls, "nav-host-source", Localize("Back"), !busy, "footer");
        AddButton(controls, "host-world-next", Localize("Continue"), !busy, "footer");
    }

    private void BuildHostSeedControls(
        List<RaceInGameControl> controls,
        RaceWorldSetupSettings setup,
        bool busy)
    {
        IReadOnlyList<string> selected = AutoCreateSpecialWorldSeed.ParseList(setup.SpecialSeeds);
        foreach (string seed in AutoCreateSpecialWorldSeed.All)
        {
            AddToggle(
                controls,
                "special-seed:" + seed,
                Localize(seed),
                selected.Contains(seed, StringComparer.OrdinalIgnoreCase),
                !busy,
                "special-seeds",
                SpecialSeedIcon(seed),
                Localize(seed));
        }

        AddText(
            controls,
            "secret-seeds",
            Localize("Secret seeds"),
            setup.SecretSeeds,
            !busy,
            512,
            true,
            "secret-seeds",
            Localize("Enter secret seeds separated by spaces."));
        AddButton(controls, "nav-host-world", Localize("Back"), !busy, "footer");
        AddButton(controls, "host-seeds-apply", Localize("Apply"), !busy, "footer");
    }

    private void BuildHostFilterControls(
        List<RaceInGameControl> controls,
        RaceWorldSetupSettings setup,
        bool busy)
    {
        bool advancedFiltersEligible = AutoCreateAdvancedFilterEligibility.IsEligible(setup);
        const string pyramidGroup = "primary-choice:pyramid";
        const string pyramidCoinPileGroup = "primary-choice:pyramid-coin-piles";
        const string crimsonGroup = "primary-choice:crimson";
        const string jungleGroup = "primary-choice:jungle-route";
        const string lifeCrystalGroup = "primary-choice:life-crystal";
        AddToggle(
            controls,
            "boss-failure-penalty",
            Localize("Enable boss failure penalty"),
            setup.BossFailurePenaltyEnabled,
            !busy,
            "race-rules-row");
        AddToggle(
            controls,
            "rng",
            Localize("Shared key RNG"),
            setup.RngControlEnabled,
            !busy,
            "race-rules-row");
        for (int index = 0; index < RaceBossPenaltyConfiguration.Bosses.Count; index++)
        {
            RaceBossPenaltyDescriptor boss = RaceBossPenaltyConfiguration.Bosses[index];
            AddToggle(
                controls,
                "boss-penalty-kind:" + boss.Key,
                Localize(boss.Label),
                RaceBossPenalty.AreKindsEnabled(setup.BossPenaltyEnabledKinds, boss.Kind),
                !busy && setup.BossFailurePenaltyEnabled,
                "boss-penalty-kinds");
        }

        AddToggle(
            controls,
            "pyramid",
            Localize("Pyramid"),
            setup.PyramidEnabled,
            !busy,
            pyramidGroup);
        foreach (string item in AutoCreatePyramidFilterItem.All)
        {
            AddToggle(
                controls,
                "pyramid-item:" + item,
                Localize(item),
                (setup.PyramidItemMask & AutoCreatePyramidFilterItem.Mask(item)) != 0,
                !busy && setup.PyramidEnabled,
                pyramidGroup);
        }

        int pyramidCoinPileMinimum = AutoCreatePyramidCoinPileMinimum.Normalize(
            setup.PyramidCoinPileMinimum);
        AddToggle(
            controls,
            "pyramid-coin-piles",
            Localize("Pyramid coin piles"),
            pyramidCoinPileMinimum > 0,
            !busy && setup.PyramidEnabled,
            pyramidCoinPileGroup);
        foreach (int minimum in AutoCreatePyramidCoinPileMinimum.All.Where(value => value > 0))
        {
            AddToggle(
                controls,
                "pyramid-coin-pile-min:" + minimum.ToString(CultureInfo.InvariantCulture),
                minimum == AutoCreatePyramidCoinPileMinimum.All[^1]
                    ? minimum.ToString(CultureInfo.InvariantCulture) + "+"
                    : minimum.ToString(CultureInfo.InvariantCulture),
                pyramidCoinPileMinimum > 0 && minimum >= pyramidCoinPileMinimum,
                !busy && setup.PyramidEnabled && pyramidCoinPileMinimum > 0,
                pyramidCoinPileGroup);
        }

        AddToggle(
            controls,
            "crimson",
            Localize("Dungeon-side Crimson"),
            advancedFiltersEligible && setup.CrimsonEnabled,
            !busy && advancedFiltersEligible,
            crimsonGroup);
        AddChoiceControls(
            controls,
            "crimson-distance:",
            AutoCreateCrimsonDistance.All,
            setup.CrimsonDistance,
            !busy && advancedFiltersEligible && setup.CrimsonEnabled,
            crimsonGroup,
            isSelected: value =>
                advancedFiltersEligible && setup.CrimsonEnabled &&
                AutoCreateCrimsonDistance.Includes(setup.CrimsonDistance, value));
        bool jungleEnabled =
            advancedFiltersEligible &&
            AutoCreateJungleRouteDepth.Normalize(setup.JungleRouteDepth) !=
            AutoCreateJungleRouteDepth.None;
        AddToggle(
            controls,
            "jungle-route",
            Localize("Jungle main route"),
            jungleEnabled,
            !busy && advancedFiltersEligible,
            jungleGroup);
        AddChoiceControls(
            controls,
            "jungle-depth:",
            AutoCreateJungleRouteDepth.All,
            setup.JungleRouteDepth,
            !busy && jungleEnabled,
            jungleGroup,
            isSelected: value =>
                jungleEnabled &&
                AutoCreateJungleRouteDepth.Includes(setup.JungleRouteDepth, value));
        int lifeCrystalMinimum = AutoCreateResourceMinimum.NormalizeLifeCrystals(
            setup.LifeCrystalMinimum);
        AddToggle(
            controls,
            "life-crystal",
            Localize("Life Crystal"),
            lifeCrystalMinimum > 0,
            !busy && advancedFiltersEligible,
            lifeCrystalGroup);
        foreach (int minimum in AutoCreateResourceMinimum.LifeCrystals.Where(value => value > 0))
        {
            AddToggle(
                controls,
                "life-crystal-min:" + minimum.ToString(CultureInfo.InvariantCulture),
                minimum == AutoCreateResourceMinimum.LifeCrystals[^1]
                    ? minimum.ToString(CultureInfo.InvariantCulture) + "+"
                    : minimum.ToString(CultureInfo.InvariantCulture),
                lifeCrystalMinimum > 0 && minimum >= lifeCrystalMinimum,
                !busy && advancedFiltersEligible && lifeCrystalMinimum > 0,
                lifeCrystalGroup);
        }

        AddButton(controls, "nav-host-world", Localize("Back"), !busy, "footer");
        AddButton(controls, "host-generate", Localize("Generate and upload"), !busy, "footer");
    }

    private void BuildRoomPreparationControls(
        List<RaceInGameControl> controls,
        RaceRoomState state)
    {
        bool busy = Volatile.Read(ref inGameMenuBusy) != 0;
        AddLabel(controls, "room-code-label", Localize("Room code"), state.RoomCode, "room-header");
        foreach (RacePlayerState player in state.Players)
        {
            bool technicallyReady =
                player.PlayerFileStatus == RacePlayerFileStatus.Ready &&
                player.WorldFileStatus == RaceWorldFileStatus.Ready &&
                (player.RngControlStatus is RaceRngControlStatus.Enabled or
                    RaceRngControlStatus.NotEnabled) &&
                player.ServerConnectionStatus == RaceServerConnectionStatus.Connected;
            string readiness = !technicallyReady
                ? "Preparing"
                : player.IsHost || player.IsReady
                    ? "Ready"
                    : "Not Ready";
            string label = player.IsHost
                ? "\u2605 " + player.Nickname
                : player.Nickname;
            AddLabel(
                controls,
                "member:" + player.Nickname,
                label,
                Localize(readiness),
                "members");
        }

        RaceLocalPreparationStage localPreparation = ResolveLocalPreparationStage(state);
        if (localPreparation != RaceLocalPreparationStage.None)
        {
            AddLabel(
                controls,
                "local-preparation",
                FormatLocalPreparationStage(localPreparation),
                string.Empty,
                "local-preparation");
        }

        if (IsHostInCurrentRoom)
        {
            AddButton(controls, "room-close", Localize("Close room"), !busy, "footer");
            AddButton(
                controls,
                "start",
                Localize("Start Race"),
                !busy && state.Status == RaceRoomStatus.Ready,
                "footer");
        }
        else
        {
            RacePlayerState? localPlayer = state.Players.FirstOrDefault(player =>
                string.Equals(player.Nickname, session.Nickname, StringComparison.OrdinalIgnoreCase));
            bool technicallyReady =
                localPlayer is not null &&
                localPlayer.PlayerFileStatus == RacePlayerFileStatus.Ready &&
                localPlayer.WorldFileStatus == RaceWorldFileStatus.Ready &&
                (localPlayer.RngControlStatus is RaceRngControlStatus.Enabled or
                    RaceRngControlStatus.NotEnabled) &&
                localPlayer.ServerConnectionStatus == RaceServerConnectionStatus.Connected;
            AddButton(
                controls,
                "leave-room",
                Localize("Leave room"),
                !busy,
                "footer",
                description: Localize("Are you sure you want to leave the room?"));
            AddButton(
                controls,
                "ready",
                Localize(localPlayer?.IsReady == true ? "Not Ready" : "Ready"),
                !busy && technicallyReady && state.ScheduledStartUtc is null,
                "footer");
        }
    }

    private RaceLocalPreparationStage ResolveLocalPreparationStage(RaceRoomState state)
    {
        RaceLocalPreparationStage stage = LocalPreparationStage;
        if (stage != RaceLocalPreparationStage.None)
        {
            return stage;
        }

        RacePlayerState? localPlayer = state.Players.FirstOrDefault(player =>
            string.Equals(player.Nickname, session.Nickname, StringComparison.OrdinalIgnoreCase));
        if (localPlayer is null || !IsPreparationReady(state, localPlayer))
        {
            return RaceLocalPreparationStage.None;
        }

        return localPlayer.IsHost || localPlayer.IsReady
            ? RaceLocalPreparationStage.Ready
            : RaceLocalPreparationStage.WaitForManualReady;
    }

    private string FormatLocalPreparationStage(RaceLocalPreparationStage stage)
    {
        string stageText = Localize(stage switch
        {
            RaceLocalPreparationStage.DownloadWorld => "Download world",
            RaceLocalPreparationStage.ValidateWorld => "Validate world",
            RaceLocalPreparationStage.AnalyzeWorld => "Analyze world",
            RaceLocalPreparationStage.WaitForGame => "Wait for game",
            RaceLocalPreparationStage.PrepareMemoryControl => "Prepare memory control",
            RaceLocalPreparationStage.CreateRacePlayer => "Create Race player",
            RaceLocalPreparationStage.AlmostReady => "Almost ready",
            RaceLocalPreparationStage.ConnectToServer => "Connect to server",
            RaceLocalPreparationStage.WaitForManualReady => "Wait for manual ready",
            RaceLocalPreparationStage.Ready => "Preparation ready",
            _ => string.Empty
        });
        return string.Format(
            CultureInfo.CurrentCulture,
            Localize("Local preparation: {0}"),
            stageText);
    }

    private void BuildRoomManagementControls(
        List<RaceInGameControl> controls,
        RaceRoomState state)
    {
        BuildRoomStatusControls(controls, state);
        bool busy = Volatile.Read(ref inGameMenuBusy) != 0;
        AddButton(controls, "room-back", Localize("Back"), !busy, "footer");
        AddButton(controls, "room-close", Localize("Close room"), !busy, "footer");
        AddButton(
            controls,
            "room-restart",
            Localize("Restart current Race"),
            !busy && state.Status != RaceRoomStatus.Starting,
            "footer");
    }

    private void BuildRoomStatusControls(
        List<RaceInGameControl> controls,
        RaceRoomState state)
    {
        bool busy = Volatile.Read(ref inGameMenuBusy) != 0;
        AddLabel(controls, "room-code-label", Localize("Room code"), state.RoomCode, "room-header");
        foreach (RacePlayerState player in state.Players)
        {
            string value = string.Join(
                " / ",
                Localize(player.PlayerFileStatus.ToString()),
                Localize(player.WorldFileStatus.ToString()),
                Localize(player.RngControlStatus.ToString()));
            string label = player.IsHost
                ? "\u2605 " + player.Nickname
                : player.Nickname;
            AddLabel(controls, "member:" + player.Nickname, label, value, "members");
            if (IsHostInCurrentRoom && !player.IsHost)
            {
                AddButton(
                    controls,
                    "kick:" + player.Nickname,
                    Localize("Kick"),
                    !busy,
                    "member-action");
            }
        }

    }

    private void AddProgressControls(List<RaceInGameControl> controls)
    {
        AddButton(controls, "cancel", Localize("Cancel"), true, "footer");
    }

    private string BuildInGameStatus(RaceRoomState? state)
    {
        if (!string.IsNullOrWhiteSpace(inGameMenuStatus))
        {
            return inGameMenuStatus;
        }

        if (ServerConnectionStatus is
            RaceServerConnectionStatus.Reconnecting or
            RaceServerConnectionStatus.ConnectionFailed)
        {
            return Localize(ServerConnectionStatus.ToString());
        }

        return state is null ? string.Empty : Localize(state.Status.ToString());
    }

    private static void AddLabel(
        List<RaceInGameControl> controls,
        string id,
        string label,
        string value,
        string layoutGroup = "",
        string iconPath = "",
        string description = "")
    {
        controls.Add(new RaceInGameControl(
            id,
            RaceInGameControlKind.Label,
            label,
            value,
            false,
            false,
            0,
            0,
            true,
            layoutGroup,
            iconPath,
            description));
    }

    private static void AddText(
        List<RaceInGameControl> controls,
        string id,
        string label,
        string value,
        bool enabled,
        int maxLength,
        bool allowEmpty,
        string layoutGroup = "",
        string description = "")
    {
        controls.Add(new RaceInGameControl(
            id,
            RaceInGameControlKind.TextField,
            label,
            value,
            enabled,
            false,
            0,
            maxLength,
            allowEmpty,
            layoutGroup,
            string.Empty,
            description));
    }

    private static void AddToggle(
        List<RaceInGameControl> controls,
        string id,
        string label,
        bool selected,
        bool enabled,
        string layoutGroup = "",
        string iconPath = "",
        string description = "")
    {
        controls.Add(new RaceInGameControl(
            id,
            RaceInGameControlKind.Toggle,
            label,
            string.Empty,
            enabled,
            selected,
            0,
            0,
            true,
            layoutGroup,
            iconPath,
            description));
    }

    private static void AddButton(
        List<RaceInGameControl> controls,
        string id,
        string label,
        bool enabled,
        string layoutGroup = "",
        string iconPath = "",
        string description = "")
    {
        controls.Add(new RaceInGameControl(
            id,
            RaceInGameControlKind.Button,
            label,
            string.Empty,
            enabled,
            false,
            0,
            0,
            true,
            layoutGroup,
            iconPath,
            description));
    }

    private void AddChoiceControls(
        List<RaceInGameControl> controls,
        string prefix,
        IEnumerable<string> values,
        string selected,
        bool enabled,
        string layoutGroup,
        Func<string, string>? icon = null,
        Func<string, bool>? isSelected = null)
    {
        foreach (string value in values)
        {
            AddToggle(
                controls,
                prefix + value,
                Localize(value),
                isSelected?.Invoke(value) ??
                    string.Equals(selected, value, StringComparison.OrdinalIgnoreCase),
                enabled,
                layoutGroup,
                icon?.Invoke(value) ?? string.Empty,
                Localize(value));
        }
    }

    private static string WorldSizeIcon(string value) => value switch
    {
        AutoCreateWorldSize.Small => "Images/UI/WorldCreation/IconSizeSmall",
        AutoCreateWorldSize.Large => "Images/UI/WorldCreation/IconSizeLarge",
        _ => "Images/UI/WorldCreation/IconSizeMedium"
    };

    private static string WorldDifficultyIcon(string value) => value switch
    {
        AutoCreateWorldDifficulty.Journey => "Images/UI/WorldCreation/IconDifficultyCreative",
        AutoCreateWorldDifficulty.Expert => "Images/UI/WorldCreation/IconDifficultyExpert",
        AutoCreateWorldDifficulty.Master => "Images/UI/WorldCreation/IconDifficultyMaster",
        _ => "Images/UI/WorldCreation/IconDifficultyNormal"
    };

    private static string WorldEvilIcon(string value) => value switch
    {
        AutoCreateWorldEvil.Corruption => "Images/UI/WorldCreation/IconEvilCorruption",
        AutoCreateWorldEvil.Crimson => "Images/UI/WorldCreation/IconEvilCrimson",
        _ => "Images/UI/WorldCreation/IconEvilRandom"
    };

    private static string SpecialSeedIcon(string value) => value switch
    {
        AutoCreateSpecialWorldSeed.NotTheBees => "terraria-seed:notthebees",
        AutoCreateSpecialWorldSeed.Drunk => "terraria-seed:drunk",
        AutoCreateSpecialWorldSeed.Celebration => "terraria-seed:celebration",
        AutoCreateSpecialWorldSeed.TheConstant => "terraria-seed:theconstant",
        AutoCreateSpecialWorldSeed.ForTheWorthy => "terraria-seed:fortheworthy",
        AutoCreateSpecialWorldSeed.NoTraps => "terraria-seed:notraps",
        AutoCreateSpecialWorldSeed.Remix => "terraria-seed:remix",
        AutoCreateSpecialWorldSeed.Zenith => "terraria-seed:zenith",
        AutoCreateSpecialWorldSeed.Skyblock => "terraria-seed:skyblock",
        _ => string.Empty
    };

}
