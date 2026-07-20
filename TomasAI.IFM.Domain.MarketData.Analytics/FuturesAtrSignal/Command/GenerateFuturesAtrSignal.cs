using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command;

public static class GenerateFuturesAtrSignal
{
    /// <summary>
    /// Handles the execution of the GenerateFuturesAtrSignalFromIntraDayDataCommand by computing a new futures ATR signal model based on the provided intraday data and existing ATR signal information. 
    /// The method evaluates the computed model to determine the appropriate trend direction and updates the command state with a newly created FuturesAtrSignalGeneratedEvent 
    /// that encapsulates the generated signal information, including subject, entity identifier, and signal details. 
    /// The method returns true if the state was successfully updated with the new event; otherwise, it returns false.
    /// </summary>
    /// <param name="e">The command containing the futures ATR signal identifier and the intra-day price data to process.</param>
    /// <param name="state">The command state that maintains the current ATR signal and event history. Will be updated with the generated event.</param>
    /// <returns>
    /// <see langword="true"/> if the signal event was successfully generated and the state was updated;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public static bool Execute(this GenerateFuturesAtrSignalCommand e, FuturesAtrSignalCommandState state)
        => e.Compute(state.AtrSignal, state.AtrSignals, out var model) switch
        {
            _ when model.IsSignalInitializing
                => state.Update(e.CreateFuturesAtrSignalIntraDayGeneratedEvent(FuturesTrendDirectionType.Init, model)),
            _ when model.IsSignalUpTrending
                => state.Update(e.CreateFuturesAtrSignalIntraDayGeneratedEvent(FuturesTrendDirectionType.UpTrending, model)),
            _ when model.IsSignalDownTrending
                => state.Update(e.CreateFuturesAtrSignalIntraDayGeneratedEvent(FuturesTrendDirectionType.DownTrending, model)),
            _ => state.Update(e.CreateFuturesAtrSignalIntraDayGeneratedEvent(FuturesTrendDirectionType.TrendReversal, model)),
        };

    /// <summary>
    /// Attempts to compute a new futures ATR signal model based on the provided intraday data and existing ATR signal
    /// information.
    /// </summary>
    /// <param name="e">The command containing the intraday futures data used for computation. Cannot be null.</param>
    /// <param name="atrSignal">The existing ATR signal read model to use as a basis for computation. Cannot be null.</param>
    /// <param name="model">When this method returns, contains the computed futures ATR signal model if the operation succeeds; otherwise,
    /// contains null.</param>
    /// <returns>true if the computation was successful and the model was created; otherwise, false.</returns>
    internal static bool Compute(this GenerateFuturesAtrSignalCommand e, FuturesAtrSignalReadModel? atrSignal, IReadOnlyCollection<FuturesAtrSignalReadModel> atrSignals, out FuturesAtrSignalCompute model)
        => FuturesAtrSignalCompute.Create(e.EntityId.PeriodLength, atrSignal, atrSignals, out model);

    /// <summary>
    /// Creates a new FuturesAtrSignalGeneratedEvent based on the provided command, trend direction, and computed signal information.
    /// </summary>
    /// <param name="e">The command containing the details required to generate the Futures ATR signal, including contract identifier,
    /// value date, and time period. Cannot be null.</param>
    /// <param name="trendDirection">The direction of the trend to associate with the generated Futures ATR signal.</param>
    /// <param name="computed">The computed futures ATR signal information.</param>
    /// <returns>A FuturesAtrSignalGeneratedEvent that encapsulates the generated signal information, including subject, entity
    /// identifier, and signal details.</returns>
    internal static FuturesAtrSignalGeneratedEvent CreateFuturesAtrSignalIntraDayGeneratedEvent(this GenerateFuturesAtrSignalCommand e, FuturesTrendDirectionType trendDirection, FuturesAtrSignalCompute computed)
    {
        var entityId = e.FuturesAtrSignalId.ToEntityId();
        return new FuturesAtrSignalGeneratedEvent
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
