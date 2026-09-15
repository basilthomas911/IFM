using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command;

/// <summary>Handles one start transition for the futures ADX signal.</summary>
public static class StartFuturesAdxSignal
{
    /// <summary>Applies the start event to the loaded signal state.</summary>
    public static ServiceResult<GuidResult> Execute(this StartFuturesAdxSignalCommand command, FuturesAdxSignalCommandState state)
    {
        var applied = state.Update(new FuturesAdxSignalStartedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesAdxSignalStartedEvent.Actor,
                FuturesAdxSignalStartedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            StartedOn = command.OriginatedOn,
            StartedBy = command.OriginatedBy
        }, command);
        return applied
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: unable to apply lifecycle event");
    }
}
