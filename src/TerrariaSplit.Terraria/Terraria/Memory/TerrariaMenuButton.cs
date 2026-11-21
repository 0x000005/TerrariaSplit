using System.Drawing;

namespace TerrariaSplit.Terraria.Memory;

internal enum TerrariaMenuButton
{
    SinglePlayer, NewPlayer, CharacterInfo, CharacterClothing, PasteTemplate, PlayerDifficulty,
    CreatePlayer, Submit, PlayPlayer, NewWorld, WorldSize, WorldDifficulty, WorldEvil,
    WorldSeed, AdvancedMenu, RandomSeed, AdvancedSeed, SpecialSeed, ApplySeed, CreateWorld
}

// Index is the game's own difficulty/evil value, world size code, special-seed index, or player-list index.
internal readonly record struct TerrariaMenuTarget(TerrariaMenuButton Button, int Index = 0);

internal readonly record struct TerrariaMenuButtonPosition(Point Point, string Detail);
