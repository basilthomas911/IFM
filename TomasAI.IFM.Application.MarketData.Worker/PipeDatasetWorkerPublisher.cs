using MessagePack;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

[assembly: InternalsVisibleTo("TomasAI.IFM.Application.MarketData.UnitTests")]

namespace TomasAI.IFM.Application.MarketData.Worker;

internal sealed class PipeDatasetWorkerPublisher(
    Stream stream,
    string dataset,
    DateOnly valueDate,
    Guid workerInstanceId,
    long manifestRevision, Stream? retentionAcknowledgments = null) : ITickAggregationEventPublisher, MarketData.Pricing.IOptionTradeEvidenceWriter
{
    readonly SemaphoreSlim writer = new(1, 1);
    Guid generation;
    long sequence;
    int running;
    int closed;
    int disposed;

    public bool IsRunning => Volatile.Read(ref running) != 0;
    public async ValueTask WriteAsync(MarketData.Pricing.OptionTradeEvidence evidence, CancellationToken cancellationToken)
    {
        evidence.Validate();
        if (retentionAcknowledgments is null) throw new InvalidOperationException("No durable retention acknowledgment pipe.");
        await writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsRunning || Volatile.Read(ref closed) != 0 || generation == Guid.Empty
                || evidence.Source.GenerationId != generation || evidence.Source.Dataset != dataset || evidence.Source.ValueDate != valueDate)
                throw new InvalidOperationException("Option trade publication is not admitted.");
            var publication = Interlocked.Increment(ref sequence);
            await DatasetPublicationFrameCodec.WriteAsync(stream, new()
            {
                Dataset = dataset, ValueDate = valueDate, WorkerInstanceId = workerInstanceId, GenerationId = generation,
                ManifestRevision = manifestRevision, PublicationSequence = publication,
                Kind = DatasetPublicationKind.OptionTradeEvidence, Payload = MessagePackSerializer.Serialize(evidence)
            }, cancellationToken).ConfigureAwait(false);
            await OptionTradeAcknowledgment.ReadAsync(retentionAcknowledgments, publication, generation, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // A timeout may leave a late acknowledgment in the pipe. Never reuse that generation's writer.
            Volatile.Write(ref closed, 1);
            Volatile.Write(ref running, 0);
            throw;
        }
        finally { writer.Release(); }
    }
    public ValueTask StartAsync()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref closed) != 0, this);
        Volatile.Write(ref running, 1);
        return ValueTask.CompletedTask;
    }
    public ValueTask StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StartAsync();
    }

    /// <summary>
    /// Opens publication only after startup has supplied the actual native generation.
    /// This publisher belongs to exactly one epoch and is never rebound on recovery.
    /// </summary>
    public async ValueTask BindGenerationAsync(Guid value, CancellationToken cancellationToken)
    {
        if (value == Guid.Empty) throw new ArgumentException("Generation is required.", nameof(value));
        await writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref closed) != 0, this);
            if (generation != Guid.Empty)
                throw new InvalidOperationException("A dataset publisher cannot change generation.");
            generation = value;
        }
        finally { writer.Release(); }
    }

    /// <summary>Rejects further events and drains the current write before teardown.</summary>
    public async ValueTask CloseAsync(CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref closed, 1);
        Volatile.Write(ref running, 0);
        await writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        writer.Release();
    }

    public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent value) =>
        PublishAsync(value, CancellationToken.None);
    public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent value,
        CancellationToken cancellationToken) => WriteAsync(DatasetPublicationKind.MarketPrice,
            MessagePackSerializer.Serialize(value), cancellationToken);

    public ValueTask PublishAsync(FuturesTradeReplayBatchRealtimeEvent value) =>
        PublishAsync(value, CancellationToken.None);
    public ValueTask PublishAsync(FuturesTradeReplayBatchRealtimeEvent value,
        CancellationToken cancellationToken) => WriteAsync(DatasetPublicationKind.TradeReplayBatch,
            MessagePackSerializer.Serialize(value), cancellationToken);

    public ValueTask PublishAsync(FuturesSessionStatisticsUpdatedRealtimeEvent value) =>
        PublishAsync(value, CancellationToken.None);
    public ValueTask PublishAsync(FuturesSessionStatisticsUpdatedRealtimeEvent value,
        CancellationToken cancellationToken) => WriteAsync(DatasetPublicationKind.SessionStatistics,
            MessagePackSerializer.Serialize(value), cancellationToken);

    public ValueTask PublishAsync(FuturesTickTradeDataChangedEvent value) =>
        PublishAsync(value, CancellationToken.None);
    public ValueTask PublishAsync(FuturesTickTradeDataChangedEvent value,
        CancellationToken cancellationToken) => WriteAsync(DatasetPublicationKind.Trade,
            MessagePackSerializer.Serialize(value), cancellationToken);

    public async ValueTask PublishAsync(FuturesTickQuoteDataChangedEvent value,
        ITickQuoteBufferLease lease) =>
        await PublishAsync(value, lease, CancellationToken.None).ConfigureAwait(false);
    public async ValueTask PublishAsync(FuturesTickQuoteDataChangedEvent value,
        ITickQuoteBufferLease lease, CancellationToken cancellationToken)
    {
        try
        {
            await WriteAsync(DatasetPublicationKind.Quote,
                MessagePackSerializer.Serialize(value), cancellationToken).ConfigureAwait(false);
        }
        finally { lease.Dispose(); }
    }

    async ValueTask WriteAsync(DatasetPublicationKind kind, byte[] payload,
        CancellationToken cancellationToken)
    {
        // Startup events have no qualified native identity yet. Retired epoch events
        // must be discarded, never stamped with the replacement epoch's generation.
        if (!IsRunning || Volatile.Read(ref closed) != 0) return;
        await writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsRunning || Volatile.Read(ref closed) != 0 || generation == Guid.Empty) return;
            await DatasetPublicationFrameCodec.WriteAsync(stream, new()
            {
                Dataset = dataset,
                ValueDate = valueDate,
                WorkerInstanceId = workerInstanceId,
                GenerationId = generation,
                ManifestRevision = manifestRevision,
                PublicationSequence = Interlocked.Increment(ref sequence),
                Kind = kind,
                Payload = payload
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { writer.Release(); }
    }

    public ValueTask StopAsync()
    {
        Volatile.Write(ref running, 0);
        return ValueTask.CompletedTask;
    }
    public ValueTask StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StopAsync();
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        await CloseAsync(CancellationToken.None).ConfigureAwait(false);
        writer.Dispose();
    }
}
