using System.Buffers;
using System.Buffers.Binary;
using TomasAI.IFM.Application.MarketData.Pricing;
using MessagePack;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Application.MarketData.MarketOutlook;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

public static class DatasetPublicationFrameCodec
{
    public const int MaximumFrameBytes = 1024 * 1024;
    static readonly MessagePackSerializerOptions WireOptions = MessagePackSerializer.DefaultOptions.WithSecurity(MessagePackSecurity.UntrustedData);

    public static async ValueTask WriteAsync(Stream stream, DatasetPublicationEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Validate(envelope);
        using var buffer = new PooledFrameBuffer(ArrayPool<byte>.Shared);
        MessagePackSerializer.Serialize(buffer, envelope, WireOptions, cancellationToken);
        if (buffer.PayloadLength > MaximumFrameBytes)
            throw new InvalidDataException("Dataset publication frame is too large.");
        BinaryPrimitives.WriteInt32LittleEndian(buffer.WrittenMemory.Span, buffer.PayloadLength);
        await stream.WriteAsync(buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<DatasetPublicationEnvelope> ReadAsync(Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var prefix = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (length is < 2 or > MaximumFrameBytes)
            throw new InvalidDataException($"Dataset publication frame length {length} is invalid.");
        var payload = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            await stream.ReadExactlyAsync(payload.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
            var envelope = MessagePackSerializer.Deserialize<DatasetPublicationEnvelope>(payload.AsMemory(0, length),
                WireOptions, cancellationToken);
            Validate(envelope);
            return envelope;
        }
        finally { ArrayPool<byte>.Shared.Return(payload, clearArray: true); }
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
