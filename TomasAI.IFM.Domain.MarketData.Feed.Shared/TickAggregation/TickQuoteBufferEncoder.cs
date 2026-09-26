using System.Buffers.Binary;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

/// <summary>
/// Encodes a bounded tick-quote segment into its canonical binary representation.
/// </summary>
public static class TickQuoteBufferEncoder
{
    const int FixedItemSize = 9 * (sizeof(int) + sizeof(long))
        + sizeof(int) + sizeof(short)
        + 2 * sizeof(int);

    /// <summary>
    /// Creates the exact binary value for a bounded quote segment.
    /// </summary>
    public static byte[] Encode(FuturesTickQuoteDataSegment segment)
    {
        var payload = new byte[EncodedLength(segment)];
        EncodeInto(segment, payload);
        return payload;
    }

    /// <summary>
    /// Encodes a bounded quote segment into an owned, exact-length pooled buffer.
    /// </summary>
    public static PooledTickQuoteBuffer EncodePooled(FuturesTickQuoteDataSegment segment)
    {
        var owner = PooledTickQuoteBuffer.Rent(EncodedLength(segment));
        try
        {
            EncodeInto(segment, owner.Buffer);
            return owner;
        }
        catch
        {
            owner.Dispose();
            throw;
        }
    }

    static int EncodedLength(FuturesTickQuoteDataSegment segment)
    {
        if (segment.Buffer is null || segment.Count is 0 or > FuturesTickQuoteDataSegment.MaximumCount
            || segment.Count > segment.Buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(segment));
        var length = sizeof(int);
        foreach (ref readonly var quote in segment.Items)
            length = checked(length + sizeof(int) + ItemSize(quote));
        return length;
    }

    static void EncodeInto(FuturesTickQuoteDataSegment segment, byte[] payload)
    {
        var output = payload.AsSpan();
        var offset = 0;
        WriteInt32(output, ref offset, segment.Count);
        foreach (ref readonly var quote in segment.Items)
        {
            WriteInt32(output, ref offset, ItemSize(quote));
            WriteLong(output, ref offset, quote.SourceSequence);
            WriteLong(output, ref offset, quote.EventTimestampNanoseconds);
            WriteLong(output, ref offset, quote.ReceiveTimestampNanoseconds);
            WriteShort(output, ref offset, quote.HeaderFlags);
            WriteLong(output, ref offset, quote.BidPriceRaw);
            WriteDecimal(output, ref offset, quote.BidPrice);
            WriteLong(output, ref offset, quote.BidSize);
            WriteLong(output, ref offset, quote.BidCount);
            WriteLong(output, ref offset, quote.AskPriceRaw);
            WriteDecimal(output, ref offset, quote.AskPrice);
            WriteLong(output, ref offset, quote.AskSize);
            WriteLong(output, ref offset, quote.AskCount);
        }
        if (offset != payload.Length)
            throw new InvalidOperationException("Quote encoding length mismatch.");
    }

    static int ItemSize(in FuturesTickQuoteData quote)
        => FixedItemSize + DecimalSize(quote.BidPrice) + DecimalSize(quote.AskPrice);

    static int DecimalSize(decimal? value)
    {
        if (value is null)
            return 0;
        Span<byte> unscaled = stackalloc byte[13];
        return sizeof(int) + WriteUnscaled(value.Value, unscaled);
    }

    static void WriteLong(Span<byte> output, ref int offset, long value)
    {
        WriteInt32(output, ref offset, sizeof(long));
        BinaryPrimitives.WriteInt64BigEndian(output.Slice(offset, sizeof(long)), value);
        offset += sizeof(long);
    }

    static void WriteShort(Span<byte> output, ref int offset, short value)
    {
        WriteInt32(output, ref offset, sizeof(short));
        BinaryPrimitives.WriteInt16BigEndian(output.Slice(offset, sizeof(short)), value);
        offset += sizeof(short);
    }

    static void WriteDecimal(Span<byte> output, ref int offset, decimal? value)
    {
        if (value is null)
        {
            WriteInt32(output, ref offset, -1);
            return;
        }

        Span<byte> unscaled = stackalloc byte[13];
        var numberLength = WriteUnscaled(value.Value, unscaled);
        WriteInt32(output, ref offset, sizeof(int) + numberLength);
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value.Value, bits);
        WriteInt32(output, ref offset, (bits[3] >> 16) & 0xff);
        unscaled[..numberLength].CopyTo(output[offset..]);
        offset += numberLength;
    }

    static int WriteUnscaled(decimal value, Span<byte> destination)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);

        Span<byte> number = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(number.Slice(1, 4), (uint)bits[2]);
        BinaryPrimitives.WriteUInt32BigEndian(number.Slice(5, 4), (uint)bits[1]);
        BinaryPrimitives.WriteUInt32BigEndian(number.Slice(9, 4), (uint)bits[0]);
        var negative = value < 0;
        if (negative)
        {
            for (var index = 0; index < number.Length; index++)
                number[index] = (byte)~number[index];
            for (var index = number.Length - 1; index >= 0; index--)
            {
                if (++number[index] != 0)
                    break;
            }
        }

        var sign = negative ? (byte)0xff : (byte)0;
        var signBit = negative ? 0x80 : 0;
        var start = 0;
        while (start < number.Length - 1 && number[start] == sign
            && (number[start + 1] & 0x80) == signBit)
            start++;
        var length = number.Length - start;
        number[start..].CopyTo(destination);
        return length;
    }

    static void WriteInt32(Span<byte> output, ref int offset, int value)
    {
        BinaryPrimitives.WriteInt32BigEndian(output.Slice(offset, sizeof(int)), value);
        offset += sizeof(int);
    }
}
