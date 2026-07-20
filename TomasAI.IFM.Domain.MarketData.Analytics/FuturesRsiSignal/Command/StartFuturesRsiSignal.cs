using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command;

public static class StartFuturesRsiSignal
{
    /// <summary>
    /// Handle a <see cref="StartFuturesRsiSignalCommand"/> by building the corresponding
    /// <see cref="FuturesRsiSignalStartedEvent"/> and updating the actor state.
    /// </summary>
    public static bool Execute(this StartFuturesRsiSignalCommand e, FuturesRsiSignalCommandState state)
        => state.Update(e.CreateFuturesRsiSignalStartedEvent(), e);

    internal static FuturesRsiSignalStartedEvent CreateFuturesRsiSignalStartedEvent(this StartFuturesRsiSignalCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesRsiSignalStartedEvent.Actor, FuturesRsiSignalStartedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            StartedOn = e.OriginatedOn,
            StartedBy = e.OriginatedBy
        };

}
