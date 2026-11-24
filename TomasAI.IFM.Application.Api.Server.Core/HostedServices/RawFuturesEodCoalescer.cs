using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

/// <summary>Constant-space EOD views. Each contract has one cumulative trade view and one latest statistics view.</summary>
public sealed class RawFuturesEodCoalescer(IActorSupervisor supervisor, ILogger<RawFuturesEodCoalescer> logger)
    : BackgroundService, IRawFuturesEodSink
{
    readonly Lock gate = new();
    readonly Dictionary<FuturesEodDataId, FuturesEodTradeBatchRealtimeEvent> trades = [];
    readonly Dictionary<FuturesEodDataId, FuturesSessionStatisticsUpdatedRealtimeEvent> statistics = [];
    readonly Dictionary<FuturesEodDataId, Guid> sentTrades = [];
    readonly Dictionary<FuturesEodDataId, Guid> sentStatistics = [];
    public void Observe(FuturesMarketPriceUpdatedRealtimeEvent price)
    {
        if (price.UpdateSource != FuturesMarketPriceUpdateSource.Trade || price.SourceTrade is not { } trade) return;
        var id = new FuturesEodDataId(trade.EntityId.ContractId, trade.EntityId.ValueDate);
        lock (gate)
        {
            if (!trades.TryGetValue(id, out var current))
            {
                foreach (var old in trades.Keys.Where(key => key.ValueDate.DayNumber < id.ValueDate.DayNumber - 14).ToArray())
                { trades.Remove(old); sentTrades.Remove(old); }
                foreach (var old in statistics.Keys.Where(key => key.ValueDate.DayNumber < id.ValueDate.DayNumber - 14).ToArray())
                { statistics.Remove(old); sentStatistics.Remove(old); }
                if (trades.Count >= 256) throw new InvalidOperationException("EOD coalescer contract capacity exhausted.");
            }
            trades[id] = new()
            {
                Subject = new(ActorType.Realtime, FuturesEodTradeBatchRealtimeEvent.Actor, FuturesEodTradeBatchRealtimeEvent.Verb, id.Format()),
                Id = trade.Id, EntityId = id, CommandId = trade.CommandId, AggregateId = trade.AggregateId, ReceivedOn = trade.ReceivedOn,
                Last = trade with { SourceDataset = price.SourceDataset, SourceGenerationId = price.SourceGenerationId },
                High = Math.Max(current?.High ?? trade.TradeData.Price, trade.TradeData.Price),
                Low = Math.Min(current?.Low ?? trade.TradeData.Price, trade.TradeData.Price)
            };
        }
    }
    public void Observe(FuturesSessionStatisticsUpdatedRealtimeEvent source)
    {
        lock (gate)
        {
            if (!statistics.ContainsKey(source.EntityId) && statistics.Count >= 256) throw new InvalidOperationException("EOD statistics capacity exhausted.");
            statistics[source.EntityId] = source;
        }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!supervisor.IsReady) await Task.Delay(50, stoppingToken).ConfigureAwait(false);
        var producer = supervisor.GetProducer(new(ActorType.Realtime, FuturesEodTradeBatchRealtimeEvent.Actor));
        var pulse = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            FuturesEodTradeBatchRealtimeEvent[] view; FuturesSessionStatisticsUpdatedRealtimeEvent[] stats;
            lock (gate)
            {
                var newest = trades.Count == 0 ? default : trades.Keys.Max(key => key.ValueDate);
                var heartbeat = pulse++ % 5 == 0;
                view = trades.Values.Where(value => heartbeat && value.EntityId.ValueDate == newest
                    || !sentTrades.TryGetValue(value.EntityId, out var id) || id != value.Id).ToArray();
                stats = statistics.Values.Where(value => !sentStatistics.TryGetValue(value.EntityId, out var id) || id != value.Id).ToArray();
            }
            try
            {
                foreach (var source in stats)
                {
                    await producer.SendAsync<FuturesSessionStatisticsUpdatedRealtimeEvent, FuturesEodDataId>(source.Subject, source, stoppingToken).ConfigureAwait(false);
                    lock (gate) sentStatistics[source.EntityId] = source.Id;
                }
                foreach (var source in view)
                {
                    await producer.SendAsync<FuturesEodTradeBatchRealtimeEvent, FuturesEodDataId>(source.Subject, source, stoppingToken).ConfigureAwait(false);
                    lock (gate) sentTrades[source.EntityId] = source.Id;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "EOD coalesced publication failed; cumulative extrema remain retained"); }
            await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
        }
    }
}
