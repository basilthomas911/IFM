using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command;

public static class FuturesAdxSignalLifecycle
{
    public static ServiceResult<GuidResult> Execute(this StartFuturesAdxSignalCommand command, FuturesAdxSignalCommandState state)
        => Result(command, state.Update(new FuturesAdxSignalStartedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesAdxSignalStartedEvent.Actor, FuturesAdxSignalStartedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            StartedOn = command.OriginatedOn,
            StartedBy = command.OriginatedBy
        }, command));

    public static ServiceResult<GuidResult> Execute(this StopFuturesAdxSignalCommand command, FuturesAdxSignalCommandState state)
        => Result(command, state.Update(new FuturesAdxSignalStoppedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesAdxSignalStoppedEvent.Actor, FuturesAdxSignalStoppedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            StoppedOn = command.OriginatedOn,
            StoppedBy = command.OriginatedBy
        }, command));

    static ServiceResult<GuidResult> Result(ICommand command, bool updated)
        => updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: unable to apply lifecycle event");
}
