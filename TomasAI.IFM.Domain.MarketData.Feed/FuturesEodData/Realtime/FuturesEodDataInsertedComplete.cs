using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;

/// <summary>Handles one completed futures EOD projection.</summary>
public static class FuturesEodDataInsertedComplete
{
    /// <summary>Runs the existing completion behavior through the realtime context.</summary>
    public static async ValueTask ExecuteAsync(this FuturesEodDataInsertedCompleteEvent domainEvent,
        IFuturesEodDataRealtimeContext context, FuturesEodDataEventParameters parameters)
    {
        _ = await Event.FuturesEodDataInsertedComplete.ExecuteAsync(
            domainEvent, context, context, parameters).ConfigureAwait(false);
    }
}