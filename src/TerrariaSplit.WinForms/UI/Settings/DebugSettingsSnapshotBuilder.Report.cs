namespace TerrariaSplit.UI.Settings;

internal static partial class DebugSettingsSnapshotBuilder
{
    private static string BuildReport(DebugSettingsSnapshot snapshot, Func<string, string> localize)
    {
        var lines = new List<string>();

        AppendReportSection(
            lines,
            localize,
            "Quick Status",
            [
                ("Terraria process", snapshot.QuickStatus.ProcessDetected.Text),
                ("Window", snapshot.QuickStatus.WindowDetected.Text),
                ("Window status", snapshot.QuickStatus.WindowStatus.Text),
                ("Watcher attached", snapshot.QuickStatus.WatcherAttached.Text),
                ("Memory ready", snapshot.QuickStatus.MemoryReady.Text),
                ("Boss flags ready", snapshot.QuickStatus.BossFlagsReady.Text),
                ("Game state", snapshot.QuickStatus.GameState.Text),
                ("Last updated", snapshot.QuickStatus.LastUpdated.Text)
            ]);

        AppendReportSection(
            lines,
            localize,
            "Performance",
            [
                ("Sampling frequency", snapshot.Performance.WatcherPoll),
                ("Control frequency", snapshot.Performance.ControlTick),
                ("Split timer refresh rate", snapshot.Performance.StatusPaint),
                ("Main timer refresh rate", snapshot.Performance.TimerPaint),
                ("Main timer layered update", snapshot.Performance.TimerLayeredUpdate)
            ]);

        AppendReportSection(
            lines,
            localize,
            "Window & Coordinates",
            [
                ("PID", snapshot.Window.ProcessId),
                ("Start time", snapshot.Window.ProcessStartTime),
                ("Process path", snapshot.Window.ProcessPath),
                ("Process architecture", snapshot.Window.ProcessArchitecture),
                ("Process version", snapshot.Window.ProcessVersion),
                ("Window handle", snapshot.Window.WindowHandle),
                ("Window title", snapshot.Window.WindowTitle),
                ("Responding", snapshot.Window.Responding),
                ("Visible", snapshot.Window.Visible),
                ("Minimized", snapshot.Window.Minimized),
                ("Maximized", snapshot.Window.Maximized),
                ("Foreground", snapshot.Window.Foreground),
                ("Window bounds", snapshot.Window.WindowBounds),
                ("Client size", snapshot.Window.ClientSize),
                ("Menu scale", snapshot.Window.MenuScale),
                ("Logical menu size", snapshot.Window.LogicalMenuSize)
            ]);

        AppendReportSection(
            lines,
            localize,
            "Auto Create Route",
            [
                ("Player files", snapshot.Automation.PlayerFiles),
                ("World files", snapshot.Automation.WorldFiles),
                ("Favorite players", snapshot.Automation.FavoritePlayers),
                ("Favorite worlds", snapshot.Automation.FavoriteWorlds),
                ("Player name", snapshot.Automation.PlayerName),
                ("Player difficulty", snapshot.Automation.PlayerDifficulty),
                ("World size", snapshot.Automation.WorldSize),
                ("World difficulty", snapshot.Automation.WorldDifficulty),
                ("World evil", snapshot.Automation.WorldEvil),
                ("Catch stars", snapshot.Automation.CatchStars),
                ("Catch stars through", snapshot.Automation.CatchStarsThrough),
                ("Catch speed", snapshot.Automation.CatchSpeed),
                ("Filter pyramid", snapshot.Automation.PyramidFilter),
                ("Required pyramid items", snapshot.Automation.PyramidItems),
                ("Return to main menu on filter failure", snapshot.Automation.ReturnToMainMenuOnFilterFailure),
                ("Initial wait ms", snapshot.Automation.WindowActivationDelay),
                ("Pre-click wait ms", snapshot.Automation.ClickFocusDelay),
                ("Mouse / key duration ms", snapshot.Automation.InputPressDuration),
                ("Adjacent operation delay ms", snapshot.Automation.ShortActionDelay),
                ("Cross-menu operation delay ms", snapshot.Automation.MenuActionDelay),
                ("Pyramid filter post wait ms", snapshot.Automation.PyramidFilterPostDelay)
            ]);

        AppendMultilineSection(lines, localize, "Click sequence", snapshot.Automation.AutoCreateSequence);

        AppendReportSection(
            lines,
            localize,
            "Boss Progress",
            [
                ("Skeletron", snapshot.BossProgress.Skeletron),
                ("Wall of Flesh", snapshot.BossProgress.WallOfFlesh),
                ("Destroyer", snapshot.BossProgress.Destroyer),
                ("The Twins", snapshot.BossProgress.Twins),
                ("Skeletron Prime", snapshot.BossProgress.SkeletronPrime),
                ("Plantera", snapshot.BossProgress.Plantera),
                ("Golem", snapshot.BossProgress.Golem),
                ("Lunatic Cultist", snapshot.BossProgress.LunaticCultist),
                ("Moon Lord", snapshot.BossProgress.MoonLord)
            ]);

        AppendReportSection(
            lines,
            localize,
            "World Generation",
            [
                ("Current pass", snapshot.WorldGeneration.CurrentPass),
                ("Current seed", snapshot.WorldGeneration.CurrentSeed),
                ("Progress message", snapshot.WorldGeneration.ProgressMessage),
                ("Current progress", snapshot.WorldGeneration.CurrentProgress),
                ("Total progress", snapshot.WorldGeneration.TotalProgress)
            ]);

        AppendReportSection(
            lines,
            localize,
            "Memory & Layout",
            [
                ("Probe attempts", snapshot.Memory.ProbeAttempts),
                ("Last probe", snapshot.Memory.LastProbe),
                ("Layout status", snapshot.Memory.LayoutStatus),
                ("Probe error", snapshot.Memory.ProbeError),
                ("Main module base", snapshot.Memory.MainModuleBase),
                ("Main module size", snapshot.Memory.MainModuleSize),
                ("GameMenu address", snapshot.Memory.GameMenuAddress),
                ("Boss fact addresses", snapshot.Memory.BossFactAddresses),
                ("Hardmode address", snapshot.Memory.HardmodeAddress),
                ("Generation progress address", snapshot.Memory.GenerationProgressAddress),
                ("Generation controller address", snapshot.Memory.GenerationControllerAddress),
                ("Failure stage", snapshot.Memory.FailureStage)
            ]);

        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendReportSection(
        List<string> lines,
        Func<string, string> localize,
        string title,
        params (string Label, string Value)[] rows)
    {
        if (lines.Count > 0)
        {
            lines.Add(string.Empty);
        }

        lines.Add(localize(title));
        foreach ((string label, string value) in rows)
        {
            lines.Add($"{localize(label)}: {value}");
        }
    }

    private static void AppendMultilineSection(
        List<string> lines,
        Func<string, string> localize,
        string title,
        string content)
    {
        if (lines.Count > 0)
        {
            lines.Add(string.Empty);
        }

        lines.Add(localize(title));
        lines.AddRange(content.Split([Environment.NewLine], StringSplitOptions.None));
    }
}
