using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketSignals.Realtime.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command;

public static class GenerateFuturesMacdSignal
{
    /// <summary>
    /// Executes the GenerateFuturesMacdSignalCommand by computing the MACD signal based on the provided FuturesRsiSignals and the current state of the FuturesMacdSignal. 
    /// Depending on the computed signal, it updates the state with a corresponding FuturesMacdSignalGeneratedEvent indicating the trend direction (Init, UpTrending, DownTrending, or Flat).
    /// </summary>
    /// <param name="e">The command containing the input RSI signals used to generate the MACD signal compute model.</param>
    /// <param name="state">The current state of the FuturesMacdSignal.</param>
    /// <returns>true if the operation succeeds; otherwise, false.</returns>
    public static bool Execute(this GenerateFuturesMacdSignalCommand e, FuturesMacdSignalCommandState state)
        => e.Compute(state.MacdSignals, out var model) switch
        {
            _ when model.IsSignalInitializing
                => state.Update(e.CreateFuturesMacdSignalGeneratedEvent(FuturesTrendDirectionType.Init, model), e),
            _ when model.IsSignalUpTrending
                => state.Update(e.CreateFuturesMacdSignalGeneratedEvent(FuturesTrendDirectionType.UpTrending, model), e),
            _ when model.IsSignalDownTrending
                => state.Update(e.CreateFuturesMacdSignalGeneratedEvent(FuturesTrendDirectionType.DownTrending, model), e),
            _ => state.Update(e.CreateFuturesMacdSignalGeneratedEvent(FuturesTrendDirectionType.Flat, model), e),
        };

    /// <summary>
    /// Computes the MACD signal based on the provided FuturesRsiSignals and the current state of the FuturesMacdSignal.
    /// </summary>
    /// <param name="e">The command containing the input RSI signals used to generate the MACD signal compute model.</param>
    /// <param name="computeModel">When this method returns, contains the resulting futures MACD signal compute model if the operation succeeds;
    /// otherwise, contains null.</param>
    /// <returns>true if the compute model was successfully created; otherwise, false.</returns>
    internal static bool Compute(
        this GenerateFuturesMacdSignalCommand e,
        IReadOnlyCollection<FuturesMacdSignalReadModel> previousMacdSignals,
        out FuturesMacdSignalCompute computeModel)
       => FuturesMacdSignalCompute.Create(
           e.FuturesPrice,
           previousMacdSignals,
           e.EntityId.Configuration,
           out computeModel);

    /// <summary>
    /// Creates a FuturesMacdSignalGeneratedEvent based on the provided command, trend direction, and computed MACD signal.
    /// </summary>
    /// <param name="e">The command containing the input RSI signals used to generate the MACD signal compute model.</param>
    /// <param name="trendDirection">The trend direction type indicating the computed signal direction.</param>
    /// <param name="computed">The computed MACD signal values.</param>
    /// <returns>The generated event representing the futures MACD signal.</returns>
    internal static FuturesMacdSignalGeneratedEvent CreateFuturesMacdSignalGeneratedEvent(this GenerateFuturesMacdSignalCommand e, FuturesTrendDirectionType trendDirection, FuturesMacdSignalCompute computed)
    {
        var entityId = e.FuturesMacdSignalId.ToEntityId();
        var signal = new FuturesMacdSignalReadModel(
            e.FuturesMacdSignalId.ContractId,
            e.FuturesMacdSignalId.ValueDate,
            e.FuturesMacdSignalId.TimePeriod,
            e.FuturesMacdSignalId.SignalEmaPeriod,
            e.FuturesMacdSignalId.FastEmaPeriod,
            e.FuturesMacdSignalId.SlowEmaPeriod,
            e.FuturesMacdSignalId.Timestamp,
            e.FuturesPrice,
            computed.MacdLine,
            computed.SignalLine,
            computed.Histogram,
            trendDirection,
            computed.TrendDirectionStrength(),
            computed.FastEma,
            computed.SlowEma)
        {
            Metadata = e.Observation is { } observation
                ? FuturesRegimeRsiSignalState.Metadata(
                    observation,
                    MarketAnalyticsSignalKind.Macd,
                    $"macd-{e.EntityId.SignalEmaPeriod}-{e.EntityId.FastEmaPeriod}-{e.EntityId.SlowEmaPeriod}-legacy-v1",
                    "macd-legacy-compatible-v1")
                : null
        };
        return new FuturesMacdSignalGeneratedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesMacdSignalGeneratedEvent.Actor, FuturesMacdSignalGeneratedEvent.Verb, entityId.Format()),
            EntityId = entityId,
            FuturesMacdSignal = signal,
            CreatedBy = e.OriginatedBy,
            CreatedOn = e.OriginatedOn
        };
    }

}
