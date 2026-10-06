using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
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
    /// <summary>Computes and validates the Futures Bb Signal command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this GenerateFuturesBbSignalCommand command, FuturesBbSignalCommandState state)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(state);
        command.Compute(state.FuturesBbCheckpoint, out var futuresBbTransition);
        if (futuresBbTransition is { IsApplied: false })
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        var errorMsg = "unable to apply FuturesBbSignal event";
        var updated = futuresBbTransition switch
        {
            _ when futuresBbTransition is null
                => command.UpdateFailed(ref errorMsg, "Bollinger EMA and bar observation identities must match."),
            _ when futuresBbTransition.FuturesBbSignal is null
                => command.UpdateFailed(ref errorMsg, "computed FuturesBbSignal is missing"),
            _ => state.Update(command.CreateFuturesBbSignalGeneratedEvent(futuresBbTransition), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the proposed Futures Bb Signal result from the supplied business inputs without mutating actor state or pending events.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresBbCheckpoint">The immutable accumulator checkpoint to read or record for this domain transition.</param>
    /// <param name="futuresBbTransition">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>True when the proposed business result advances or is accepted; otherwise, false. The output retains the no-change or rejection decision.</returns>
    /// <remarks>Accumulator validation exceptions propagate to the command actor exception boundary.</remarks>
    internal static bool Compute(this GenerateFuturesBbSignalCommand command, FuturesBbAccumulatorCheckpoint? futuresBbCheckpoint,
        out FuturesBbAccumulatorResult? futuresBbTransition)
    {
        if (command.Observation is null || command.EmaSignal is null
            || command.EmaSignal.Metadata?.ObservationId != command.Observation.ObservationId)
        {
            futuresBbTransition = null;
            return false;
        }
        futuresBbTransition = FuturesBbAccumulator.Apply(futuresBbCheckpoint, command.Observation, command.EmaSignal);
        return futuresBbTransition.IsApplied;
    }

    /// <summary>Creates the Futures Bb Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresBbTransition">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesBbSignalGeneratedEvent CreateFuturesBbSignalGeneratedEvent(this GenerateFuturesBbSignalCommand command,
        FuturesBbAccumulatorResult futuresBbTransition) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, FuturesBbSignalGeneratedEvent.Actor, FuturesBbSignalGeneratedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        FuturesBbSignal = futuresBbTransition.FuturesBbSignal!,
        FuturesBbCheckpoint = futuresBbTransition.FuturesBbCheckpoint
    };
}
