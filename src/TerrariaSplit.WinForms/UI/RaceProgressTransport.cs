using System.Threading.Channels;
using TerrariaSplit.Race.Client;
using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.UI;

internal sealed class RaceProgressTransport : IDisposable
{
    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(1);
    private const string StartProgressKey = "start";

    private readonly RaceClientSession session;
    private readonly IAppLogger logger;
    private readonly Channel<ProgressUpload> uploads = Channel.CreateUnbounded<ProgressUpload>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly CancellationTokenSource cancellation = new();
    private readonly HashSet<string> reportedProgressKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Task pump;
    private long activePackageRevision;
    private string activeRunId = string.Empty;
    private bool disposed;

    public RaceProgressTransport(RaceClientSession session, IAppLogger logger)
    {
        this.session = session;
        this.logger = logger;
        pump = Task.Run(() => DrainAsync(cancellation.Token));
    }

    public bool RequiresReset(RaceRoomState state)
    {
        return Volatile.Read(ref activePackageRevision) != state.PackageRevision ||
            string.IsNullOrWhiteSpace(Volatile.Read(ref activeRunId));
    }

    public void Reset(RaceRoomState state, string nickname)
    {
        Volatile.Write(ref activePackageRevision, state.PackageRevision);
        Volatile.Write(ref activeRunId, Guid.NewGuid().ToString("N"));
        reportedProgressKeys.Clear();
        uploads.Writer.TryWrite(ProgressUpload.ForReset(
            new RaceProgressResetRequest(
                state.RoomCode,
                nickname,
                state.PackageRevision,
                Volatile.Read(ref activeRunId))));
    }

    public void Clear()
    {
        Volatile.Write(ref activePackageRevision, 0);
        Volatile.Write(ref activeRunId, string.Empty);
        reportedProgressKeys.Clear();
    }

    public void QueueReports(
        string roomCode,
        string nickname,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        bool runStarted)
    {
        long packageRevision = Volatile.Read(ref activePackageRevision);
        string runId = Volatile.Read(ref activeRunId);
        if (packageRevision <= 0 || string.IsNullOrWhiteSpace(runId))
        {
            return;
        }

        if (runStarted && reportedProgressKeys.Add(StartProgressKey))
        {
            uploads.Writer.TryWrite(ProgressUpload.ForStart(new RaceRunStartReport(
                roomCode,
                nickname,
                DateTimeOffset.UtcNow)
            {
                PackageRevision = packageRevision,
                RunId = runId
            }));
        }

        foreach (RaceSplitReport report in RaceSplitReportFactory.CreateProgressReports(
                     roomCode,
                     nickname,
                     statuses))
        {
            string progressKey = RaceSplitReportFactory.CreateProgressKey(report);
            if (!reportedProgressKeys.Add(progressKey))
            {
                continue;
            }

            uploads.Writer.TryWrite(ProgressUpload.ForSplit(report with
            {
                PackageRevision = packageRevision,
                RunId = runId
            }));
        }
    }

    public void QueueDeath(string roomCode, string nickname, string deathMessage)
    {
        long packageRevision = Volatile.Read(ref activePackageRevision);
        string runId = Volatile.Read(ref activeRunId);
        if (!session.IsInRoom ||
            packageRevision <= 0 ||
            string.IsNullOrWhiteSpace(runId) ||
            string.IsNullOrWhiteSpace(roomCode) ||
            string.IsNullOrWhiteSpace(nickname))
        {
            return;
        }

        uploads.Writer.TryWrite(ProgressUpload.ForDeath(new RaceDeathReport(
            roomCode,
            nickname,
            DateTimeOffset.UtcNow,
            deathMessage)
        {
            PackageRevision = packageRevision,
            RunId = runId
        }));
    }

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (ProgressUpload upload in uploads.Reader.ReadAllAsync(cancellationToken))
            {
                await SendWithRetryAsync(upload, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task SendWithRetryAsync(
        ProgressUpload upload,
        CancellationToken cancellationToken)
    {
        int retryCount = 0;
        while (!cancellationToken.IsCancellationRequested && IsCurrent(upload))
        {
            try
            {
                RaceProgressSendResult result = await SendAsync(upload, cancellationToken).ConfigureAwait(false);
                if (result is RaceProgressSendResult.Accepted or RaceProgressSendResult.Obsolete)
                {
                    return;
                }
            }
            catch (Exception ex) when (IsConnectionExitException(ex))
            {
                logger.Info("Race progress upload will retry: " + ex.Message);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Race progress upload failed and will retry.");
            }

            int delaySeconds = Math.Min(1 << Math.Min(retryCount, 5), 30);
            retryCount++;
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<RaceProgressSendResult> SendAsync(
        ProgressUpload upload,
        CancellationToken cancellationToken)
    {
        switch (upload)
        {
            case ProgressUpload.Reset reset:
            {
                RaceOperationResult<RaceRoomProgressState> result = await session.ResetProgressAsync(
                    reset.Request.PackageRevision,
                    reset.Request.RunId,
                    cancellationToken).ConfigureAwait(false);
                return Classify(result.Succeeded, result.ErrorCode, result.Message, "reset progress");
            }
            case ProgressUpload.Start start:
            {
                RaceOperationResult<RaceRoomProgressState> result = await session.ReportStartAsync(
                    start.Report,
                    cancellationToken).ConfigureAwait(false);
                return Classify(result.Succeeded, result.ErrorCode, result.Message, "start report");
            }
            case ProgressUpload.Split split:
            {
                RaceOperationResult<RaceRoomProgressState> result = await session.ReportSplitAsync(
                    split.Report,
                    cancellationToken).ConfigureAwait(false);
                return Classify(result.Succeeded, result.ErrorCode, result.Message, "split report");
            }
            case ProgressUpload.Death death:
            {
                RaceOperationResult<RaceRoomState> result = await session.ReportDeathAsync(
                    death.Report,
                    cancellationToken).ConfigureAwait(false);
                return Classify(result.Succeeded, result.ErrorCode, result.Message, "death report");
            }
            default:
                throw new NotSupportedException($"Unsupported Race progress upload {upload.GetType().Name}.");
        }
    }

    private RaceProgressSendResult Classify(
        bool succeeded,
        string errorCode,
        string message,
        string operation)
    {
        if (succeeded)
        {
            return RaceProgressSendResult.Accepted;
        }

        logger.Info($"Race {operation} rejected. Error={errorCode} Message={message}.");
        return RaceProgressSendResult.Obsolete;
    }

    private bool IsCurrent(ProgressUpload upload)
    {
        return session.IsInRoom &&
            upload.PackageRevision == Volatile.Read(ref activePackageRevision) &&
            string.Equals(upload.RunId, Volatile.Read(ref activeRunId), StringComparison.Ordinal);
    }

    private static bool IsConnectionExitException(Exception exception)
    {
        return exception is InvalidOperationException or HttpRequestException or
            OperationCanceledException or TimeoutException or ObjectDisposedException;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        uploads.Writer.TryComplete();
        cancellation.Cancel();
        try
        {
            pump.Wait(DisposeTimeout);
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static item => item is OperationCanceledException))
        {
        }

        cancellation.Dispose();
    }

    private abstract record ProgressUpload
    {
        public abstract long PackageRevision { get; }

        public abstract string RunId { get; }

        public sealed record Reset(RaceProgressResetRequest Request) : ProgressUpload
        {
            public override long PackageRevision => Request.PackageRevision;

            public override string RunId => Request.RunId;
        }

        public sealed record Start(RaceRunStartReport Report) : ProgressUpload
        {
            public override long PackageRevision => Report.PackageRevision;

            public override string RunId => Report.RunId;
        }

        public sealed record Split(RaceSplitReport Report) : ProgressUpload
        {
            public override long PackageRevision => Report.PackageRevision;

            public override string RunId => Report.RunId;
        }

        public sealed record Death(RaceDeathReport Report) : ProgressUpload
        {
            public override long PackageRevision => Report.PackageRevision;

            public override string RunId => Report.RunId;
        }

        public static ProgressUpload ForReset(RaceProgressResetRequest request)
        {
            return new Reset(request);
        }

        public static ProgressUpload ForStart(RaceRunStartReport report)
        {
            return new Start(report);
        }

        public static ProgressUpload ForSplit(RaceSplitReport report)
        {
            return new Split(report);
        }

        public static ProgressUpload ForDeath(RaceDeathReport report)
        {
            return new Death(report);
        }
    }

    private enum RaceProgressSendResult
    {
        Accepted,
        Obsolete
    }
}
