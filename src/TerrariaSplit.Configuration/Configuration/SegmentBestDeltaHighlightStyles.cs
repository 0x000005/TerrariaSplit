namespace TerrariaSplit.Configuration;

public static class SegmentBestDeltaHighlightStyles
{
    public const string None = "None";
    public const string Rainbow = "Rainbow";
    public const string Aurora = "Aurora";

    private static readonly IReadOnlyList<string> ids = new[]
    {
        None,
        Rainbow,
        Aurora
    };

    public static IReadOnlyList<string> Ids => ids;

    public static string Normalize(string? id)
    {
        if (string.Equals(id, "Breathe", StringComparison.OrdinalIgnoreCase))
        {
            return Aurora;
        }

        return ids.Any(candidate => string.Equals(candidate, id, StringComparison.OrdinalIgnoreCase))
            ? ids.First(candidate => string.Equals(candidate, id, StringComparison.OrdinalIgnoreCase))
            : None;
    }

    public static string GetDisplayName(string id)
    {
        return Normalize(id) switch
        {
            None => "None",
            Rainbow => "Neon",
            Aurora => "Breathe",
            _ => "None"
        };
    }
}
