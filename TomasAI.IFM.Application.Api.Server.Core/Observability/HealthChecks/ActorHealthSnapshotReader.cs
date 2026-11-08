using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.Observability.HealthChecks;

/// <summary>Shares one immutable runtime capture across diagnostic views; reuse expires one second after capture starts.</summary>
public sealed class ActorHealthSnapshotReader
{
    readonly Func<SupervisorRuntimeSnapshot> capture;
    readonly TimeProvider clock;
    readonly object gate = new();
    sealed record Frame(long Started, SupervisorRuntimeSnapshot Snapshot);
    Frame? current;

    public ActorHealthSnapshotReader(IActorSupervisor supervisor, TimeProvider clock)
        : this(() => supervisor.RuntimeContext.CaptureSnapshot(), clock) { }
    internal ActorHealthSnapshotReader(Func<SupervisorRuntimeSnapshot> capture, TimeProvider clock)
    { this.capture = capture; this.clock = clock; }

    public SupervisorRuntimeSnapshot Capture()
    {
        var frame = Volatile.Read(ref current);
        if (frame is not null && clock.GetElapsedTime(frame.Started) < TimeSpan.FromSeconds(1)) return frame.Snapshot;
        lock (gate)
        {
            frame = current;
            if (frame is not null && clock.GetElapsedTime(frame.Started) < TimeSpan.FromSeconds(1)) return frame.Snapshot;
            var started = clock.GetTimestamp();
            var snapshot = capture();
            Volatile.Write(ref current, new(started, snapshot));
            return snapshot;
        }
    }

    public void Invalidate() { lock (gate) Volatile.Write(ref current, null); }
}
