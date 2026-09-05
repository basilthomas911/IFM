using System.Buffers.Binary;
using MessagePack;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Application.MarketData.MarketOutlook;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

public enum DatasetPublicationKind : byte
{
    Trade = 1,
    Quote = 2,
    MarketPrice = 3,
    SessionStatistics = 4
}

[MessagePackObject]
public sealed record DatasetPublicationEnvelope
{
    [Key(0)] public required string Dataset { get; init; }
    [Key(1)] public required DateOnly ValueDate { get; init; }
    [Key(2)] public required Guid WorkerInstanceId { get; init; }
    [Key(3)] public required Guid GenerationId { get; init; }
    [Key(4)] public required long ManifestRevision { get; init; }
    [Key(5)] public required long PublicationSequence { get; init; }
    [Key(6)] public required DatasetPublicationKind Kind { get; init; }
    [Key(7)] public required byte[] Payload { get; init; }
}

public static class DatasetPublicationFrameCodec
{
    public const int MaximumFrameBytes = 1024 * 1024;

    public static async ValueTask WriteAsync(Stream stream, DatasetPublicationEnvelope envelope,
        CancellationToken cancellationToken)
    {
        Validate(envelope);
        var payload = MessagePackSerializer.Serialize(envelope);
        if (payload.Length > MaximumFrameBytes)
            throw new InvalidDataException("Dataset publication frame is too large.");
        var prefix = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
        await stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<DatasetPublicationEnvelope> ReadAsync(Stream stream,
        CancellationToken cancellationToken)
    {
        var prefix = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (length is < 2 or > MaximumFrameBytes)
            throw new InvalidDataException($"Dataset publication frame length {length} is invalid.");
        var payload = GC.AllocateUninitializedArray<byte>(length);
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        var envelope = MessagePackSerializer.Deserialize<DatasetPublicationEnvelope>(payload);
        Validate(envelope);
        return envelope;
    }

    static void Validate(DatasetPublicationEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (string.IsNullOrWhiteSpace(envelope.Dataset) || envelope.Dataset.Length > 64
            || envelope.ValueDate == default || envelope.WorkerInstanceId == Guid.Empty
            || envelope.GenerationId == Guid.Empty || envelope.ManifestRevision < 1
            || envelope.PublicationSequence < 1 || !Enum.IsDefined(envelope.Kind)
            || envelope.Payload is null || envelope.Payload.Length == 0
            || envelope.Payload.Length > MaximumFrameBytes)
            throw new InvalidDataException("Dataset publication identity or bounds are invalid.");
    }
}

/// <summary>Rejects stale worker output before translating it to the existing realtime publisher.</summary>
public sealed class DatasetPublicationIngress(
    DatasetWorkerAdmissionRegistry admissions,
    ITickAggregationEventPublisher publisher,
    IMarketDataOperationsRecorder recorder,
    DatasetWorkerCurrentValues? currentValues = null)
{
    public async ValueTask<bool> AcceptAsync(DatasetPublicationEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = new DatasetWorkerAdmission(envelope.Dataset, envelope.ValueDate,
            envelope.WorkerInstanceId, envelope.GenerationId, envelope.ManifestRevision);
        Record(MarketDataOperationOutcome.Received, envelope);
        if (!admissions.TryAccept(identity, envelope.PublicationSequence, out var generationCancellation))
        {
            Record(MarketDataOperationOutcome.Failed, envelope);
            return false;
        }
        // The mirror also verifies identity under its own reset lock. A publication that passed
        // admission just before Close cannot repopulate cleared dataset values afterward.
        if (currentValues is not null && !currentValues.AcceptPublication(envelope))
        {
            Record(MarketDataOperationOutcome.Failed, envelope);
            return false;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            generationCancellation.ThrowIfCancellationRequested();
            // The publisher enqueues this token and returns before transmission. A short-lived
            // linked CTS would disconnect reset cancellation at that return boundary. Ownership
            // therefore transfers with the lasting generation token, not the caller's read token.
            switch (envelope.Kind)
            {
                case DatasetPublicationKind.Trade:
                    await publisher.PublishAsync(
                        MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(envelope.Payload),
                        generationCancellation).ConfigureAwait(false);
                    break;
                case DatasetPublicationKind.Quote:
                    var quote = MessagePackSerializer.Deserialize<FuturesTickQuoteDataChangedEvent>(envelope.Payload);
                    await publisher.PublishAsync(quote,
                        new DeserializedQuoteLease(quote.QuoteData.Buffer, quote.QuoteCount),
                        generationCancellation).ConfigureAwait(false);
                    break;
                case DatasetPublicationKind.MarketPrice:
                    await publisher.PublishAsync(
                        MessagePackSerializer.Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(envelope.Payload),
                        generationCancellation).ConfigureAwait(false);
                    break;
                case DatasetPublicationKind.SessionStatistics:
                    await publisher.PublishAsync(
                        MessagePackSerializer.Deserialize<FuturesSessionStatisticsUpdatedRealtimeEvent>(envelope.Payload),
                        generationCancellation).ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidDataException("Unknown dataset publication kind.");
            }
        }
        catch (OperationCanceledException) when (generationCancellation.IsCancellationRequested)
        {
            // An expected reset must not fault the shared publication reader/data plane.
            Record(MarketDataOperationOutcome.Failed, envelope);
            return false;
        }
        Record(MarketDataOperationOutcome.Published, envelope);
        return true;
    }

    void Record(MarketDataOperationOutcome outcome, DatasetPublicationEnvelope envelope)
    {
        try
        {
            recorder.Record(new MarketDataOperationMeasurement(
                MarketDataOperationStage.DatabentoGenerationIngress, outcome,
                MarketOutlookUpdateKind.FeedHealth, Guid.Empty, DateTime.UtcNow));
        }
        catch
        {
            // Operational telemetry is never allowed to interrupt realtime ingress.
        }
    }

    sealed class DeserializedQuoteLease(FuturesTickQuoteData[] buffer, ushort count)
        : ITickQuoteBufferLease
    {
        public FuturesTickQuoteData[] Buffer { get; } = buffer;
        public ushort Count { get; private set; } = count;
        public void SetCount(ushort value)
        {
            if (value > Buffer.Length) throw new ArgumentOutOfRangeException(nameof(value));
            Count = value;
        }
        public void Dispose() { }
    }
}
