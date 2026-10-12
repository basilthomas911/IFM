using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime;

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
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(marketDataDb);
        ArgumentNullException.ThrowIfNull(source);
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
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(signals);
        ArgumentNullException.ThrowIfNull(source);
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

    public void Confirm(FuturesTdiSignalEvaluation evaluation) 
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        _signals[evaluation.EntityId] = evaluation.Signal;
    }
}
