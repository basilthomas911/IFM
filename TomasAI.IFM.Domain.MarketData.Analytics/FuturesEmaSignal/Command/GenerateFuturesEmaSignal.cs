using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
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
    /// <summary>Computes and validates the Futures Ema Signal command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this GenerateFuturesEmaSignalCommand command, FuturesEmaSignalCommandState state)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(state);
        command.Compute(state.FuturesEmaCheckpoint, out var futuresEmaTransition);
        if (!futuresEmaTransition.IsApplied)
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        var errorMsg = "unable to apply FuturesEmaSignal event";
        var updated = futuresEmaTransition switch
        {
            _ when futuresEmaTransition.FuturesEmaSignal is null
                => command.UpdateFailed(ref errorMsg, "computed FuturesEmaSignal is missing"),
            _ => state.Update(command.CreateFuturesEmaSignalGeneratedEvent(futuresEmaTransition), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the proposed Futures Ema Signal result from the supplied business inputs without mutating actor state or pending events.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresEmaCheckpoint">The immutable accumulator checkpoint to read or record for this domain transition.</param>
    /// <param name="futuresEmaTransition">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>True when the proposed business result advances or is accepted; otherwise, false. The output retains the no-change or rejection decision.</returns>
    /// <remarks>Accumulator validation exceptions propagate to the command actor exception boundary.</remarks>
    internal static bool Compute(this GenerateFuturesEmaSignalCommand command, FuturesEmaAccumulatorCheckpoint? futuresEmaCheckpoint,
        out FuturesEmaAccumulatorResult futuresEmaTransition)
    {
        futuresEmaTransition = FuturesEmaAccumulator.Apply(futuresEmaCheckpoint, command.Observation);
        return futuresEmaTransition.IsApplied;
    }

    /// <summary>Creates the Futures Ema Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresEmaTransition">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesEmaSignalGeneratedEvent CreateFuturesEmaSignalGeneratedEvent(this GenerateFuturesEmaSignalCommand command,
        FuturesEmaAccumulatorResult futuresEmaTransition) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, FuturesEmaSignalGeneratedEvent.Actor, FuturesEmaSignalGeneratedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        FuturesEmaSignal = futuresEmaTransition.FuturesEmaSignal!,
        FuturesEmaCheckpoint = futuresEmaTransition.FuturesEmaCheckpoint,
        Observation = command.Observation
    };
}
