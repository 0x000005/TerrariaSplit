namespace TerrariaSplit.Localization;

internal static class LanguageNames
{
    public const string English = "English";
    public const string Chinese = "中文";
    public const string LegacyChinese = "涓枃";

    public static bool IsChinese(string? language)
    {
        return string.Equals(language, Chinese, StringComparison.Ordinal) ||
            string.Equals(language, "\u4E2D\u6587", StringComparison.Ordinal) ||
            string.Equals(language, LegacyChinese, StringComparison.Ordinal);
    }

    public static string Normalize(string? language)
    {
        return IsChinese(language) ? Chinese : English;
    }
}
