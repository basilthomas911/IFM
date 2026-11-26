namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>Opt-in infrastructure boundary for a bounded, ordered, all-or-failed realtime delivery group.</summary>
public interface IBatchedRealtimeActor
{
    /// <summary>Returns only after all inputs have a recoverable owner. The scheduler settles individual deliveries afterward.</summary>
    ValueTask HandleBatchAsync(IReadOnlyList<IActorMessage> messages, ActorThreadId threadId, CancellationToken cancellationToken);
}
