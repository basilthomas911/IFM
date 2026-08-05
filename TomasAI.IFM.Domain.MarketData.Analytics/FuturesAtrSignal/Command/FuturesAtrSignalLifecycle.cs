using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command;

public static class FuturesAtrSignalLifecycle
{
    public static ServiceResult<GuidResult> Execute(this StartFuturesAtrSignalCommand command, FuturesAtrSignalCommandState state)
        => Result(command, state.Update(new FuturesAtrSignalStartedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesAtrSignalStartedEvent.Actor, FuturesAtrSignalStartedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            StartedOn = command.OriginatedOn,
            StartedBy = command.OriginatedBy
        }, command));

    public static ServiceResult<GuidResult> Execute(this StopFuturesAtrSignalCommand command, FuturesAtrSignalCommandState state)
        => Result(command, state.Update(new FuturesAtrSignalStoppedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesAtrSignalStoppedEvent.Actor, FuturesAtrSignalStoppedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            StoppedOn = command.OriginatedOn,
            StoppedBy = command.OriginatedBy
        }, command));

    static ServiceResult<GuidResult> Result(ICommand command, bool updated)
        => updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: unable to apply lifecycle event");
}
