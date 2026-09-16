namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>
/// Keeps exact-length CQL buffers in a bounded process-wide cache. The Cassandra
/// binder uses the entire byte array, so an oversized ArrayPool rental is not valid.
/// </summary>
internal sealed class PooledTickQuoteCqlBuffer : IDisposable
{
    private const long MaximumIdleBytes = 64L * 1024 * 1024;
    private const int MaximumIdlePerLength = 64;
    private static readonly object Gate = new();
    private static readonly Dictionary<int, Stack<byte[]>> Idle = [];
    private static long _idleBytes;
    private static long _newArrays;

    private byte[]? _buffer;

    private PooledTickQuoteCqlBuffer(byte[] buffer) => _buffer = buffer;

    public byte[] Buffer => Volatile.Read(ref _buffer)
        ?? throw new ObjectDisposedException(nameof(PooledTickQuoteCqlBuffer));

    public static long NewArrays => Interlocked.Read(ref _newArrays);
    public static long IdleBytes => Interlocked.Read(ref _idleBytes);

    public static PooledTickQuoteCqlBuffer Rent(int length)
    {
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
        lock (Gate)
        {
            if (Idle.TryGetValue(length, out var entries) && entries.Count != 0)
            {
                _idleBytes -= length;
                return new PooledTickQuoteCqlBuffer(entries.Pop());
            }
        }
        Interlocked.Increment(ref _newArrays);
        return new PooledTickQuoteCqlBuffer(new byte[length]);
    }

    public void Dispose()
    {
        var buffer = Interlocked.Exchange(ref _buffer, null);
        if (buffer is null) return;
        lock (Gate)
        {
            if (_idleBytes + buffer.Length > MaximumIdleBytes) return;
            if (!Idle.TryGetValue(buffer.Length, out var entries))
                Idle[buffer.Length] = entries = new Stack<byte[]>();
            if (entries.Count >= MaximumIdlePerLength) return;
            entries.Push(buffer);
            _idleBytes += buffer.Length;
        }
    }
}
