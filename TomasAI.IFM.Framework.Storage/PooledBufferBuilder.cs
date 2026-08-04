using System.Buffers;
using System.Runtime.CompilerServices;

namespace TomasAI.IFM.Framework.Storage;

/// <summary>
/// Provides a high-performance buffer builder for value types that utilizes pooled memory to minimize allocations when
/// constructing collections of elements.
/// </summary>
/// <remarks>This builder manages its underlying storage using a memory pool, enabling efficient reuse of
/// memory and reducing garbage collection pressure. The buffer automatically grows as needed when additional elements
/// are added. Callers must dispose the instance when finished to release the pooled memory. After calling
/// MoveToResult, MoveToArray, or Dispose, the instance should not be used.</remarks>
/// <typeparam name="T">The type of value elements stored in the buffer.</typeparam>
internal struct PooledBufferBuilder<T> where T : struct
{
    private IMemoryOwner<T>? _owner;
    private int _count;

    public PooledBufferBuilder(int capacity)
    {
        _owner = MemoryPool<T>.Shared.Rent(capacity);
        _count = 0;
    }

    public void Add(T item)
    {
        if ((uint)_count >= (uint)_owner!.Memory.Length)
            Grow();

        _owner!.Memory.Span[_count++] = item;
    }

    public PooledReadOnlyBuffer<T> MoveToResult()
    {
        var owner = _owner ?? throw new ObjectDisposedException(nameof(PooledBufferBuilder<T>));
        int count = _count;

        _owner = null!;
        _count = 0;

        return new PooledReadOnlyBuffer<T>(owner, count);
    }

    /// <summary>
    /// Copies the populated portion into an application-owned array and immediately
    /// returns the temporary growth buffer to the pool. Use this when the public
    /// return type does not communicate a disposal requirement to its caller.
    /// </summary>
    public T[] MoveToArray()
    {
        var owner = _owner ?? throw new ObjectDisposedException(nameof(PooledBufferBuilder<T>));
        var count = _count;
        try
        {
            return owner.Memory.Span[..count].ToArray();
        }
        finally
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                owner.Memory.Span[..count].Clear();
            owner.Dispose();
            _owner = null;
            _count = 0;
        }
    }

    public void Dispose()
    {
        if (_owner is { } owner)
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                owner.Memory.Span[.._count].Clear();
            owner.Dispose();
        }
        _owner = null!;
        _count = 0;
    }

    private void Grow()
    {
        var owner = _owner ?? throw new ObjectDisposedException(nameof(PooledBufferBuilder<T>));
        int newCapacity = checked(owner.Memory.Length == 0 ? 16 : owner.Memory.Length * 2);

        IMemoryOwner<T> newOwner = MemoryPool<T>.Shared.Rent(newCapacity);
        owner.Memory.Span[.._count].CopyTo(newOwner.Memory.Span);

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            owner.Memory.Span[.._count].Clear();
        owner.Dispose();
        _owner = newOwner;
    }
}
