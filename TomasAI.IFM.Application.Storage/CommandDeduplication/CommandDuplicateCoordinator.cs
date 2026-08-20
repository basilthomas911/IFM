using System.Collections.Concurrent;

namespace TomasAI.IFM.Application.Storage.CommandDeduplication;

/// <summary>
/// Bounded process-local accelerator for a durable command-id reservation.
/// PostgreSQL remains authoritative for cache misses and across processes.
/// </summary>
internal sealed class CommandDuplicateCoordinator
{
    public const int DefaultCompletedCapacity = 100_000;
    public const string CapacityEnvironmentVariable = "IFM_COMMAND_DUPLICATE_CACHE_CAPACITY";

    readonly int _completedCapacity;
    readonly ConcurrentDictionary<Guid, byte> _completed = new();
    readonly ConcurrentQueue<Guid> _completedOrder = new();
    readonly ConcurrentDictionary<Guid, Lazy<Task<bool>>> _inFlight = new();

    public CommandDuplicateCoordinator(int completedCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(completedCapacity);
        _completedCapacity = completedCapacity;
    }

    public int CompletedCount => _completed.Count;

    public static int ReadConfiguredCapacity()
    {
        var configured = Environment.GetEnvironmentVariable(CapacityEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configured))
            return DefaultCompletedCapacity;
        if (int.TryParse(configured, out var capacity) && capacity > 0)
            return capacity;
        throw new InvalidOperationException(
            $"{CapacityEnvironmentVariable} must be a positive integer when configured.");
    }

    public ValueTask<bool> TryAcceptAsync(
        Guid commandId,
        Func<CancellationToken, Task<bool>> reserveDurably,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reserveDurably);
        cancellationToken.ThrowIfCancellationRequested();

        if (_completed.ContainsKey(commandId))
            return ValueTask.FromResult(false);

        return TryAcceptSlowAsync(commandId, reserveDurably, cancellationToken);
    }

    async ValueTask<bool> TryAcceptSlowAsync(
        Guid commandId,
        Func<CancellationToken, Task<bool>> reserveDurably,
        CancellationToken cancellationToken)
    {
        var candidate = new Lazy<Task<bool>>(
            () => ReserveAndRememberAsync(commandId, reserveDurably, cancellationToken),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var operation = _inFlight.GetOrAdd(commandId, candidate);
        var ownsReservation = ReferenceEquals(operation, candidate);

        try
        {
            var accepted = ownsReservation
                ? await operation.Value.ConfigureAwait(false)
                : await operation.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
            return ownsReservation && accepted;
        }
        finally
        {
            if (ownsReservation)
                _inFlight.TryRemove(new KeyValuePair<Guid, Lazy<Task<bool>>>(commandId, operation));
        }
    }

    async Task<bool> ReserveAndRememberAsync(
        Guid commandId,
        Func<CancellationToken, Task<bool>> reserveDurably,
        CancellationToken cancellationToken)
    {
        var accepted = await reserveDurably(cancellationToken).ConfigureAwait(false);
        Remember(commandId);
        return accepted;
    }

    void Remember(Guid commandId)
    {
        if (!_completed.TryAdd(commandId, 0))
            return;

        _completedOrder.Enqueue(commandId);
        while (_completed.Count > _completedCapacity && _completedOrder.TryDequeue(out var oldest))
            _completed.TryRemove(oldest, out _);
    }
}
