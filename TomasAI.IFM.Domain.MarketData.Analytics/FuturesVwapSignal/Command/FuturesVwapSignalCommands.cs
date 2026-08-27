using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command;

/// <summary>Handles live and private-recovery event-sourced VWAP commands.</summary>
public static class FuturesVwapSignalCommands
{
    /// <summary>Applies one live futures trade.</summary>
    public static ServiceResult<GuidResult> Execute(
        this UpdateFuturesVwapSignalCommand command,
        FuturesVwapSignalCommandState state) => Append(command,
            FuturesVwapAccumulator.ApplyLive(command.EntityId, state.Checkpoint,
                command.Observation, command.Configuration), state);

    /// <summary>Applies one bounded private exact-recovery batch.</summary>
    public static ServiceResult<GuidResult> Execute(
        this RecoverFuturesVwapSignalCommand command,
        FuturesVwapSignalCommandState state) => Append(command,
            FuturesVwapAccumulator.ApplyRecovery(command.EntityId, state.Checkpoint,
                command.RecoveryGenerationId, command.BatchOrdinal, command.IsFirstBatch,
                command.IsFinalBatch, command.Trades, command.Configuration), state);

    static ServiceResult<GuidResult> Append(
        ICommand command,
        FuturesVwapAccumulatorResult result,
        FuturesVwapSignalCommandState state)
    {
        if (!result.Changed) return new ServiceOk<GuidResult>(new(command.CommandId));
        var entityId = command is UpdateFuturesVwapSignalCommand update
            ? update.EntityId : ((RecoverFuturesVwapSignalCommand)command).EntityId;
        return state.Update(new FuturesVwapSignalUpdatedEvent
        {
            Subject = new(ActorType.Event, FuturesVwapSignalUpdatedEvent.Actor,
                FuturesVwapSignalUpdatedEvent.Verb, entityId.Format()),
            EntityId = entityId,
            Checkpoint = result.Checkpoint,
            Signal = result.Signal
        }, command)
            ? new ServiceOk<GuidResult>(new(command.CommandId))
            : new ServiceFailed<GuidResult>(FuturesVwapSignalUpdatedEvent.ErrorCode,
                "VWAP command state rejected the transition.");
    }
}
