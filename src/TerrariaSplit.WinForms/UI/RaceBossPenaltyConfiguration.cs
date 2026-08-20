using TerrariaSplit.Configuration;
using TerrariaSplit.Race.InGame;

namespace TerrariaSplit.UI;

internal sealed record RaceBossPenaltyDescriptor(
    string Key,
    string Label,
    RaceBossPenaltyKind Kind,
    Func<RaceBossPenaltySettings, RaceBossPenaltyBossSettings> GetSettings);

internal static class RaceBossPenaltyConfiguration
{
    public static IReadOnlyList<RaceBossPenaltyDescriptor> Bosses { get; } =
    [
        new("Skeletron", "Skeletron", RaceBossPenaltyKind.Skeletron, settings => settings.Skeletron),
        new("WallOfFlesh", "Wall of Flesh", RaceBossPenaltyKind.WallOfFlesh, settings => settings.WallOfFlesh),
        new("SkeletronPrime", "Skeletron Prime", RaceBossPenaltyKind.SkeletronPrime, settings => settings.SkeletronPrime),
        new("Twins", "The Twins", RaceBossPenaltyKind.Twins, settings => settings.Twins),
        new("Destroyer", "Destroyer", RaceBossPenaltyKind.Destroyer, settings => settings.Destroyer),
        new("Plantera", "Plantera", RaceBossPenaltyKind.Plantera, settings => settings.Plantera),
        new("Golem", "Golem", RaceBossPenaltyKind.Golem, settings => settings.Golem),
        new("LunaticCultist", "Lunatic Cultist", RaceBossPenaltyKind.LunaticCultist, settings => settings.LunaticCultist)
    ];

    public static string Encode(RaceBossPenaltySettings? settings)
    {
        settings ??= new RaceBossPenaltySettings();
        var values = new int[RaceBossPenalty.ScheduleValueCount];
        int offset = 0;
        foreach (RaceBossPenaltyDescriptor descriptor in Bosses)
        {
            Append(values, ref offset, descriptor.GetSettings(settings));
        }

        return RaceBossPenalty.TryCreateSchedule(values, out RaceBossPenaltySchedule? schedule)
            ? schedule.Encode()
            : RaceBossPenalty.DefaultSchedule.Encode();
    }

    public static string NormalizeOrDefault(string? value)
    {
        return RaceBossPenalty.ParseScheduleOrDefault(value ?? string.Empty).Encode();
    }

    private static void Append(
        int[] values,
        ref int offset,
        RaceBossPenaltyBossSettings? boss)
    {
        boss ??= new RaceBossPenaltyBossSettings();
        values[offset++] = Normalize(boss.JourneyBaseSeconds);
        values[offset++] = Normalize(boss.JourneyProportionalSeconds);
        values[offset++] = Normalize(boss.ClassicBaseSeconds);
        values[offset++] = Normalize(boss.ClassicProportionalSeconds);
        values[offset++] = Normalize(boss.ExpertBaseSeconds);
        values[offset++] = Normalize(boss.ExpertProportionalSeconds);
        values[offset++] = Normalize(boss.MasterBaseSeconds);
        values[offset++] = Normalize(boss.MasterProportionalSeconds);
    }

    private static int Normalize(int seconds)
    {
        return Math.Clamp(seconds, 0, RaceBossPenaltySettings.MaximumSeconds);
    }
}
