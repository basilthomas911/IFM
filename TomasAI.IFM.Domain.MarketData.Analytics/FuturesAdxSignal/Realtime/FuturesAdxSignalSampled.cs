using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketSignals.Realtime.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime;

public static class FuturesAdxSignalSampled
{
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesAdxSignalSampledRealtimeEvent sampled,
        IRealtimeProjector<FuturesAdxSignalRealtimeActor> projector,
        FuturesAdxSignalRealtimeState state,
        ILogger logger)
    {
        var evaluation = state.Evaluate(sampled);
        if (!await projector.ProcessRealtimeEventAsync(evaluation.Generated).ConfigureAwait(false))
            return false;
        state.Confirm(evaluation);
        logger.LogDebug("Projected realtime ADX {TimePeriod} for {ContractId} at {SourceSequence}",
            sampled.EntityId.TimePeriod, sampled.EntityId.ContractId, sampled.SourceSequence);
        return true;
    }
}

/// <summary>Actor-owned, bounded ADX calculation state with no replay contract.</summary>
public sealed class FuturesAdxSignalRealtimeState
{
    const int MaxSignalHistory = 256;
    readonly ConcurrentDictionary<FuturesAdxSignalEntityId, List<FuturesAdxSignalReadModel>> _signals = new();

    public FuturesAdxSignalEvaluation Evaluate(FuturesAdxSignalSampledRealtimeEvent sampled)
    {
        var history = _signals.GetOrAdd(sampled.EntityId, static _ => []);
        var previous = history.LastOrDefault();
        _ = FuturesAdxSignalCompute.Create(sampled.EntityId.PeriodLength, previous, history, out var computed);
        var direction = computed.IsSignalInitializing
            ? FuturesTrendDirectionType.Init
            : computed.IsSignalUpTrending
                ? FuturesTrendDirectionType.UpTrending
                : computed.IsSignalDownTrending
                    ? FuturesTrendDirectionType.DownTrending
                    : FuturesTrendDirectionType.TrendReversal;
        var signal = new FuturesAdxSignalReadModel(
            sampled.EntityId.ContractId,
            sampled.EntityId.ValueDate,
            sampled.EntityId.TimePeriod,
            sampled.EntityId.PeriodLength,
            TimeOnly.FromDateTime(sampled.SourceEventTimestamp),
            sampled.FuturesPrice,
            computed.PlusDI,
            computed.MinusDI,
            computed.AdxValue,
            direction,
            computed.TrendDirectionStrength()) with
        {
            Metadata = sampled.Observation is { } observation
                ? FuturesRegimeRsiSignalState.Metadata(
                    observation, MarketAnalyticsSignalKind.Adx,
                    $"adx-{sampled.EntityId.PeriodLength}-legacy-v1", "adx-legacy-compatible-v1")
                : null
        };
        var generated = new FuturesAdxSignalGeneratedEvent
        {
            Subject = new(ActorType.Realtime, FuturesAdxSignalRealtimeActor.ActorName,
                FuturesAdxSignalGeneratedEvent.Verb, sampled.EntityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = sampled.EntityId,
            CommandId = sampled.CommandId == Guid.Empty ? sampled.Id : sampled.CommandId,
            AggregateId = sampled.AggregateId,
            EventSource = sampled.EventName,
            ReceivedOn = DateTime.UtcNow,
            FuturesAdxSignal = signal,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = sampled.UserName
        };
        return new(sampled.EntityId, signal, generated);
    }

    public void Confirm(FuturesAdxSignalEvaluation evaluation)
    {
        var history = _signals.GetOrAdd(evaluation.EntityId, static _ => []);
        history.Add(evaluation.Signal);
        if (history.Count > MaxSignalHistory)
            history.RemoveRange(0, history.Count - MaxSignalHistory);
    }
}

public sealed record FuturesAdxSignalEvaluation(
    FuturesAdxSignalEntityId EntityId,
    FuturesAdxSignalReadModel Signal,
    FuturesAdxSignalGeneratedEvent Generated);
