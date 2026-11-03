using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime;

/// <summary>Acknowledges a tick aggregation lifecycle notification.</summary>
public static class FuturesTickTradeDataInserted
{

    extension(FuturesTickTradeDataInsertedEvent domainEvent)
    {
        /// <summary>Completes because the owning projection already applied the observation.</summary>
        public ValueTask ExecuteAsync(ITickAggregationRealtimeContext context)
            => ValueTask.CompletedTask;
    }
}