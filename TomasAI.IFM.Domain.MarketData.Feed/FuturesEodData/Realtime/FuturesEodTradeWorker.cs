using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;

/// <summary>
/// Experimental, bounded, single-consumer handoff for already admitted EOD trades.
/// This queue is process-local; it does not provide source replay or durability.
/// </summary>
internal sealed class FuturesEodTradeWorker
{
    readonly Channel<WorkItem> _queue;
    readonly Func<FuturesTickTradeDataInsertedEvent, ValueTask<bool>> _process;
    readonly ILogger _logger;
    readonly Task _run;
    readonly CancellationToken generationToken = TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.Current?.Token ?? default;
    long _pending;
    long _processed;

    internal FuturesEodTradeWorker(int capacity,
        Func<FuturesTickTradeDataInsertedEvent, ValueTask<bool>> process, ILogger logger)
    {
        _process = process;
        _logger = logger;
        _queue = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });
        _run = Task.Run(RunAsync);
    }

    internal long Pending => Interlocked.Read(ref _pending);

    internal async ValueTask EnqueueAsync(FuturesTickTradeDataInsertedEvent trade)
    {
        Interlocked.Increment(ref _pending);
        try
        {
            await _queue.Writer.WriteAsync(new WorkItem(trade, null, null, Stopwatch.GetTimestamp(), Activity.Current?.Context ?? default), generationToken).ConfigureAwait(false);
        }
        catch
        {
            Interlocked.Decrement(ref _pending);
            throw;
        }
    }

    internal async ValueTask EnqueueOperationAsync(Func<ValueTask> operation)
    {
        Interlocked.Increment(ref _pending);
        try
        {
            await _queue.Writer.WriteAsync(new WorkItem(null, operation, null, Stopwatch.GetTimestamp(), Activity.Current?.Context ?? default), generationToken).ConfigureAwait(false);
        }
        catch
        {
            Interlocked.Decrement(ref _pending);
            throw;
        }
    }

    internal async ValueTask DrainAsync()
    {
        var drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await _queue.Writer.WriteAsync(new WorkItem(null, null, drained, Stopwatch.GetTimestamp(), default)).ConfigureAwait(false);
        await drained.Task.ConfigureAwait(false);
    }

    internal async ValueTask StopAsync()
    {
        _queue.Writer.TryComplete();
        await _run.ConfigureAwait(false);
    }

    async Task RunAsync()
    {
        try
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(generationToken).ConfigureAwait(false))
            {
                generationToken.ThrowIfCancellationRequested();
                if (item.Barrier is { } barrier)
                {
                    barrier.TrySetResult();
                    continue;
                }
                if (item.Operation is { } operation)
                {
                    try { await AwaitGenerationAsync(operation().AsTask()).ConfigureAwait(false); }
                    finally { Interlocked.Decrement(ref _pending); }
                    continue;
                }
                var trade = item.Trade!;
                var started = Stopwatch.GetTimestamp();
                var queueWaitMilliseconds = Stopwatch.GetElapsedTime(item.EnqueuedAt, started).TotalMilliseconds;
                using var trace = item.TraceContext == default ? null :
                    TomasAI.IFM.Shared.EventModelActor.ActorTrace.Source.StartActivity("eod.process", ActivityKind.Consumer, item.TraceContext);
                try
                {
                    if (!await AwaitGenerationAsync(_process(trade).AsTask()).ConfigureAwait(false))
                        throw new InvalidOperationException(
                            $"EOD projection rejected trade {trade.Id} ({trade.TickDataId}).");
                    RecordProcessed(trade, started, queueWaitMilliseconds);
                }
                finally
                {
                    Interlocked.Decrement(ref _pending);
                }
            }
        }
        catch (OperationCanceledException) when (generationToken.IsCancellationRequested)
        {
            _queue.Writer.TryComplete();
            while (_queue.Reader.TryRead(out var item))
            {
                item.Barrier?.TrySetCanceled(generationToken);
                if (item.Barrier is null) Interlocked.Decrement(ref _pending);
            }
        }
        catch (Exception exception)
        {
            global::TomasAI.IFM.Domain.MarketData.Feed.ActorLifecycleDiagnosticLog.Write24(_logger, exception, Pending);
            _queue.Writer.TryComplete(exception);
            while (_queue.Reader.TryRead(out var item))
                item.Barrier?.TrySetException(exception);
            throw;
        }
    }

    /// <summary>Tracks processed observations and emits bounded queue/processing latency diagnostics.</summary>
    void RecordProcessed(FuturesTickTradeDataInsertedEvent trade, long started, double queueWaitMilliseconds)
    {
                    var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    var count = Interlocked.Increment(ref _processed);
                    if (elapsed >= 250 || (count & 63) == 0)
                        {

            var diagnosticLogger = _logger;
            if (diagnosticLogger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Information))
            {
                global::TomasAI.IFM.Domain.MarketData.Feed.StorageMigrationDiagnosticLog.Write8(diagnosticLogger, null, nameof(FuturesEodTradeWorker), nameof(RunAsync), trade.EntityId.ContractId, trade.Id, trade.TickDataId, Pending, count, queueWaitMilliseconds, elapsed);
            }
        
}
    }

    async Task AwaitGenerationAsync(Task work)
    {
        try { await work.WaitAsync(generationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (generationToken.IsCancellationRequested && !work.IsCompleted)
        {
            _ = ObserveAbandonedAsync(work);
            throw;
        }
    }

    async Task<T> AwaitGenerationAsync<T>(Task<T> work)
    {
        await AwaitGenerationAsync((Task)work).ConfigureAwait(false);
        return await work.ConfigureAwait(false);
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")] 
    async Task ObserveAbandonedAsync(Task work)
    {
        try { await work.ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            // Abandoned work was canceled when its worker generation was detached.
        }
        catch (Exception error) { global::TomasAI.IFM.Domain.MarketData.Feed.ActorLifecycleDiagnosticLog.Write23(_logger, error); }
    }

    readonly record struct WorkItem(FuturesTickTradeDataInsertedEvent? Trade,
        Func<ValueTask>? Operation,
        TaskCompletionSource? Barrier, long EnqueuedAt, ActivityContext TraceContext);
}
