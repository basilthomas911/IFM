namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>A bounded, opt-in observation of one realtime actor route for recovery qualification.</summary>
/// <remarks>Counts only successful handler, delivery completion, and message cleanup. This is actor-execution
/// evidence, not proof that a downstream projector persisted data.</remarks>
public sealed class RealtimeActorCompletionEvidence(TimeProvider? time = null)
{
    readonly System.Threading.Lock gate = new();
    readonly TimeProvider clock = time ?? TimeProvider.System;
    RealtimeActorCompletionTarget? target;
    long completed;
    DateTimeOffset? lastCompletedUtc;

    /// <summary>Arms one exact dataset, generation, and actor route; prior evidence is discarded.</summary>
    public void Arm(RealtimeActorCompletionTarget value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value.Dataset) || value.Dataset.Length > 64
            || value.GenerationId == Guid.Empty || string.IsNullOrWhiteSpace(value.ActorName)
            || string.IsNullOrWhiteSpace(value.Verb) || value.ActorType != ActorType.Realtime)
            throw new ArgumentException("A complete realtime actor target is required.", nameof(value));
        lock (gate)
        {
            completed = 0;
            lastCompletedUtc = null;
            Volatile.Write(ref target, value);
        }
    }

    /// <summary>Disarms observation and discards evidence from the previous generation.</summary>
    public void Disarm()
    {
        lock (gate)
        {
            Volatile.Write(ref target, null);
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
        var expected = Volatile.Read(ref target);
        if (expected is null || !string.Equals(expected.Dataset, dataset, StringComparison.Ordinal)
            || expected.GenerationId != generationId || expected.ActorType != subject.ActorType
            || !string.Equals(expected.ActorName, subject.Name, StringComparison.Ordinal)
            || !string.Equals(expected.Verb, subject.Verb, StringComparison.Ordinal)) return;
        lock (gate)
        {
            // Arm/disarm can race the fast path. Never credit a completion to a replacement observation.
            if (!ReferenceEquals(target, expected)) return;
            completed++;
            lastCompletedUtc = clock.GetUtcNow();
        }
    }
}
