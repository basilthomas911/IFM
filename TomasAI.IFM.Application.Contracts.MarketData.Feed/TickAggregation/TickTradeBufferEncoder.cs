using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

/// <summary>Native frozen UDT-list encoding with one exact-length pooled owner per chunk.</summary>
public static class TickTradeBufferEncoder
{
    public const int MaximumEncodedBytes = 64_000;
    public static int EncodedLength(TradeStorageSegment segment)
    {
        if (segment.Count is < 1 or > TradeStorageSegment.MaximumCount) throw new ArgumentOutOfRangeException(nameof(segment));
        var length = 4;
        foreach (ref readonly var record in segment.Items) length = checked(length + 4 + ItemLength(record));
        if (length > MaximumEncodedBytes) throw new ArgumentOutOfRangeException(nameof(segment), "Trade chunk exceeds its byte limit.");
        return length;
    }
    static int ItemLength(in TradeStorageRecord r) => 196 + TickQuoteBufferEncoder.DecimalSize(r.Trade.Price)
        + Encoding.UTF8.GetByteCount(r.AggregateId) + Encoding.UTF8.GetByteCount(r.EventSource);

    public static PooledTickQuoteBuffer EncodePooled(TradeStorageSegment segment)
    {
        var owner = PooledTickQuoteBuffer.Rent(EncodedLength(segment));
        try
        {
            var bytes = owner.Buffer.AsSpan();
            var offset = 0;
            TickQuoteBufferEncoder.WriteInt32(bytes, ref offset, segment.Count);
            foreach (ref readonly var r in segment.Items)
            {
                TickQuoteBufferEncoder.WriteInt32(bytes, ref offset, ItemLength(r));
                TickQuoteBufferEncoder.WriteLong(bytes, ref offset, r.Trade.SourceSequence);
                TickQuoteBufferEncoder.WriteLong(bytes, ref offset, r.Trade.EventTimestampNanoseconds);
                TickQuoteBufferEncoder.WriteLong(bytes, ref offset, r.Trade.ReceiveTimestampNanoseconds);
                TickQuoteBufferEncoder.WriteShort(bytes, ref offset, r.Trade.HeaderFlags);
                TickQuoteBufferEncoder.WriteLong(bytes, ref offset, r.Trade.PriceRaw);
                TickQuoteBufferEncoder.WriteDecimal(bytes, ref offset, r.Trade.Price);
                TickQuoteBufferEncoder.WriteLong(bytes, ref offset, r.Trade.Size);
                TickQuoteBufferEncoder.WriteShort(bytes, ref offset, r.Trade.Action);
                TickQuoteBufferEncoder.WriteShort(bytes, ref offset, r.Trade.Side);
                TickQuoteBufferEncoder.WriteShort(bytes, ref offset, r.Trade.DbnFlags);
                TickQuoteBufferEncoder.WriteLong(bytes, ref offset, r.SequenceId);
                TickQuoteBufferEncoder.WriteLong(bytes, ref offset, r.AggregationTicks);
                WriteGuid(bytes, ref offset, r.EventId);
                WriteGuid(bytes, ref offset, r.CommandId);
                TickQuoteBufferEncoder.WriteLong(bytes, ref offset, r.EventLogId);
                WriteText(bytes, ref offset, r.AggregateId);
                WriteText(bytes, ref offset, r.EventSource);
                TickQuoteBufferEncoder.WriteLong(bytes, ref offset, r.ReceivedTicks);
                TickQuoteBufferEncoder.WriteLong(bytes, ref offset, r.AcceptanceOrdinal);
            }
            if (offset != bytes.Length) throw new InvalidDataException("Trade CQL length mismatch.");
            return owner;
        }
        catch { owner.Dispose(); throw; }
    }
    static void WriteGuid(Span<byte> bytes, ref int offset, Guid value)
    {
        TickQuoteBufferEncoder.WriteInt32(bytes, ref offset, 16);
        if (!value.TryWriteBytes(bytes.Slice(offset, 16), bigEndian: true, out _)) throw new InvalidDataException("Invalid UUID buffer.");
        offset += 16;
    }
    static void WriteText(Span<byte> bytes, ref int offset, string value)
    {
        var length = Encoding.UTF8.GetByteCount(value);
        TickQuoteBufferEncoder.WriteInt32(bytes, ref offset, length);
        offset += Encoding.UTF8.GetBytes(value, bytes.Slice(offset, length));
    }

    /// <summary>Decodes one unmapped UDT supplied by the driver, without JSON or per-field objects.</summary>
    public static TradeStorageRecord DecodeItem(ReadOnlySpan<byte> bytes)
    {
        var r = new ItemReader(bytes);
        var trade = new FuturesTickTradeData(checked((uint)r.Long()), r.Long(), r.Long(), checked((byte)r.Short()),
            r.Long(), r.Decimal(), checked((uint)r.Long()), checked((byte)r.Short()), checked((byte)r.Short()), checked((byte)r.Short()));
        var result = new TradeStorageRecord(trade, r.Long(), r.Long(), r.Guid(), r.Guid(), r.Long(), r.Text(), r.Text(), r.Long(), r.Long());
        if (!r.Empty) throw new InvalidDataException("Trailing trade UDT fields.");
        return result;
    }

    /// <summary>Decodes the native list buffer returned for unmapped UDTs by the current Scylla driver.</summary>
    public static TradeStorageSegment DecodeList(ReadOnlySpan<byte> bytes, int expectedCount)
    {
        if (expectedCount is < 1 or > TradeStorageSegment.MaximumCount || bytes.Length < 4
            || bytes.Length > MaximumEncodedBytes || BinaryPrimitives.ReadInt32BigEndian(bytes) != expectedCount)
            throw new InvalidDataException("Invalid trade CQL list.");
        var segment = TradeStorageSegmentFormatter.Rent(expectedCount);
        try
        {
            var values = segment.WritableItems;
            bytes = bytes[4..];
            for (var index = 0; index < expectedCount; index++)
            {
                if (bytes.Length < 4) throw new InvalidDataException("Truncated trade list.");
                var length = BinaryPrimitives.ReadInt32BigEndian(bytes); bytes = bytes[4..];
                if (length < 0 || length > bytes.Length) throw new InvalidDataException("Invalid trade item length.");
                values[index] = DecodeItem(bytes[..length]); bytes = bytes[length..];
            }
            if (!bytes.IsEmpty) throw new InvalidDataException("Trailing trade list bytes.");
            return segment;
        }
        catch { segment.Dispose(); throw; }
    }
    ref struct ItemReader(ReadOnlySpan<byte> input)
    {
        ReadOnlySpan<byte> remaining = input;
        public bool Empty => remaining.IsEmpty;
        ReadOnlySpan<byte> Field()
        {
            if (remaining.Length < 4) throw new InvalidDataException("Truncated trade UDT.");
            var length = BinaryPrimitives.ReadInt32BigEndian(remaining); remaining = remaining[4..];
            if (length < 0 || length > remaining.Length) throw new InvalidDataException("Invalid trade UDT field.");
            var value = remaining[..length]; remaining = remaining[length..]; return value;
        }
        public long Long() { var v = Field(); if (v.Length != 8) throw new InvalidDataException($"Invalid bigint length {v.Length}; {remaining.Length} bytes remain."); return BinaryPrimitives.ReadInt64BigEndian(v); }
        public short Short() { var v = Field(); if (v.Length != 2) throw new InvalidDataException("Invalid smallint."); return BinaryPrimitives.ReadInt16BigEndian(v); }
        public Guid Guid() { var v = Field(); if (v.Length != 16) throw new InvalidDataException("Invalid UUID."); return new Guid(v, bigEndian: true); }
        public string Text() => Encoding.UTF8.GetString(Field());
        public decimal Decimal()
        {
            var v = Field();
            if (v.Length is < 5 or > 17) throw new InvalidDataException("Invalid decimal.");
            var scale = BinaryPrimitives.ReadInt32BigEndian(v);
            if (scale is < 0 or > 28) throw new InvalidDataException("Invalid decimal scale.");
            var value = (decimal)new BigInteger(v[4..], isUnsigned: false, isBigEndian: true);
            for (var index = 0; index < scale; index++) value /= 10;
            return value;
        }
    }
}
