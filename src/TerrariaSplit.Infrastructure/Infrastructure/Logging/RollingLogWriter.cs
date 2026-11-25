using System.Text;
using System.Text.Json;

namespace TerrariaSplit.Infrastructure;

internal sealed class RollingLogWriter(AppLogOptions options, string sessionHeader) : IDisposable
{
    private string path = System.IO.Path.GetFullPath(options.FilePath);
    private StreamWriter? output;
    private long bytes;
    private bool fallbackUsed;
    private bool formatChecked;
    public string Path => Volatile.Read(ref path);
    public string? RecoveryReason { get; private set; }

    public void Write(string line)
    {
        try { WriteCore(line); }
        catch (Exception exception) when (!fallbackUsed && exception is IOException or UnauthorizedAccessException)
        {
            RecoveryReason = exception.ToString();
            output?.Dispose();
            output = null;
            fallbackUsed = true;
            path = System.IO.Path.Combine(options.FallbackDirectory, System.IO.Path.GetFileName(options.FilePath));
            try { WriteCore(line); }
            catch (Exception fallbackFailure)
            {
                throw new IOException("Both log locations failed. Preferred location failure: " + exception, fallbackFailure);
            }
        }
    }

    private void WriteCore(string line)
    {
        int length = Encoding.UTF8.GetByteCount(line) + 1;
        EnsureOpen();
        if (bytes + length > options.FileByteLimit)
        {
            Rotate();
            EnsureOpen();
        }
        output!.WriteLine(line);
        bytes += length;
    }

    private void EnsureOpen()
    {
        if (output is not null) return;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        try
        {
            output = Open(Path);
        }
        catch (IOException) when (File.Exists(Path))
        {
            // Concurrent application instances must not rotate or interleave each other's files.
            path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!,
                System.IO.Path.GetFileNameWithoutExtension(Path) + "." + Environment.ProcessId + "." + Guid.NewGuid().ToString("N")[..8] + ".log");
            output = Open(Path);
        }
        bytes = output.BaseStream.Length;
        if (!formatChecked && bytes > 0)
        {
            formatChecked = true;
            if (!HasCurrentFormat())
            {
                // Preserve the previous file without mixing old text/JSON schemas into the new active log.
                Rotate();
                EnsureOpen();
                return;
            }
        }
        formatChecked = true;
        output.WriteLine(sessionHeader);
        bytes += Encoding.UTF8.GetByteCount(sessionHeader) + 1;
    }

    private bool HasCurrentFormat()
    {
        using var input = new StreamReader(new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
        string? first = input.ReadLine();
        if (first is null) return false;
        try
        {
            using var document = JsonDocument.Parse(first);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("eventCode", out var code) && code.ValueKind == JsonValueKind.String && code.GetString() == "session.start" &&
                document.RootElement.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Object && data.TryGetProperty("schemaVersion", out var version) &&
                version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out int schema) && schema == 1;
        }
        catch (JsonException) { return false; }
    }

    private void Rotate()
    {
        output!.Dispose();
        output = null;
        for (int index = options.FileCount - 1; index >= 1; index--)
        {
            string source = index == 1 ? Path : ArchivePath(index - 1);
            string target = ArchivePath(index);
            if (File.Exists(source)) File.Move(source, target, overwrite: true);
        }
        if (options.FileCount == 1) File.Delete(Path);
    }

    private static StreamWriter Open(string path) => new(new FileStream(path, FileMode.Append, FileAccess.Write,
        FileShare.Read | FileShare.Delete), new UTF8Encoding(false)) { NewLine = "\n" };
    private string ArchivePath(int index) => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!,
        System.IO.Path.GetFileNameWithoutExtension(Path) + "." + index + ".log");
    public IEnumerable<string> GetPaths() => new[] { Path }
        .Concat(Enumerable.Range(1, options.FileCount - 1).Select(ArchivePath)).Where(File.Exists);
    public void Flush() => output?.Flush();
    public void Dispose() => output?.Dispose();
}
