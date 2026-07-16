using System.Security.Cryptography;
using System.Text;
using TerrariaSplit.Race.Determinism;

namespace TerrariaSplit.Race.Contracts;

[Flags]
public enum RaceDeterminismCapability
{
    None = 0,
    WorldLock = RaceDeterminismProtocol.WorldLockCapability,
    NpcDirectDrops = RaceDeterminismProtocol.NpcDirectDropsCapability,
    PlayerTriggeredResults = RaceDeterminismProtocol.PlayerTriggeredResultsCapability,
    AlchemyAndLuck = RaceDeterminismProtocol.AlchemyAndLuckCapability,
    WorldTransitions = RaceDeterminismProtocol.WorldTransitionsCapability,
    StardustTownAndNaturalEvents = RaceDeterminismProtocol.StardustTownAndNaturalEventsCapability
}

public sealed record RaceDeterminismPackage(
    int ProtocolVersion,
    string EpochId,
    string EntropySeedBase64,
    string TerrariaCompatibilityId,
    RaceDeterminismCapability EnabledCapabilities,
    int ChancePolicyVersion)
{
    public bool TryValidate(out string error)
    {
        if (ProtocolVersion != RaceDeterminismProtocol.CurrentVersion ||
            ChancePolicyVersion != RaceDeterminismProtocol.CurrentChancePolicyVersion)
        {
            error = "The Race determinism protocol is not supported.";
            return false;
        }

        if (!Guid.TryParseExact(EpochId, "N", out _))
        {
            error = "The Race determinism epoch is invalid.";
            return false;
        }

        if (!string.Equals(
                TerrariaCompatibilityId,
                RaceDeterminismProtocol.TerrariaCompatibilityId,
                StringComparison.Ordinal))
        {
            error = "The Terraria compatibility id is not supported.";
            return false;
        }

        RaceDeterminismCapability known = (RaceDeterminismCapability)RaceDeterminismProtocol.KnownCapabilities;
        if (EnabledCapabilities == RaceDeterminismCapability.None ||
            (EnabledCapabilities & ~known) != 0)
        {
            error = "The Race determinism capability set is invalid.";
            return false;
        }

        try
        {
            if (Convert.FromBase64String(EntropySeedBase64).Length != RaceDeterminismProtocol.EntropySeedLength)
            {
                error = "The Race determinism entropy seed has an invalid length.";
                return false;
            }
        }
        catch (FormatException)
        {
            error = "The Race determinism entropy seed is invalid.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public byte[] GetEntropySeed()
    {
        if (!TryValidate(out string error))
        {
            throw new InvalidOperationException(error);
        }

        return Convert.FromBase64String(EntropySeedBase64);
    }

    public string CreateDigest()
    {
        string canonical = string.Join(
            "|",
            ProtocolVersion,
            EpochId,
            EntropySeedBase64,
            TerrariaCompatibilityId,
            (int)EnabledCapabilities,
            ChancePolicyVersion);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
