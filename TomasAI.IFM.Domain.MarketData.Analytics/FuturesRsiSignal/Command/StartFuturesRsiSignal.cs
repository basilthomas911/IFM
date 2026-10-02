using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command;

public static class StartFuturesRsiSignal
{
    /// <summary>
    /// Handle a <see cref="StartFuturesRsiSignalCommand"/> by building the corresponding
    /// <see cref="FuturesRsiSignalStartedEvent"/> and updating the actor state.
    /// </summary>
    public static ServiceResult<GuidResult> Execute(this StartFuturesRsiSignalCommand e, FuturesRsiSignalCommandState state, IMarketSessionCalendar? calendar = null)
    {
        var seed = FuturesRsiHistoricalSeedModel.Decide(e, state.AccumulatorCheckpoint, calendar, DateTimeOffset.UtcNow);
        if (e.ForceHistoricalInitialization && seed.Observations.Length == 0)
            return e.UpdateFailed($"RSI four-hour initialization rejected: {seed.Reason}.");
        var checkpoint = e.ForceHistoricalInitialization ? null : state.AccumulatorCheckpoint;
        TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels.FuturesRsiSignalReadModel? signal = null;
        var warmSignals = new List<TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels.FuturesRsiSignalReadModel>();
        foreach (var observation in seed.Observations)
        {
            var result = FuturesRsiWilderAccumulator.Apply(checkpoint, observation, e.EntityId.PeriodLength);
            checkpoint = result.Checkpoint;
            signal = FuturesRsiWilderSignalFactory.Create(observation, e.EntityId.PeriodLength, result);
            if (signal is { IsWarm: true, Metadata.IsValid: true })
                warmSignals.Add(signal);
        }
        var started = e.CreateFuturesRsiSignalStartedEvent() with
        {
            HistoricalSeedCount = seed.Observations.Length,
            HistoricalSeedReason = seed.Reason,
            RestoredSignal = e.ForceHistoricalInitialization || state.AccumulatorCheckpoint is null
                ? null : state.FuturesRsiSignals.LastOrDefault(),
            RestoredCheckpoint = e.ForceHistoricalInitialization ? null : state.AccumulatorCheckpoint,
            ResetForHistoricalSeed = e.ForceHistoricalInitialization,
            HistoricalWarmSignals = warmSignals
                .TakeLast(FuturesTdiConfiguration.Standard.RequiredRsiSamples)
                .SkipLast(1).ToArray()
        };
        if (!state.Update(started, e))
            return e.UpdateFailed("RSI start could not be applied.");
        // Publish only the final seed result; intermediate historical values must not race newer cache observations.
        if (signal is not null && !state.Update(new FuturesRsiSignalGeneratedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesRsiSignalGeneratedEvent.Actor, FuturesRsiSignalGeneratedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            FuturesRsiSignal = signal,
            AccumulatorCheckpoint = checkpoint,
            CreatedBy = e.OriginatedBy,
            CreatedOn = e.OriginatedOn
        }, e)) throw new InvalidOperationException("A validated RSI historical checkpoint was rejected after its start event.");
        if (e.ForceHistoricalInitialization && signal is { IsWarm: true }
            && state.FuturesRsiSignals.Count >= FuturesTdiConfiguration.Standard.RequiredRsiSamples)
        {
            var window = state.FuturesRsiSignals
                .TakeLast(FuturesTdiConfiguration.Standard.RequiredRsiSamples).ToArray();
            if (!state.Update(new FuturesRsiSignalsGeneratedEvent
            {
                Subject = new ActorSubject(ActorType.Event,
                    FuturesRsiSignalsGeneratedEvent.Actor,
                    FuturesRsiSignalsGeneratedEvent.Verb, e.EntityId.Format()),
                EntityId = e.EntityId,
                FuturesRsiSignalsId = new FuturesRsiSignalsId(
                    signal.ContractId, signal.ValueDate, signal.Timestamp),
                FuturesRsiSignals = window,
                PeriodLength = e.EntityId.PeriodLength,
                CreatedBy = e.OriginatedBy,
                CreatedOn = e.OriginatedOn
            }, e))
                throw new InvalidOperationException("The initialized TDI RSI window was rejected.");
        }
        else if (e.ForceHistoricalInitialization)
            return e.UpdateFailed("RSI four-hour initialization did not provide a warm TDI window.");
        return new ServiceOk<GuidResult>(new(e.CommandId));
    }

    internal static FuturesRsiSignalStartedEvent CreateFuturesRsiSignalStartedEvent(this StartFuturesRsiSignalCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesRsiSignalStartedEvent.Actor, FuturesRsiSignalStartedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            ValueDate = e.EntityId.ValueDate,
            StartedOn = e.OriginatedOn,
            StartedBy = e.OriginatedBy
        };

}
