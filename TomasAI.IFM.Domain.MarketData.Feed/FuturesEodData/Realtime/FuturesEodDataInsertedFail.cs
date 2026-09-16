using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;

/// <summary>Handles one failed futures EOD projection.</summary>
public static class FuturesEodDataInsertedFail
{
    /// <summary>Logs the classified projection failure without retrying it.</summary>
    public static ValueTask ExecuteAsync(this FuturesEodDataInsertedFailEvent domainEvent, IFuturesEodDataRealtimeContext context)
    {
        context.Logger.LogErrorEvent(FuturesEodDataRealtimeActor.ActorName,
            "{EventName} for {EntityId}: {ErrorMessage}; no replay or retry will be attempted",
            domainEvent.EventName, domainEvent.Subject.EntityId, domainEvent.ErrorMessage);
        return ValueTask.CompletedTask;
    }
}