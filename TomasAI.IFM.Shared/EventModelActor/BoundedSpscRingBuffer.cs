using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TomasAI.IFM.Shared.EventModelActor;

[StructLayout(LayoutKind.Explicit, Size = 128)]
internal struct SpscPaddedPosition
{
    [FieldOffset(64)] public int Value;
}

/// <summary>
/// Allocation-free bounded ring for exactly one logical producer and one concurrent consumer.
/// The producer and consumer may move between OS threads, but their operations must never overlap with another
/// producer or consumer respectively.
/// </summary>
internal sealed class BoundedSpscRingBuffer<T> where T : struct
{
    readonly T[] _slots;
    readonly int _capacity;
    readonly int _mask;
    SpscPaddedPosition _head;
    SpscPaddedPosition _tail;
    int _completed;

    internal BoundedSpscRingBuffer(int capacity)
    {
        if (capacity <= 0 || !BitOperations.IsPow2(capacity))
            throw new ArgumentOutOfRangeException(nameof(capacity), "SPSC ring capacity must be a positive power of two.");

        _capacity = capacity;
        _mask = capacity - 1;
        _slots = new T[capacity];
    }

    internal int Capacity => _capacity;

    internal int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var head = Volatile.Read(ref _head.Value);
            var tail = Volatile.Read(ref _tail.Value);
            return (int)((uint)head - (uint)tail);
        }
    }

    internal bool IsFull
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var head = Volatile.Read(ref _head.Value);
            var tail = Volatile.Read(ref _tail.Value);
            return (uint)(head - tail) >= (uint)_capacity;
        }
    }

    internal bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var tail = _tail.Value;
            var head = Volatile.Read(ref _head.Value);
            return head == tail;
        }
    }

    internal bool IsCompleted => Volatile.Read(ref _completed) != 0;

    internal void Complete() => Volatile.Write(ref _completed, 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryEnqueue(T item)
    {
        if (Volatile.Read(ref _completed) != 0)
            return false;

        // Only the producer writes head; acquire tail once to test capacity.
        var head = _head.Value;
        var tail = Volatile.Read(ref _tail.Value);
        if ((uint)(head - tail) >= (uint)_capacity)
            return false;

        _slots[head & _mask] = item;
        Volatile.Write(ref _head.Value, head + 1);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryDequeue(out T item)
    {
        // Only the consumer writes tail; acquire head once to test availability.
        var tail = _tail.Value;
        var head = Volatile.Read(ref _head.Value);
        if (head == tail)
        {
            item = default;
            return false;
        }

        var slot = tail & _mask;
        item = _slots[slot];
        _slots[slot] = default;
        Volatile.Write(ref _tail.Value, tail + 1);
        return true;
    }

}
