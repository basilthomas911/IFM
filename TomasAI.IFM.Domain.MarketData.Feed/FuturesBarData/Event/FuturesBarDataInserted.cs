using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event;

/// <summary>Handles <see cref="FuturesBarDataInsertedEvent"/> as a terminal bar-data notification.</summary>
public static class FuturesBarDataInserted
{
    /// <summary>Acknowledges the event without producing another side effect.</summary>
    public static ValueTask<bool> ExecuteAsync(this FuturesBarDataInsertedEvent eventValue, IFuturesBarDataEventContext context, IEventActorContext commandApi, IEventActorContext eventApi, FuturesBarDataEventParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(eventValue); ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult(true);
    }
}
