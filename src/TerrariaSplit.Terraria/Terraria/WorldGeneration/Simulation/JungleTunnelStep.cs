namespace TerrariaSplit.Terraria.WorldGeneration.Simulation;

internal readonly record struct JungleTunnelStep(
    int Sequence,
    double CenterX,
    double CenterY,
    double Strength,
    int Left,
    int Top,
    int RightExclusive,
    int BottomExclusive);
