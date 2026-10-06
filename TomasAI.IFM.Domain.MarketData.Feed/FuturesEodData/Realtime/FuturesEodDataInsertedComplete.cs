using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;

/// <summary>Handles one completed futures EOD projection.</summary>
public static class FuturesEodDataInsertedComplete
{
    /// <summary>Runs the existing completion behavior through the realtime context.</summary>
    public static async ValueTask ExecuteAsync(this FuturesEodDataInsertedCompleteEvent domainEvent,
        IFuturesEodDataRealtimeContext context, FuturesEodDataEventParameters parameters)
    {
        // Completion remains observable, but a delayed persisted snapshot must not regress live displays.
        if (CurrentFuturesEodCache.Shared.TryGet(domainEvent.EntityId.ContractId, domainEvent.EntityId.ValueDate, out var current)
            && current != domainEvent.FuturesEodData) return;
        _ = await Event.FuturesEodDataInsertedComplete.ExecuteCoreAsync(
            domainEvent, context, context, parameters, context.Logger).ConfigureAwait(false);
    }
}
