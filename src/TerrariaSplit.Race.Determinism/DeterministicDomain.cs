using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace TerrariaSplit.Race.Determinism
{
    public static class DeterministicDomainSeed
    {
        public static byte[] Derive(
            byte[] entropySeed,
            int protocolVersion,
            string domainId,
            string eventKey)
        {
            if (entropySeed == null || entropySeed.Length == 0)
            {
                throw new ArgumentException("Entropy seed is required.", "entropySeed");
            }

            if (string.IsNullOrWhiteSpace(domainId))
            {
                throw new ArgumentException("Domain id is required.", "domainId");
            }

            if (eventKey == null)
            {
                throw new ArgumentNullException("eventKey");
            }

            string canonical = string.Join(
                "|",
                protocolVersion.ToString(CultureInfo.InvariantCulture),
                domainId,
                eventKey);
            using (var hmac = new HMACSHA256(entropySeed))
            {
                return hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            }
        }

        public static int ToPositiveInt32(byte[] seed)
        {
            if (seed == null || seed.Length < 4)
            {
                throw new ArgumentException("At least four seed bytes are required.", "seed");
            }

            return (seed[0] | seed[1] << 8 | seed[2] << 16 | seed[3] << 24) & int.MaxValue;
        }
    }

    public static class DeterministicChanceRoller
    {
        public static bool Roll(
            byte[] entropySeed,
            int protocolVersion,
            string domainId,
            string eventKey,
            long numerator,
            long denominator)
        {
            if (numerator < 0 || denominator <= 0 || numerator > denominator)
            {
                throw new ArgumentOutOfRangeException("numerator");
            }

            if (numerator == 0)
            {
                return false;
            }

            if (numerator == denominator)
            {
                return true;
            }

            ulong bound = (ulong)denominator;
            ulong threshold = unchecked(0UL - bound) % bound;
            for (long attempt = 0; ; attempt++)
            {
                byte[] seed = DeterministicDomainSeed.Derive(
                    entropySeed,
                    protocolVersion,
                    domainId,
                    eventKey + "|" + attempt.ToString(CultureInfo.InvariantCulture));
                for (int offset = 0; offset <= seed.Length - sizeof(ulong); offset += sizeof(ulong))
                {
                    ulong sample = BitConverter.ToUInt64(seed, offset);
                    if (sample >= threshold)
                    {
                        return sample % bound < (ulong)numerator;
                    }
                }
            }
        }
    }

    public sealed class DeterministicEventCounter
    {
        private readonly object sync = new object();
        private readonly Dictionary<string, long> counters =
            new Dictionary<string, long>(StringComparer.Ordinal);

        public long Next(string domainId, string sourceKey)
        {
            if (string.IsNullOrWhiteSpace(domainId))
            {
                throw new ArgumentException("Domain id is required.", "domainId");
            }

            if (sourceKey == null)
            {
                throw new ArgumentNullException("sourceKey");
            }

            string key = domainId + "\n" + sourceKey;
            lock (sync)
            {
                long current;
                counters.TryGetValue(key, out current);
                long next = checked(current + 1);
                counters[key] = next;
                return next;
            }
        }

        public void Clear()
        {
            lock (sync)
            {
                counters.Clear();
            }
        }
    }

    public sealed class IntegerChanceAccumulator
    {
        private readonly ulong initialPhase;
        private long phase;
        private long denominator;

        public IntegerChanceAccumulator(ulong initialPhase)
        {
            this.initialPhase = initialPhase;
        }

        public bool Step(long numerator, long nextDenominator)
        {
            if (numerator < 0 || nextDenominator <= 0 || numerator > nextDenominator)
            {
                throw new ArgumentOutOfRangeException("numerator");
            }

            if (denominator == 0)
            {
                denominator = nextDenominator;
                phase = (long)(initialPhase % (ulong)nextDenominator);
            }
            else if (denominator != nextDenominator)
            {
                phase = (long)((BigInteger)phase * nextDenominator / denominator);
                denominator = nextDenominator;
            }

            phase += numerator;
            if (phase < denominator)
            {
                return false;
            }

            phase %= denominator;
            return true;
        }

        public void Reset()
        {
            phase = 0;
            denominator = 0;
        }
    }
}
