using System.Diagnostics;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;

/// <summary>Completion-aware, per-contract EOD execution. No volatile queue admission is an acknowledgement.</summary>
public static class FuturesEodDataRealtimeContextExtensions
{
    const int TradeWorkerCapacityPerContract = 2048, MaximumTradeWorkers = 16;
    extension(IFuturesEodDataRealtimeContext context)
    {
        public async ValueTask ReceiveCumulativeAsync(IEventActorContext<FuturesEodDataRealtimeActor> actorContext, FuturesEodTradeBatchRealtimeEvent source)
        {
            if (source.Last.EntityId.ContractId != source.EntityId.ContractId || source.Last.EntityId.ValueDate != source.EntityId.ValueDate
                || source.Low <= 0 || source.Low > source.Last.TradeData.Price || source.High < source.Last.TradeData.Price)
                throw new InvalidOperationException("Cumulative EOD source identity or extrema are invalid.");
            await context.OrderAsync(actorContext, source.EntityId.ContractId, EodDeliveryLane.Identity(source), async () =>
            {
                var trade = global::TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.TickAggregationRealtimeEvents.ToInsertedEvent(source.Last);
                if (!await FuturesTickTradeDataInserted.ExecuteCumulativeAsync(trade, source.High, source.Low,
                    actorContext, context.MarketDataApi, context.Projector, context.Logger).ConfigureAwait(false))
                    throw new InvalidOperationException("Cumulative EOD projection failed.");
                context.TradeDispatch.ReadCache.Invalidate(source.EntityId);
            }, true).ConfigureAwait(false);
        }
        public void StartTradeWorkers()
        {
            var state = context.TradeDispatch;
            lock (state.RegistryGate) { state.Stopping = false; state.WorkersEnabled = context.EnableAsyncTradeWorker; }
        }
        public async ValueTask StopTradeWorkersAsync()
        {
            var state = context.TradeDispatch; FuturesEodTradeWorker[] workers; EodDeliveryLane[] lanes;
            lock (state.RegistryGate)
            { state.Stopping = true; workers = state.Workers.Values.ToArray(); lanes = state.Lanes.Values.ToArray(); }
            try { await Task.WhenAll(workers.Select(worker => worker.StopAsync().AsTask())).ConfigureAwait(false); }
            finally
            {
                await Task.WhenAll(lanes.Select(lane => lane.CloseAsync().AsTask())).ConfigureAwait(false);
                lock (state.RegistryGate) { state.Workers.Clear(); state.Lanes.Clear(); state.WorkersEnabled = false; }
                state.ReadCache.Clear(); Interlocked.Exchange(ref state.Handoffs, 0);
            }
        }
        public ValueTask DispatchStatisticsAsync(IEventActorContext<FuturesEodDataRealtimeActor> actorContext, FuturesSessionStatisticsUpdatedRealtimeEvent source)
            => context.OrderAsync(actorContext, source.EntityId.ContractId, EodDeliveryLane.Identity(source), async () =>
            {
                context.TradeDispatch.ReadCache.Invalidate(new(source.EntityId.ContractId, source.EntityId.ValueDate));
                if (!await source.ExecuteAsync(actorContext, context.Projector, context.Logger).ConfigureAwait(false))
                    throw new InvalidOperationException($"EOD statistics projection failed for {source.EntityId}.");
            }, context.TradeDispatch.WorkersEnabled);
        public ValueTask ReceiveStatisticsAsync(IEventActorContext<FuturesEodDataRealtimeActor> actorContext, FuturesSessionStatisticsUpdatedRealtimeEvent source)
            => context.DispatchStatisticsAsync(actorContext, source);
        public ValueTask InvalidateUpdateAsync(FuturesEodDataUpdatedEvent update)
        {
            var lane = context.GetLane(update.EntityId.ContractId);
            return lane.ExecuteAsync(EodDeliveryLane.Identity(update), () => { context.TradeDispatch.ReadCache.Invalidate(update.EntityId); return ValueTask.CompletedTask; });
        }
        public async ValueTask ReceiveTradeAsync(IEventActorContext<FuturesEodDataRealtimeActor> actorContext, FuturesTickTradeDataInsertedEvent trade)
        {
            if (trade.AssetTypeId != AssetTypeId.Futures || !context.MarketDataApi.IsTickDataStreamActive(trade.EntityId.ContractId)) return;
            var state = context.TradeDispatch; var started = Stopwatch.GetTimestamp(); var outcome = "Failed";
            try
            {
                if (state.WorkersEnabled) await context.GetWorker(actorContext, trade.EntityId.ContractId).EnqueueAsync(trade).ConfigureAwait(false);
                else await context.GetLane(trade.EntityId.ContractId).ExecuteAsync(EodDeliveryLane.Identity(trade), () => context.ProjectTradeAsync(actorContext, trade)).ConfigureAwait(false);
                outcome = "Succeeded";
            }
            finally
            {
                var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds; var count = Interlocked.Increment(ref state.Handoffs);
                if (elapsed >= 10 || (count & 127) == 0 || outcome == "Failed")
                    global::TomasAI.IFM.Domain.MarketData.Feed.ActorLifecycleDiagnosticLog.Write15(context.Logger, null, trade.EntityId.ContractId, trade.Id, trade.TickDataId, outcome, count, elapsed);
            }
        }
        async ValueTask ProjectTradeAsync(IEventActorContext<FuturesEodDataRealtimeActor> actorContext, FuturesTickTradeDataInsertedEvent trade)
        {
            if (!await trade.ExecuteWithCacheAsync(actorContext, context.MarketDataApi, context.StatusConsoleWriter,
                context.Projector, context.Logger, context.TradeDispatch.ReadCache).ConfigureAwait(false))
                throw new InvalidOperationException($"EOD trade projection failed for {trade.TickDataId}.");
        }
        internal ValueTask OrderAsync(IEventActorContext<FuturesEodDataRealtimeActor> actorContext, string contract, string identity, Func<ValueTask> operation, bool worker)
        {
            var lane = context.GetLane(contract);
            return worker ? context.GetWorker(actorContext, contract).EnqueueOperationAsync(() => lane.ExecuteAsync(identity, operation))
                : lane.ExecuteAsync(identity, operation);
        }
        EodDeliveryLane GetLane(string contract)
        {
            var state = context.TradeDispatch;
            lock (state.RegistryGate)
            {
                if (state.Stopping) throw new InvalidOperationException("EOD intake is stopping.");
                if (state.Lanes.TryGetValue(contract, out var lane)) return lane;
                if (state.Lanes.Count >= MaximumTradeWorkers) throw new InvalidOperationException("EOD contract lane capacity reached.");
                lane = new(); state.Lanes.Add(contract, lane); return lane;
            }
        }
        FuturesEodTradeWorker GetWorker(IEventActorContext<FuturesEodDataRealtimeActor> actorContext, string contract)
        {
            var state = context.TradeDispatch;
            lock (state.RegistryGate)
            {
                if (state.Stopping) throw new InvalidOperationException("EOD intake is stopping.");
                if (state.Workers.TryGetValue(contract, out var worker)) return worker;
                if (state.Workers.Count >= MaximumTradeWorkers) throw new InvalidOperationException("EOD trade worker capacity reached.");
                var lane = context.GetLane(contract);
                worker = new(TradeWorkerCapacityPerContract, async trade =>
                { await lane.ExecuteAsync(EodDeliveryLane.Identity(trade), () => context.ProjectTradeAsync(actorContext, trade)).ConfigureAwait(false); return true; }, context.Logger);
                state.Workers.Add(contract, worker); return worker;
            }
        }
    }
}
