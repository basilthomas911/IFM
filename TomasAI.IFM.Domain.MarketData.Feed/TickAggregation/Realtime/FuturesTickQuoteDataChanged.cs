using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime;

/// <summary>Projects one normalized quote change through the realtime projector.</summary>
public static class FuturesTickQuoteDataChanged
{
    /// <summary>Converts and submits the normalized observation.</summary>
    public static async ValueTask ExecuteAsync(this FuturesTickQuoteDataChangedEvent domainEvent, ITickAggregationRealtimeContext context)
    {
        _ = await context.Projector.ProcessRealtimeEventAsync(domainEvent.ToInsertedEvent()).ConfigureAwait(false);
    }
}