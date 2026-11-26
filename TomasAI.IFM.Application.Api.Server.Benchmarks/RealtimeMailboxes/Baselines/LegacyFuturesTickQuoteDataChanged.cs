using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;

/// <summary>Projects one normalized quote change through the realtime projector.</summary>
public static class LegacyTickQuoteChanged
{

    extension(FuturesTickQuoteDataChangedEvent domainEvent)
    {
        /// <summary>Converts and submits the normalized observation.</summary>
        public async ValueTask ExecuteLegacyAsync(ITickAggregationRealtimeContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(domainEvent);
            _ = await context.Projector.ProcessRealtimeEventAsync(domainEvent.ToInsertedEvent()).ConfigureAwait(false);
        }
    }
}