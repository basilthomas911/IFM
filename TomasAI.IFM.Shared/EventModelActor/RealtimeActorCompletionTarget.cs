namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Exact supervised generation and actor route whose completed messages are observed.</summary>
public sealed record RealtimeActorCompletionTarget(string Dataset, Guid GenerationId,
    ActorType ActorType, string ActorName, string Verb);
