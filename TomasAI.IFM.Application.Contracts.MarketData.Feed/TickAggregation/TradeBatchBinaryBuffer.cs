using System.Buffers;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

/// <summary>Hard-bounded MessagePack storage; even the backing array stays below the LOH threshold.</summary>
public sealed class TradeBatchBinaryBuffer : IBufferWriter<byte>, IDisposable
{
    readonly PooledTickQuoteBuffer owner = PooledTickQuoteBuffer.Rent(TickTradeBufferEncoder.MaximumEncodedBytes);
    int written;
    public ReadOnlySpan<byte> WrittenSpan => owner.Buffer.AsSpan(0, written);
    public ReadOnlyMemory<byte> WrittenMemory => owner.Buffer.AsMemory(0, written);
    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count > owner.Buffer.Length - written) throw new InvalidDataException("Trade batch exceeds its byte bound.");
        written += count;
    }
    void Require(int hint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hint);
        if (Math.Max(1, hint) > owner.Buffer.Length - written) throw new InvalidDataException("Trade batch exceeds its byte bound.");
    }
    public Memory<byte> GetMemory(int sizeHint = 0) { Require(sizeHint); return owner.Buffer.AsMemory(written); }
    public Span<byte> GetSpan(int sizeHint = 0) { Require(sizeHint); return owner.Buffer.AsSpan(written); }
    public void Dispose() => owner.Dispose();
}
