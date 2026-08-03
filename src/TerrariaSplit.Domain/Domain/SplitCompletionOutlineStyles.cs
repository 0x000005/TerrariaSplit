namespace TerrariaSplit.Domain;

public static class SplitCompletionOutlineStyles
{
    public const string None = "None";
    public const string Rainbow = "Rainbow";
    public const string Aurora = "Aurora";
    public const string Gold = "Gold";

    private static readonly IReadOnlyList<string> ids = new[]
    {
        None,
        Rainbow,
        Aurora,
        Gold
    };

    public static IReadOnlyList<string> Ids => ids;

    public static string Normalize(string? id)
    {
        return ids.Any(candidate => string.Equals(candidate, id, StringComparison.OrdinalIgnoreCase))
            ? ids.First(candidate => string.Equals(candidate, id, StringComparison.OrdinalIgnoreCase))
            : None;
    }

    public static string GetDisplayName(string id)
    {
        return Normalize(id) switch
        {
            None => "None",
            Rainbow => "Rainbow",
            Aurora => "Aurora",
            Gold => "Gold",
            _ => "None"
        };
    }

}
