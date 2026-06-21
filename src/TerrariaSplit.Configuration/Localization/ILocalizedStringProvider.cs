namespace TerrariaSplit.Localization;

internal interface ILocalizedStringProvider
{
    bool TryGet(string key, out string value);
}
