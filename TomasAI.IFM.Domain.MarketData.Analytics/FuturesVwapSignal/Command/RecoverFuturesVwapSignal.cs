using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command;

/// <summary>Handles one private VWAP recovery batch.</summary>
public static class RecoverFuturesVwapSignal
{
    /// <summary>Computes and validates the Futures VWAP Signal command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(
        this RecoverFuturesVwapSignalCommand command,
        FuturesVwapSignalCommandState state)
    {
        command.Compute(state.FuturesVwapCheckpoint, out var futuresVwapRecoveryChange);
        if (futuresVwapRecoveryChange.Accepted && futuresVwapRecoveryChange.FuturesVwapTransition?.Changed != true)
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        var errorMsg = "unable to apply VWAP recovery event";
        var updated = futuresVwapRecoveryChange switch
        {
            _ when !futuresVwapRecoveryChange.Accepted
                => command.UpdateFailed(ref errorMsg, futuresVwapRecoveryChange.RejectionReason!),
            _ => state.Update(command.CreateFuturesVwapSignalUpdatedEvent(futuresVwapRecoveryChange.FuturesVwapTransition!), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the proposed Futures VWAP Signal result from the supplied business inputs without mutating actor state or pending events.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresVwapCheckpoint">The immutable accumulator checkpoint to read or record for this domain transition.</param>
    /// <param name="futuresVwapRecoveryChange">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>True when the proposed business result advances or is accepted; otherwise, false. The output retains the no-change or rejection decision.</returns>
    /// <remarks>Accumulator validation exceptions propagate to the command actor exception boundary.</remarks>
    internal static bool Compute(this RecoverFuturesVwapSignalCommand command, FuturesVwapCheckpoint? futuresVwapCheckpoint,
        out FuturesVwapRecoveryChange futuresVwapRecoveryChange)
    {
        if (command.IsFirstBatch && futuresVwapCheckpoint is { } existing
            && existing.RecoveryGenerationId == command.RecoveryGenerationId
            && existing.RecoveryBatchOrdinal >= command.BatchOrdinal)
        {
            futuresVwapRecoveryChange = new(null, null);
            return true;
        }
        if (!command.IsFirstBatch && (futuresVwapCheckpoint is null
            || futuresVwapCheckpoint.RecoveryGenerationId != command.RecoveryGenerationId
            || command.BatchOrdinal != futuresVwapCheckpoint.RecoveryBatchOrdinal + 1))
        {
            var isDuplicate = futuresVwapCheckpoint is not null
                && futuresVwapCheckpoint.RecoveryGenerationId == command.RecoveryGenerationId
                && command.BatchOrdinal <= futuresVwapCheckpoint.RecoveryBatchOrdinal;
            futuresVwapRecoveryChange = new(null, isDuplicate ? null : "VWAP recovery requires a matching, contiguous batch.");
            return isDuplicate;
        }
        var futuresVwapTransition = FuturesVwapAccumulator.ApplyRecovery(command.EntityId, futuresVwapCheckpoint,
            command.RecoveryGenerationId, command.BatchOrdinal, command.IsFirstBatch,
            command.IsFinalBatch, command.Trades, command.Configuration,
            command.LiveStreamEpochId, command.LiveTradeOrdinal);
        futuresVwapRecoveryChange = new(futuresVwapTransition, null);
        return true;
    }

    /// <summary>Creates the Futures VWAP Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresVwapTransition">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesVwapSignalUpdatedEvent CreateFuturesVwapSignalUpdatedEvent(
        this RecoverFuturesVwapSignalCommand command, FuturesVwapAccumulatorResult futuresVwapTransition) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, FuturesVwapSignalUpdatedEvent.Actor, FuturesVwapSignalUpdatedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        FuturesVwapCheckpoint = futuresVwapTransition.FuturesVwapCheckpoint,
        FuturesVwapSignal = futuresVwapTransition.FuturesVwapSignal
    };
}
