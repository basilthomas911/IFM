using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command;

public static class FuturesMacdSignalLifecycle
{
    public static ServiceResult<GuidResult> Execute(this StartFuturesMacdSignalCommand command, FuturesMacdSignalCommandState state)
        => command.UpdateResult(() => state.Update(new FuturesMacdSignalStartedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesMacdSignalStartedEvent.Actor, FuturesMacdSignalStartedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            StartedOn = command.OriginatedOn,
            StartedBy = command.OriginatedBy
        }, command));

    public static ServiceResult<GuidResult> Execute(this StopFuturesMacdSignalCommand command, FuturesMacdSignalCommandState state)
        => command.UpdateResult(() => state.Update(new FuturesMacdSignalStoppedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesMacdSignalStoppedEvent.Actor, FuturesMacdSignalStoppedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            StoppedOn = command.OriginatedOn,
            StoppedBy = command.OriginatedBy
        }, command));
}
