namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>A bounded, opt-in observation of one realtime actor route for recovery qualification.</summary>
/// <remarks>Counts only successful handler, delivery completion, and message cleanup. This is actor-execution
/// evidence, not proof that a downstream projector persisted data.</remarks>
public sealed class RealtimeActorCompletionEvidence(TimeProvider? time = null)
{
    readonly object gate = new();
    readonly TimeProvider clock = time ?? TimeProvider.System;
    RealtimeActorCompletionTarget? target;
    long completed;
    DateTimeOffset? lastCompletedUtc;

    /// <summary>Arms one exact dataset, generation, and actor route; prior evidence is discarded.</summary>
    public void Arm(RealtimeActorCompletionTarget value)
    {
        if (string.IsNullOrWhiteSpace(value.Dataset) || value.Dataset.Length > 64
            || value.GenerationId == Guid.Empty || string.IsNullOrWhiteSpace(value.ActorName)
            || string.IsNullOrWhiteSpace(value.Verb) || value.ActorType != ActorType.Realtime)
            throw new ArgumentException("A complete realtime actor target is required.", nameof(value));
        lock (gate)
        {
            target = value;
            completed = 0;
            lastCompletedUtc = null;
        }
    }

    /// <summary>Disarms observation and discards evidence from the previous generation.</summary>
    public void Disarm()
    {
        lock (gate)
        {
            target = null;
            completed = 0;
            lastCompletedUtc = null;
        }
    }

    /// <summary>Returns a point-in-time snapshot without retaining individual messages.</summary>
    public RealtimeActorCompletionSnapshot Capture()
    {
        lock (gate) return new(target, completed, lastCompletedUtc);
    }

    internal void Record(string? dataset, Guid generationId, ActorSubject subject)
    {
        lock (gate)
        {
            if (target is not { } expected || expected.Dataset != dataset
                || expected.GenerationId != generationId || expected.ActorType != subject.ActorType
                || expected.ActorName != subject.Name || expected.Verb != subject.Verb)
                return;
            completed++;
            lastCompletedUtc = clock.GetUtcNow();
        }
    }
}

/// <summary>Exact supervised generation and actor route whose completed messages are observed.</summary>
public sealed record RealtimeActorCompletionTarget(string Dataset, Guid GenerationId,
    ActorType ActorType, string ActorName, string Verb);

/// <summary>Bounded actor completion evidence. This alone does not qualify downstream admission.</summary>
public sealed record RealtimeActorCompletionSnapshot(RealtimeActorCompletionTarget? Target,
    long Completed, DateTimeOffset? LastCompletedUtc);
