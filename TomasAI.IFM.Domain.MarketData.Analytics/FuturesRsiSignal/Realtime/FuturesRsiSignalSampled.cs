using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime;

/// <summary>Computes and projects a timer-selected RSI observation without event-stream replay.</summary>
public static class FuturesRsiSignalSampled
{
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesRsiSignalSampledRealtimeEvent sampled,
        IEventActorContext context,
        IRealtimeProjector<FuturesRsiSignalRealtimeActor> projector,
        FuturesRsiSignalRealtimeState state,
        IBlackboardService blackboard,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(sampled);
        var evaluation = state.Evaluate(sampled);
        if (!await projector.ProcessRealtimeEventAsync(evaluation.Generated).ConfigureAwait(false))
            return false;

        var window = state.Confirm(evaluation);
        blackboard.MarketDataAnalytics.FuturesRsiSignal.Set(sampled.EntityId, evaluation.Signal);
        if (window is not null)
        {
            await context.SendAsync<FuturesRsiSignalsGeneratedEvent, FuturesRsiSignalEntityId>(window)
                .ConfigureAwait(false);
        }

        logger.LogDebug(
            "Projected realtime RSI {TimePeriod}/{PeriodLength} for {ContractId} at source sequence {SourceSequence}",
            sampled.EntityId.TimePeriod,
            sampled.EntityId.PeriodLength,
            sampled.EntityId.ContractId,
            sampled.SourceSequence);
        return true;
    }
}

/// <summary>Actor-owned, bounded RSI calculation state; it is never replayed or checkpointed.</summary>
public sealed class FuturesRsiSignalRealtimeState
{
    const int MaxSignalHistory = 256;
    readonly ConcurrentDictionary<FuturesRsiSignalEntityId, List<FuturesRsiSignalReadModel>> _signals = new();

    public FuturesRsiSignalEvaluation Evaluate(FuturesRsiSignalSampledRealtimeEvent sampled)
    {
        ArgumentNullException.ThrowIfNull(sampled);
        var history = _signals.GetOrAdd(sampled.EntityId, static _ => []);
        var signalId = new FuturesRsiSignalId(
            sampled.EntityId.ContractId,
            sampled.EntityId.ValueDate,
            sampled.EntityId.TimePeriod,
            sampled.EntityId.PeriodLength,
            TimeOnly.FromDateTime(sampled.SourceEventTimestamp));
        var signal = history.GenerateRsiSignal(signalId, sampled.FuturesPrice) with
        {
            SourceSequence = sampled.SourceSequence,
            SourceEventTimestamp = sampled.SourceEventTimestamp
        };
        var generated = new FuturesRsiSignalGeneratedEvent
        {
            Subject = new(ActorType.Realtime, FuturesRsiSignalRealtimeActor.ActorName,
                FuturesRsiSignalGeneratedEvent.Verb, sampled.EntityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = sampled.EntityId,
            CommandId = sampled.CommandId == Guid.Empty ? sampled.Id : sampled.CommandId,
            AggregateId = sampled.AggregateId,
            EventSource = sampled.EventName,
            ReceivedOn = DateTime.UtcNow,
            FuturesRsiSignal = signal,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = sampled.UserName
        };
        return new(sampled, signal, generated);
    }

    public FuturesRsiSignalsGeneratedEvent? Confirm(FuturesRsiSignalEvaluation evaluation)
    {
        var history = _signals.GetOrAdd(evaluation.Sampled.EntityId, static _ => []);
        history.Add(evaluation.Signal);
        if (history.Count > MaxSignalHistory)
            history.RemoveRange(0, history.Count - MaxSignalHistory);

        var configuration = FuturesTdiConfiguration.Standard;
        if (evaluation.Sampled.EntityId.PeriodLength != configuration.RsiPeriod
            || !FuturesTdiConfiguration.IsSupportedIntraday(evaluation.Sampled.EntityId.TimePeriod)
            || !history.CanGenerateFuturesRsiSignals(configuration.RequiredRsiSamples))
            return null;

        var window = history.GenerateFuturesRsiSignals(configuration.RequiredRsiSamples).ToArray();
        if (window.Length < configuration.RequiredRsiSamples)
            return null;

        return new FuturesRsiSignalsGeneratedEvent
        {
            Subject = new(ActorType.Realtime, FuturesRsiSignalRealtimeActor.ActorName,
                FuturesRsiSignalsGeneratedEvent.Verb, evaluation.Sampled.EntityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = evaluation.Sampled.EntityId,
            CommandId = evaluation.Generated.CommandId,
            AggregateId = evaluation.Generated.AggregateId,
            EventSource = evaluation.Generated.EventName,
            ReceivedOn = DateTime.UtcNow,
            FuturesRsiSignalsId = new(
                evaluation.Signal.ContractId,
                evaluation.Signal.ValueDate,
                evaluation.Signal.Timestamp),
            FuturesRsiSignals = window,
            PeriodLength = evaluation.Sampled.EntityId.PeriodLength,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = evaluation.Generated.CreatedBy
        };
    }
}

public sealed record FuturesRsiSignalEvaluation(
    FuturesRsiSignalSampledRealtimeEvent Sampled,
    FuturesRsiSignalReadModel Signal,
    FuturesRsiSignalGeneratedEvent Generated);
