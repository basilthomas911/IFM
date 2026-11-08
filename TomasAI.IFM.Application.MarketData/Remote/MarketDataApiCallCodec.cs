using System.Buffers;
using MessagePack;
using TomasAI.IFM.Application.MarketData.Databento.Workers;

namespace TomasAI.IFM.Application.MarketData.Remote;

/// <summary>Writes the existing nested binary RPC format without independently allocating each argument.</summary>
internal static class MarketDataApiCallCodec
{
    internal static byte[] Encode(string operation, Type[] types, object?[] arguments,
        CancellationToken token = default, ArrayPool<byte>? pool = null)
    {
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(arguments);
        var descriptor = MarketDataOperationCatalog.Shared.Resolve(operation, types);
        if (arguments.Length != descriptor.ParameterCount)
            throw new ArgumentException("Market-data argument count differs from its operation.", nameof(arguments));
        token.ThrowIfCancellationRequested();
        using var values = new PooledWriter(pool ?? ArrayPool<byte>.Shared);
        Span<(int Offset, int Length)> slices = arguments.Length <= 16
            ? stackalloc (int, int)[arguments.Length] : new (int, int)[arguments.Length];
        for (var index = 0; index < arguments.Length; index++)
        {
            var start = values.WrittenCount;
            if (arguments[index] is { } value)
                MessagePackSerializer.Serialize(value.GetType(), values, value,
                    MarketDataHostConnection.Serialization, token);
            slices[index] = (start, values.WrittenCount - start);
        }
        var length = descriptor.Prefix.Length;
        foreach (var slice in slices)
            length = checked(length + slice.Length + (slice.Length <= byte.MaxValue ? 2 : slice.Length <= ushort.MaxValue ? 3 : 5));
        var body = GC.AllocateUninitializedArray<byte>(length);
        descriptor.Prefix.CopyTo(body);
        var offset = descriptor.Prefix.Length;
        foreach (var slice in slices)
        {
            token.ThrowIfCancellationRequested();
            if (!MessagePackPrimitives.TryWriteBinHeader(body.AsSpan(offset), (uint)slice.Length, out var written))
                throw new InvalidOperationException("RPC binary header exceeded its calculated capacity.");
            offset += written;
            values.WrittenSpan.Slice(slice.Offset, slice.Length).CopyTo(body.AsSpan(offset));
            offset += slice.Length;
        }
        // Transport owns this independent array until its asynchronous send finishes.
        return body;
    }

    sealed class PooledWriter(ArrayPool<byte> pool) : IBufferWriter<byte>, IDisposable
    {
        byte[]? buffer;
        bool disposed;
        public int WrittenCount { get; private set; }
        public ReadOnlySpan<byte> WrittenSpan => buffer is null ? [] : buffer.AsSpan(0, WrittenCount);

        public void Advance(int count)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (buffer is null) throw new InvalidOperationException("Advance requires a rented buffer.");
            if (count < 0 || count > buffer.Length - WrittenCount)
                throw new ArgumentOutOfRangeException(nameof(count));
            WrittenCount += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0) { Ensure(sizeHint); return buffer.AsMemory(WrittenCount); }
        public Span<byte> GetSpan(int sizeHint = 0) { Ensure(sizeHint); return buffer.AsSpan(WrittenCount); }

        void Ensure(int sizeHint)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
            if (buffer is null) { buffer = pool.Rent(Math.Max(256, sizeHint)); return; }
            var needed = checked(WrittenCount + Math.Max(1, sizeHint));
            if (needed <= buffer.Length) return;
            var capacity = (int)Math.Min(Array.MaxLength, Math.Max((long)needed, (long)buffer.Length * 2));
            var replacement = pool.Rent(capacity);
            buffer.AsSpan(0, WrittenCount).CopyTo(replacement);
            buffer.AsSpan().Clear();
            pool.Return(buffer);
            buffer = replacement;
        }

        public void Dispose()
        {
            disposed = true;
            var previous = Interlocked.Exchange(ref buffer, null);
            if (previous is null) return;
            // A formatter can throw after writing but before advancing its final
            // span, so clear the entire rental rather than only committed bytes.
            previous.AsSpan().Clear();
            pool.Return(previous);
        }
    }
}
