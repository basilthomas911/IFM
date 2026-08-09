using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TomasAI.IFM.Shared.EventModelActor;

[StructLayout(LayoutKind.Explicit, Size = 128)]
internal struct MpscPaddedPosition
{
    [FieldOffset(64)] public long Value;
}

/// <summary>
/// Allocation-free bounded multi-producer ring with sequence-stamped slots and a scheduler-serialized consumer.
/// </summary>
internal sealed class BoundedMpscRingBuffer<T> where T : struct
{
    struct Slot
    {
        public long Sequence;
        public T Item;
    }

    readonly Slot[] _slots;
    readonly int _capacity;
    readonly int _mask;
    MpscPaddedPosition _enqueuePosition;
    MpscPaddedPosition _dequeuePosition;
    int _completed;

    internal BoundedMpscRingBuffer(int capacity)
    {
        if (capacity <= 0 || !BitOperations.IsPow2(capacity))
            throw new ArgumentOutOfRangeException(nameof(capacity), "MPSC ring capacity must be a positive power of two.");

        _capacity = capacity;
        _mask = capacity - 1;
        _slots = new Slot[capacity];
        for (var index = 0; index < capacity; index++)
            _slots[index].Sequence = index;
    }

    internal int Capacity => _capacity;

    internal int Count
    {
        get
        {
            var count = Volatile.Read(ref _enqueuePosition.Value) - Volatile.Read(ref _dequeuePosition.Value);
            return (int)Math.Clamp(count, 0, _capacity);
        }
    }

    internal bool IsCompleted => Volatile.Read(ref _completed) != 0;

    internal void Complete() => Volatile.Write(ref _completed, 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryEnqueueReserved(T item)
    {
        if (IsCompleted)
            return false;

        // The mailbox capacity semaphore has already reserved one physical slot. A ticket therefore cannot
        // overrun the consumer; it only needs to wait for an earlier producer to publish/recycle this exact slot.
        var position = Interlocked.Increment(ref _enqueuePosition.Value) - 1;
        ref var slot = ref _slots[(int)(position & _mask)];
        var spinner = new SpinWait();
        while (Volatile.Read(ref slot.Sequence) != position)
            spinner.SpinOnce();

        slot.Item = item;
        Volatile.Write(ref slot.Sequence, position + 1);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryDequeue(out T item)
    {
        // Actor scheduling guarantees one concurrent consumer. Volatile publication supports ownership moving
        // between different pool workers without paying for a redundant consumer CAS loop.
        var position = Volatile.Read(ref _dequeuePosition.Value);
        ref var slot = ref _slots[(int)(position & _mask)];
        if (Volatile.Read(ref slot.Sequence) != position + 1)
        {
            item = default;
            return false;
        }

        item = slot.Item;
        slot.Item = default;
        Volatile.Write(ref _dequeuePosition.Value, position + 1);
        Volatile.Write(ref slot.Sequence, position + _capacity);
        return true;
    }
}
