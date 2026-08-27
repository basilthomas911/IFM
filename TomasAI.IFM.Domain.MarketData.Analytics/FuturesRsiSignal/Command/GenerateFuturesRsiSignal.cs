using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesRsiSignal;

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
    public static ServiceResult<GuidResult> Execute(this GenerateFuturesRsiSignalCommand e, FuturesRsiSignalCommandState state)
    {
        FuturesRsiWilderResult? wilderResult = null;
        var futuresRsiSignal = e.Observation is { } observation
            ? FuturesRsiWilderSignalFactory.Create(
                observation,
                e.EntityId.PeriodLength,
                wilderResult = FuturesRsiWilderAccumulator.Apply(
                    state.AccumulatorCheckpoint,
                    observation,
                    e.EntityId.PeriodLength))
            : state.FuturesRsiSignals.GenerateRsiSignal(e.FuturesRsiSignalId, e.FuturesPrice) with
            {
                SourceSequence = e.SourceSequence,
                SourceEventTimestamp = e.SourceEventTimestamp
            };
        var futuresRsiSignalGeneratedEvent = e.CreateFuturesRsiSignalGeneratedEvent(
            futuresRsiSignal,
            wilderResult?.Checkpoint);
        if (state.Update(futuresRsiSignalGeneratedEvent, e))
        {
            var outputWindow = Math.Max(
                e.EntityId.PeriodLength,
                FuturesTdiConfiguration.Standard.RequiredRsiSamples);
            if (e.EntityId.PeriodLength == FuturesTdiConfiguration.Standard.RsiPeriod
                && state.FuturesRsiSignals.CanGenerateFuturesRsiSignals(outputWindow))
            {
                var futuresRsiSignals = state.FuturesRsiSignals.GenerateFuturesRsiSignals(outputWindow);
                state.Update(e.CreateFuturesRsiSignalsGeneratedEvent(futuresRsiSignal, futuresRsiSignals, e.EntityId.PeriodLength), e);
            }
            return new ServiceOk<GuidResult>(new GuidResult(e.CommandId));
        }
        return e.UpdateFailed($"{e.CommandName}: unable to apply generated RSI signal event");
    }

    /// <summary>
    /// Creates a <see cref="FuturesRsiSignalGeneratedEvent"/> based on the provided command and the generated RSI signal.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="futuresRsiSignal"></param>
    /// <param name="periodLength"></param>
    /// <returns></returns>
    internal static FuturesRsiSignalGeneratedEvent CreateFuturesRsiSignalGeneratedEvent(
        this GenerateFuturesRsiSignalCommand e,
        FuturesRsiSignalReadModel futuresRsiSignal,
        FuturesRsiAccumulatorCheckpoint? accumulatorCheckpoint = null)
       => new()
       {
           Subject = new ActorSubject(ActorType.Event, FuturesRsiSignalGeneratedEvent.Actor, FuturesRsiSignalGeneratedEvent.Verb, e.EntityId.Format()),
           EntityId = e.EntityId,
           FuturesRsiSignal = futuresRsiSignal,
           AccumulatorCheckpoint = accumulatorCheckpoint,
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
           PeriodLength = periodLength,
           CreatedBy = e.OriginatedBy,
           CreatedOn = e.OriginatedOn
       };

}
