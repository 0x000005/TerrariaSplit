namespace TerrariaSplit.Terraria.Memory;

internal readonly record struct TerrariaMemoryResolveResult(
    string Stage,
    string StatusDetail,
    bool? ObservedGameMenu);
