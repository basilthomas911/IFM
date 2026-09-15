using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command;

/// <summary>Handles intraday Futures ATR commands and records replayable Wilder state.</summary>
public static class GenerateFuturesAtrSignal
{
    /// <summary>
    /// Applies a completed intraday trade-session bar to the persisted Wilder accumulator checkpoint and records the
    /// resulting ATR signal when the observation advances the stream.
    /// </summary>
    /// <param name="e">The command containing the ATR identity and completed trade-session bar.</param>
    /// <param name="state">The command state containing the latest persisted Wilder accumulator checkpoint.</param>
    /// <returns>
    /// <see langword="true"/> if the signal event was successfully generated and the state was updated;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public static ServiceResult<GuidResult> Execute(this GenerateFuturesAtrSignalCommand e, FuturesAtrSignalCommandState state)
    {
        if (e.Observation is not { } observation)
            return e.UpdateFailed($"{e.CommandName}: a completed trade-session bar observation is required");
        if (!observation.IsComplete || !observation.IsValid)
            return e.UpdateFailed($"{e.CommandName}: source bar must be valid and completed");

        return ExecuteWilder(e, state, observation);
    }

    static ServiceResult<GuidResult> ExecuteWilder(
        GenerateFuturesAtrSignalCommand command,
        FuturesAtrSignalCommandState state,
        FuturesTradeSessionBarReadModel observation)
    {
        if (!FuturesIntradaySignalActivationProfile.TimeFrames.Contains(command.EntityId.TimePeriod)
            || observation.TimeFrame != command.EntityId.TimePeriod
            || !string.Equals(observation.ContractId, command.EntityId.ContractId, StringComparison.Ordinal)
            || observation.ValueDate != command.EntityId.ValueDate)
            return command.UpdateFailed("The closed observation does not match the intraday ATR identity.");
        if (!FuturesAtrWilderAccumulator.TryApply(
                observation,
                command.EntityId.PeriodLength,
                state.CalculationState,
                out var result))
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));

        var signal = FuturesAtrWilderSignalFactory.Create(command.FuturesAtrSignalId, observation, result);
        var entityId = command.FuturesAtrSignalId.ToEntityId();
        var updated = state.Update(new FuturesAtrSignalGeneratedEvent
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesAtrSignalGeneratedEvent.Actor,
                FuturesAtrSignalGeneratedEvent.Verb,
                entityId.Format()),
            EntityId = entityId,
            FuturesAtrSignal = signal,
            CalculationState = result.Checkpoint,
            CreatedBy = command.OriginatedBy,
            CreatedOn = command.OriginatedOn
        }, command);
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: unable to apply generated Wilder ATR event");
    }

}
