using System.Buffers;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

/// <summary>Owns temporary serialization storage until the asynchronous transport write completes.</summary>
internal sealed class PooledFrameBuffer(ArrayPool<byte> pool) : IBufferWriter<byte>, IDisposable
{
    byte[] buffer = pool.Rent(4096);
    int written = sizeof(int);
    public int PayloadLength => written - sizeof(int);
    public Memory<byte> WrittenMemory => buffer.AsMemory(0, written);
    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count > buffer.Length - written) throw new ArgumentOutOfRangeException(nameof(count));
        written += count;
    }
    public Memory<byte> GetMemory(int sizeHint = 0) { Ensure(sizeHint); return buffer.AsMemory(written); }
    public Span<byte> GetSpan(int sizeHint = 0) { Ensure(sizeHint); return buffer.AsSpan(written); }
    void Ensure(int sizeHint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
        if (Math.Max(1, sizeHint) <= buffer.Length - written) return;
        var next = pool.Rent(Math.Max(checked(written + Math.Max(1, sizeHint)), checked(buffer.Length * 2)));
        buffer.AsSpan(0, written).CopyTo(next);
        pool.Return(buffer, clearArray: true);
        buffer = next;
    }
    public void Dispose()
    {
        var owned = buffer;
        buffer = [];
        if (owned.Length != 0) pool.Return(owned, clearArray: true);
    }
}
