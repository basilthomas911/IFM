using System.Buffers;

namespace TomasAI.IFM.Framework.Storage;

/// <summary>
/// Provides a high-performance buffer builder for value types that utilizes pooled memory to minimize allocations when
/// constructing collections of elements.
/// </summary>
/// <remarks>This ref struct manages its underlying storage using a memory pool, enabling efficient reuse of
/// memory and reducing garbage collection pressure. The buffer automatically grows as needed when additional elements
/// are added. Callers must dispose the instance when finished to release the pooled memory. After calling MoveToResult
/// or Dispose, the instance should not be used.</remarks>
/// <typeparam name="T">The type of value elements stored in the buffer.</typeparam>
internal ref struct PooledBufferBuilder<T> where T : struct
{
    private IMemoryOwner<T> _owner;
    private Span<T> _span;
    private int _count;

    public PooledBufferBuilder(int capacity)
    {
        _owner = MemoryPool<T>.Shared.Rent(capacity);
        _span = _owner.Memory.Span;
        _count = 0;
    }

    public void Add(T item)
    {
        if ((uint)_count >= (uint)_span.Length)
            Grow();

        _span[_count++] = item;
    }

    public PooledReadOnlyBuffer<T> MoveToResult()
    {
        var owner = _owner;
        int count = _count;

        _owner = null!;
        _span = default;
        _count = 0;

        return new PooledReadOnlyBuffer<T>(owner, count);
    }

    public void Dispose()
    {
        _owner?.Dispose();
        _owner = null!;
        _span = default;
        _count = 0;
    }

    private void Grow()
    {
        int newCapacity = checked(_span.Length == 0 ? 16 : _span.Length * 2);

        IMemoryOwner<T> newOwner = MemoryPool<T>.Shared.Rent(newCapacity);
        Span<T> newSpan = newOwner.Memory.Span;
        _span[.._count].CopyTo(newSpan);

        _owner.Dispose();
        _owner = newOwner;
        _span = newSpan;
    }
}
