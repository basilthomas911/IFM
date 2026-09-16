using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;

/// <summary>Handles one realtime EOD insertion notification.</summary>
public static class FuturesEodDataInserted
{
    /// <summary>Publishes the inserted EOD value to the actor-owned blackboard.</summary>
    public static ValueTask ExecuteAsync(this FuturesEodDataInsertedEvent domainEvent, IFuturesEodDataRealtimeContext context)
    {
        context.BlackboardService.MarketDataFeed.FuturesEodData.Set(
            domainEvent.FuturesEodData.ContractId,
            domainEvent.FuturesEodData.ValueDate,
            domainEvent.FuturesEodData);
        return ValueTask.CompletedTask;
    }
}