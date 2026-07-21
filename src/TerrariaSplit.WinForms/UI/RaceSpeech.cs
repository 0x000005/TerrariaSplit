using System.Globalization;
using System.Speech.Synthesis;
using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.UI;

internal sealed record RaceVoiceOption(string Name, string CultureName)
{
    public string DisplayName => string.IsNullOrWhiteSpace(CultureName)
        ? Name
        : $"{Name} ({CultureName})";
}

internal sealed record RaceSpeechQueueItem(
    RaceGroupCompleted Completion,
    string SplitDisplayName,
    bool IsChinese)
{
    public string Key => string.Join(
        "|",
        Completion.RoomCode.Trim().ToUpperInvariant(),
        Completion.PackageRevision.ToString(CultureInfo.InvariantCulture),
        Completion.RunId.Trim(),
        Completion.Nickname.Trim().ToUpperInvariant(),
        Completion.SplitIndex.ToString(CultureInfo.InvariantCulture));
}

internal interface IRaceSpeechEngine
{
    IReadOnlyList<RaceVoiceOption> GetInstalledVoices();

    Task SpeakAsync(string text, RaceVoiceSettings settings, CancellationToken cancellationToken);
}

internal sealed class WindowsRaceSpeechEngine : IRaceSpeechEngine
{
    public IReadOnlyList<RaceVoiceOption> GetInstalledVoices()
    {
        try
        {
            using var synthesizer = new SpeechSynthesizer();
            return synthesizer.GetInstalledVoices()
                .Where(static voice => voice.Enabled)
                .Select(static voice => new RaceVoiceOption(
                    voice.VoiceInfo.Name,
                    voice.VoiceInfo.Culture?.Name ?? string.Empty))
                .DistinctBy(static voice => voice.Name, StringComparer.OrdinalIgnoreCase)
                .OrderBy(static voice => voice.CultureName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static voice => voice.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException)
        {
            return [];
        }
    }

    public async Task SpeakAsync(
        string text,
        RaceVoiceSettings settings,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var synthesizer = new SpeechSynthesizer
        {
            Volume = Math.Clamp(settings.Volume, 0, 100),
            Rate = ToSynthesizerRate(settings.SpeedPercent)
        };
        if (!string.IsNullOrWhiteSpace(settings.VoiceName) &&
            synthesizer.GetInstalledVoices().Any(voice =>
                voice.Enabled &&
                string.Equals(voice.VoiceInfo.Name, settings.VoiceName, StringComparison.OrdinalIgnoreCase)))
        {
            synthesizer.SelectVoice(settings.VoiceName);
        }

        synthesizer.SetOutputToDefaultAudioDevice();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<SpeakCompletedEventArgs>? completed = null;
        completed = (_, args) =>
        {
            if (args.Cancelled)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            else if (args.Error is not null)
            {
                completion.TrySetException(args.Error);
            }
            else
            {
                completion.TrySetResult();
            }
        };
        synthesizer.SpeakCompleted += completed;
        using CancellationTokenRegistration registration = cancellationToken.Register(
            static state => ((SpeechSynthesizer)state!).SpeakAsyncCancelAll(),
            synthesizer);
        try
        {
            synthesizer.SpeakAsync(text);
            await completion.Task.ConfigureAwait(false);
        }
        finally
        {
            synthesizer.SpeakCompleted -= completed;
        }
    }

    internal static int ToSynthesizerRate(int speedPercent)
    {
        double multiplier = Math.Clamp(speedPercent, 50, 200) / 100d;
        return Math.Clamp((int)Math.Round(5d * Math.Log2(multiplier)), -10, 10);
    }
}

internal sealed class RaceSpeechCoordinator : IDisposable
{
    private const int MaximumQueueLength = 256;
    private readonly object sync = new();
    private readonly Queue<RaceSpeechQueueItem> announcements = new();
    private readonly HashSet<string> knownAnnouncements = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim available = new(0);
    private readonly CancellationTokenSource disposeCancellation = new();
    private readonly IRaceSpeechEngine engine;
    private readonly Action<Exception>? reportFailure;
    private readonly Task pump;
    private RaceVoiceSettings settings = new();
    private PreviewItem? pendingPreview;
    private CancellationTokenSource? currentSpeechCancellation;
    private bool currentSpeechIsPreview;
    private bool disposed;

    public RaceSpeechCoordinator(IRaceSpeechEngine engine, Action<Exception>? reportFailure = null)
    {
        this.engine = engine;
        this.reportFailure = reportFailure;
        pump = Task.Run(() => PumpAsync(disposeCancellation.Token));
    }

    public IReadOnlyList<RaceVoiceOption> InstalledVoices => engine.GetInstalledVoices();

    public void ApplySettings(RaceVoiceSettings nextSettings)
    {
        RaceVoiceSettings normalized = CloneSettings(nextSettings);
        lock (sync)
        {
            settings = normalized;
        }

        if (!normalized.Enabled)
        {
            Clear();
        }
    }

    public bool Enqueue(RaceSpeechQueueItem item)
    {
        CancellationTokenSource? previewToCancel = null;
        lock (sync)
        {
            if (disposed || !settings.Enabled || knownAnnouncements.Contains(item.Key))
            {
                return false;
            }

            if (announcements.Count >= MaximumQueueLength)
            {
                return false;
            }

            knownAnnouncements.Add(item.Key);
            announcements.Enqueue(item);
            pendingPreview = null;
            if (currentSpeechIsPreview)
            {
                previewToCancel = currentSpeechCancellation;
            }
        }

        previewToCancel?.Cancel();
        available.Release();
        return true;
    }

    public void Preview(RaceVoiceSettings previewSettings, bool isChinese)
    {
        RaceVoiceSettings normalized = CloneSettings(previewSettings);
        lock (sync)
        {
            if (disposed || !normalized.Enabled)
            {
                return;
            }

            pendingPreview = new PreviewItem(isChinese, normalized);
        }

        available.Release();
    }

    public void RemovePendingForPlayer(RacePlayerProgressReset reset)
    {
        lock (sync)
        {
            if (announcements.Count == 0)
            {
                return;
            }

            RaceSpeechQueueItem[] retained = announcements
                .Where(item =>
                    !string.Equals(item.Completion.RoomCode, reset.RoomCode, StringComparison.OrdinalIgnoreCase) ||
                    item.Completion.PackageRevision != reset.PackageRevision ||
                    !string.Equals(item.Completion.Nickname, reset.Nickname, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            announcements.Clear();
            foreach (RaceSpeechQueueItem item in retained)
            {
                announcements.Enqueue(item);
            }
        }
    }

    public void Clear()
    {
        CancellationTokenSource? current;
        lock (sync)
        {
            announcements.Clear();
            knownAnnouncements.Clear();
            pendingPreview = null;
            current = currentSpeechCancellation;
        }

        current?.Cancel();
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
        }

        Clear();
        disposeCancellation.Cancel();
        available.Release();
        try
        {
            pump.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static item => item is OperationCanceledException))
        {
        }

        disposeCancellation.Dispose();
        available.Dispose();
    }

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await available.WaitAsync(cancellationToken).ConfigureAwait(false);
                SpeechWorkItem? work = TakeNext();
                if (work is null)
                {
                    continue;
                }

                using var speechCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                lock (sync)
                {
                    currentSpeechCancellation = speechCancellation;
                    currentSpeechIsPreview = work.IsPreview;
                }

                try
                {
                    await engine.SpeakAsync(work.Text, work.Settings, speechCancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (speechCancellation.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    reportFailure?.Invoke(ex);
                }
                finally
                {
                    lock (sync)
                    {
                        if (ReferenceEquals(currentSpeechCancellation, speechCancellation))
                        {
                            currentSpeechCancellation = null;
                            currentSpeechIsPreview = false;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private SpeechWorkItem? TakeNext()
    {
        lock (sync)
        {
            if (disposed)
            {
                return null;
            }

            if (settings.Enabled && announcements.TryDequeue(out RaceSpeechQueueItem? item))
            {
                return new SpeechWorkItem(
                    RaceSpeechTextFormatter.Format(
                        item.Completion.Nickname,
                        item.SplitDisplayName,
                        item.Completion.ElapsedMilliseconds,
                        item.IsChinese),
                    CloneSettings(settings),
                    IsPreview: false);
            }

            if (pendingPreview is PreviewItem preview)
            {
                pendingPreview = null;
                return new SpeechWorkItem(
                    RaceSpeechTextFormatter.FormatPreview(preview.IsChinese),
                    CloneSettings(preview.Settings),
                    IsPreview: true);
            }

            return null;
        }
    }

    private static RaceVoiceSettings CloneSettings(RaceVoiceSettings? source)
    {
        source ??= new RaceVoiceSettings();
        return new RaceVoiceSettings
        {
            Enabled = source.Enabled,
            VoiceName = source.VoiceName?.Trim() ?? string.Empty,
            SpeedPercent = Math.Clamp(source.SpeedPercent, 50, 200),
            Volume = Math.Clamp(source.Volume, 0, 100)
        };
    }

    private sealed record PreviewItem(bool IsChinese, RaceVoiceSettings Settings);

    private sealed record SpeechWorkItem(string Text, RaceVoiceSettings Settings, bool IsPreview);
}

internal static class RaceSpeechTextFormatter
{
    public static string FormatPreview(bool isChinese)
    {
        return Format(
            isChinese ? "玩家" : "Player",
            isChinese ? "月亮领主" : "Moon Lord",
            3_662_030,
            isChinese);
    }

    public static string Format(string nickname, string splitName, long elapsedMilliseconds, bool isChinese)
    {
        long safeMilliseconds = Math.Max(0, elapsedMilliseconds);
        long totalSeconds = safeMilliseconds / 1000;
        int seconds = (int)(totalSeconds % 60);
        int minutes = (int)(totalSeconds / 60 % 60);
        long hours = totalSeconds / 3600;
        return isChinese
            ? $"{nickname}完成分段：{splitName}，用时{FormatChineseDuration(hours, minutes, seconds)}。"
            : $"{nickname} completed split: {splitName}. Time: {FormatEnglishDuration(hours, minutes, seconds)}.";
    }

    public static string FormatGameMessage(
        string nickname,
        string splitName,
        long elapsedMilliseconds,
        bool isChinese)
    {
        long safeMilliseconds = Math.Max(0, elapsedMilliseconds);
        long totalSeconds = safeMilliseconds / 1000;
        int centiseconds = (int)(safeMilliseconds % 1000 / 10);
        int seconds = (int)(totalSeconds % 60);
        int minutes = (int)(totalSeconds / 60 % 60);
        long hours = totalSeconds / 3600;
        string duration = FormatGameDuration(hours, minutes, seconds, centiseconds);
        return isChinese
            ? $"{nickname}完成分段：{splitName}，用时{duration}。"
            : $"{nickname} completed split: {splitName}. Time: {duration}.";
    }

    private static string FormatGameDuration(
        long hours,
        int minutes,
        int seconds,
        int centiseconds)
    {
        if (hours > 0)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{hours}:{minutes:D2}:{seconds:D2}.{centiseconds:D2}");
        }

        if (minutes > 0)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{minutes}:{seconds:D2}.{centiseconds:D2}");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{seconds}.{centiseconds:D2}");
    }

    private static string FormatChineseDuration(long hours, int minutes, int seconds)
    {
        var parts = new List<string>();
        if (hours > 0)
        {
            parts.Add(ToChineseNumber(hours) + "小时");
        }

        if (minutes > 0 || hours > 0)
        {
            parts.Add(ToChineseNumber(minutes) + "分");
        }

        parts.Add(ToChineseNumber(seconds) + "秒");
        return string.Concat(parts);
    }

    private static string FormatEnglishDuration(long hours, int minutes, int seconds)
    {
        var parts = new List<string>();
        if (hours > 0)
        {
            parts.Add(ToEnglishNumber(hours) + (hours == 1 ? " hour" : " hours"));
        }

        if (minutes > 0 || hours > 0)
        {
            parts.Add(ToEnglishNumber(minutes) + (minutes == 1 ? " minute" : " minutes"));
        }

        parts.Add(ToEnglishNumber(seconds) + (seconds == 1 ? " second" : " seconds"));
        return string.Join(", ", parts);
    }

    private static string ToChineseNumber(long value)
    {
        if (value < 0 || value > 999)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        if (value < 10)
        {
            return ToChineseDigit((int)value);
        }

        if (value < 20)
        {
            return "十" + (value == 10 ? string.Empty : ToChineseDigit((int)value % 10));
        }

        if (value < 100)
        {
            return ToChineseDigit((int)value / 10) + "十" +
                (value % 10 == 0 ? string.Empty : ToChineseDigit((int)value % 10));
        }

        int remainder = (int)value % 100;
        return ToChineseDigit((int)value / 100) + "百" +
            (remainder == 0
                ? string.Empty
                : remainder < 10
                    ? "零" + ToChineseDigit(remainder)
                    : ToChineseNumber(remainder));
    }

    private static string ToChineseDigit(int value) => value switch
    {
        0 => "零",
        1 => "一",
        2 => "二",
        3 => "三",
        4 => "四",
        5 => "五",
        6 => "六",
        7 => "七",
        8 => "八",
        9 => "九",
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    private static string ToEnglishNumber(long value)
    {
        if (value is < 0 or > 999)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        string[] ones = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"];
        string[] tens = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];
        if (value < 20)
        {
            return ones[value];
        }

        if (value < 100)
        {
            return tens[value / 10] + (value % 10 == 0 ? string.Empty : "-" + ones[value % 10]);
        }

        return ones[value / 100] + " hundred" +
            (value % 100 == 0 ? string.Empty : " " + ToEnglishNumber(value % 100));
    }

}
