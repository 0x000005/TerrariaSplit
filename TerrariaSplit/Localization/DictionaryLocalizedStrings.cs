namespace TerrariaSplit.Localization;

internal sealed class DictionaryLocalizedStrings : ILocalizedStringProvider
{
    private readonly IReadOnlyDictionary<string, string> values;

    public DictionaryLocalizedStrings(IReadOnlyDictionary<string, string> values)
    {
        this.values = values;
    }

    public bool TryGet(string key, out string value)
    {
        return values.TryGetValue(key, out value!);
    }
}
