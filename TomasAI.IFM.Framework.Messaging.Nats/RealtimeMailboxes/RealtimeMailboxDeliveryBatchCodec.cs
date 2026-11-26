using System.Buffers.Binary;
using System.Security.Cryptography;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;

/// <summary>Bounded broker transport grouping; every original delivery retains its individual owner proof.</summary>
internal static class RealtimeMailboxDeliveryBatchCodec
{
    static ReadOnlySpan<byte> Magic => "IFMBATCH"u8;
    const int HeaderBytes = 16, DigestBytes = 32, MaximumCount = 64;

    public static byte[] Encode(IReadOnlyList<byte[]> deliveries, int maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(deliveries);
        if (maximumBytes <= 0 || deliveries.Count is < 1 or > MaximumCount)
            throw new InvalidDataException("Realtime broker batch count or byte bound is invalid.");
        long total = HeaderBytes + DigestBytes;
        foreach (var delivery in deliveries)
        {
            ValidateInner(delivery);
            total += sizeof(int) + delivery.Length;
            if (total > maximumBytes) throw new InvalidDataException("Realtime broker batch exceeds its total byte bound.");
        }
        var wire = new byte[checked((int)total)];
        Magic.CopyTo(wire); BinaryPrimitives.WriteInt32LittleEndian(wire.AsSpan(8), 1);
        BinaryPrimitives.WriteInt32LittleEndian(wire.AsSpan(12), deliveries.Count);
        var offset = HeaderBytes;
        foreach (var delivery in deliveries)
        {
            BinaryPrimitives.WriteInt32LittleEndian(wire.AsSpan(offset), delivery.Length); offset += sizeof(int);
            delivery.CopyTo(wire, offset); offset += delivery.Length;
        }
        SHA256.HashData(wire.AsSpan(0, offset), wire.AsSpan(offset, DigestBytes));
        return wire;
    }

    public static IReadOnlyList<byte[]> Decode(byte[] wire, int maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(wire);
        if (maximumBytes <= 0 || wire.Length > maximumBytes) throw new InvalidDataException("Realtime broker wire exceeds its byte bound.");
        if (wire.Length >= sizeof(int) && BinaryPrimitives.ReadInt32LittleEndian(wire) == 2)
        {
            // Preserve the legacy byte array; its existing source codec verifies the complete inner proof.
            return [wire];
        }
        if (wire.Length < HeaderBytes + sizeof(int) + sizeof(int) + DigestBytes || !wire.AsSpan(0, 8).SequenceEqual(Magic))
            throw new InvalidDataException("Unknown or truncated realtime broker batch envelope.");
        if (BinaryPrimitives.ReadInt32LittleEndian(wire.AsSpan(8)) != 1) throw new InvalidDataException("Unknown realtime broker batch version.");
        var count = BinaryPrimitives.ReadInt32LittleEndian(wire.AsSpan(12));
        if (count is < 1 or > MaximumCount) throw new InvalidDataException("Realtime broker batch count is outside 1..64.");
        var end = wire.Length - DigestBytes;
        Span<byte> actual = stackalloc byte[DigestBytes]; SHA256.HashData(wire.AsSpan(0, end), actual);
        if (!CryptographicOperations.FixedTimeEquals(actual, wire.AsSpan(end))) throw new InvalidDataException("Realtime broker batch digest differs.");
        // Validate every frame before allocating members or letting the first member run.
        var offset = HeaderBytes;
        for (var i = 0; i < count; i++)
        {
            if (offset > end - sizeof(int)) throw new InvalidDataException("Realtime broker batch member length is missing.");
            var length = BinaryPrimitives.ReadInt32LittleEndian(wire.AsSpan(offset)); offset += sizeof(int);
            if (length < sizeof(int) || length > end - offset || BinaryPrimitives.ReadInt32LittleEndian(wire.AsSpan(offset)) != 2)
                throw new InvalidDataException("Realtime broker batch member is invalid or truncated.");
            offset += length;
        }
        if (offset != end) throw new InvalidDataException("Realtime broker batch contains trailing bytes.");
        var deliveries = new byte[count][]; offset = HeaderBytes;
        for (var i = 0; i < count; i++)
        {
            var length = BinaryPrimitives.ReadInt32LittleEndian(wire.AsSpan(offset)); offset += sizeof(int);
            deliveries[i] = wire.AsSpan(offset, length).ToArray(); offset += length;
        }
        return deliveries;
    }

    static void ValidateInner(byte[]? delivery)
    {
        if (delivery is null || delivery.Length < sizeof(int) || BinaryPrimitives.ReadInt32LittleEndian(delivery) != 2)
            throw new InvalidDataException("Realtime broker batches contain original version2 delivery wires only.");
    }
}
