using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime;

/// <summary>Handles one classified tick projection failure.</summary>
public static class FuturesTickTradeDataInsertedFail
{
    /// <summary>Logs the failure without replaying the realtime observation.</summary>
    public static ValueTask ExecuteAsync(this FuturesTickTradeDataInsertedFailEvent domainEvent, ITickAggregationRealtimeContext context)
    {
        context.Logger.LogErrorEvent(Actor.TickAggregationRealtimeActor.ActorName,
            "{EventName} for {EntityId}: {ErrorMessage}",
            domainEvent.EventName, domainEvent.EntityId, domainEvent.ErrorMessage);
        return ValueTask.CompletedTask;
    }
}