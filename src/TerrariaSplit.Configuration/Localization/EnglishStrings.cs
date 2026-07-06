namespace TerrariaSplit.Localization;

internal sealed class EnglishStrings : ILocalizedStringProvider
{
    private static readonly IReadOnlyDictionary<string, string> Values = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "Race...", "Online..." },
        { "Race", "Online" },
        { "Race leaderboard", "Online leaderboard" },
        { "Race settings", "Online settings" },
        { "Not in a race room", "Not in an online room" },
        { "Left race room", "Left online room" },
        { "Race server URL is required.", "Online server URL is required." },
        { "Invalid race request.", "Invalid online request." },
        { "Race room was not found.", "Online room was not found." },
        { "Race room is closed.", "Online room is closed." },
        { "Invalid race split report.", "Invalid online split report." },
        { "Join or create a race room before sending race updates.", "Join or create an online room before sending online updates." },
        { "Copy Room Info", "Copy Room Info" },
        { "Server: {0}", "Server: {0}" },
        { "Room code: {0}", "Room code: {0}" },
        { "Room host route override hint", "All members will use your route and reference times. To adjust them, close the room first, then reopen it after applying changes." },
        { "Room member route override hint", "In this room, the timer route and reference times are temporarily replaced with the route and reference times specified by the host." },
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
