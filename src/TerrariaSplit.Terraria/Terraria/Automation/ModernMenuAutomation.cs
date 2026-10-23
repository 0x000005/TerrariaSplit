using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using TerrariaSplit.Terraria.Memory;

namespace TerrariaSplit.Terraria.Automation;

internal sealed record MenuExpectation(TerrariaMenuPage Before, TerrariaMenuPage After,
    Func<TerrariaMenuSnapshot, bool>? Completed = null, string Detail = "",
    bool SkipIfSatisfied = false, Func<TerrariaMenuSnapshot, bool>? SourceCondition = null,
    Func<TerrariaMenuSnapshot, TerrariaMenuSnapshot, bool>? Changed = null)
{
    public bool Matches(TerrariaMenuSnapshot s, TerrariaMenuSnapshot? before = null) => s.Valid && s.Page == After &&
        (Completed?.Invoke(s) ?? true) && (Changed is null || before is not null && Changed(before, s));
    public static MenuExpectation Page(TerrariaMenuPage before, TerrariaMenuPage after) => new(before, after);
    public static MenuExpectation Ensure(TerrariaMenuPage page, Func<TerrariaMenuSnapshot, bool> completed,
        string detail, Func<TerrariaMenuSnapshot, bool>? observable = null) => new(page, page, completed, detail, true, observable);
    public static MenuExpectation RandomSeed() => new(TerrariaMenuPage.Advanced, TerrariaMenuPage.Advanced,
        Detail: "random seed changed", Changed: (before, after) => !string.IsNullOrWhiteSpace(after.Seed) && after.Seed != before.Seed);
}

internal interface IModernMenuInput
{
    bool IsForeground { get; }
    bool Position(TerrariaMenuGeometry geometry, Func<TerrariaMenuGeometry, Point> target, out Point point, out string detail);
    bool Mouse(bool down);
    void Key(Keys key, bool down);
    void UnicodeText(string text);
}

internal sealed class ModernMenuAutomation : IPyramidVisibleSeedReader, IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);
    internal static readonly TimeSpan StepTimeout = TimeSpan.FromMilliseconds(1000);
    internal static readonly TimeSpan ListTimeout = TimeSpan.FromMilliseconds(2000);
    private const int TextBatchLength = 64; // Leave headroom in Terraria.Main's 100-character input queue.
    private static readonly Keys[] Modifiers = [Keys.ControlKey, Keys.LControlKey, Keys.RControlKey,
        Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey, Keys.Menu, Keys.LMenu, Keys.RMenu, Keys.LWin, Keys.RWin];
    private readonly ITerrariaMenuObserver observer;
    private readonly IModernMenuInput input;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;

    public ModernMenuAutomation(ITerrariaMenuObserver observer, IModernMenuInput input,
        Func<TimeSpan, CancellationToken, Task> delay)
    { this.observer = observer; this.input = input; this.delay = delay; }

    public Task InitializeAsync(CancellationToken token) => observer.InitializeAsync(token);

    public string? ReadCurrentSeed() => SeedText(observer.Read());

    public bool TryPredictNextSeedBatch(int count, out IReadOnlyList<string> seedTexts, out string detail)
    {
        seedTexts = [];
        detail = "Menu RNG advances each frame; filtering reads the actual seed after each acknowledged click.";
        return false;
    }

    public async Task<PyramidVisibleSeedReadResult> WaitForSeedAfterRandomizeAsync(string? previousSeedText, CancellationToken token)
    {
        int attempts = 0;
        TerrariaMenuSnapshot last = TerrariaMenuSnapshot.Unknown("No seed sample.");
        try
        {
            TerrariaMenuSnapshot found = await WaitAsync("random seed changed", s =>
            {
                attempts++;
                last = s;
                return SeedText(s) is { } seed && !string.Equals(seed, previousSeedText, StringComparison.Ordinal);
            }, token, TimeSpan.FromSeconds(1));
            return PyramidVisibleSeedReadResult.FromSeed(found.Seed!, attempts);
        }
        catch (TimeoutException)
        {
            string? seed = SeedText(last);
            return PyramidVisibleSeedReadResult.Failed(seed is null ? TerrariaWorldCreationSeedStatus.Unknown : TerrariaWorldCreationSeedStatus.Seed,
                attempts, seed ?? string.Empty);
        }
    }

    private static string? SeedText(TerrariaMenuSnapshot snapshot) =>
        snapshot.Valid && snapshot.Page is TerrariaMenuPage.World or TerrariaMenuPage.Advanced && !string.IsNullOrWhiteSpace(snapshot.Seed)
            ? snapshot.Seed : null;

    internal async Task<TerrariaMenuSnapshot> WaitAsync(string step, Func<TerrariaMenuSnapshot, bool> predicate,
        CancellationToken token, TimeSpan? timeout = null)
    {
        var clock = Stopwatch.StartNew();
        var last = TerrariaMenuSnapshot.Unknown("No sample.");
        TimeSpan limit = timeout ?? StepTimeout;
        while (clock.Elapsed < limit)
        {
            token.ThrowIfCancellationRequested();
            if (!input.IsForeground) throw new InvalidOperationException($"{step}: Terraria lost foreground focus.");
            last = observer.Read();
            if (predicate(last))
            {
                FileAppLogger.Instance.Info($"State-driven menu {step}; elapsedMs={clock.Elapsed.TotalMilliseconds:F0}; {last.Detail}");
                return last;
            }
            await delay(PollInterval, token).ConfigureAwait(false);
        }
        throw new TimeoutException($"{step}: expected state was not reached within {limit.TotalSeconds:F1}s. {last.Detail}");
    }

    public async Task ClickAsync(string step, TerrariaMenuGeometry geometry, Func<TerrariaMenuGeometry, Point> target,
        MenuExpectation expected, CancellationToken token, Func<CancellationToken, Task>? beforePress = null)
    {
        TimeSpan budget = expected.After is TerrariaMenuPage.PlayerSelect or TerrariaMenuPage.WorldSelect ? ListTimeout : StepTimeout;
        TerrariaMenuSnapshot? baseline = null;
        bool mayHavePressed = false;
        bool HasResult(TerrariaMenuSnapshot s) => expected.Matches(s, baseline) &&
            (s.Page == TerrariaMenuPage.Generating ? !s.MouseDown : s.Ready);
        bool ObservableCompletion() => expected.Before != expected.After || expected.SkipIfSatisfied || expected.Changed is not null ||
            expected.Completed is not null && baseline is not null && !expected.Matches(baseline, baseline);
        bool SourceReady(TerrariaMenuSnapshot s) => s.Ready && s.Page == expected.Before &&
            (expected.Before != TerrariaMenuPage.Keyboard || s.CanSubmit) && (expected.SourceCondition?.Invoke(s) ?? true);
        for (int attempt = 1; ; attempt++)
        {
            var clock = Stopwatch.StartNew();
            Task<TerrariaMenuSnapshot> Wait(string phase, Func<TerrariaMenuSnapshot, bool> predicate) =>
                WaitAsync($"{step} / attempt {attempt}/{AutomationRetryPolicy.MaxAttempts} / {phase}; target={expected.Detail}",
                    predicate, token, budget - clock.Elapsed);
            try
            {
                TerrariaMenuSnapshot source = await Wait("source ready or prior result", s => SourceReady(s) ||
                    mayHavePressed && s.Valid && s.Page == expected.After && (s.Ready || HasResult(s)));
                baseline ??= source;
                if ((expected.SkipIfSatisfied || mayHavePressed && ObservableCompletion()) && HasResult(source)) return;
                if (mayHavePressed && expected.Before != expected.After && source.Page == expected.After)
                {
                    // The previous click already navigated/submitted. Retry confirmation, not the side effect.
                    await Wait("prior result confirmation", HasResult);
                    return;
                }
                if (!input.Position(geometry, target, out Point point, out string detail)) throw new MenuInputException(step + ": " + detail);
                TerrariaMenuSnapshot before = await Wait($"hover at {point.X},{point.Y}", s => SourceReady(s) &&
                    (!s.UsesMenuUi || s.Hover != IntPtr.Zero && s.CursorAt(point)));
                if (beforePress is not null)
                {
                    await beforePress(token);
                    token.ThrowIfCancellationRequested();
                    before = observer.Read();
                    if (!input.IsForeground) throw new InvalidOperationException(step + ": Terraria lost foreground focus.");
                    if (!SourceReady(before) || before.UsesMenuUi && (before.Hover == IntPtr.Zero || !before.CursorAt(point)))
                        throw new MenuInputException(step + ": menu input changed while verifying the paste source.");
                }
                bool completedBeforePress = expected.Matches(before, baseline);
                if ((expected.SkipIfSatisfied || mayHavePressed && ObservableCompletion()) && completedBeforePress) return;
                token.ThrowIfCancellationRequested();
                if (clock.Elapsed >= budget) throw new TimeoutException(step + ": attempt budget exhausted before mouse down.");
                try
                {
                    mayHavePressed = true;
                    if (!input.Mouse(true)) throw new MenuInputException(step + ": mouse down failed.");
                    await Wait("down acknowledged; before=" + before.Detail, s => s.Valid &&
                        (!completedBeforePress && expected.Matches(s, baseline) ||
                         s.MouseDown && (before.UsesMenuUi
                             ? s.Page == expected.Before && s.UiMouseDown && s.LastDown != IntPtr.Zero &&
                               s.LastDown == s.Hover && s.CursorAt(point)
                             : expected.Matches(s, baseline))));
                }
                finally
                {
                    // Even cancelled attempts must release their own input before another press is possible.
                    for (int release = 1; !input.Mouse(false); release++)
                    {
                        if (release == AutomationRetryPolicy.MaxAttempts)
                            throw new InvalidOperationException(step + ": mouse release failed after three attempts.");
                        await delay(AutomationRetryPolicy.Delay, CancellationToken.None);
                    }
                }
                await Wait("result and release; before=" + before.Detail, HasResult);
                return;
            }
            catch (Exception ex) when ((ex is TimeoutException or MenuInputException) && attempt < AutomationRetryPolicy.MaxAttempts)
            {
                FileAppLogger.Instance.Info($"State-driven menu {step} / attempt {attempt}/{AutomationRetryPolicy.MaxAttempts} failed; retryDelayMs=50; error={ex.Message}");
                await delay(AutomationRetryPolicy.Delay, token);
            }
        }
    }

    private sealed class MenuInputException(string message) : InvalidOperationException(message);

    public async Task EnterTextAsync(string text, CancellationToken token)
    {
        ValidateText(text);
        TerrariaMenuSnapshot before = await WaitAsync("text input ready / modifiers released", KeyboardReady, token);
        if (before.TextMaximumLength is { } maximum && text.Length > maximum)
            throw new InvalidDataException($"Text input exceeds Terraria's input limit: length={text.Length}; maximum={maximum}.");
        if (before.Text == text) return;
        if (!string.IsNullOrEmpty(before.Text))
            await ChordAsync(Keys.Z, s => s.Page == TerrariaMenuPage.Keyboard && s.Text == "", token);
        for (int offset = 0; offset < text.Length;)
        {
            int length = Math.Min(TextBatchLength, text.Length - offset);
            if (char.IsHighSurrogate(text[offset + length - 1])) length--;
            string prefix = text[..offset];
            await WaitAsync("text batch ready", s => KeyboardReady(s) && s.Text == prefix, token);
            string batch = text.Substring(offset, length);
            input.UnicodeText(batch);
            offset += length;
            string expected = text[..offset];
            await WaitAsync($"text acknowledged {offset}/{text.Length}", s =>
            {
                if (!s.Valid || s.Page != TerrariaMenuPage.Keyboard) return false;
                if (s.Text is { } actual && !expected.StartsWith(actual, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Text input mismatch at {offset}/{text.Length}; actual={actual}; expected={expected}.");
                return s.Text == expected;
            }, token);
        }
        await WaitAsync("text input complete", s => KeyboardReady(s) && s.Text == text, token);
    }

    private static bool KeyboardReady(TerrariaMenuSnapshot s) => s.Ready && s.Page == TerrariaMenuPage.Keyboard &&
        Modifiers.All(key => !s.KeyDown((int)key));

    private static void ValidateText(string text)
    {
        if (text.Length > TerrariaMenuStateReader.MaximumMenuTextLength)
            throw new InvalidDataException($"Text exceeds the verified menu-read limit: length={text.Length}; maximum={TerrariaMenuStateReader.MaximumMenuTextLength}.");
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsControl(c)) throw new InvalidDataException("Text input contains a control character.");
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) { i++; continue; }
            if (char.IsSurrogate(c)) throw new InvalidDataException("Text input contains an invalid Unicode surrogate.");
        }
    }

    private async Task ChordAsync(Keys key, Func<TerrariaMenuSnapshot, bool> completed, CancellationToken token)
    {
        try
        {
            input.Key(Keys.ControlKey, true); input.Key(key, true);
            await WaitAsync("keyboard " + key, s => s.Valid && completed(s), token);
        }
        finally { input.Key(key, false); input.Key(Keys.ControlKey, false); }
        await WaitAsync("keyboard release " + key, s => s.Valid && !s.KeyDown((int)key), token);
    }

    internal static Func<TerrariaMenuSnapshot, bool> TemplateMatches(string code)
    {
        int begin = code.IndexOf('{'), end = code.LastIndexOf('}');
        if (begin < 0 || end <= begin) throw new InvalidDataException("Invalid player template JSON.");
        using JsonDocument document = JsonDocument.Parse(code[begin..(end + 1)]);
        var expected = new Dictionary<string, int>();
        float? pitch = null;
        foreach (JsonProperty p in document.RootElement.EnumerateObject())
        {
            string? field = p.Name.ToLowerInvariant() switch
            {
                "hairstyle" => "hair", "clothingstyle" => "skinVariant", "voicestyle" => "voiceVariant",
                "haircolor" => "hairColor", "eyecolor" => "eyeColor", "skincolor" => "skinColor", "shirtcolor" => "shirtColor",
                "undershirtcolor" => "underShirtColor", "pantscolor" => "pantsColor", "shoecolor" => "shoeColor", _ => null
            };
            if (p.Name.Equals("voicePitch", StringComparison.OrdinalIgnoreCase)) pitch = p.Value.GetSingle();
            if (field is null) continue;
            expected[field] = p.Value.ValueKind == JsonValueKind.String ? Convert.ToInt32(p.Value.GetString()!.TrimStart('#'), 16) : p.Value.GetInt32();
        }
        return s => expected.All(p => s.PlayerValues.TryGetValue(p.Key, out int v) &&
            (p.Key.EndsWith("Color", StringComparison.Ordinal) ?
                ((v & 255) << 16 | (v >> 8 & 255) << 8 | (v >> 16 & 255)) == p.Value : v == p.Value)) &&
            (!pitch.HasValue || s.PlayerValues.TryGetValue("voicePitchOffset", out int bits) && Math.Abs(BitConverter.Int32BitsToSingle(bits) - pitch.Value) < 0.001f);
    }

    internal static bool SpecialSeedsReadable(TerrariaMenuSnapshot snapshot)
    {
        if (snapshot.SpecialSeedLayoutError is { } error) throw new InvalidDataException(error);
        return snapshot.SpecialSeeds is not null;
    }

    public void Dispose() => observer.Dispose();
}
