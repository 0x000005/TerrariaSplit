namespace TerrariaSplit.Configuration;

internal static class SettingsTokenParser
{
    private static readonly char[] ListSeparators = ['|', ',', ';', '\r', '\n', '\t', '\uFF0C', '\uFF1B'];

    public static string NormalizeAliasKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Trim().Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    public static IEnumerable<string> SplitList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (string token in value.Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return token;
        }
    }
}
