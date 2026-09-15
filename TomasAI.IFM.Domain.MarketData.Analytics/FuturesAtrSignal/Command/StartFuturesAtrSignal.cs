using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command;

/// <summary>Handles one start transition for the futures ATR signal.</summary>
public static class StartFuturesAtrSignal
{
    /// <summary>Applies the start event to the loaded signal state.</summary>
    public static ServiceResult<GuidResult> Execute(this StartFuturesAtrSignalCommand command, FuturesAtrSignalCommandState state)
    {
        var applied = state.Update(new FuturesAtrSignalStartedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesAtrSignalStartedEvent.Actor,
                FuturesAtrSignalStartedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            StartedOn = command.OriginatedOn,
            StartedBy = command.OriginatedBy
        }, command);
        return applied
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: unable to apply lifecycle event");
    }
}
