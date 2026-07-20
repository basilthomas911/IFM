using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command;

public static class GenerateFuturesAdxDailySignal
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    public static bool Execute(this GenerateFuturesAdxDailySignalCommand e, FuturesAdxSignalCommandState state)
        => e.Compute(state.AdxSignal, state.AdxSignals, out var model) switch
        {
            _ when model.IsSignalInitializing
                => state.Update(e.CreateFuturesAdxDailySignalGeneratedEvent(FuturesTrendDirectionType.Init, model)),
            _ when model.IsSignalUpTrending
                => state.Update(e.CreateFuturesAdxDailySignalGeneratedEvent(FuturesTrendDirectionType.UpTrending, model)),
            _ when model.IsSignalDownTrending
                => state.Update(e.CreateFuturesAdxDailySignalGeneratedEvent(FuturesTrendDirectionType.DownTrending, model)),
            _ => state.Update(e.CreateFuturesAdxDailySignalGeneratedEvent(FuturesTrendDirectionType.TrendReversal, model)),
        };

    /// <summary>
    /// Attempts to create a new futures ADX signal compute model based on the specified command and read model.
    /// </summary>
    /// <param name="e">The command containing the input ITI signals used to generate the ADX signal compute model.</param>
    /// <param name="adxSignal">The read model representing the current ADX signal state to use as a basis for computation.</param>
    /// <param name="computeModel">When this method returns, contains the resulting futures ADX signal compute model if the operation succeeds;
    /// otherwise, contains null.</param>
    /// <returns>true if the compute model was successfully created; otherwise, false.</returns>
    internal static bool Compute(this GenerateFuturesAdxDailySignalCommand e, FuturesAdxSignalReadModel? adxSignal,  IReadOnlyCollection<FuturesAdxSignalReadModel> adxSignals, out FuturesAdxSignalCompute computeModel)
        => FuturesAdxSignalCompute.Create( e.EntityId.PeriodLength, adxSignal, adxSignals, out computeModel);

    /// <summary>
    /// Creates a <see cref="FuturesAdxSignalGeneratedEvent"/> from the given command, trend direction, and computed ADX signal values.
    /// </summary>
    /// <param name="e">The command containing the input ITI signals used to generate the ADX signal.</param>
    /// <param name="trendDirection">The trend direction type for the generated event.</param>
    /// <param name="computed">The computed ADX signal values.</param>
    /// <returns>The generated futures ADX signal event.</returns>
    internal static FuturesAdxDailySignalGeneratedEvent CreateFuturesAdxDailySignalGeneratedEvent(this GenerateFuturesAdxDailySignalCommand e, FuturesTrendDirectionType trendDirection, FuturesAdxSignalCompute computed)
    {
        var entityId = new FuturesAdxDailySignalEntityId(e.FuturesAdxSignalId.ContractId,  e.EntityId.TimePeriod, e.EntityId.PeriodLength);
        return new FuturesAdxDailySignalGeneratedEvent
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesAdxSignalGeneratedEvent.Actor, FuturesAdxSignalGeneratedEvent.Verb, entityId.Format()),
            EntityId = entityId,
            FuturesAdxSignal = new(e.EntityId.ContractId, e.FuturesAdxSignalId.ValueDate,e.EntityId.TimePeriod, e.EntityId.PeriodLength, TimeOnly.FromDateTime(DateTime.UtcNow), 
               e.FuturesPrice, computed.PlusDI, computed.MinusDI, computed.AdxValue, trendDirection, computed.TrendDirectionStrength()),
            CreatedBy = e.OriginatedBy,
            CreatedOn = e.OriginatedOn
        };
    }
}
