using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Service.Health;

/// <summary>Tracks active lifecycle actions without retaining unbounded history.</summary>
public sealed class SupervisorOperationStore : ISupervisorOperationStore
{
    const int RetentionLimit = 256;
    readonly ConcurrentQueue<SupervisorHealthOperation> _recent = [];
    readonly ConcurrentDictionary<Guid, byte> _recorded = [];
    int _active;
    long _revision;
    public int ActiveOperationCount => Math.Max(0, Volatile.Read(ref _active));
    public long LatestRevision => Interlocked.Read(ref _revision);
    public IReadOnlyList<SupervisorHealthOperation> RecentOperations => [.. _recent];
    internal SupervisorHealthOperation Started(ActorThreadId threadId, long generation)
    {
        Interlocked.Increment(ref _active); Interlocked.Increment(ref _revision);
        return new(Guid.NewGuid(), threadId, generation, DateTime.UtcNow, null, null,
            "Mailbox remained at its configured limit for fifteen minutes.");
    }
    internal void Completed(SupervisorHealthOperation operation, SupervisorOperationOutcome outcome, string reason)
    {
        _recent.Enqueue(operation with { CompletedUtc = DateTime.UtcNow, Outcome = outcome, Reason = reason });
        while (_recent.Count > RetentionLimit) _recent.TryDequeue(out _);
        Interlocked.Decrement(ref _active); Interlocked.Increment(ref _revision);
    }

    /// <summary>Records the projected terminal outcome once for each operation ID.</summary>
    public void Record(SupervisorActorOperationRequest request, SupervisorActorOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);
        if (!_recorded.TryAdd(request.OperationId, 0)) return;
        var now = DateTime.UtcNow;
        _recent.Enqueue(new(request.OperationId, request.Target, request.ExpectedGeneration, now, now,
            result.Outcome, $"{request.Requester}: {request.Reason}; {result.Stage}: {result.FailureReason}"));
        while (_recent.Count > RetentionLimit && _recent.TryDequeue(out var removed))
            _recorded.TryRemove(removed.OperationId, out _);
        Interlocked.Increment(ref _revision);
    }
}
