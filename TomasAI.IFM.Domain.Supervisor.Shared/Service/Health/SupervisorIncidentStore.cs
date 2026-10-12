using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Service.Health;

/// <summary>Provides the current bounded incident count directly from the latest snapshot.</summary>
public sealed class SupervisorIncidentStore : ISupervisorIncidentStore
{
    readonly ConcurrentDictionary<ActorThreadId, SupervisorActorIncident> _active = [];
    readonly ConcurrentDictionary<ActorThreadId, ConcurrentQueue<DateTime>> _restarts = [];
    readonly ConcurrentDictionary<ActorThreadId, byte> _manualReview = [];
    public int ActiveIncidentCount => _active.Count;
    public IReadOnlyList<SupervisorActorIncident> ActiveIncidents => [.. _active.Values.OrderBy(value => value.ThreadId.ToString(), StringComparer.Ordinal)];

    public bool Acknowledge(ActorThreadId threadId, string requester, string reason)
    {
        if (string.IsNullOrWhiteSpace(requester) || string.IsNullOrWhiteSpace(reason)) return false;
        _manualReview.TryRemove(threadId, out _);
        return _active.TryRemove(threadId, out _);
    }

    internal void Observe(SupervisorActorMetricsSnapshot snapshot)
    {
        var observed = new HashSet<ActorThreadId>();
        foreach (var thread in snapshot.Actors.SelectMany(actor => actor.Threads))
        {
            if (thread.Health == SupervisorActorHealth.Healthy) continue;
            observed.Add(thread.ThreadId);
            _active.AddOrUpdate(thread.ThreadId,
                _ => new(thread.ThreadId, thread.Health, snapshot.ObservedUtc, snapshot.ObservedUtc,
                    thread.Generation, thread.QueueDepth, thread.QueueCapacity),
                (_, current) => current with { Health = thread.Health, LastObservedUtc = snapshot.ObservedUtc,
                    Generation = thread.Generation, QueueDepth = thread.QueueDepth, QueueCapacity = thread.QueueCapacity });
        }
        foreach (var threadId in _active.Keys.Where(threadId => !observed.Contains(threadId) && !_manualReview.ContainsKey(threadId)))
            _active.TryRemove(threadId, out _);
    }

    internal void RecordRestart(ActorThreadId threadId, long generation)
    {
        var now = DateTime.UtcNow;
        var history = _restarts.GetOrAdd(threadId, static _ => []);
        history.Enqueue(now);
        while (history.TryPeek(out var oldest) && now - oldest > TimeSpan.FromHours(24)) history.TryDequeue(out _);
        var last24Hours = history.Count;
        var last60Minutes = history.Count(value => now - value <= TimeSpan.FromHours(1));
        if (last60Minutes < 2 && last24Hours < 4) return;
        _manualReview[threadId] = 0;
        _active.AddOrUpdate(threadId,
            key => new(key, SupervisorActorHealth.Critical, now, now, generation, 0, 0,
                last60Minutes, last24Hours, true),
            (_, current) => current with { Health = SupervisorActorHealth.Critical, LastObservedUtc = now,
                Generation = generation, RestartsIn60Minutes = last60Minutes,
                RestartsIn24Hours = last24Hours, RequiresTraderReview = true });
    }
}
