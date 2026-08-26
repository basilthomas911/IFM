using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketSignals.Realtime.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime;

public static class FuturesMacdSignalSampled
{
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesMacdSignalSampledRealtimeEvent sampled,
        IRealtimeProjector<FuturesMacdSignalRealtimeActor> projector,
        FuturesMacdSignalRealtimeState state,
        ILogger logger)
    {
        var evaluation = state.Evaluate(sampled);
        if (!await projector.ProcessRealtimeEventAsync(evaluation.Generated).ConfigureAwait(false))
            return false;
        state.Confirm(evaluation);
        logger.LogDebug("Projected realtime MACD {TimePeriod} for {ContractId} at {SourceSequence}",
            sampled.EntityId.TimePeriod, sampled.EntityId.ContractId, sampled.SourceSequence);
        return true;
    }
}

/// <summary>Actor-owned, bounded conventional-MACD state with no replay contract.</summary>
public sealed class FuturesMacdSignalRealtimeState
{
    const int MaxSignalHistory = 256;
    readonly ConcurrentDictionary<FuturesMacdSignalEntityId, List<FuturesMacdSignalReadModel>> _signals = new();

    public FuturesMacdSignalEvaluation Evaluate(FuturesMacdSignalSampledRealtimeEvent sampled)
    {
        var history = _signals.GetOrAdd(sampled.EntityId, static _ => []);
        var signalId = new FuturesMacdSignalId(
            sampled.EntityId.ContractId,
            sampled.EntityId.ValueDate,
            sampled.EntityId.TimePeriod,
            sampled.EntityId.SignalEmaPeriod,
            sampled.EntityId.FastEmaPeriod,
            sampled.EntityId.SlowEmaPeriod,
            TimeOnly.FromDateTime(sampled.SourceEventTimestamp));
        var command = new GenerateFuturesMacdSignalCommand(signalId, sampled.FuturesPrice)
        {
            CommandId = sampled.CommandId == Guid.Empty ? sampled.Id : sampled.CommandId
        };
        _ = command.Compute(history, out var computed);
        var direction = computed.IsSignalInitializing
            ? FuturesTrendDirectionType.Init
            : computed.IsSignalUpTrending
                ? FuturesTrendDirectionType.UpTrending
                : computed.IsSignalDownTrending
                    ? FuturesTrendDirectionType.DownTrending
                    : FuturesTrendDirectionType.Flat;
        var generated = command.CreateFuturesMacdSignalGeneratedEvent(direction, computed) with
        {
            Subject = new(ActorType.Realtime, FuturesMacdSignalRealtimeActor.ActorName,
                FuturesMacdSignalGeneratedEvent.Verb, sampled.EntityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = command.CommandId,
            AggregateId = sampled.AggregateId,
            EventSource = sampled.EventName,
            ReceivedOn = DateTime.UtcNow
        };
        var signal = generated.FuturesMacdSignal with
        {
            Metadata = sampled.Observation is { } observation
                ? FuturesRegimeRsiSignalState.Metadata(
                    observation, MarketAnalyticsSignalKind.Macd,
                    $"macd-{sampled.EntityId.FastEmaPeriod}-{sampled.EntityId.SlowEmaPeriod}-{sampled.EntityId.SignalEmaPeriod}-v1",
                    "macd-conventional-v1")
                : null
        };
        return new(sampled.EntityId, signal, generated with { FuturesMacdSignal = signal });
    }

    public void Confirm(FuturesMacdSignalEvaluation evaluation)
    {
        var history = _signals.GetOrAdd(evaluation.EntityId, static _ => []);
        history.Add(evaluation.Signal);
        if (history.Count > MaxSignalHistory)
            history.RemoveRange(0, history.Count - MaxSignalHistory);
    }
}

public sealed record FuturesMacdSignalEvaluation(
    FuturesMacdSignalEntityId EntityId,
    FuturesMacdSignalReadModel Signal,
    FuturesMacdSignalGeneratedEvent Generated);
