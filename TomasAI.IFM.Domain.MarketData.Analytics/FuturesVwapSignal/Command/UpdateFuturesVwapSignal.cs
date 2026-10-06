using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command;

/// <summary>Handles a live trade or a complete tick-source checkpoint for VWAP.</summary>
public static class UpdateFuturesVwapSignal
{
    /// <summary>Computes and validates the Futures VWAP Signal command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(
        this UpdateFuturesVwapSignalCommand command,
        FuturesVwapSignalCommandState state)
    {
        command.Compute(state.FuturesVwapCheckpoint, out var futuresVwapTransition);
        if (!futuresVwapTransition.Changed)
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        var updated = state.Update(command.CreateFuturesVwapSignalUpdatedEvent(futuresVwapTransition), command);
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: unable to apply VWAP checkpoint transition");
    }

    /// <summary>Computes the proposed Futures VWAP Signal result from the supplied business inputs without mutating actor state or pending events.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresVwapCheckpoint">The immutable accumulator checkpoint to read or record for this domain transition.</param>
    /// <param name="futuresVwapTransition">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>True when the proposed business result advances or is accepted; otherwise, false. The output retains the no-change or rejection decision.</returns>
    /// <remarks>Accumulator validation exceptions propagate to the command actor exception boundary.</remarks>
    internal static bool Compute(this UpdateFuturesVwapSignalCommand command, FuturesVwapCheckpoint? futuresVwapCheckpoint,
        out FuturesVwapAccumulatorResult futuresVwapTransition)
    {
        futuresVwapTransition = command.SourceCheckpoint is { } source
            ? FuturesVwapAccumulator.ApplySourceCheckpoint(command.EntityId, futuresVwapCheckpoint,
                source, command.Configuration, command.SessionStartUtc, command.SessionEndUtc)
            : FuturesVwapAccumulator.ApplyLive(command.EntityId, futuresVwapCheckpoint,
                command.Observation, command.Configuration);
        return futuresVwapTransition.Changed;
    }

    /// <summary>Creates the Futures VWAP Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresVwapTransition">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesVwapSignalUpdatedEvent CreateFuturesVwapSignalUpdatedEvent(
        this UpdateFuturesVwapSignalCommand command, FuturesVwapAccumulatorResult futuresVwapTransition) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, FuturesVwapSignalUpdatedEvent.Actor, FuturesVwapSignalUpdatedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        FuturesVwapCheckpoint = futuresVwapTransition.FuturesVwapCheckpoint,
        FuturesVwapSignal = futuresVwapTransition.FuturesVwapSignal
    };
}
