using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime;

/// <summary>Projects one normalized trade change through the realtime projector.</summary>
public static class FuturesTickTradeDataChanged
{
    /// <summary>Converts and submits the normalized observation.</summary>
    public static async ValueTask ExecuteAsync(this FuturesTickTradeDataChangedEvent domainEvent, ITickAggregationRealtimeContext context)
    {
        _ = await context.Projector.ProcessRealtimeEventAsync(domainEvent.ToInsertedEvent()).ConfigureAwait(false);
    }
}