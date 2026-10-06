using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
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
    /// <summary>Computes and validates the Futures Vx Term Structure Signal command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(
        this UpdateFuturesVxTermStructureSignalCommand command,
        FuturesVxTermStructureSignalCommandState state)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(state);
        command.Compute(state.FuturesVxTermStructureCheckpoint, out var futuresVxTermStructureTransition);
        if (!futuresVxTermStructureTransition.Changed)
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        var errorMsg = "unable to apply FuturesVxTermStructureSignal event";
        var updated = futuresVxTermStructureTransition switch
        {
            _ => state.Update(command.CreateFuturesVxTermStructureSignalUpdatedEvent(futuresVxTermStructureTransition), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the proposed Futures Vx Term Structure Signal result from the supplied business inputs without mutating actor state or pending events.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresVxTermStructureCheckpoint">The immutable accumulator checkpoint to read or record for this domain transition.</param>
    /// <param name="futuresVxTermStructureTransition">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>True when the proposed business result advances or is accepted; otherwise, false. The output retains the no-change or rejection decision.</returns>
    /// <remarks>Accumulator validation exceptions propagate to the command actor exception boundary.</remarks>
    internal static bool Compute(this UpdateFuturesVxTermStructureSignalCommand command, FuturesVxTermStructureCheckpoint? futuresVxTermStructureCheckpoint,
        out FuturesVxTermStructureAccumulatorResult futuresVxTermStructureTransition)
    {
        futuresVxTermStructureTransition = FuturesVxTermStructureAccumulator.Apply(command.EntityId, futuresVxTermStructureCheckpoint, command.Observation, command.Configuration);
        return futuresVxTermStructureTransition.Changed;
    }

    /// <summary>Creates the Futures Vx Term Structure Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresVxTermStructureTransition">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesVxTermStructureSignalUpdatedEvent CreateFuturesVxTermStructureSignalUpdatedEvent(this UpdateFuturesVxTermStructureSignalCommand command,
        FuturesVxTermStructureAccumulatorResult futuresVxTermStructureTransition) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, FuturesVxTermStructureSignalUpdatedEvent.Actor, FuturesVxTermStructureSignalUpdatedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        FuturesVxTermStructureSignal = futuresVxTermStructureTransition.FuturesVxTermStructureSignal,
        FuturesVxTermStructureCheckpoint = futuresVxTermStructureTransition.FuturesVxTermStructureCheckpoint
    };
}
