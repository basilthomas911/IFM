using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;

/// <summary>Projects one normalized trade change through the realtime projector.</summary>
public static class LegacyTickTradeChanged
{

    extension(FuturesTickTradeDataChangedEvent domainEvent)
    {
        /// <summary>Converts and submits the normalized observation.</summary>
        public async ValueTask ExecuteLegacyAsync(ITickAggregationRealtimeContext context)
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