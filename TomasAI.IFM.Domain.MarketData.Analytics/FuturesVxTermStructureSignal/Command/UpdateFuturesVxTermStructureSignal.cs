using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Command;

/// <summary>Handles event-sourced VX curve leg updates.</summary>
public static class UpdateFuturesVxTermStructureSignal
{
    /// <summary>Calculates and appends one durable VX state transition.</summary>
    public static ServiceResult<GuidResult> Execute(
        this UpdateFuturesVxTermStructureSignalCommand command,
        FuturesVxTermStructureSignalCommandState state)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(state);
        var result = FuturesVxTermStructureAccumulator.Apply(
            command.EntityId, state.Checkpoint, command.Observation, command.Configuration);
        return state.Update(new FuturesVxTermStructureSignalUpdatedEvent
        {
            Subject = new(ActorType.Event, FuturesVxTermStructureSignalUpdatedEvent.Actor,
                FuturesVxTermStructureSignalUpdatedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            Checkpoint = result.Checkpoint,
            Signal = result.Signal
        }, command)
            ? new ServiceOk<GuidResult>(new(command.CommandId))
            : new ServiceFailed<GuidResult>(FuturesVxTermStructureSignalUpdatedEvent.ErrorCode,
                "VX term-structure state rejected the update.");
    }
}
