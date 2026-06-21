namespace TerrariaSplit.Localization;

internal static class TerrariaLanguageCodes
{
    public const string English = "en-US";
    public const string ChineseSimplified = "zh-Hans";

    public static string FromAppLanguage(string? language)
    {
        return LanguageNames.IsChinese(language) ? ChineseSimplified : English;
    }
}
