using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;

/// <summary>Handles one completed VX EOD projection.</summary>
public static class VixFuturesEodDataInsertedComplete
{

    extension(VixFuturesEodDataInsertedCompleteEvent domainEvent)
    {
        /// <summary>Runs the existing VX completion behavior through the realtime context.</summary>
        public async ValueTask ExecuteAsync(IFuturesEodDataRealtimeContext context, FuturesEodDataEventParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(domainEvent);
            ArgumentNullException.ThrowIfNull(parameters);
            _ = await Event.VixFuturesEodDataInsertedComplete.ExecuteCoreAsync(
                domainEvent, context, parameters, context.Logger).ConfigureAwait(false);
        }
    }
}
