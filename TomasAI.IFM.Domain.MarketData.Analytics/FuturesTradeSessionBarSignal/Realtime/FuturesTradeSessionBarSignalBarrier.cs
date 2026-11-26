using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime;

/// <summary>Handles the private server-clock barrier used to close elapsed trade-session bars.</summary>
public static class FuturesTradeSessionBarSignalBarrier
{

    extension(FuturesTradeSessionBarSignalBarrierRealtimeEvent @event)
    {
        /// <summary>Forwards every bar completed by the UTC barrier to the Command actor.</summary>
        public async ValueTask<bool> ExecuteAsync(
            IFuturesTradeSessionBarSignalRealtimeContext context,
            ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(@event);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(logger);
            var id = @event.EntityId;
            await context.Accumulators.ProcessAsync(id,
                accumulator => accumulator.CloseThrough(@event.BarrierUtc),
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
