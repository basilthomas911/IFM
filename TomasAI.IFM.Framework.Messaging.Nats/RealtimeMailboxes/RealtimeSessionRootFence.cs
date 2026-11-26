using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;

/// <summary>Capture-only pause of generated root observations, before archive admission or publication.
/// Accepted publications finish; new ones wait without being acknowledged or discarded.</summary>
public sealed class RealtimeSessionRootFence
{
    readonly object gate = new();
    TaskCompletionSource released = Completed();
    string? leaseId, boundaryId;
    int active;
    static TaskCompletionSource Completed() { var value = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); value.SetResult(); return value; }
    public static bool IsRoot(ActorSubject subject) => subject.ActorType == ActorType.Realtime && (subject.Name, subject.Verb) is
        ("FuturesTradeSessionBarSignal", "Barrier") or
        ("FuturesTradePositionRealtime", "EvaluateFuturesExitClock") or
        ("IronCondorTradePositionRealtime", "IronCondorMonitoringInputsChanged");

    public void Hold(string lease, string boundary)
    {
        if (!Guid.TryParseExact(lease,"N",out _) || !Guid.TryParseExact(boundary,"N",out _)) throw new ArgumentException("Capture identities must be GUIDs in N format.");
        lock (gate)
        {
            if (leaseId is not null) { if (leaseId != lease || boundaryId != boundary) throw new InvalidOperationException("Another capture boundary owns the root fence."); return; }
            leaseId=lease; boundaryId=boundary; released=new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
    public (string? LeaseId,string? BoundaryId,int Active,bool Held) Snapshot()
    { lock(gate) return (leaseId,boundaryId,active,leaseId is not null); }
    public void Release(string lease)
    {
        lock(gate)
        {
            if(leaseId != lease)throw new InvalidOperationException("Capture lease does not own the fence.");
            leaseId=null;boundaryId=null;released.TrySetResult();
        }
    }
    public async ValueTask<IDisposable> EnterAsync(CancellationToken token)
    {
        while(true)
        {
            token.ThrowIfCancellationRequested();
            Task wait;
            lock(gate)
            {
                if(leaseId is null) { active++; return new Admission(this); }
                wait=released.Task;
            }
            await wait.WaitAsync(token).ConfigureAwait(false);
        }
    }
    sealed class Admission(RealtimeSessionRootFence owner) : IDisposable
    {
        RealtimeSessionRootFence? current=owner;
        public void Dispose() { var value=Interlocked.Exchange(ref current,null);if(value is not null)lock(value.gate)value.active--; }
    }
}
