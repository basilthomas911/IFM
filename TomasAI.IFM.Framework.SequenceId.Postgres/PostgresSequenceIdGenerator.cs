using System.Collections.Concurrent;

namespace TomasAI.IFM.Framework.SequenceId.Postgres;

/// <summary>
/// Allocates unique identifiers from disjoint ranges reserved through PostgreSQL sequences.
/// </summary>
/// <remarks>
/// PostgreSQL is the system-wide authority. Each application instance reserves a range with
/// one database call and serves that range through an atomic, lock-free fast path. A separate
/// refill gate per sequence prevents duplicate range reservations inside an application instance.
/// Gaps are expected when a process stops before consuming its active ranges.
/// </remarks>
public sealed class PostgresSequenceIdGenerator(ISequenceIdDbContext sequenceIdDb)
    : ISequenceIdGenerator, IDisposable
{
    readonly ISequenceIdDbContext _sequenceIdDb =
        sequenceIdDb ?? throw new ArgumentNullException(nameof(sequenceIdDb));
    readonly ConcurrentDictionary<SequenceName, Lazy<SequenceState>> _states = new();

    int _disposed;

    /// <inheritdoc />
    public ValueTask<long> GetSequenceIdAsync(
        SequenceName sequenceName,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();

        var state = _states.GetOrAdd(sequenceName, static _ => new Lazy<SequenceState>(static () => new SequenceState())).Value;
        var block = Volatile.Read(ref state.ActiveBlock);
        if (block is not null && block.TryGetNext(out var sequenceId))
            return ValueTask.FromResult(sequenceId);

        return RefillAndGetNextAsync(state, sequenceName, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<long> GetHighWatermarkAsync(
        SequenceName sequenceName,
        CancellationToken cancellationToken = default)
        => await _sequenceIdDb
            .GetCurrentSequenceIdAsync(sequenceName, cancellationToken)
            .ConfigureAwait(false);

    async ValueTask<long> RefillAndGetNextAsync(
        SequenceState state,
        SequenceName sequenceName,
        CancellationToken cancellationToken)
    {
        await state.RefillGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another caller may have replenished the range while this caller waited.
            var block = Volatile.Read(ref state.ActiveBlock);
            if (block is not null && block.TryGetNext(out var sequenceId))
                return sequenceId;

            if (!state.AllocationSizeValidated)
            {
                var allocationSize = await _sequenceIdDb
                    .GetSequenceAllocationSizeAsync(sequenceName, cancellationToken)
                    .ConfigureAwait(false);
                if (allocationSize != SequenceIdSettings.AllocationSize)
                {
                    throw new InvalidOperationException(
                        $"PostgreSQL sequence '{sequenceName}' has increment {allocationSize}; " +
                        $"the allocator requires {SequenceIdSettings.AllocationSize}.");
                }

                state.AllocationSizeValidated = true;
            }

            var rangeStart = await _sequenceIdDb
                .GetNextSequenceIdAsync(sequenceName, cancellationToken)
                .ConfigureAwait(false);
            var rangeEnd = checked(rangeStart + (SequenceIdSettings.AllocationSize - 1L));
            var nextBlock = new SequenceBlock(rangeStart, rangeEnd);
            Volatile.Write(ref state.ActiveBlock, nextBlock);

            if (!nextBlock.TryGetNext(out sequenceId))
                throw new InvalidOperationException(
                    $"PostgreSQL returned an invalid range start for sequence '{sequenceName}'.");

            return sequenceId;
        }
        finally
        {
            state.RefillGate.Release();
        }
    }

    /// <summary>Releases refill gates after host consumers have stopped.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        foreach (var state in _states.Values)
            if (state.IsValueCreated) state.Value.Dispose();
    }

    sealed class SequenceState : IDisposable
    {
        internal readonly SemaphoreSlim RefillGate = new(1, 1);
        internal SequenceBlock? ActiveBlock;
        internal bool AllocationSizeValidated;
        public void Dispose() => RefillGate.Dispose();
    }

    sealed class SequenceBlock(long rangeStart, long rangeEnd)
    {
        long _current = checked(rangeStart - 1L);

        internal bool TryGetNext(out long sequenceId)
        {
            while (true)
            {
                var current = Volatile.Read(ref _current);
                if (current >= rangeEnd)
                {
                    sequenceId = default;
                    return false;
                }

                var next = current + 1L;
                if (Interlocked.CompareExchange(ref _current, next, current) == current)
                {
                    sequenceId = next;
                    return true;
                }
            }
        }
    }
}
