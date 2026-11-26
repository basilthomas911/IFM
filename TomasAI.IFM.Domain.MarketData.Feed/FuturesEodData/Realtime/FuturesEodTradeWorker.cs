using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;

/// <summary>Bounded execution handoff. Callers retain source ownership until actual work completes.</summary>
internal sealed class FuturesEodTradeWorker
{
    readonly Channel<WorkItem> queue;
    readonly Func<FuturesTickTradeDataInsertedEvent, ValueTask<bool>> process;
    readonly ILogger logger;
    readonly Task run;
    readonly CancellationToken generationToken = TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.Current?.Token ?? default;
    long pending, processed;

    internal FuturesEodTradeWorker(int capacity, Func<FuturesTickTradeDataInsertedEvent, ValueTask<bool>> process, ILogger logger)
    {
        this.process = process; this.logger = logger;
        queue = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(capacity)
        { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait });
        run = Task.Run(RunAsync);
    }
    internal long Pending => Interlocked.Read(ref pending);
    internal ValueTask EnqueueAsync(FuturesTickTradeDataInsertedEvent trade)
        => EnqueueCoreAsync(new(trade, null, new(TaskCreationOptions.RunContinuationsAsynchronously), Stopwatch.GetTimestamp(), Activity.Current?.Context ?? default));
    internal ValueTask EnqueueOperationAsync(Func<ValueTask> operation)
        => EnqueueCoreAsync(new(null, operation, new(TaskCreationOptions.RunContinuationsAsynchronously), Stopwatch.GetTimestamp(), Activity.Current?.Context ?? default));
    async ValueTask EnqueueCoreAsync(WorkItem item)
    {
        Interlocked.Increment(ref pending);
        try { await queue.Writer.WriteAsync(item, generationToken).ConfigureAwait(false); }
        catch { Interlocked.Decrement(ref pending); throw; }
        // Never abandon an admitted operation or release its source while its side effects are unfinished.
        await item.Completion.Task.ConfigureAwait(false);
    }
    internal ValueTask DrainAsync() => EnqueueOperationAsync(static () => ValueTask.CompletedTask);
    internal async ValueTask StopAsync() { queue.Writer.TryComplete(); await run.ConfigureAwait(false); }
    async Task RunAsync()
    {
        try
        {
            await foreach (var item in queue.Reader.ReadAllAsync(generationToken).ConfigureAwait(false))
            {
                Exception? failure = null;
                try
                {
                    generationToken.ThrowIfCancellationRequested();
                    if (item.Operation is { } operation) await operation().ConfigureAwait(false);
                    else
                    {
                        var trade = item.Trade!; var started = Stopwatch.GetTimestamp();
                        using var trace = item.TraceContext == default ? null : TomasAI.IFM.Shared.EventModelActor.ActorTrace.Source.StartActivity("eod.process", ActivityKind.Consumer, item.TraceContext);
                        if (!await process(trade).ConfigureAwait(false)) throw new InvalidOperationException($"EOD projection rejected trade {trade.Id} ({trade.TickDataId}).");
                        var count = Interlocked.Increment(ref processed);
                        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                        if (elapsed >= 250 || (count & 63) == 0)
                            global::TomasAI.IFM.Domain.MarketData.Feed.StorageMigrationDiagnosticLog.Write8(logger, null, nameof(FuturesEodTradeWorker), nameof(RunAsync), trade.EntityId.ContractId, trade.Id, trade.TickDataId, Pending, count,
                                Stopwatch.GetElapsedTime(item.EnqueuedAt, started).TotalMilliseconds, elapsed);
                    }
                    generationToken.ThrowIfCancellationRequested();
                }
                catch (Exception error) { failure = error; }
                finally { Interlocked.Decrement(ref pending); }
                if (failure is OperationCanceledException && generationToken.IsCancellationRequested) item.Completion.TrySetCanceled(generationToken);
                else if (failure is not null) item.Completion.TrySetException(failure);
                else item.Completion.TrySetResult();
            }
        }
        catch (OperationCanceledException) when (generationToken.IsCancellationRequested)
        {
            queue.Writer.TryComplete(); FailPending(new OperationCanceledException(generationToken));
        }
        catch (Exception error)
        {
            queue.Writer.TryComplete(error); FailPending(error); throw;
        }
    }
    void FailPending(Exception error)
    {
        while (queue.Reader.TryRead(out var item))
        { item.Completion.TrySetException(error); Interlocked.Decrement(ref pending); }
    }
    readonly record struct WorkItem(FuturesTickTradeDataInsertedEvent? Trade, Func<ValueTask>? Operation,
        TaskCompletionSource Completion, long EnqueuedAt, ActivityContext TraceContext);
}
