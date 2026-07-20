using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command;

public static class GenerateFuturesMacdDailySignal
{
    /// <summary>
    /// Executes the GenerateFuturesMacdSignalCommand by computing the MACD signal based on the provided FuturesRsiSignals and the current state of the FuturesMacdSignal. 
    /// Depending on the computed signal, it updates the state with a corresponding FuturesMacdSignalGeneratedEvent indicating the trend direction (Init, UpTrending, DownTrending, or Flat).
    /// </summary>
    /// <param name="e">The command containing the input RSI signals used to generate the MACD signal compute model.</param>
    /// <param name="state">The current state of the FuturesMacdSignal.</param>
    /// <returns>true if the operation succeeds; otherwise, false.</returns>
    public static bool Execute(this GenerateFuturesMacdDailySignalCommand e, FuturesMacdSignalCommandState state)
        => e.Compute(e.EntityId.PeriodLength, state.MacdSignals, out var model) switch
        {
            _ when model.IsSignalInitializing
                => state.Update(e.CreateFuturesMacdDailySignalGeneratedEvent(FuturesTrendDirectionType.Init, model), e),
            _ when model.IsSignalUpTrending
                => state.Update(e.CreateFuturesMacdDailySignalGeneratedEvent(FuturesTrendDirectionType.UpTrending, model), e),
            _ when model.IsSignalDownTrending
                => state.Update(e.CreateFuturesMacdDailySignalGeneratedEvent(FuturesTrendDirectionType.DownTrending, model), e),
            _ => state.Update(e.CreateFuturesMacdDailySignalGeneratedEvent(FuturesTrendDirectionType.Flat, model), e),
        };

    /// <summary>
    /// Computes the MACD signal based on the provided FuturesRsiSignals and the current state of the FuturesMacdSignal.
    /// </summary>
    /// <param name="e">The command containing the input RSI signals used to generate the MACD signal compute model.</param>
    /// <param name="computeModel">When this method returns, contains the resulting futures MACD signal compute model if the operation succeeds;
    /// otherwise, contains null.</param>
    /// <returns>true if the compute model was successfully created; otherwise, false.</returns>
    internal static bool Compute(this GenerateFuturesMacdDailySignalCommand e, int periodLength, IReadOnlyCollection<FuturesMacdSignalReadModel> previousMacdSignals, out FuturesMacdSignalCompute computeModel)
       => FuturesMacdSignalCompute.Create(periodLength, previousMacdSignals, out computeModel);

    /// <summary>
    /// Creates a FuturesMacdSignalGeneratedEvent based on the provided command, trend direction, and computed MACD signal.
    /// </summary>
    /// <param name="e">The command containing the input RSI signals used to generate the MACD signal compute model.</param>
    /// <param name="trendDirection">The trend direction type indicating the computed signal direction.</param>
    /// <param name="computed">The computed MACD signal values.</param>
    /// <returns>The generated event representing the futures MACD signal.</returns>
    internal static FuturesMacdDailySignalGeneratedEvent CreateFuturesMacdDailySignalGeneratedEvent(this GenerateFuturesMacdDailySignalCommand e, FuturesTrendDirectionType trendDirection, FuturesMacdSignalCompute computed)
    {
        var entityId = e.FuturesMacdSignalId.ToDailyEntityId();
        return new FuturesMacdDailySignalGeneratedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesMacdDailySignalGeneratedEvent.Actor, FuturesMacdDailySignalGeneratedEvent.Verb, entityId.Format()),
            EntityId = entityId,
            FuturesMacdSignal = new(e.FuturesMacdSignalId.ContractId, e.FuturesMacdSignalId.ValueDate, e.FuturesMacdSignalId.TimePeriod, e.FuturesMacdSignalId.PeriodLength, e.FuturesMacdSignalId.Timestamp,
                e.FuturesPrice,computed.MacdLine, computed.SignalLine, computed.Histogram, trendDirection, computed.TrendDirectionStrength()),
            CreatedBy = e.OriginatedBy,
            CreatedOn = e.OriginatedOn
        };
    }

}
