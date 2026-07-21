using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command;

public static class GenerateFuturesAtrDailySignal
{
    /// <summary>
    /// Handles the execution of the <see cref="GenerateFuturesAtrSignalFromItiSignalsCommand"/> by computing the new ATR signal state based on the provided ITI signals and updating the command state accordingly.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    public static ServiceResult<GuidResult> Execute(this GenerateFuturesAtrDailySignalCommand e, FuturesAtrSignalCommandState state)
    {
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
