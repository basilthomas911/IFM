using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Realtime;

public static class FuturesAtrSignalSampled
{
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesAtrSignalSampledRealtimeEvent sampled,
        IRealtimeProjector<FuturesAtrSignalRealtimeActor> projector,
        FuturesAtrSignalRealtimeState state,
        ILogger logger)
    {
        var evaluation = state.Evaluate(sampled);
        if (!await projector.ProcessRealtimeEventAsync(evaluation.Generated).ConfigureAwait(false))
            return false;
        state.Confirm(evaluation);
        logger.LogDebug("Projected realtime ATR {TimePeriod} for {ContractId} at {SourceSequence}",
            sampled.EntityId.TimePeriod, sampled.EntityId.ContractId, sampled.SourceSequence);
        return true;
    }
}

/// <summary>Actor-owned, bounded ATR calculation state with no replay contract.</summary>
public sealed class FuturesAtrSignalRealtimeState
{
    const int MaxSignalHistory = 256;
    readonly ConcurrentDictionary<FuturesAtrSignalEntityId, List<FuturesAtrSignalReadModel>> _signals = new();

    public FuturesAtrSignalEvaluation Evaluate(FuturesAtrSignalSampledRealtimeEvent sampled)
    {
        var history = _signals.GetOrAdd(sampled.EntityId, static _ => []);
        var previous = history.LastOrDefault();
        _ = FuturesAtrSignalCompute.Create(sampled.EntityId.PeriodLength, previous, history, out var computed);
        var direction = computed.IsSignalInitializing
            ? FuturesTrendDirectionType.Init
            : computed.IsSignalUpTrending
                ? FuturesTrendDirectionType.UpTrending
                : computed.IsSignalDownTrending
                    ? FuturesTrendDirectionType.DownTrending
                    : FuturesTrendDirectionType.TrendReversal;
        var signal = new FuturesAtrSignalReadModel(
            sampled.EntityId.ContractId,
            sampled.EntityId.ValueDate,
            sampled.EntityId.TimePeriod,
            sampled.EntityId.PeriodLength,
            TimeOnly.FromDateTime(sampled.SourceEventTimestamp),
            sampled.FuturesPrice,
            computed.AtrValue,
            computed.TrueRange,
            direction,
            computed.TrendDirectionStrength());
        var generated = new FuturesAtrSignalGeneratedEvent
        {
            Subject = new(ActorType.Realtime, FuturesAtrSignalRealtimeActor.ActorName,
                FuturesAtrSignalGeneratedEvent.Verb, sampled.EntityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = sampled.EntityId,
            CommandId = sampled.CommandId == Guid.Empty ? sampled.Id : sampled.CommandId,
            AggregateId = sampled.AggregateId,
            EventSource = sampled.EventName,
            ReceivedOn = DateTime.UtcNow,
            FuturesAtrSignal = signal,
            AtrSignalSource = FuturesAtrSignalSourceType.FuturesIntraDayData,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = sampled.UserName
        };
        return new(sampled.EntityId, signal, generated);
    }

    public void Confirm(FuturesAtrSignalEvaluation evaluation)
    {
        var history = _signals.GetOrAdd(evaluation.EntityId, static _ => []);
        history.Add(evaluation.Signal);
        if (history.Count > MaxSignalHistory)
            history.RemoveRange(0, history.Count - MaxSignalHistory);
    }
}

public sealed record FuturesAtrSignalEvaluation(
    FuturesAtrSignalEntityId EntityId,
    FuturesAtrSignalReadModel Signal,
    FuturesAtrSignalGeneratedEvent Generated);
