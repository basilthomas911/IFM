using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command;

public static class GenerateFuturesTdiSignal
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    public static bool Execute(this GenerateFuturesTdiSignalCommand e, FuturesTdiSignalCommandState state)
          => e.Compute(state.TdiSignal, out var model) switch
          {
              _ when model.IsSignalInitializing
                    => state.Update(e.CreateFuturesTdiSignalGeneratedEvent(FuturesTrendDirectionType.Init, model), e),
              _ when model.IsSignalUpTrending
                    => state.Update(e.CreateFuturesTdiSignalGeneratedEvent(FuturesTrendDirectionType.UpTrending, model), e),
              _ when model.IsSignalDownTrending
                    => state.Update(e.CreateFuturesTdiSignalGeneratedEvent(FuturesTrendDirectionType.DownTrending, model), e),
              _ when model.IsSignalTrendReversal
                    => state.Update(e.CreateFuturesTdiSignalGeneratedEvent(FuturesTrendDirectionType.TrendReversal, model), e),
              _ => state.Update(e.CreateFuturesTdiSignalGeneratedEvent(FuturesTrendDirectionType.Flat, model), e),
          };

    /// <summary>
    /// Attempts to create a new futures TDI signal compute model based on the specified command and read model.
    /// </summary>
    /// <param name="e">The command containing the input RSI signals used to generate the TDI signal compute model.</param>
    /// <param name="tdiSignal">The read model representing the current TDI signal state to use as a basis for computation.</param>
    /// <param name="computeModel">When this method returns, contains the resulting futures TDI signal compute model if the operation succeeds;
    /// otherwise, contains null.</param>
    /// <returns>true if the compute model was successfully created; otherwise, false.</returns>
    internal static bool Compute(this GenerateFuturesTdiSignalCommand e, FuturesTdiSignalReadModel? tdiSignal, out FuturesTdiSignalCompute computeModel)
        => FuturesTdiSignalCompute.Create(e.FuturesRsiSignals, tdiSignal, out computeModel);

    /// <summary>
    /// Creates a new instance of the <see cref="FuturesTdiSignalGeneratedEvent"/> using the specified command
    /// and trend direction type.
    /// </summary>
    internal static FuturesTdiSignalGeneratedEvent CreateFuturesTdiSignalGeneratedEvent(this GenerateFuturesTdiSignalCommand e, FuturesTrendDirectionType trendDirection, FuturesTdiSignalCompute computed)
    {
        var entityId = new FuturesTdiSignalEntityId(e.FuturesTdiSignalId.ContractId, e.FuturesTdiSignalId.ValueDate, e.EntityId.TimePeriod);
        return new FuturesTdiSignalGeneratedEvent
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesTdiSignalGeneratedEvent.Actor, FuturesTdiSignalGeneratedEvent.Verb, entityId.Format()),
            EntityId = entityId,
            FuturesTdiSignal = new(e.FuturesTdiSignalId.ContractId, e.FuturesTdiSignalId.ValueDate, e.EntityId.TimePeriod, e.FuturesTdiSignalId.Timestamp,
                computed.UpTrendCount, computed.DownTrendCount, trendDirection, computed.TrendDirectionStrength()),
            CreatedBy = e.OriginatedBy,
            CreatedOn = e.OriginatedOn
        };
    }
}
