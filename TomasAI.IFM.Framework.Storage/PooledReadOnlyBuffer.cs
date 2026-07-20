using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TomasAI.IFM.Framework.Storage;

/// <summary>
/// Represents a read-only, disposable buffer of value type elements that is backed by pooled memory for efficient
/// resource management.
/// </summary>
/// <remarks>This class provides indexed and enumerated access to a sequence of elements stored in pooled memory,
/// implementing the IReadOnlyList<T> interface. The buffer must be disposed when no longer needed to release the
/// underlying memory resources. Accessing any member after disposal will throw an ObjectDisposedException. The buffer
/// is intended for scenarios where temporary, high-performance access to a sequence of value types is required without
/// incurring frequent allocations.</remarks>
/// <typeparam name="T">The type of value elements contained in the buffer.</typeparam>
public sealed class PooledReadOnlyBuffer<T> : IReadOnlyList<T>, IDisposable
    where T : struct
{
    private IMemoryOwner<T>? _owner;
    private readonly int _count;

    internal PooledReadOnlyBuffer(IMemoryOwner<T> owner, int count)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _count = count;
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfDisposed();
            return _count;
        }
    }

    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfDisposed();
            if ((uint)index >= (uint)_count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _owner!.Memory.Span[index];
        }
    }

    public ReadOnlyMemory<T> Memory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfDisposed();
            return _owner!.Memory[.._count];
        }
    }

    public ReadOnlySpan<T> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfDisposed();
            return _owner!.Memory.Span[.._count];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator()
    {
        ThrowIfDisposed();
        return new Enumerator(_owner!.Memory, _count);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        ThrowIfDisposed();
        for (int i = 0; i < _count; i++)
            yield return _owner!.Memory.Span[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();

    public void Dispose()
    {
        if (_owner is null) return;

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            _owner.Memory.Span[.._count].Clear();

        _owner.Dispose();
        _owner = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_owner is null)
            throw new ObjectDisposedException(nameof(PooledReadOnlyBuffer<T>));
    }

    public struct Enumerator
    {
        private readonly ReadOnlyMemory<T> _memory;
        private readonly int _count;
        private int _index;

        internal Enumerator(ReadOnlyMemory<T> memory, int count)
        {
            _memory = memory;
            _count = count;
            _index = -1;
        }

        public T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _memory.Span[_index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            int next = _index + 1;
            if ((uint)next < (uint)_count)
            {
                _index = next;
                return true;
            }

            return false;
        }
    }
}