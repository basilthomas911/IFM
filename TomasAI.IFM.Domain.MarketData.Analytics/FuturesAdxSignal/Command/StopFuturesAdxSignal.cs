using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command;

/// <summary>Handles one stop transition for the futures ADX signal.</summary>
public static class StopFuturesAdxSignal
{
    /// <summary>Applies the stop event to the loaded signal state.</summary>
    public static ServiceResult<GuidResult> Execute(this StopFuturesAdxSignalCommand command, FuturesAdxSignalCommandState state)
    {
        var applied = state.Update(new FuturesAdxSignalStoppedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesAdxSignalStoppedEvent.Actor,
                FuturesAdxSignalStoppedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            StoppedOn = command.OriginatedOn,
            StoppedBy = command.OriginatedBy
        }, command);
        return applied
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: unable to apply lifecycle event");
    }
}
