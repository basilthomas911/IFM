using TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command;

/// <summary>Handles event-sourced Bollinger generation commands.</summary>
public static class GenerateFuturesBbSignal
{
    /// <summary>Calculates and appends one Bollinger domain event.</summary>
    public static ServiceResult<GuidResult> Execute(this GenerateFuturesBbSignalCommand command, FuturesBbSignalCommandState state)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(state);
        var result = FuturesBbAccumulator.Apply(state.Checkpoint, command.Observation, command.EmaSignal);
        return state.Update(new FuturesBbSignalGeneratedEvent
        {
            Subject = new(ActorType.Event, FuturesBbSignalGeneratedEvent.Actor,
                FuturesBbSignalGeneratedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            Signal = result.Signal,
            Checkpoint = result.Checkpoint
        }, command)
            ? new ServiceOk<GuidResult>(new(command.CommandId))
            : new ServiceFailed<GuidResult>(FuturesBbSignalGeneratedEvent.ErrorCode,
                "Bollinger state rejected the generated event.");
    }
}
