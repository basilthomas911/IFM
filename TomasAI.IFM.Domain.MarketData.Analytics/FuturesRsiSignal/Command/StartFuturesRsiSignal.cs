using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command;

public static class StartFuturesRsiSignal
{
    /// <summary>
    /// Handle a <see cref="StartFuturesRsiSignalCommand"/> by building the corresponding
    /// <see cref="FuturesRsiSignalStartedEvent"/> and updating the actor state.
    /// </summary>
    public static ServiceResult<GuidResult> Execute(this StartFuturesRsiSignalCommand e, FuturesRsiSignalCommandState state)
        => e.UpdateResult(() => state.Update(e.CreateFuturesRsiSignalStartedEvent(), e));

    internal static FuturesRsiSignalStartedEvent CreateFuturesRsiSignalStartedEvent(this StartFuturesRsiSignalCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesRsiSignalStartedEvent.Actor, FuturesRsiSignalStartedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            StartedOn = e.OriginatedOn,
            StartedBy = e.OriginatedBy
        };

}
