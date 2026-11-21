using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TerrariaSplit.Terraria.Automation;
using TerrariaSplit.Terraria.Memory;

namespace TerrariaSplit.Tests;

internal static class ClipboardAutomationTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Async("template clipboard retries occupation and restores only after application is acknowledged", TestSuite.Core, PasteJourney);
        yield return TestCase.Async("template clipboard retries changed text and accepts equal text with a new sequence without stealing restore ownership", TestSuite.Core, ChangedClipboardJourney);
        yield return TestCase.Async("template clipboard mismatch stops after three total attempts and preserves the last external copy", TestSuite.Core, PersistentMismatchJourney);
        yield return TestCase.Async("template clipboard occupation has bounded retries and cancellation preserves the original data", TestSuite.Core, FailureJourney);
        yield return TestCase.Sync("template clipboard snapshots retain readable image formats when Bitmap is unavailable", TestSuite.Core, ImageSnapshotJourney);
    }

    private sealed class Clipboard(object original) : IAutomationClipboard
    {
        public object? Data = original;
        public uint Sequence { get; private set; } = 1;
        public int WriteFailures, ReadFailures, RestoreFailures;
        public int Writes, Restores;
        public Action? AfterWrite;
        public object? Capture() => Data;
        public string Text
        {
            get
            {
                if (ReadFailures > 0) { ReadFailures--; throw new ExternalException("Clipboard busy."); }
                return Data as string ?? "";
            }
        }
        public void SetText(string text)
        {
            Writes++;
            if (WriteFailures > 0) { WriteFailures--; throw new ExternalException("Clipboard busy."); }
            Data = text; Sequence++; AfterWrite?.Invoke();
        }
        public void CopyByUser(string text) { Data = text; Sequence++; }
        public void Restore(object? saved)
        {
            Restores++;
            if (RestoreFailures > 0) { RestoreFailures--; throw new ExternalException("Clipboard busy."); }
            Data = saved; Sequence++;
        }
    }

    private sealed class Observer : ITerrariaMenuObserver
    {
        public TerrariaMenuSnapshot State = new(true, TerrariaMenuPage.Character, new(200), false, 0,
            false, false, new(300), IntPtr.Zero, 4200, 0, 1, 0, 1, null, null, false, null,
            new Dictionary<string, int>(), new int[8], "character", new PointF(100, 100));
        public TerrariaMenuSnapshot Read() => State;
        public bool TryReadButton(TerrariaMenuSnapshot menu, TerrariaMenuTarget target, out TerrariaMenuButtonPosition position, out string detail)
        { detail = "fake template button"; position = new(new(100, 100), detail); return true; }
        public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
        public void Dispose() { }
    }

    private sealed class Input(Observer observer) : IModernMenuInput
    {
        public bool IsForeground => true;
        public int Downs, Ups;
        public Action<bool>? OnMouse;
        public bool Position(Point point, out string detail)
        { observer.State = observer.State with { MousePosition = point }; detail = ""; return true; }
        public bool Mouse(bool down)
        {
            if (down) Downs++; else Ups++;
            observer.State = observer.State with { MouseDown = down, UiMouseDown = down, LastDown = down ? observer.State.Hover : IntPtr.Zero };
            OnMouse?.Invoke(down); return true;
        }
        public void Key(Keys key, bool down) => throw new InvalidOperationException("Template paste must not send keyboard shortcuts.");
        public void UnicodeText(string text) => throw new InvalidOperationException("Template paste must not type JSON.");
    }

    private static async Task PasteJourney(CancellationToken token)
    {
        const string template = "{\"hairStyle\":4}";
        object original = new(); // Represents non-text clipboard data, which must not become an empty clipboard.
        var clipboard = new Clipboard(original) { WriteFailures = 1, ReadFailures = 1, RestoreFailures = 1 };
        int retries = 0;
        var observer = new Observer(); var input = new Input(observer);
        bool released = false; int applicationTicks = 0;
        input.OnMouse = down => { Check.Equal(template, clipboard.Text); if (!down) released = true; };
        using var menu = new ModernMenuAutomation(observer, input, (_, _) =>
        {
            Check.Equal(template, clipboard.Text); Check.Equal(0, clipboard.Restores);
            if (released && ++applicationTicks == 2)
                observer.State = observer.State with { PlayerValues = new Dictionary<string, int> { ["hair"] = 4 } };
            return Task.CompletedTask;
        });
        await using (var scope = await ClipboardBackupScope.SetTextAsync(template, (_, _) =>
            { retries++; return Task.CompletedTask; }, token, clipboard))
        {
            Check.Equal(2, clipboard.Writes); Check.Equal(2, retries);
            await menu.ClickAsync("paste template", new(TerrariaMenuButton.AdvancedMenu),
                new(TerrariaMenuPage.Character, TerrariaMenuPage.Character, ModernMenuAutomation.TemplateMatches(template)), token, scope.EnsureTextAsync);
            Check.Equal(2, applicationTicks); Check.Equal(0, clipboard.Restores);
        }
        Check.True(ReferenceEquals(original, clipboard.Data));
        Check.Equal(2, clipboard.Restores); Check.Equal(1, input.Downs); Check.Equal(1, input.Ups);
    }

    private static async Task ChangedClipboardJourney(CancellationToken token)
    {
        foreach (string mode in new[] { "different", "same", "after-paste" })
        {
            var clipboard = new Clipboard(new object());
            var observer = new Observer(); var input = new Input(observer);
            int retries = 0;
            using var menu = new ModernMenuAutomation(observer, input, (_, _) => Task.CompletedTask);
            await using (var scope = await ClipboardBackupScope.SetTextAsync("template", (duration, _) =>
                { Check.Equal(TimeSpan.FromMilliseconds(50), duration); retries++; return Task.CompletedTask; }, token, clipboard))
            {
                clipboard.CopyByUser(mode == "same" ? "template" : "new user text");
                input.OnMouse = down =>
                {
                    Check.Equal("template", clipboard.Text);
                    if (!down && mode == "after-paste") clipboard.CopyByUser("latest user text");
                };
                await menu.ClickAsync("paste template", new(TerrariaMenuButton.AdvancedMenu),
                    MenuExpectation.Page(TerrariaMenuPage.Character, TerrariaMenuPage.Character), token, scope.EnsureTextAsync);
                Check.Equal(1, input.Downs); Check.Equal(1, input.Ups);
            }
            Check.Equal(mode == "same" ? 0 : 1, retries);
            Check.Equal(mode == "same" ? 1 : 2, clipboard.Writes);
            Check.Equal(mode == "different" ? 1 : 0, clipboard.Restores);
            Check.Equal(mode == "same" ? "template" : mode == "different" ? "new user text" : "latest user text", clipboard.Text);
        }
    }

    private static async Task PersistentMismatchJourney(CancellationToken token)
    {
        var clipboard = new Clipboard("original");
        clipboard.AfterWrite = () => clipboard.CopyByUser("external " + clipboard.Writes);
        int retries = 0;
        await Check.ThrowsAsync<ExternalException>(async () =>
        {
            await using var scope = await ClipboardBackupScope.SetTextAsync("template", (duration, _) =>
                { Check.Equal(TimeSpan.FromMilliseconds(50), duration); retries++; return Task.CompletedTask; }, token, clipboard);
        });
        Check.Equal(3, clipboard.Writes); Check.Equal(2, retries); Check.Equal(0, clipboard.Restores);
        Check.Equal("external 3", clipboard.Text);
    }

    private static async Task FailureJourney(CancellationToken token)
    {
        foreach (bool cancel in new[] { false, true })
        {
            object original = new();
            var clipboard = new Clipboard(original) { WriteFailures = 100 };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            Task<ClipboardBackupScope> task = ClipboardBackupScope.SetTextAsync("template", (_, _) =>
            {
                if (cancel) cts.Cancel();
                return Task.CompletedTask;
            }, cts.Token, clipboard);
            if (cancel) await Check.ThrowsAsync<OperationCanceledException>(async () => { _ = await task; });
            else await Check.ThrowsAsync<ExternalException>(async () => { _ = await task; });
            Check.Equal(cancel ? 1 : 3, clipboard.Writes);
            Check.True(ReferenceEquals(original, clipboard.Data)); Check.Equal(0, clipboard.Restores);
        }
    }

    private static void ImageSnapshotJourney()
    {
        byte[] png = [1, 2, 3];
        using var dib = new MemoryStream([4, 5, 6]);
        var source = new Formats(new()
        {
            [DataFormats.Bitmap] = null, ["PNG"] = png, [DataFormats.Dib] = dib,
            [DataFormats.UnicodeText] = "original user text", ["Unavailable"] = null
        });
        MemoryStream savedDib;
        using (var snapshot = SystemAutomationClipboard.CaptureSnapshot(source))
        {
            IDataObject captured = snapshot.Data;
            Check.False(captured.GetDataPresent(DataFormats.Bitmap, autoConvert: false));
            var savedPng = (byte[])captured.GetData("PNG", autoConvert: false)!;
            Check.False(ReferenceEquals(png, savedPng)); Check.True(savedPng.SequenceEqual(png));
            savedDib = (MemoryStream)captured.GetData(DataFormats.Dib, autoConvert: false)!;
            Check.False(ReferenceEquals(dib, savedDib)); Check.True(savedDib.ToArray().SequenceEqual(dib.ToArray()));
            Check.Equal("original user text", captured.GetData(DataFormats.UnicodeText, autoConvert: false) as string);
        }
        Check.True(dib.CanRead); Check.False(savedDib.CanRead);
        Check.Throws<ExternalException>(() => SystemAutomationClipboard.CaptureSnapshot(new Formats(new() { [DataFormats.Bitmap] = null })));
        using var empty = SystemAutomationClipboard.CaptureSnapshot(new Formats(new()));
        Check.Equal(0, empty.Data.GetFormats(autoConvert: false).Length);
    }

    // Deliberately model the native provider's untyped, advertised-but-unavailable formats.
#pragma warning disable WFO1001
    private sealed class Formats(Dictionary<string, object?> values) : IDataObject
    {
        public object? GetData(string format, bool autoConvert)
        {
            if (format == "Unavailable") throw new ExternalException("Provider cannot render this format.");
            return values.GetValueOrDefault(format);
        }
        public object? GetData(string format) => GetData(format, false);
        public object? GetData(Type format) => GetData(format.FullName!, false);
        public bool GetDataPresent(string format, bool autoConvert) => values.ContainsKey(format);
        public bool GetDataPresent(string format) => values.ContainsKey(format);
        public bool GetDataPresent(Type format) => values.ContainsKey(format.FullName!);
        public string[] GetFormats(bool autoConvert) => values.Keys.ToArray();
        public string[] GetFormats() => GetFormats(false);
        public void SetData(string format, bool autoConvert, object? data) => throw new NotSupportedException();
        public void SetData(string format, object? data) => throw new NotSupportedException();
        public void SetData(Type format, object? data) => throw new NotSupportedException();
        public void SetData(object? data) => throw new NotSupportedException();
    }
#pragma warning restore WFO1001
}
