using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command;

/// <summary>Handles cross-value-date Daily, Weekly, and Monthly Futures ATR command streams.</summary>
public static class GenerateFuturesAtrDailySignal
{
    /// <summary>
    /// Applies a completed daily observation to the day-based Wilder checkpoint and records the generated signal.
    /// </summary>
    /// <param name="e">The day-based ATR command.</param>
    /// <param name="state">The event-sourced ATR command state.</param>
    /// <returns>The command result containing the accepted command identity.</returns>
    public static ServiceResult<GuidResult> Execute(this GenerateFuturesAtrDailySignalCommand e, FuturesAtrSignalCommandState state)
    {
        if (e.Observation is not { } observation)
            return e.UpdateFailed($"{e.CommandName}: a completed daily trade-session bar observation is required");

        return ExecuteWilder(e, state, observation);
    }

    static ServiceResult<GuidResult> ExecuteWilder(
        GenerateFuturesAtrDailySignalCommand command,
        FuturesAtrSignalCommandState state,
        FuturesTradeSessionBarReadModel observation)
    {
        if (!FuturesAtrDailySignalActivationProfile.IsSupported(command.EntityId.TimePeriod)
            || observation.TimeFrame != TimeFrameType.Daily
            || !string.Equals(observation.ContractId, command.EntityId.ContractId, StringComparison.Ordinal)
            || observation.ValueDate != command.FuturesAtrSignalId.ValueDate)
            throw new ArgumentException("The daily observation does not match the day-based ATR identity.");
        if (!FuturesAtrWilderAccumulator.TryApply(
                observation,
                command.EntityId.PeriodLength,
                state.CalculationState,
                out var result))
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));

        var signal = FuturesAtrWilderSignalFactory.Create(command.FuturesAtrSignalId, observation, result);
        var entityId = command.FuturesAtrSignalId.ToDailyEntityId();
        var updated = state.Update(new FuturesAtrDailySignalGeneratedEvent
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesAtrDailySignalGeneratedEvent.Actor,
                FuturesAtrDailySignalGeneratedEvent.Verb,
                entityId.Format()),
            EntityId = entityId,
            FuturesAtrSignal = signal,
            CalculationState = result.Checkpoint,
            CreatedBy = command.OriginatedBy,
            CreatedOn = command.OriginatedOn
        }, command);
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: unable to apply generated daily Wilder ATR event");
    }

}
