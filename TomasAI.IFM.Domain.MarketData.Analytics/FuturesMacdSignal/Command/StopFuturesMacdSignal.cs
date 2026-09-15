using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command;

/// <summary>Handles one stop transition for the futures MACD signal.</summary>
public static class StopFuturesMacdSignal
{
    /// <summary>Applies the stop event to the loaded signal state.</summary>
    public static ServiceResult<GuidResult> Execute(this StopFuturesMacdSignalCommand command, FuturesMacdSignalCommandState state)
    {
        var applied = state.Update(new FuturesMacdSignalStoppedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesMacdSignalStoppedEvent.Actor,
                FuturesMacdSignalStoppedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            StoppedOn = command.OriginatedOn,
            StoppedBy = command.OriginatedBy
        }, command);
        return applied
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: unable to apply lifecycle event");
    }
}
