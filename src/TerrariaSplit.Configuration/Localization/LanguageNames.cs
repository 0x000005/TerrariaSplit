namespace TerrariaSplit.Localization;

internal static class LanguageNames
{
    public const string English = "English";
    public const string Chinese = "中文";
    private const string ChineseEnglishName = "Chinese";

    public static bool IsChinese(string? language)
    {
        return string.Equals(language, Chinese, StringComparison.Ordinal) ||
            string.Equals(language, ChineseEnglishName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(language, TerrariaLanguageCodes.ChineseSimplified, StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string? language)
    {
        return IsChinese(language) ? Chinese : English;
    }
}
