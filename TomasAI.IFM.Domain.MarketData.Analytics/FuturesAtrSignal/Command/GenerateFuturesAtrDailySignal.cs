using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
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
        if (e.Observation is { } observation)
            return ExecuteWilder(e, state, observation);

        var updated = e.Compute(state.AtrSignal, state.AtrSignals, out var model) switch
        {
            _ when model.IsSignalInitializing
                => state.Update(e.CreateFuturesAtrDailySignalGeneratedEvent(FuturesTrendDirectionType.Init, model)),
            _ when model.IsSignalUpTrending
                => state.Update(e.CreateFuturesAtrDailySignalGeneratedEvent(FuturesTrendDirectionType.UpTrending, model)),
            _ when model.IsSignalDownTrending
                => state.Update(e.CreateFuturesAtrDailySignalGeneratedEvent(FuturesTrendDirectionType.DownTrending, model)),
            _ => state.Update(e.CreateFuturesAtrDailySignalGeneratedEvent(FuturesTrendDirectionType.TrendReversal, model)),
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(e.CommandId))
            : e.UpdateFailed($"{e.CommandName}: unable to apply generated ATR signal event");
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

    /// <summary>
    /// Attempts to create a new futures ATR signal compute model based on the specified command and read model.
    /// </summary>
    /// <param name="e">The command containing the input ITI signals used to generate the ATR signal compute model.</param>
    /// <param name="atrSignal">The read model representing the current ATR signal state to use as a basis for computation.</param>
    /// <param name="computeModel">When this method returns, contains the resulting futures ATR signal compute model if the operation succeeds;
    /// otherwise, contains null.</param>
    /// <returns>true if the compute model was successfully created; otherwise, false.</returns>
    static bool Compute(this GenerateFuturesAtrDailySignalCommand e, FuturesAtrSignalReadModel atrSignal, IReadOnlyCollection<FuturesAtrSignalReadModel> atrSignals, out FuturesAtrSignalCompute computeModel)
        => FuturesAtrSignalCompute.Create(e.EntityId.PeriodLength, atrSignal, atrSignals, out computeModel);

    /// <summary>
    /// Creates a new instance of the <see cref="FuturesAtrSignalGeneratedEvent"/> using the specified command
    /// and trend direction type.
    /// </summary>
    static FuturesAtrDailySignalGeneratedEvent CreateFuturesAtrDailySignalGeneratedEvent(this GenerateFuturesAtrDailySignalCommand e, FuturesTrendDirectionType trendDirection, FuturesAtrSignalCompute computed)
    {
        var entityId = e.FuturesAtrSignalId.ToDailyEntityId();
        return new FuturesAtrDailySignalGeneratedEvent
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesAtrSignalGeneratedEvent.Actor, FuturesAtrSignalGeneratedEvent.Verb, entityId.Format()),
            EntityId = entityId,
            FuturesAtrSignal = new(e.FuturesAtrSignalId.ContractId, e.FuturesAtrSignalId.ValueDate, e.EntityId.TimePeriod, e.EntityId.PeriodLength, e.FuturesAtrSignalId.Timestamp,
                e.FuturesPrice, computed.AtrValue, computed.TrueRange, trendDirection, computed.TrendDirectionStrength()),
            CreatedBy = e.OriginatedBy,
            CreatedOn = e.OriginatedOn
        };
    }
}
