using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;

/// <summary>Publishes the live VX snapshot to the current-session blackboard.</summary>
public static class VixFuturesEodDataInserted
{
    /// <summary>Reads the worker-owned snapshot without waiting for its persistence completion.</summary>
    public static ValueTask ExecuteAsync(this VixFuturesEodDataInsertedEvent domainEvent, IFuturesEodDataRealtimeContext context)
    {
        var tick = domainEvent.VixFuturesTickData;
        if (CurrentVixEodCache.Shared.TryGet(tick.ContractId, tick.ValueDate, out var current))
            context.BlackboardService.MarketDataFeed.VixFuturesEodData.Set(tick.ContractId, tick.ValueDate, [current!]);
        return ValueTask.CompletedTask;
    }
}