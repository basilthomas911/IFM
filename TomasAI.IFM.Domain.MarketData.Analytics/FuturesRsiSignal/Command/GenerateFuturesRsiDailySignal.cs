using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command;

public static class GenerateFuturesRsiDailySignal
{
    /// <summary>
    /// Executes the GenerateFuturesRsiDailySignalCommand by generating a new RSI signal based on the provided end-of-day data and updating the state accordingly.
    /// If the state is updated successfully, it checks if enough valid RSI signals exist to generate a collection of futures RSI signals, and if so, generates and updates the state with those signals as well.
    /// </summary>
    /// <param name="e">The command containing the details required to generate the RSI signal.</param>
    /// <param name="state">The state to update with the generated RSI signal.</param>
    /// <returns><see langword="true"/> if the state was updated successfully; otherwise, <see langword="false"/></returns>
    public static bool Execute(this GenerateFuturesRsiDailySignalCommand e, FuturesRsiSignalCommandState state)
    {
        var futuresRsiSignal = state.FuturesRsiSignals.GenerateRsiSignal(e.FuturesRsiSignalId, e.FuturesPrice);
        var futuresRsiSignalGeneratedEvent = e.CreateFuturesRsiDailySignalGeneratedEvent(futuresRsiSignal);
        if (state.Update(futuresRsiSignalGeneratedEvent, e))
        {
            if (state.FuturesRsiSignals.CanGenerateFuturesRsiSignals(e.EntityId.PeriodLength))
            {
                var futuresRsiSignals = state.FuturesRsiSignals.GenerateFuturesRsiSignals(e.EntityId.PeriodLength);
                state.Update(e.CreateFuturesRsiDailySignalsGeneratedEvent(futuresRsiSignal, futuresRsiSignals, e.EntityId.PeriodLength), e);
            }
            return true;
        }
        return false;
    }

    internal static FuturesRsiDailySignalGeneratedEvent CreateFuturesRsiDailySignalGeneratedEvent(this GenerateFuturesRsiDailySignalCommand e, FuturesRsiSignalReadModel futuresRsiSignal)
          => new()
          {
              Subject = new ActorSubject(ActorType.Event, FuturesRsiDailySignalGeneratedEvent.Actor, FuturesRsiDailySignalGeneratedEvent.Verb, e.EntityId.Format()),
              EntityId = e.EntityId,
              FuturesRsiSignal = futuresRsiSignal,
              CreatedBy = e.OriginatedBy,
              CreatedOn = e.OriginatedOn
          };
  
    internal static FuturesRsiDailySignalsGeneratedEvent CreateFuturesRsiDailySignalsGeneratedEvent(
        this GenerateFuturesRsiDailySignalCommand e, FuturesRsiSignalReadModel futuresRsiSignal, IReadOnlyCollection<FuturesRsiSignalReadModel> futuresRsiSignals, int periodLength)
       => new()
       {
           Subject = new ActorSubject(ActorType.Event, FuturesRsiDailySignalsGeneratedEvent.Actor, FuturesRsiDailySignalsGeneratedEvent.Verb, e.EntityId.Format()),
           EntityId = e.EntityId,
           FuturesRsiSignalsId = new FuturesRsiSignalsId(futuresRsiSignal.ContractId, futuresRsiSignal.ValueDate, futuresRsiSignal.Timestamp),
           FuturesRsiSignals = [.. futuresRsiSignals],
           CreatedBy = e.OriginatedBy,
           CreatedOn = e.OriginatedOn
       };
}
