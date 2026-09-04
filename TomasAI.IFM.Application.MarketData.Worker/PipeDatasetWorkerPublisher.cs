using MessagePack;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Application.MarketData.Worker;

internal sealed class PipeDatasetWorkerPublisher(
    Stream stream,
    string dataset,
    DateOnly valueDate,
    Guid workerInstanceId,
    Guid initialGeneration,
    long manifestRevision = 1) : ITickAggregationEventPublisher
{
    readonly SemaphoreSlim writer = new(1, 1);
    Guid generation = initialGeneration;
    long sequence;
    int running;

    public bool IsRunning => Volatile.Read(ref running) != 0;
    public ValueTask StartAsync()
    {
        Volatile.Write(ref running, 1);
        return ValueTask.CompletedTask;
    }
    public ValueTask StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StartAsync();
    }

    public void ChangeGeneration(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Generation is required.", nameof(value));
        generation = value;
        Interlocked.Exchange(ref sequence, 0);
    }

    public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent value) =>
        PublishAsync(value, CancellationToken.None);
    public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent value,
        CancellationToken cancellationToken) => WriteAsync(DatasetPublicationKind.MarketPrice,
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
        if (!IsRunning) throw new InvalidOperationException("Worker publisher is not running.");
        await writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
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
        await StopAsync().ConfigureAwait(false);
        writer.Dispose();
    }
}
