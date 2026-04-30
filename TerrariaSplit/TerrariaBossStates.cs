namespace TerrariaSplit;

internal readonly record struct TerrariaBossStates(
    bool? Skeletron,
    bool? WallOfFlesh,
    bool? Destroyer,
    bool? Twins,
    bool? SkeletronPrime,
    bool? Plantera,
    bool? Golem,
    bool? LunaticCultist,
    bool? SolarPillar,
    bool? VortexPillar,
    bool? NebulaPillar,
    bool? StardustPillar,
    bool? MoonLord)
{
    public static TerrariaBossStates Unknown => new(
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    public bool? Get(BossFlag flag)
    {
        return flag switch
        {
            BossFlag.Skeletron => Skeletron,
            BossFlag.WallOfFlesh => WallOfFlesh,
            BossFlag.Destroyer => Destroyer,
            BossFlag.Twins => Twins,
            BossFlag.SkeletronPrime => SkeletronPrime,
            BossFlag.Plantera => Plantera,
            BossFlag.Golem => Golem,
            BossFlag.LunaticCultist => LunaticCultist,
            BossFlag.SolarPillar => SolarPillar,
            BossFlag.VortexPillar => VortexPillar,
            BossFlag.NebulaPillar => NebulaPillar,
            BossFlag.StardustPillar => StardustPillar,
            BossFlag.MoonLord => MoonLord,
            _ => null
        };
    }
}
