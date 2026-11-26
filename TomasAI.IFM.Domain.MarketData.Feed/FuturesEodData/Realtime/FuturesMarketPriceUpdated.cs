using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;

/// <summary>Handles one VX market-price observation for the EOD realtime branch.</summary>
public static class FuturesMarketPriceUpdated
{

    extension(FuturesMarketPriceUpdatedRealtimeEvent domainEvent)
    {
        /// <summary>Updates the VX EOD quote projection.</summary>
        public async ValueTask ExecuteAsync(
            IFuturesEodDataRealtimeContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            await context.OrderAsync(context, domainEvent.EntityId.ContractId, EodDeliveryLane.Identity(domainEvent), async () =>
            {
                if (!await VxQuoteMarketPriceUpdated.ExecuteVxQuoteAsync(domainEvent, context.MarketDataApi, context.Projector,
                    context.StatusConsoleWriter, context.Logger).ConfigureAwait(false))
                    throw new InvalidOperationException("VX EOD quote projection failed.");
            }, context.TradeDispatch.WorkersEnabled).ConfigureAwait(false);
        }
    }
}