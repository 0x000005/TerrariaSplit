using System.Runtime.CompilerServices;

namespace TerrariaSplit.Tests;

internal static class TestAssert
{
    public static void Equal<T>(
        T expected,
        T actual,
        [CallerLineNumber] int lineNumber = 0)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Line {lineNumber}: expected '{expected}', got '{actual}'.");
        }
    }
}
