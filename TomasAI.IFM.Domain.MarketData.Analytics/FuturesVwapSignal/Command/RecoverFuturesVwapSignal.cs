using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command;

/// <summary>Handles one private VWAP recovery batch.</summary>
public static class RecoverFuturesVwapSignal
{
    /// <summary>Applies a contiguous recovery batch and appends an event only if it advanced.</summary>
    public static ServiceResult<GuidResult> Execute(
        this RecoverFuturesVwapSignalCommand command,
        FuturesVwapSignalCommandState state)
    {
        if (command.IsFirstBatch && state.Checkpoint is { } existing
            && existing.RecoveryGenerationId == command.RecoveryGenerationId
            && existing.RecoveryBatchOrdinal >= command.BatchOrdinal)
            return new ServiceOk<GuidResult>(new(command.CommandId));
        if (!command.IsFirstBatch && (state.Checkpoint is null
            || state.Checkpoint.RecoveryGenerationId != command.RecoveryGenerationId
            || command.BatchOrdinal != state.Checkpoint.RecoveryBatchOrdinal + 1))
        {
            if (state.Checkpoint is not null
                && state.Checkpoint.RecoveryGenerationId == command.RecoveryGenerationId
                && command.BatchOrdinal <= state.Checkpoint.RecoveryBatchOrdinal)
                return new ServiceOk<GuidResult>(new(command.CommandId));
            return command.UpdateFailed("VWAP recovery requires a matching, contiguous batch.");
        }

        var result = FuturesVwapAccumulator.ApplyRecovery(command.EntityId, state.Checkpoint,
            command.RecoveryGenerationId, command.BatchOrdinal, command.IsFirstBatch,
            command.IsFinalBatch, command.Trades, command.Configuration);
        return FuturesVwapSignalTransition.Append(command, command.EntityId, result, state);
    }
}
