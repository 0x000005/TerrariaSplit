namespace TerrariaSplit;

internal readonly record struct TerrariaMemoryResolveResult(
    string Stage,
    string StatusDetail,
    bool? ObservedGameMenu);
