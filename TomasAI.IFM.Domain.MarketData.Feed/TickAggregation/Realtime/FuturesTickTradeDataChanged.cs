using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime;

/// <summary>Projects one normalized trade change through the realtime projector.</summary>
public static class FuturesTickTradeDataChanged
{

    extension(FuturesTickTradeDataChangedEvent domainEvent)
    {
        /// <summary>Converts and submits the normalized observation.</summary>
        public async ValueTask ExecuteAsync(ITickAggregationRealtimeContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(domainEvent);
            if (domainEvent.OptionMarketPriceObservation is { } observation)
            {
                await context.SendOptionTradeTickPriceDataUpdatedEventAsync(domainEvent, observation.OptionTickData).ConfigureAwait(false);
                // A bid/ask mark never becomes a source trade or contributes to traded volume.
                if (observation.PriceBasis == OptionMarketPriceBasis.QuoteMidpoint) return;
            }
            _ = await context.Projector.ProcessRealtimeEventAsync(domainEvent.ToInsertedEvent()).ConfigureAwait(false);
        }
    }
}