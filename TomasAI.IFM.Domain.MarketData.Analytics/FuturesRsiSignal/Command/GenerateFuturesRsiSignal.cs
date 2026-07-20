using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command;

public static class GenerateFuturesRsiSignal
{
    /// <summary>
    /// Executes the GenerateFuturesRsiSignalCommand by generating a new RSI signal based on the provided end-of-day data and updating the state accordingly.
    /// If the state is updated successfully, it checks if enough valid RSI signals exist to generate a collection of futures RSI signals, and if so, generates and updates the state with those signals as well.
    /// </summary>
    /// <param name="e">The command containing the details required to generate the RSI signal.</param>
    /// <param name="state">The state to update with the generated RSI signal.</param>
    /// <returns><see langword="true"/> if the state was updated successfully; otherwise, <see langword="false"/></returns>
    public static bool Execute(this GenerateFuturesRsiSignalCommand e, FuturesRsiSignalCommandState state)
    {
        var futuresRsiSignal = state.FuturesRsiSignals.GenerateRsiSignal(e.FuturesRsiSignalId, e.FuturesPrice);
        var futuresRsiSignalGeneratedEvent = e.CreateFuturesRsiSignalGeneratedEvent(futuresRsiSignal);
        if (state.Update(futuresRsiSignalGeneratedEvent, e))
        {
            if (state.FuturesRsiSignals.CanGenerateFuturesRsiSignals(e.EntityId.PeriodLength))
            {
                var futuresRsiSignals = state.FuturesRsiSignals.GenerateFuturesRsiSignals(e.EntityId.PeriodLength);
                state.Update(e.CreateFuturesRsiSignalsGeneratedEvent(futuresRsiSignal, futuresRsiSignals, e.EntityId.PeriodLength), e);
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Creates a <see cref="FuturesRsiSignalGeneratedEvent"/> based on the provided command and the generated RSI signal.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="futuresRsiSignal"></param>
    /// <param name="periodLength"></param>
    /// <returns></returns>
    internal static FuturesRsiSignalGeneratedEvent CreateFuturesRsiSignalGeneratedEvent(this GenerateFuturesRsiSignalCommand e, FuturesRsiSignalReadModel futuresRsiSignal)
       => new()
       {
           Subject = new ActorSubject(ActorType.Event, FuturesRsiSignalGeneratedEvent.Actor, FuturesRsiSignalGeneratedEvent.Verb, e.EntityId.Format()),
           EntityId = e.EntityId,
           FuturesRsiSignal = futuresRsiSignal,
           CreatedBy = e.OriginatedBy,
           CreatedOn = e.OriginatedOn
       };

   

    /// <summary>
    /// Creates a <see cref="FuturesRsiSignalsGeneratedEvent"/> based on the provided command, the latest RSI signal, and a collection of valid RSI signals.
    /// </summary>
    /// <param name="e">The command containing the details required to generate the event.</param>
    /// <param name="futuresRsiSignal">The latest RSI signal to include in the event.</param>
    /// <param name="futuresRsiSignals">A collection of valid RSI signals to associate with the event.</param>
    /// <param name="periodLength">The period length used for RSI calculation, which is included in the event.</param>
    /// <returns>A new <see cref="FuturesRsiSignalsGeneratedEvent"/> instance initialized with the provided data.</returns>
    internal static FuturesRsiSignalsGeneratedEvent CreateFuturesRsiSignalsGeneratedEvent(
        this GenerateFuturesRsiSignalCommand e, FuturesRsiSignalReadModel futuresRsiSignal, IReadOnlyCollection<FuturesRsiSignalReadModel> futuresRsiSignals, int periodLength)
       => new()
       {
           Subject = new ActorSubject(ActorType.Event, FuturesRsiSignalsGeneratedEvent.Actor, FuturesRsiSignalsGeneratedEvent.Verb, e.EntityId.Format()),
           EntityId = e.EntityId,
           FuturesRsiSignalsId = new FuturesRsiSignalsId(futuresRsiSignal.ContractId, futuresRsiSignal.ValueDate, futuresRsiSignal.Timestamp),
           FuturesRsiSignals = [.. futuresRsiSignals],
           CreatedBy = e.OriginatedBy,
           CreatedOn = e.OriginatedOn
       };

}
