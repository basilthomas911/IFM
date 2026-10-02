using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;

/// <summary>Process-local, per-contract dispatch state owned by the realtime event context.</summary>
public sealed class FuturesEodTradeDispatchState
{
    internal readonly FuturesEodTradeReadCache ReadCache = new();
    internal readonly Dictionary<string, FuturesEodTradeWorker> Workers = new(StringComparer.Ordinal);
    internal bool WorkersEnabled;
    internal long Handoffs;
}

/// <summary>Handlers for the EOD realtime event context.</summary>
public static class FuturesEodDataRealtimeContextExtensions
{
    const int TradeWorkerCapacityPerContract = 2048;
    const int MaximumTradeWorkers = 16;

    static readonly IReadOnlyDictionary<Type, Func<IEvent, IFuturesEodDataRealtimeContext,
        FuturesEodDataEventParameters, ValueTask>> ReceiveMap =
        new Dictionary<Type, Func<IEvent, IFuturesEodDataRealtimeContext,
            FuturesEodDataEventParameters, ValueTask>>
        {
            [typeof(FuturesMarketPriceUpdatedRealtimeEvent)] = static (@event, context, _) =>
                ((FuturesMarketPriceUpdatedRealtimeEvent)@event).ExecuteAsync(context),
            [typeof(FuturesSessionStatisticsUpdatedRealtimeEvent)] = static async (@event, context, parameters) =>
            {
                _ = await ((FuturesSessionStatisticsUpdatedRealtimeEvent)@event).ExecuteAsync(
                    context, context.Projector, context.Logger).ConfigureAwait(false);
            },
            [typeof(FuturesEodDataInsertedEvent)] = static (@event, context, _) =>
                ((FuturesEodDataInsertedEvent)@event).ExecuteAsync(context),
            [typeof(FuturesEodDataInsertedCompleteEvent)] = static (@event, context, parameters) =>
                ((FuturesEodDataInsertedCompleteEvent)@event).ExecuteAsync(context, parameters),
            [typeof(VixFuturesEodDataInsertedCompleteEvent)] = static (@event, context, parameters) =>
                ((VixFuturesEodDataInsertedCompleteEvent)@event).ExecuteAsync(context, parameters),
            [typeof(FuturesEodDataInsertedFailEvent)] = static (@event, context, _) =>
                ((FuturesEodDataInsertedFailEvent)@event).ExecuteAsync(context),
            [typeof(VixFuturesEodDataInsertedFailEvent)] = static (@event, context, _) =>
                ((VixFuturesEodDataInsertedFailEvent)@event).ExecuteAsync(context),
            [typeof(VixFuturesEodDataInsertedEvent)] = static (@event, context, _) =>
                ((VixFuturesEodDataInsertedEvent)@event).ExecuteAsync(context),
            [typeof(FuturesEodSessionStatisticsUpdatedEvent)] = static (@event, context, _) =>
                ((FuturesEodSessionStatisticsUpdatedEvent)@event).ExecuteAsync(context),
            // The durable event actor already wrote this update to the blackboard.
            [typeof(FuturesEodDataUpdatedEvent)] = static (_, _, _) => ValueTask.CompletedTask
        };

    /// <summary>Starts the optional per-contract trade workers.</summary>
    public static void StartTradeWorkers(this IFuturesEodDataRealtimeContext context)
    {
        var state = context.TradeDispatch;
        state.WorkersEnabled = context.EnableAsyncTradeWorker;
        if (state.WorkersEnabled)
            context.Logger.LogInformation(
                "Futures EOD experimental per-contract trade workers enabled; capacityPerContract={Capacity}; maxContracts={MaximumContracts}.",
                TradeWorkerCapacityPerContract, MaximumTradeWorkers);
    }

    /// <summary>Drains workers before the projector is stopped.</summary>
    public static async ValueTask StopTradeWorkersAsync(this IFuturesEodDataRealtimeContext context)
    {
        var state = context.TradeDispatch;
        try
        {
            await Task.WhenAll(state.Workers.Values.Select(static worker =>
                worker.StopAsync().AsTask())).ConfigureAwait(false);
        }
        finally
        {
            state.Workers.Clear();
            state.WorkersEnabled = false;
            state.ReadCache.Clear();
            Interlocked.Exchange(ref state.Handoffs, 0);
        }
    }

    /// <summary>Dispatches an admitted EOD realtime event.</summary>
    public static async ValueTask ReceiveAsync(this IFuturesEodDataRealtimeContext context,
        IEventActorContext<FuturesEodDataRealtimeActor> actorContext, IEvent domainEvent,
        FuturesEodDataEventParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actorContext);
        ArgumentNullException.ThrowIfNull(domainEvent);
        var state = context.TradeDispatch;
        if (domainEvent is FuturesTickTradeDataInsertedEvent trade)
        {
            await context.ReceiveTradeAsync(actorContext, trade).ConfigureAwait(false);
            return;
        }
        if (state.WorkersEnabled)
        {
            if (domainEvent is FuturesSessionStatisticsUpdatedRealtimeEvent statistics)
            {
                await context.ReceiveStatisticsAsync(actorContext, statistics).ConfigureAwait(false);
                return;
            }
            if (domainEvent is FuturesEodDataUpdatedEvent update)
            {
                if (state.Workers.TryGetValue(update.EntityId.ContractId, out var worker))
                    await worker.EnqueueOperationAsync(() =>
                    {
                        state.ReadCache.Invalidate(update.EntityId);
                        return ValueTask.CompletedTask;
                    }).ConfigureAwait(false);
                else
                    state.ReadCache.Invalidate(update.EntityId);
                return;
            }
        }
        if (domainEvent is FuturesSessionStatisticsUpdatedRealtimeEvent directStatistics)
            state.ReadCache.Invalidate(new FuturesEodDataId(
                directStatistics.EntityId.ContractId, directStatistics.EntityId.ValueDate));
        else if (domainEvent is FuturesEodDataUpdatedEvent directUpdate)
            state.ReadCache.Invalidate(directUpdate.EntityId);
        if (!ReceiveMap.TryGetValue(domainEvent.GetType(), out var handler))
            throw new InvalidOperationException($"No EOD realtime handler is registered for {domainEvent.GetType().FullName}.");
        await handler(domainEvent, context, parameters).ConfigureAwait(false);
    }

    /// <summary>Hands a trade to its contract worker, or processes it inline when workers are disabled.</summary>
    public static async ValueTask ReceiveTradeAsync(this IFuturesEodDataRealtimeContext context,
        IEventActorContext<FuturesEodDataRealtimeActor> actorContext,
        FuturesTickTradeDataInsertedEvent trade)
    {
        var state = context.TradeDispatch;
        var started = Stopwatch.GetTimestamp();
        var outcome = "Failed";
        try
        {
            if (state.WorkersEnabled)
            {
                await context.GetOrCreateTradeWorker(actorContext, trade.EntityId.ContractId)
                    .EnqueueAsync(trade).ConfigureAwait(false);
                outcome = "Queued";
                return;
            }
            var processed = await trade.ExecuteWithCacheAsync(
                actorContext, context.MarketDataApi, context.BlackboardService,
                context.StatusConsoleWriter, context.Projector, context.Logger,
                state.ReadCache).ConfigureAwait(false);
            outcome = processed ? "Succeeded" : "Rejected";
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var count = Interlocked.Increment(ref state.Handoffs);
            if (elapsed >= 10 || (count & 127) == 0 || outcome == "Failed")
                context.Logger.LogInformation(
                    "Futures EOD trade handler exit: {ContractId}; SourceId={SourceId}; TickDataId={TickDataId}; outcome={Outcome}; handoffCount={HandoffCount}; elapsed {ElapsedMilliseconds:F3} ms.",
                    trade.EntityId.ContractId, trade.Id, trade.TickDataId, outcome, count, elapsed);
        }
    }

    /// <summary>Orders session statistics after trades for the same contract.</summary>
    public static async ValueTask ReceiveStatisticsAsync(this IFuturesEodDataRealtimeContext context,
        IEventActorContext<FuturesEodDataRealtimeActor> actorContext,
        FuturesSessionStatisticsUpdatedRealtimeEvent statistics)
    {
        var worker = context.GetOrCreateTradeWorker(actorContext, statistics.EntityId.ContractId);
        await worker.EnqueueOperationAsync(async () =>
        {
            context.TradeDispatch.ReadCache.Invalidate(new FuturesEodDataId(
                statistics.EntityId.ContractId, statistics.EntityId.ValueDate));
            if (!await statistics.ExecuteAsync(actorContext, context.Projector,
                    context.Logger).ConfigureAwait(false))
                throw new InvalidOperationException(
                    $"EOD statistics projection failed for {statistics.EntityId}.");
        }).ConfigureAwait(false);
    }

    static FuturesEodTradeWorker GetOrCreateTradeWorker(this IFuturesEodDataRealtimeContext context,
        IEventActorContext<FuturesEodDataRealtimeActor> actorContext, string contractId)
    {
        var state = context.TradeDispatch;
        if (state.Workers.TryGetValue(contractId, out var worker))
            return worker;
        if (state.Workers.Count >= MaximumTradeWorkers)
            throw new InvalidOperationException(
                $"Futures EOD trade worker limit ({MaximumTradeWorkers}) reached for {contractId}.");
        worker = new FuturesEodTradeWorker(TradeWorkerCapacityPerContract,
            trade => trade.ExecuteWithCacheAsync(actorContext, context.MarketDataApi,
                context.BlackboardService, context.StatusConsoleWriter,
                context.Projector, context.Logger, state.ReadCache), context.Logger);
        state.Workers.Add(contractId, worker);
        context.Logger.LogInformation(
            "Futures EOD trade worker started for {ContractId}; capacity={Capacity}.",
            contractId, TradeWorkerCapacityPerContract);
        return worker;
    }
}
