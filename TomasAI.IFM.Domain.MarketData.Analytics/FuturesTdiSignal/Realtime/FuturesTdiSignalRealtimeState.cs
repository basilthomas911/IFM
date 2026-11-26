using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using MessagePack;
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

/// <summary>Actor-owned TDI hot state with immutable result receipts and ordered publication recovery.</summary>
public sealed class FuturesTdiSignalRealtimeState(FuturesTdiResultReceipts? receipts = null, TimeProvider? clock = null)
{
    public FuturesTdiResultReceipts Receipts { get; } = receipts ?? new();
    readonly TimeProvider clock = clock ?? TimeProvider.System;
    readonly ConcurrentDictionary<string, SemaphoreSlim> processing = new(StringComparer.Ordinal);

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
        try
        {
            if (_signals.ContainsKey(entityId)) return;
            var persisted = await marketDataDb.GetLastFuturesTdiSignalAsync(
                entityId.ContractId, entityId.ValueDate, entityId.TimePeriod, entityId.ConfigurationId).ConfigureAwait(false);
            if (persisted is not null) _signals.TryAdd(entityId, persisted);
        }
        catch { _seeded.TryRemove(entityId, out _); throw; }

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
        var (identity, fingerprint) = WindowIdentity(signals, configuration);
        if (Receipts.Find(identity) is { } recorded)
        {
            if (recorded.Fingerprint != fingerprint) throw new InvalidDataException("A TDI window was replayed with conflicting inputs.");
            evaluation = recorded.Evaluation; return true;
        }
        var signalId = new FuturesTdiSignalId(
            latest.ContractId,
            latest.ValueDate,
            latest.TimePeriod,
            latest.Timestamp,
            configuration.ConfigurationId);
        var command = new GenerateFuturesTdiSignalCommand(signalId, signals, configuration)
        {
            CommandId = source.CommandId != Guid.Empty ? source.CommandId : source.Id != Guid.Empty ? source.Id : identity
        };
        _signals.TryGetValue(command.EntityId, out var previous);
        if (previous is not null && SampleTime(previous) >= FuturesTdiSignalCompute.SampleTime(latest))
            throw new InvalidDataException("TDI.LEGACY_RECEIPT_GAP;An already-applied window has no original result/event receipt. Replay cannot invent its identity.");
        if (!command.Compute(previous, out var computed) || computed is null)
        {
            evaluation = default!;
            return false;
        }
        var generated = command.CreateFuturesTdiSignalGeneratedEvent(computed) with
        {
            Subject = new(ActorType.Realtime, FuturesTdiSignalRealtimeActor.ActorName,
                FuturesTdiSignalGeneratedEvent.Verb, command.EntityId.Format()),
            Id = identity,
            CommandId = command.CommandId,
            AggregateId = source.AggregateId,
            EventSource = source.EventName,
            ReceivedOn = clock.GetUtcNow().UtcDateTime
        };
        MessageOrigins.Capture(generated, clock, causationId: source.Id == Guid.Empty ? null : source.Id);
        var completed = new FuturesTdiSignalGeneratedCompleteEvent
        {
            Id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("tdi-complete-v1:" + identity.ToString("N"))).AsSpan(0, 16)),
            Subject = new(ActorType.Realtime, FuturesTdiSignalRealtimeActor.ActorName, FuturesTdiSignalGeneratedCompleteEvent.Verb, command.EntityId.Format()),
            EntityId = generated.EntityId, CommandId = generated.CommandId, AggregateId = generated.AggregateId,
            EventSource = generated.EventSource, ReceivedOn = generated.ReceivedOn, CreatedOn = generated.CreatedOn,
            CreatedBy = generated.CreatedBy, FuturesTdiSignal = generated.FuturesTdiSignal
        };
        MessageOrigins.Capture(completed, clock, causationId: generated.Id);
        evaluation = Receipts.Prepare(new(fingerprint, generated, completed, TdiReceiptStage.Prepared)).Evaluation;
        return true;
    }

    public void Confirm(FuturesTdiSignalEvaluation evaluation) 
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        Receipts.Advance(evaluation.Generated.Id, TdiReceiptStage.Completed);
        _signals.AddOrUpdate(evaluation.EntityId, evaluation.Signal, (_, previous) =>
            SampleTime(previous) > SampleTime(evaluation.Signal) ? previous : evaluation.Signal);
    }
    static DateTime SampleTime(FuturesTdiSignalReadModel signal) => signal.SourceEventTimestamp == default
        ? signal.ValueDate.ToDateTime(signal.Timestamp) : signal.SourceEventTimestamp;
    static string Partition(string contract, TimeFrameType period) => contract + ":" + period;

    public async ValueTask<bool> ProcessAsync(FuturesRsiSignalsGeneratedEvent source, FuturesRsiSignalReadModel[] signals,
        FuturesTdiConfiguration configuration, IMarketDataDbReadContext db, IRealtimeProjector<FuturesTdiSignalRealtimeActor> projector)
    {
        var gate = processing.GetOrAdd(Partition(source.EntityId.ContractId, source.EntityId.TimePeriod), static _ => new(1, 1));
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!await RecoverPartitionAsync(projector, source.EntityId.ContractId, source.EntityId.TimePeriod, default).ConfigureAwait(false)) return false;
            await SeedAsync(source, configuration, db).ConfigureAwait(false);
            if (!TryEvaluate(source, signals, configuration, out var evaluation)) return true;
            var receipt = Receipts.Find(evaluation.Generated.Id)!;
            if (receipt.Stage != TdiReceiptStage.Completed && !await projector.ProcessRealtimeEventAsync(evaluation.Generated).ConfigureAwait(false)) return false;
            Confirm(evaluation); return true;
        }
        finally { gate.Release(); }
    }

    public async ValueTask<bool> RecoverAsync(IRealtimeProjector<FuturesTdiSignalRealtimeActor> projector, CancellationToken token = default)
    {
        foreach (var group in Receipts.Pending().GroupBy(receipt => (receipt.Generated.EntityId.ContractId, receipt.Generated.EntityId.TimePeriod)))
        {
            var gate = processing.GetOrAdd(Partition(group.Key.ContractId, group.Key.TimePeriod), static _ => new(1, 1));
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (!await RecoverPartitionAsync(projector, group.Key.ContractId, group.Key.TimePeriod, token).ConfigureAwait(false)) return false;
            }
            finally { gate.Release(); }
        }
        return true;
    }
    async ValueTask<bool> RecoverPartitionAsync(IRealtimeProjector<FuturesTdiSignalRealtimeActor> projector, string contract, TimeFrameType period, CancellationToken token)
    {
        foreach (var receipt in Receipts.Pending().Where(receipt => receipt.Generated.EntityId.ContractId == contract && receipt.Generated.EntityId.TimePeriod == period))
        {
            token.ThrowIfCancellationRequested();
            if (receipt.Stage != TdiReceiptStage.Completed && !await projector.ProcessRealtimeEventAsync(receipt.Generated, token).ConfigureAwait(false)) return false;
            Confirm(receipt.Evaluation);
        }
        return true;
    }

    static (Guid Identity, string Fingerprint) WindowIdentity(FuturesRsiSignalReadModel[] signals, FuturesTdiConfiguration configuration)
    {
        var latest = signals[^1];
        using var bytes = new MemoryStream(4096);
        using var writer = new BinaryWriter(bytes, Encoding.UTF8, true);
        writer.Write("tdi-window-v1"); writer.Write(configuration.ConfigurationId); writer.Write(FuturesTdiConfiguration.CurrentSchemaVersion);
        writer.Write(latest.ContractId); writer.Write(latest.ValueDate.DayNumber); writer.Write((int)latest.TimePeriod);
        writer.Write(StorageTicks(latest.Timestamp.Ticks)); writer.Write(latest.SourceSequence); writer.Write(StorageTicks(latest.SourceEventTimestamp.Ticks));
        writer.Flush();
        var identity = new Guid(SHA256.HashData(bytes.GetBuffer().AsSpan(0, (int)bytes.Length)).AsSpan(0, 16));
        var config = MessagePackSerializer.Serialize(configuration); writer.Write(config.Length); writer.Write(config);
        foreach (var signal in signals)
        {
            writer.Write(signal.ContractId); writer.Write(signal.ValueDate.DayNumber); writer.Write((int)signal.TimePeriod);
            writer.Write(StorageTicks(signal.Timestamp.Ticks)); writer.Write(signal.RSI == 0d ? 0d : signal.RSI);
            writer.Write(signal.Price.ToString("G29", System.Globalization.CultureInfo.InvariantCulture));
            writer.Write(signal.SourceSequence); writer.Write(StorageTicks(signal.SourceEventTimestamp.Ticks));
        }
        writer.Flush();
        return (identity, Convert.ToHexString(SHA256.HashData(bytes.GetBuffer().AsSpan(0, (int)bytes.Length))));
    }
    // Normalize comparison to PostgreSQL microseconds; receipt payload timestamps retain full precision.
    static long StorageTicks(long ticks) => ticks / 10 * 10;

}
