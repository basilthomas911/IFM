using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage.MarketDataDb;
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
        IMarketDataDbReadContext marketDataDb,
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
                && signal.IsWarm
                && signal.RSI >= 0d
                && signal.Metadata is not { IsValid: false })
            .OrderBy(static signal => signal.ValueDate)
            .ThenBy(static signal => signal.Timestamp)
            .TakeLast(configuration.RequiredRsiSamples)
            .ToArray();
        if (signals.Length < configuration.RequiredRsiSamples)
            return true;

        await state.SeedAsync(source, configuration, marketDataDb).ConfigureAwait(false);
        if (!state.TryEvaluate(source, signals, configuration, out var evaluation))
        {
            logger.LogDebug(
                "TDI window for {ContractId} was not yet calculable; retaining the prior optional value",
                source.EntityId.ContractId);
            return true;
        }
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
    readonly ConcurrentDictionary<FuturesTdiSignalEntityId, byte> _seeded = new();

    /// <summary>Seeds the prior TDI once so cross detection survives an actor or process restart.</summary>
    public async ValueTask SeedAsync(
        FuturesRsiSignalsGeneratedEvent source,
        FuturesTdiConfiguration configuration,
        IMarketDataDbReadContext marketDataDb)
    {
        var entityId = new FuturesTdiSignalEntityId(
            source.EntityId.ContractId,
            source.EntityId.ValueDate,
            source.EntityId.TimePeriod,
            configuration.ConfigurationId);
        if (!_seeded.TryAdd(entityId, 0))
            return;
        var persisted = await marketDataDb.GetLastFuturesTdiSignalAsync(
            entityId.ContractId,
            entityId.ValueDate,
            entityId.TimePeriod,
            entityId.ConfigurationId).ConfigureAwait(false);
        if (persisted is not null)
            _signals.TryAdd(entityId, persisted);
    }

    public bool TryEvaluate(
        FuturesRsiSignalsGeneratedEvent source,
        FuturesRsiSignalReadModel[] signals,
        FuturesTdiConfiguration configuration,
        out FuturesTdiSignalEvaluation evaluation)
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
        {
            evaluation = default!;
            return false;
        }
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
        evaluation = new(command.EntityId, generated.FuturesTdiSignal, generated);
        return true;
    }

    public void Confirm(FuturesTdiSignalEvaluation evaluation) =>
        _signals[evaluation.EntityId] = evaluation.Signal;
}

public sealed record FuturesTdiSignalEvaluation(
    FuturesTdiSignalEntityId EntityId,
    FuturesTdiSignalReadModel Signal,
    FuturesTdiSignalGeneratedEvent Generated);
