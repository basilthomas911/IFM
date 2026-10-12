namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Bounded actor completion evidence. This alone does not qualify downstream admission.</summary>
public sealed record RealtimeActorCompletionSnapshot(RealtimeActorCompletionTarget? Target,
    long Completed, DateTimeOffset? LastCompletedUtc);
