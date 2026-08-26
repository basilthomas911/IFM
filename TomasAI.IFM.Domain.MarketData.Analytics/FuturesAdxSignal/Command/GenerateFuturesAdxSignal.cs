using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command;

public static class GenerateFuturesAdxSignal
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    public static ServiceResult<GuidResult> Execute(this GenerateFuturesAdxSignalCommand e, FuturesAdxSignalCommandState state)
    {
        var updated = e.Compute(state.AdxSignal, state.AdxSignals, out var model) switch
        {
            _ when model.IsSignalInitializing
                => state.Update(e.CreateFuturesAdxSignalGeneratedEvent(FuturesTrendDirectionType.Init, model)),
            _ when model.IsSignalUpTrending
                => state.Update(e.CreateFuturesAdxSignalGeneratedEvent(FuturesTrendDirectionType.UpTrending, model)),
            _ when model.IsSignalDownTrending
                => state.Update(e.CreateFuturesAdxSignalGeneratedEvent(FuturesTrendDirectionType.DownTrending, model)),
            _ => state.Update(e.CreateFuturesAdxSignalGeneratedEvent(FuturesTrendDirectionType.TrendReversal, model)),
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(e.CommandId))
            : e.UpdateFailed($"{e.CommandName}: unable to apply generated ADX signal event");
    }

    /// <summary>
    /// Attempts to create a new futures ADX signal compute model based on the specified command and read model.
    /// </summary>
    /// <param name="e">The command containing the input ITI signals used to generate the ADX signal compute model.</param>
    /// <param name="adxSignal">The read model representing the current ADX signal state to use as a basis for computation.</param>
    /// <param name="computeModel">When this method returns, contains the resulting futures ADX signal compute model if the operation succeeds;
    /// otherwise, contains null.</param>
    /// <returns>true if the compute model was successfully created; otherwise, false.</returns>
    internal static bool Compute(this GenerateFuturesAdxSignalCommand e, FuturesAdxSignalReadModel? adxSignal,  IReadOnlyCollection<FuturesAdxSignalReadModel> adxSignals, out FuturesAdxSignalCompute computeModel)
        => FuturesAdxSignalCompute.Create(e.EntityId.PeriodLength, adxSignal, adxSignals, out computeModel);

    /// <summary>
    /// Creates a <see cref="FuturesAdxSignalGeneratedEvent"/> from the given command, trend direction, and computed ADX signal values.
    /// </summary>
    /// <param name="e">The command containing the input ITI signals used to generate the ADX signal.</param>
    /// <param name="trendDirection">The trend direction type for the generated event.</param>
    /// <param name="computed">The computed ADX signal values.</param>
    /// <returns>The generated futures ADX signal event.</returns>
    internal static FuturesAdxSignalGeneratedEvent CreateFuturesAdxSignalGeneratedEvent(this GenerateFuturesAdxSignalCommand e, FuturesTrendDirectionType trendDirection, FuturesAdxSignalCompute computed)
    {
        var entityId = new FuturesAdxSignalEntityId(e.FuturesAdxSignalId.ContractId, e.FuturesAdxSignalId.ValueDate, e.EntityId.TimePeriod, e.EntityId.PeriodLength);
        var signalTimestamp = e.Observation?.LastMarketEventUtc.UtcDateTime ?? DateTime.UtcNow;
        var signal = new FuturesAdxSignalReadModel(
            e.EntityId.ContractId,
            e.EntityId.ValueDate,
            e.EntityId.TimePeriod,
            e.EntityId.PeriodLength,
            TimeOnly.FromDateTime(signalTimestamp),
            e.FuturesPrice,
            computed.PlusDI,
            computed.MinusDI,
            computed.AdxValue,
            trendDirection,
            computed.TrendDirectionStrength())
        {
            Metadata = e.Observation is { } observation
                ? new MarketAnalyticsSignalMetadata
                {
                    SignalKey = new(
                        observation.MarketSeriesIdentity,
                        MarketAnalyticsSignalKind.Adx,
                        observation.TimeFrame,
                        $"adx-{e.EntityId.PeriodLength}-legacy-v1"),
                    ContractId = observation.ContractId,
                    ValueDate = observation.ValueDate,
                    ObservationId = observation.ObservationId,
                    MarketDataAsOfUtc = observation.LastMarketEventUtc,
                    CalculatedAtUtc = DateTimeOffset.UtcNow,
                    SourceSequence = observation.LastSourceSequence,
                    SchemaVersion = 1,
                    CalculationVersion = "adx-legacy-compatible-v1",
                    CalculationMethod = observation.CalculationMethod,
                    IsValid = observation.IsValid,
                    ValidationIssues = observation.ValidationIssues
                }
                : null
        };
        return new FuturesAdxSignalGeneratedEvent
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesAdxSignalGeneratedEvent.Actor, FuturesAdxSignalGeneratedEvent.Verb, entityId.Format()),
            EntityId = entityId,
            FuturesAdxSignal = signal,
            CreatedBy = e.OriginatedBy,
            CreatedOn = e.OriginatedOn
        };
    }
}
