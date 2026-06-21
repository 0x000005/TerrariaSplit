using System.Collections.Concurrent;
using System.Diagnostics;

namespace TerrariaSplit.Application;

internal sealed class RuntimeCommandSequencer
{
    private readonly ConcurrentQueue<RuntimeProcessorCommand> pendingCommands = new();
    private readonly WatcherRuntimeProcessor runtimeProcessor;
    private readonly Func<long> timestampProvider;
    private long issuedSequence;
    private long appliedSequence;

    public RuntimeCommandSequencer(
        WatcherRuntimeProcessor runtimeProcessor,
        Func<long>? timestampProvider = null)
    {
        this.runtimeProcessor = runtimeProcessor;
        this.timestampProvider = timestampProvider ?? Stopwatch.GetTimestamp;
    }

    public long Queue(RuntimeCommand command)
    {
        long sequence = Interlocked.Increment(ref issuedSequence);
        pendingCommands.Enqueue(new RuntimeProcessorCommand(sequence, command));
        return sequence;
    }

    public RuntimeCommandDrainResult Drain()
    {
        long latestAppliedSequence = Volatile.Read(ref appliedSequence);
        List<RunEvent>? events = null;
        while (pendingCommands.TryDequeue(out RuntimeProcessorCommand command))
        {
            IReadOnlyList<RunEvent> commandEvents = runtimeProcessor.ApplyCommand(
                command.Command,
                timestampProvider());
            if (commandEvents.Count > 0)
            {
                events ??= new List<RunEvent>();
                events.AddRange(commandEvents);
            }

            latestAppliedSequence = command.Sequence;
        }

        Volatile.Write(ref appliedSequence, latestAppliedSequence);
        return new RuntimeCommandDrainResult(latestAppliedSequence, events ?? []);
    }

    private readonly record struct RuntimeProcessorCommand(
        long Sequence,
        RuntimeCommand Command);
}
