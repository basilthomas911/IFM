using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command;

/// <summary>Handles event-sourced EMA generation commands.</summary>
public static class GenerateFuturesEmaSignal
{
    /// <summary>Calculates and appends one EMA domain event.</summary>
    public static ServiceResult<GuidResult> Execute(this GenerateFuturesEmaSignalCommand command, FuturesEmaSignalCommandState state)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(state);
        var result = FuturesEmaAccumulator.Apply(state.Checkpoint, command.Observation);
        return state.Update(new FuturesEmaSignalGeneratedEvent
        {
            Subject = new(ActorType.Event, FuturesEmaSignalGeneratedEvent.Actor,
                FuturesEmaSignalGeneratedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            Signal = result.Signal,
            Observation = command.Observation,
            Checkpoint = result.Checkpoint
        }, command)
            ? new ServiceOk<GuidResult>(new(command.CommandId))
            : new ServiceFailed<GuidResult>(FuturesEmaSignalGeneratedEvent.ErrorCode,
                "EMA state rejected the generated event.");
    }
}
