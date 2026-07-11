using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace TerrariaSplit.Tests;

internal enum TestSuite
{
    Core,
    Flow,
    Windows,
    Release
}

internal sealed record TestCase(
    string Name,
    TestSuite Suite,
    Func<CancellationToken, Task> Run,
    TimeSpan Timeout)
{
    public static TestCase Sync(string name, TestSuite suite, Action run, int timeoutSeconds = 20) =>
        new(name, suite, _ =>
        {
            run();
            return Task.CompletedTask;
        }, TimeSpan.FromSeconds(timeoutSeconds));

    public static TestCase Async(
        string name,
        TestSuite suite,
        Func<CancellationToken, Task> run,
        int timeoutSeconds = 30) =>
        new(name, suite, run, TimeSpan.FromSeconds(timeoutSeconds));
}

internal static class Check
{
    public static void Equal<T>(T expected, T actual, [CallerLineNumber] int line = 0)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Line {line}: expected '{expected}', got '{actual}'.");
        }
    }

    public static void True(bool value, [CallerLineNumber] int line = 0)
    {
        if (!value)
        {
            throw new InvalidOperationException($"Line {line}: expected true.");
        }
    }

    public static void False(bool value, [CallerLineNumber] int line = 0) => True(!value, line);

    public static T Is<T>(object? value, [CallerLineNumber] int line = 0)
    {
        if (value is not T typed)
        {
            throw new InvalidOperationException($"Line {line}: expected {typeof(T).Name}, got {value?.GetType().Name ?? "null"}.");
        }

        return typed;
    }

    public static void Sequence<T>(IEnumerable<T> expected, IEnumerable<T> actual, [CallerLineNumber] int line = 0)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException($"Line {line}: sequences differ.");
        }
    }

    public static TException Throws<TException>(Action action, [CallerLineNumber] int line = 0)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException($"Line {line}: expected {typeof(TException).Name}.");
    }

    public static async Task<TException> ThrowsAsync<TException>(Func<Task> action, [CallerLineNumber] int line = 0)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException($"Line {line}: expected {typeof(TException).Name}.");
    }
}

internal sealed class TestDirectory : IDisposable
{
    public TestDirectory([CallerMemberName] string name = "case")
    {
        string safeName = string.Concat(name.Select(character => char.IsLetterOrDigit(character) ? character : '-'));
        Path = System.IO.Path.Combine(
            FindSourceRoot(),
            "test",
            "Temp",
            "next",
            safeName + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(params string[] parts) => parts.Aggregate(Path, System.IO.Path.Combine);

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }

    private static string FindSourceRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(System.IO.Path.Combine(current.FullName, "TerrariaSplit.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate source root.");
    }
}

internal static class TestTiming
{
    public static long Timestamp(TimeSpan elapsed) =>
        (long)Math.Round(elapsed.TotalSeconds * Stopwatch.Frequency, MidpointRounding.AwayFromZero);
}
