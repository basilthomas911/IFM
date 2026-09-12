using System.Collections.Concurrent;
using System.Security.Cryptography;
using TomasAI.IFM.Application.Storage.CommandAudit;

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
    readonly ConcurrentDictionary<Guid, byte[]> _completed = new();
    readonly ConcurrentQueue<Guid> _completedOrder = new();
    readonly ConcurrentDictionary<Guid, InFlightReservation> _inFlight = new();

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
        => TryAcceptAsync(commandId, ReadOnlyMemory<byte>.Empty, reserveDurably, cancellationToken);

    public ValueTask<bool> TryAcceptAsync(
        Guid commandId,
        ReadOnlyMemory<byte> payloadIdentity,
        Func<CancellationToken, Task<bool>> reserveDurably,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reserveDurably);
        cancellationToken.ThrowIfCancellationRequested();

        if (_completed.TryGetValue(commandId, out var completedIdentity))
        {
            ValidateIdentity(commandId, payloadIdentity.Span, completedIdentity);
            return ValueTask.FromResult(false);
        }

        return TryAcceptSlowAsync(commandId, payloadIdentity, reserveDurably, cancellationToken);
    }

    async ValueTask<bool> TryAcceptSlowAsync(
        Guid commandId,
        ReadOnlyMemory<byte> payloadIdentity,
        Func<CancellationToken, Task<bool>> reserveDurably,
        CancellationToken cancellationToken)
    {
        var candidate = new InFlightReservation(
            payloadIdentity.ToArray(),
            new Lazy<Task<bool>>(
                () => ReserveAndRememberAsync(commandId, payloadIdentity, reserveDurably, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));
        var operation = _inFlight.GetOrAdd(commandId, candidate);
        var ownsReservation = ReferenceEquals(operation, candidate);

        if (!ownsReservation)
            ValidateIdentity(commandId, payloadIdentity.Span, operation.PayloadIdentity);

        try
        {
            var accepted = ownsReservation
                ? await operation.Reservation.Value.ConfigureAwait(false)
                : await operation.Reservation.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
            return ownsReservation && accepted;
        }
        finally
        {
            if (ownsReservation)
                _inFlight.TryRemove(new KeyValuePair<Guid, InFlightReservation>(commandId, operation));
        }
    }

    async Task<bool> ReserveAndRememberAsync(
        Guid commandId,
        ReadOnlyMemory<byte> payloadIdentity,
        Func<CancellationToken, Task<bool>> reserveDurably,
        CancellationToken cancellationToken)
    {
        var accepted = await reserveDurably(cancellationToken).ConfigureAwait(false);
        Remember(commandId, payloadIdentity.Span);
        return accepted;
    }

    void Remember(Guid commandId, ReadOnlySpan<byte> payloadIdentity)
    {
        if (!_completed.TryAdd(commandId, payloadIdentity.ToArray()))
            return;

        _completedOrder.Enqueue(commandId);
        while (_completed.Count > _completedCapacity && _completedOrder.TryDequeue(out var oldest))
            _completed.TryRemove(oldest, out _);
    }

    static void ValidateIdentity(Guid commandId, ReadOnlySpan<byte> candidate, ReadOnlySpan<byte> existing)
    {
        // Empty identities preserve the legacy coordinator API, whose JSON path had no stable payload identity.
        if (candidate.IsEmpty || existing.IsEmpty) return;
        if (candidate.Length != existing.Length || !CryptographicOperations.FixedTimeEquals(candidate, existing))
            throw new CommandAuditPayloadConflictException(commandId);
    }

    sealed record InFlightReservation(byte[] PayloadIdentity, Lazy<Task<bool>> Reservation);
}
