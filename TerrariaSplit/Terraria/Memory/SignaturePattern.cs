using System.Globalization;

namespace TerrariaSplit;

internal sealed class SignaturePattern
{
    private readonly byte[] bytes;
    private readonly bool[] wildcards;
    private readonly int firstFixedIndex;

    private SignaturePattern(byte[] bytes, bool[] wildcards)
    {
        this.bytes = bytes;
        this.wildcards = wildcards;
        firstFixedIndex = Array.FindIndex(wildcards, wildcard => !wildcard);
    }

    public int Length => bytes.Length;

    public static SignaturePattern Parse(string pattern)
    {
        string compact = pattern.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (compact.Length == 0 || compact.Length % 2 != 0)
        {
            throw new ArgumentException("Signature pattern must contain full bytes.", nameof(pattern));
        }

        var bytes = new byte[compact.Length / 2];
        var wildcards = new bool[bytes.Length];

        for (int index = 0; index < bytes.Length; index++)
        {
            string token = compact.Substring(index * 2, 2);
            if (byte.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte value))
            {
                bytes[index] = value;
            }
            else
            {
                wildcards[index] = true;
            }
        }

        return new SignaturePattern(bytes, wildcards);
    }

    public int FindIn(byte[] buffer)
    {
        if (buffer.Length < bytes.Length)
        {
            return -1;
        }

        if (firstFixedIndex < 0)
        {
            return 0;
        }

        int lastStart = buffer.Length - bytes.Length;
        byte firstFixedByte = bytes[firstFixedIndex];

        for (int start = 0; start <= lastStart; start++)
        {
            if (buffer[start + firstFixedIndex] != firstFixedByte)
            {
                continue;
            }

            if (MatchesAt(buffer, start))
            {
                return start;
            }
        }

        return -1;
    }

    private bool MatchesAt(byte[] buffer, int start)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            if (!wildcards[index] && buffer[start + index] != bytes[index])
            {
                return false;
            }
        }

        return true;
    }
}
