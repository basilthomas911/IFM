using System.Runtime.CompilerServices;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.EventModelActor;

/// <summary>Bounded latest complete-position notifications. Never used for commands, fills or financial events.</summary>
public sealed class LatestOptionSnapshotQueue(ActorAdmissionController admission) : IActorThreadQueue, IScheduledActorThreadQueue
{
    readonly object gate = new();
    readonly LinkedList<Entry> pending = new();
    readonly Dictionary<string, LinkedListNode<Entry>> byVerb = new(StringComparer.Ordinal);
    bool retired, scheduled, processing, started;
    public ActorThreadId Id { get; private set; }
    public int Count { get { lock (gate) return pending.Count; } }
    public int Capacity => 16;
    public long Replaced { get; private set; }
    public static bool Applies(ActorThreadId id) => id.ActorType == ActorType.Realtime && id.Name == "IronCondorTradePositionRealtime";
    public static bool IsDisposable(IActorMessage message, ActorSubject subject) => !message.CanReply && Applies(subject.ThreadId)
        && subject.Verb is "StrategyPositionChanged" or "IronCondorMonitoringInputsChanged";
    public IActorThreadQueue SetId(ActorThreadId id) { Id = id; return this; }
    public void Start() { lock (gate) { if (retired) throw new ObjectDisposedException(nameof(LatestOptionSnapshotQueue)); started = true; } }
    public bool Write(IActorMessage message, CancellationToken cancellationToken = default)
        => ((IScheduledActorThreadQueue)this).TryWrite(message, cancellationToken);
    public ValueTask EnqueueAsync(IActorMessage message, CancellationToken cancellationToken = default)
    {
        if (!Write(message, cancellationToken)) throw new InvalidOperationException("Option snapshot queue is unavailable.");
        return ValueTask.CompletedTask;
    }
    bool IScheduledActorThreadQueue.TryWrite(IActorMessage message, CancellationToken cancellationToken)
    {
        var charge = default(ActorAdmissionCharge);
        if (!IsDisposable(message, message.Subject) && !admission.TryReserve(message, Id.ActorType, out charge).Accepted) return false;
        var result = ((IScheduledActorThreadQueue)this).TryWriteReserved(message, charge, cancellationToken);
        if (!result.Accepted) admission.Release(charge);
        return result.Accepted;
    }
    ValueTask<bool> IScheduledActorThreadQueue.TryWriteAsync(IActorMessage message, CancellationToken cancellationToken)
        => ValueTask.FromResult(((IScheduledActorThreadQueue)this).TryWrite(message, cancellationToken));
    ActorAdmissionResult IScheduledActorThreadQueue.TryWriteReserved(IActorMessage message, ActorAdmissionCharge charge, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (retired || !started) return ActorAdmissionResult.Rejected(ActorAdmissionReason.MailboxRetired);
            var disposable = !message.CanReply && Applies(Id)
                && message.Subject.Verb is "StrategyPositionChanged" or "IronCondorMonitoringInputsChanged";
            if (disposable && byVerb.Remove(message.Subject.Verb, out var preceding))
            {
                pending.Remove(preceding);
                Release(preceding.Value, true); Replaced++;
                ActorRuntimeMetrics.Coalesced.Add(1);
            }
            if (pending.Count >= Capacity) return ActorAdmissionResult.Rejected(ActorAdmissionReason.MailboxLimit);
            var entry = new Entry(message, charge, ActorRuntimeMetrics.StartQueueWait(), disposable);
            var node = pending.AddLast(entry);
            if (disposable) byVerb[message.Subject.Verb] = node;
            ActorRuntimeMetrics.RecordAccepted(Id.ActorType);
            return ActorAdmissionResult.AcceptedResult;
        }
    }
    ValueTask<ActorAdmissionResult> IScheduledActorThreadQueue.TryWriteReservedAsync(IActorMessage message, ActorAdmissionCharge charge, CancellationToken cancellationToken)
        => ValueTask.FromResult(((IScheduledActorThreadQueue)this).TryWriteReserved(message, charge, cancellationToken));
    bool IScheduledActorThreadQueue.TryRead(out IActorMessage? message)
    {
        lock (gate)
        {
            if (pending.First is not { } node) { message = null; return false; }
            pending.RemoveFirst();
            if (node.Value.Disposable) byVerb.Remove(node.Value.Message.Subject.Verb);
            Release(node.Value, false); message = node.Value.Message; return true;
        }
    }
    void Release(Entry entry, bool dispose)
    {
        admission.Release(entry.Charge);
        ActorRuntimeMetrics.RecordDequeued(entry.Timestamp, Id.ActorType);
        if (dispose) entry.Message.Dispose();
    }
    public IEnumerable<IActorMessage> ReadAll(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested && ((IScheduledActorThreadQueue)this).TryRead(out var message)) yield return message!;
    }
    public async IAsyncEnumerable<IActorMessage> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    { foreach (var message in ReadAll(cancellationToken)) yield return message; await Task.CompletedTask.ConfigureAwait(false); }
    bool IScheduledActorThreadQueue.IsRetired { get { lock (gate) return retired; } }
    bool IScheduledActorThreadQueue.IsScheduled { get { lock (gate) return scheduled; } }
    bool IScheduledActorThreadQueue.TrySchedule() { lock (gate) { if (retired || scheduled) return false; scheduled = true; return true; } }
    bool IScheduledActorThreadQueue.TryClaimProcessing() { lock (gate) { if (retired || !scheduled || processing) return false; processing = true; return true; } }
    void IScheduledActorThreadQueue.ReleaseProcessing() { lock (gate) processing = false; }
    bool IScheduledActorThreadQueue.CompleteDrain()
    { lock (gate) { processing = false; if (pending.Count > 0) return true; scheduled = false; return false; } }
    bool IScheduledActorThreadQueue.TryRetire()
    { lock (gate) { if (pending.Count > 0 || scheduled || processing) return false; retired = true; return true; } }
    public void Stop()
    { lock (gate) { retired = true; started = false; foreach (var entry in pending) Release(entry, true); pending.Clear(); byVerb.Clear(); } }
    readonly record struct Entry(IActorMessage Message, ActorAdmissionCharge Charge, long Timestamp, bool Disposable);
}
