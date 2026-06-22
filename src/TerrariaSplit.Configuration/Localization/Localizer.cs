namespace TerrariaSplit.Localization;

public static class Localizer
{
    private static readonly ILocalizedStringProvider English = new EnglishStrings();
    private static readonly ILocalizedStringProvider Chinese = new ChineseStrings();

    public static string Get(string key, AppSettings settings)
    {
        ILocalizedStringProvider provider = LanguageNames.IsChinese(settings.General.Language) ? Chinese : English;
        return provider.TryGet(key, out string value)
            ? value
            : key;
    }
}
