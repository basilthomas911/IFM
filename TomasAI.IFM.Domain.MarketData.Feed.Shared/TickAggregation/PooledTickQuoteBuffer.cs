namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

/// <summary>
/// Owns an exact-length tick-quote byte buffer rented from a bounded process-wide cache.
/// </summary>
public sealed class PooledTickQuoteBuffer : IDisposable
{
    const long MaximumIdleBytes = 64L * 1024 * 1024;
    const int MaximumIdlePerLength = 64;
    static readonly object Gate = new();
    static readonly Dictionary<int, Stack<byte[]>> Idle = [];
    static long _idleBytes;
    static long _newArrays;

    byte[]? _buffer;

    PooledTickQuoteBuffer(byte[] buffer) => _buffer = buffer;

    /// <summary>
    /// Gets the owned exact-length byte buffer.
    /// </summary>
    public byte[] Buffer => Volatile.Read(ref _buffer)
        ?? throw new ObjectDisposedException(nameof(PooledTickQuoteBuffer));

    /// <summary>
    /// Gets the number of byte arrays allocated instead of reused.
    /// </summary>
    public static long NewArrays => Interlocked.Read(ref _newArrays);

    /// <summary>
    /// Gets the total number of bytes currently retained by the idle cache.
    /// </summary>
    public static long IdleBytes => Interlocked.Read(ref _idleBytes);

    /// <summary>
    /// Rents an exact-length tick-quote buffer.
    /// </summary>
    public static PooledTickQuoteBuffer Rent(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        lock (Gate)
        {
            if (Idle.TryGetValue(length, out var entries) && entries.Count != 0)
            {
                _idleBytes -= length;
                return new PooledTickQuoteBuffer(entries.Pop());
            }
        }

        Interlocked.Increment(ref _newArrays);
        return new PooledTickQuoteBuffer(new byte[length]);
    }

    /// <summary>
    /// Returns the owned buffer to the bounded cache.
    /// </summary>
    public void Dispose()
    {
        var buffer = Interlocked.Exchange(ref _buffer, null);
        if (buffer is null)
            return;

        lock (Gate)
        {
            if (_idleBytes + buffer.Length > MaximumIdleBytes)
                return;
            if (!Idle.TryGetValue(buffer.Length, out var entries))
                Idle[buffer.Length] = entries = new Stack<byte[]>();
            if (entries.Count >= MaximumIdlePerLength)
                return;

            entries.Push(buffer);
            _idleBytes += buffer.Length;
        }
    }
}
