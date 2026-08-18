using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime;

/// <summary>Computes TDI from each eligible realtime RSI-13 window.</summary>
public static class FuturesRsiSignalsGenerated
{
    public static async ValueTask<bool> ExecuteRealtimeAsync(
        this FuturesRsiSignalsGeneratedEvent source,
        IRealtimeProjector<FuturesTdiSignalRealtimeActor> projector,
        FuturesTdiSignalRealtimeState state,
        ILogger logger)
    {
        var configuration = FuturesTdiConfiguration.Standard;
        if (source.PeriodLength != configuration.RsiPeriod
            || !FuturesTdiConfiguration.IsSupportedIntraday(source.EntityId.TimePeriod))
            return true;

        var signals = source.FuturesRsiSignals
            .Where(signal =>
                StringComparer.Ordinal.Equals(signal.ContractId, source.EntityId.ContractId)
                && signal.ValueDate == source.EntityId.ValueDate
                && signal.TimePeriod == source.EntityId.TimePeriod
                && signal.PeriodLength == configuration.RsiPeriod
                && signal.RSI >= 0d)
            .OrderBy(static signal => signal.ValueDate)
            .ThenBy(static signal => signal.Timestamp)
            .TakeLast(configuration.RequiredRsiSamples)
            .ToArray();
        if (signals.Length < configuration.RequiredRsiSamples)
            return true;

        var evaluation = state.Evaluate(source, signals, configuration);
        if (!await projector.ProcessRealtimeEventAsync(evaluation.Generated).ConfigureAwait(false))
            return false;
        state.Confirm(evaluation);
        logger.LogDebug("Projected realtime TDI {TimePeriod} for {ContractId}",
            source.EntityId.TimePeriod, source.EntityId.ContractId);
        return true;
    }
}

/// <summary>Actor-owned last-TDI hot state; it has no event-store or replay dependency.</summary>
public sealed class FuturesTdiSignalRealtimeState
{
    readonly ConcurrentDictionary<FuturesTdiSignalEntityId, FuturesTdiSignalReadModel> _signals = new();

    public FuturesTdiSignalEvaluation Evaluate(
        FuturesRsiSignalsGeneratedEvent source,
        FuturesRsiSignalReadModel[] signals,
        FuturesTdiConfiguration configuration)
    {
        var latest = signals[^1];
        var signalId = new FuturesTdiSignalId(
            latest.ContractId,
            latest.ValueDate,
            latest.TimePeriod,
            latest.Timestamp,
            configuration.ConfigurationId);
        var command = new GenerateFuturesTdiSignalCommand(signalId, signals, configuration)
        {
            CommandId = source.CommandId == Guid.Empty ? source.Id : source.CommandId
        };
        _signals.TryGetValue(command.EntityId, out var previous);
        if (!command.Compute(previous, out var computed) || computed is null)
            throw new InvalidOperationException("The realtime TDI window did not produce a signal.");
        var generated = command.CreateFuturesTdiSignalGeneratedEvent(computed) with
        {
            Subject = new(ActorType.Realtime, FuturesTdiSignalRealtimeActor.ActorName,
                FuturesTdiSignalGeneratedEvent.Verb, command.EntityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = command.CommandId,
            AggregateId = source.AggregateId,
            EventSource = source.EventName,
            ReceivedOn = DateTime.UtcNow
        };
        return new(command.EntityId, generated.FuturesTdiSignal, generated);
    }

    public void Confirm(FuturesTdiSignalEvaluation evaluation) =>
        _signals[evaluation.EntityId] = evaluation.Signal;
}

public sealed record FuturesTdiSignalEvaluation(
    FuturesTdiSignalEntityId EntityId,
    FuturesTdiSignalReadModel Signal,
    FuturesTdiSignalGeneratedEvent Generated);
