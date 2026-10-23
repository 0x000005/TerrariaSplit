using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TerrariaSplit.Terraria.Automation;

internal interface IAutomationClipboard
{
    uint Sequence { get; }
    object? Capture();
    void SetText(string text);
    string Text { get; }
    void Restore(object? saved);
}

internal sealed class ClipboardBackupScope : IDisposable, IAsyncDisposable
{
    private const int MaxAttempts = AutomationRetryPolicy.MaxAttempts;
    private static readonly TimeSpan RetryDelay = AutomationRetryPolicy.Delay;
    private readonly IAutomationClipboard clipboard;
    private readonly string text;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private object? previousData;
    private uint ownedSequence;
    private bool written;
    private bool disposed;

    private ClipboardBackupScope(string text, IAutomationClipboard clipboard, Func<TimeSpan, CancellationToken, Task> delay)
    { this.text = text; this.clipboard = clipboard; this.delay = delay; }

    public static async Task<ClipboardBackupScope> SetTextAsync(string text,
        Func<TimeSpan, CancellationToken, Task> delay, CancellationToken token, IAutomationClipboard? clipboard = null)
    {
        var scope = new ClipboardBackupScope(text, clipboard ?? SystemAutomationClipboard.Instance, delay);
        try
        {
            for (int attempt = 1; ; attempt++)
            {
                token.ThrowIfCancellationRequested();
                try { scope.Prepare(recoverMismatch: attempt > 1); return scope; }
                catch (ExternalException ex) when (attempt < MaxAttempts)
                {
                    FileAppLogger.Instance.Info($"Clipboard prepare attempt {attempt}/{MaxAttempts} failed; retryDelayMs=50; {ex.Message}");
                    await delay(RetryDelay, token);
                }
            }
        }
        catch { await scope.DisposeAsync(); throw; }
    }

    public static AutomationResult TrySetText(string text, out ClipboardBackupScope? scope)
    {
        scope = new ClipboardBackupScope(text, SystemAutomationClipboard.Instance, Task.Delay);
        try
        {
            for (int attempt = 1; ; attempt++)
            {
                try { scope.Prepare(recoverMismatch: attempt > 1); return AutomationResult.Success("Clipboard text was verified for automation."); }
                catch (ExternalException) when (attempt < MaxAttempts) { Thread.Sleep(RetryDelay); }
            }
        }
        catch (Exception ex)
        {
            scope.Dispose(); scope = null;
            FileAppLogger.Instance.Error(ex, "Automation failed to set clipboard text.");
            return AutomationResult.Failure("Could not write to the Windows clipboard.", "Automation failed to set and verify clipboard text.", ex);
        }
    }

    private void Prepare(bool recoverMismatch)
    {
        bool recapture = !written;
        if (written)
        {
            try { Verify(); return; }
            catch (ClipboardMismatchException) when (recoverMismatch) { recapture = true; }
        }
        if (recapture || clipboard.Sequence != ownedSequence)
        {
            // If somebody copied new data during the workflow, preserve that data before reacquiring
            // the clipboard. Never restore an older backup over the user's newer clipboard.
            uint before = clipboard.Sequence;
            object? captured = clipboard.Capture();
            if (before != clipboard.Sequence)
            {
                (captured as IDisposable)?.Dispose();
                throw new ExternalException("Clipboard changed while it was being backed up.");
            }
            (previousData as IDisposable)?.Dispose();
            previousData = captured;
        }
        clipboard.SetText(text);
        written = true;
        ownedSequence = clipboard.Sequence;
        Verify();
    }

    private void Verify()
    {
        if (!written) throw new ClipboardMismatchException("Clipboard text has not been prepared.");
        uint before = clipboard.Sequence;
        string actual = clipboard.Text;
        uint after = clipboard.Sequence;
        if (before != after)
            throw new ExternalException($"Clipboard changed during verification: sequenceBefore={before}; sequenceAfter={after}.");
        if (!string.Equals(actual, text, StringComparison.Ordinal))
            throw new ClipboardMismatchException($"Clipboard text mismatch: ownedSequence={ownedSequence}; actualSequence={after}; expectedLength={text.Length}; actualLength={actual.Length}.");
        if (after != ownedSequence)
            FileAppLogger.Instance.Info($"Clipboard sequence changed but template text still matches; ownedSequence={ownedSequence}; actualSequence={after}; restore ownership was not reclaimed.");
    }

    public async Task EnsureTextAsync(CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        for (int attempt = 1; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (attempt == 1) Verify(); else Prepare(recoverMismatch: true);
                return;
            }
            catch (ExternalException ex) when (attempt < MaxAttempts)
            {
                FileAppLogger.Instance.Info($"Clipboard verification attempt {attempt}/{MaxAttempts} failed; retryDelayMs=50; {ex.Message}");
                await delay(RetryDelay, token);
            }
        }
    }

    private void Restore()
    {
        if (written && clipboard.Sequence == ownedSequence && clipboard.Text == text && clipboard.Sequence == ownedSequence)
            clipboard.Restore(previousData);
        written = false;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            for (int attempt = 1; ; attempt++)
            {
                try { Restore(); break; }
                catch (ExternalException) when (attempt < MaxAttempts) { Thread.Sleep(RetryDelay); }
            }
        }
        catch (Exception ex) { FileAppLogger.Instance.Error(ex, "Automation failed to restore clipboard data."); }
        finally { (previousData as IDisposable)?.Dispose(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            for (int attempt = 1; ; attempt++)
            {
                try { Restore(); break; }
                catch (ExternalException) when (attempt < MaxAttempts) { await Task.Delay(RetryDelay); }
            }
        }
        catch (Exception ex) { FileAppLogger.Instance.Error(ex, "Automation failed to restore clipboard data."); }
        finally { (previousData as IDisposable)?.Dispose(); }
    }

    private sealed class ClipboardMismatchException(string message) : ExternalException(message);
}

internal sealed class SystemAutomationClipboard : IAutomationClipboard
{
    public static SystemAutomationClipboard Instance { get; } = new();
    public uint Sequence => GetClipboardSequenceNumber();
    public string Text => Clipboard.GetText(TextDataFormat.UnicodeText);

    public object? Capture()
    {
        IDataObject? source = Clipboard.GetDataObject();
        if (source is null) return null;
        return CaptureSnapshot(source);
    }

    internal static SavedClipboard CaptureSnapshot(IDataObject source)
    {
        var saved = new SavedClipboard();
        try
        {
            string[] formats = source.GetFormats(autoConvert: false);
            int captured = 0;
            foreach (string format in formats)
            {
                object? sourceValue;
                try { sourceValue = source.GetData(format, autoConvert: false); }
                catch (ExternalException ex)
                {
                    FileAppLogger.Instance.Info($"Clipboard backup skipped unavailable format {format}: {ex.Message}");
                    continue;
                }
                if (sourceValue is null)
                {
                    FileAppLogger.Instance.Info($"Clipboard backup skipped unavailable format {format}.");
                    continue;
                }
                object value = sourceValue switch
                {
                    System.Drawing.Image image => image.Clone(),
                    MemoryStream stream => new MemoryStream(stream.ToArray()),
                    Array array => array.Clone(),
                    _ => sourceValue
                };
                if (!ReferenceEquals(value, sourceValue) && value is IDisposable owned) saved.Owned.Add(owned);
                saved.Data.SetData(format, autoConvert: false, data: value);
                captured++;
            }
            if (formats.Length > 0 && captured == 0)
                throw new ExternalException("The clipboard advertises data, but none of its formats could be backed up. The original clipboard was not overwritten.");
            return saved;
        }
        catch { saved.Dispose(); throw; }
    }

    public void SetText(string text)
    {
        var data = new DataObject();
        data.SetText(text, TextDataFormat.UnicodeText);
        Clipboard.SetDataObject(data, copy: true, retryTimes: 0, retryDelay: 0);
    }

    public void Restore(object? saved)
    {
        if (saved is SavedClipboard snapshot) Clipboard.SetDataObject(snapshot.Data, copy: true, retryTimes: 0, retryDelay: 0);
        else Clipboard.Clear();
    }

    internal sealed class SavedClipboard : IDisposable
    {
        public DataObject Data { get; } = new();
        public List<IDisposable> Owned { get; } = [];
        public void Dispose() { foreach (IDisposable value in Owned) value.Dispose(); }
    }

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
}
