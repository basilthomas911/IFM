using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime;

/// <summary>Handles normalized futures trades routed to the trade-session bar signal.</summary>
public static class FuturesMarketPriceUpdated
{

    extension(FuturesMarketPriceUpdatedRealtimeEvent @event)
    {
        /// <summary>Accumulates one trade and forwards every newly completed bar to the Command actor.</summary>
        public async ValueTask<bool> ExecuteAsync(
            IFuturesTradeSessionBarSignalRealtimeContext context,
            ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(@event);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(logger);
            if (@event.UpdateSource != FuturesMarketPriceUpdateSource.Trade) return true;
            var id = context.Accumulators.Resolve(@event.EntityId.ContractId, @event.EntityId.ValueDate);
            await context.Accumulators.ProcessAsync(id,
                accumulator => accumulator.Accept(@event),
                async bar =>
                {
                    var result = await context.PublishFuturesTradeSessionBarAsync(bar).ConfigureAwait(false);
                    if (!result.Success)
                        throw new IOException($"Bar {bar.ObservationId} publication failed: {result.ErrorCode}; {result.ErrorMessage}");
                }).ConfigureAwait(false);
            return true;
        }
    }
}
