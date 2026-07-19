namespace TerrariaSplit.Localization;

internal sealed class EnglishStrings : ILocalizedStringProvider
{
    private static readonly IReadOnlyDictionary<string, string> Values = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "Initializing...", "Initializing..." },
        { "Startup failed", "Startup failed" },
        { "TerrariaSplit could not finish initialization and must close.", "TerrariaSplit could not finish initialization and must close." },
        { "Race...", "Online..." },
        { "Race", "Online" },
        { "Race leaderboard", "Online leaderboard" },
        { "Race settings", "Online settings" },
        { "Room settings", "Room settings" },
        { "World filters", "World filters" },
        { "Dungeon-side Crimson", "Dungeon-side Crimson" },
        { "Jungle main route", "Jungle main route" },
        { "Deep", "Deep" },
        { "Very deep", "Very deep" },
        { "Enable RNG control", "Enable RNG control" },
        { "Not enabled", "Not enabled" },
        { "Journey player difficulty and Journey world difficulty must be selected together.", "Journey player difficulty and Journey world difficulty must be selected together." },
        { "Settings cannot be saved while in a Race room.", "Settings cannot be saved while in an online room. Leave the room first." },
        { "Voice", "Voice" },
        { "Voice announcements", "Voice announcements" },
        { "System default", "System default" },
        { "Installed voice", "Installed voice" },
        { "Speech speed", "Speech speed" },
        { "Volume", "Volume" },
        { "Preview", "Preview" },
        { "Apply voice settings", "Apply voice settings" },
        { "Not in a race room", "Not in an online room" },
        { "Left race room", "Left online room" },
        { "Race server URL is required.", "Online server URL is required." },
        { "Invalid race request.", "Invalid online request." },
        { "Race room was not found.", "Online room was not found." },
        { "Race room is closed.", "Online room is closed." },
        { "Invalid race split report.", "Invalid online split report." },
        { "Join or create a race room before sending race updates.", "Join or create an online room before sending online updates." },
        { "Server: {0}", "Server: {0}" },
        { "Room code: {0}", "Room code: {0}" },
        { "Room host route override hint", "All members will use your route and reference times. To adjust them, close the room first, then reopen it after applying changes." },
        { "Room member route override hint", "In this room, the timer route and reference times are temporarily replaced with the route and reference times specified by the host." },
        { "Room operation restrictions hint", "While you are in the room, pause, reset, time editing, settings, automatic world creation, world loading, and config switching are disabled." },
        { "Room host restart hint", "Only the host can restart the Race. Restart returns every player to the main menu and resets all player files, world files, timer progress, and RNG for a completely new run." },
        { "Race Start", "Start" },
        { "Race Starting in {0}", "Starting in {0}s" },
        { "Race Starting...", "Starting..." },
        { "Race Start failed.", "Start failed." },
        { "Restart", "Restart" },
        { "Restarting...", "Restarting..." },
        { "Restart failed.", "Restart failed." },
        { "Not Ready", "Not Ready" },
        { "Joined", "Not Ready" }
    };

    public bool TryGet(string key, out string value)
    {
        if (!Values.TryGetValue(key, out value!))
        {
            value = key;
        }

        return true;
    }
}
