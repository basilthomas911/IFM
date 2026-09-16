using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime;

/// <summary>Acknowledges a tick aggregation lifecycle notification.</summary>
public static class FuturesSessionStatisticsUpdated
{
    /// <summary>Completes because the owning projection already applied the observation.</summary>
    public static ValueTask ExecuteAsync(this FuturesSessionStatisticsUpdatedRealtimeEvent domainEvent, ITickAggregationRealtimeContext context)
        => ValueTask.CompletedTask;
}