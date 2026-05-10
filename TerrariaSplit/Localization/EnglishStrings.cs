namespace TerrariaSplit;

internal sealed class EnglishStrings : ILocalizedStringProvider
{
    public bool TryGet(string key, out string value)
    {
        value = key;
        return true;
    }
}
