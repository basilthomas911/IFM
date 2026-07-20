using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command;

public static class GenerateFuturesItiSignal
{
    /// <summary>
    /// Handles a <see cref="GenerateFuturesItiSignalCommand"/> by computing the appropriate ITI signal based on the command and current state,
    /// </summary>
    /// <param name="e">The generate ITI signal command to execute.</param>
    /// <param name="state">The current actor command state.</param>
    /// <returns><see langword="true"/> if the state was successfully updated; otherwise, <see langword="false"/>.</returns>
    public static bool Execute(this GenerateFuturesItiSignalCommand e, FuturesItiSignalCommandState state)
       => e.Compute(state, out var model) switch
       {
           _ when model.IsStartOfDay
               => state.Update(e.CreateFuturesItiSignalGeneratedEvent(model.ComputeStartOfDaySignal()), e),
           _ when model.HasUpTrendDirectionChanged
               => state.Update(e.CreateFuturesItiSignalGeneratedEvent(model.ComputeUpTrendDirectionChangedSignal()), e),
           _ when model.HasUpTrendExtremeChanged
               => state.Update(e.CreateFuturesItiSignalGeneratedEvent(model.ComputeUpTrendExtremeChangedSignal()), e),
           _ when model.HasUpTrendReversalChanged
               => state.Update(e.CreateFuturesItiSignalGeneratedEvent(model.ComputeUpTrendReversalChangedSignal()), e),
           _ when model.HasDownTrendDirectionChanged
               => state.Update(e.CreateFuturesItiSignalGeneratedEvent(model.ComputeDownTrendDirectionChangedSignal()), e),
           _ when model.HasDownTrendExtremeChanged
               => state.Update(e.CreateFuturesItiSignalGeneratedEvent(model.ComputeDownTrendExtremeChangedSignal()), e),
           _ when model.HasDownTrendReversalChanged
               => state.Update(e.CreateFuturesItiSignalGeneratedEvent(model.ComputeDownTrendReversalChangedSignal()), e),
           _ => state.Update(e.CreateFuturesItiSignalGeneratedEvent(model.ComputeTrendingSignal()), e)
       };

    /// <summary>
    /// Creates a <see cref="FuturesItiSignalCompute"/> model from the command and state.
    /// </summary>
    internal static bool Compute(this GenerateFuturesItiSignalCommand e, FuturesItiSignalCommandState state, out FuturesItiSignalCompute model)
        => FuturesItiSignalCompute.Create(e, state, out model);

    /// <summary>
    /// Creates a futures ITI signal generated event from the command and computed signal.
    /// </summary>
    internal static FuturesItiSignalGeneratedEvent CreateFuturesItiSignalGeneratedEvent(this GenerateFuturesItiSignalCommand e, FuturesItiSignalV2ReadModel computed)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesItiSignalGeneratedEvent.Actor, FuturesItiSignalGeneratedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            FuturesItiSignal = computed,
            CreatedOn = e.OriginatedOn,
            CreatedBy = e.OriginatedBy
        };

}
