namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Represents the unique identifier for an actor thread, including its type, name, and associated entity ID.
/// </summary>
/// <remarks>This is a zero-allocation value type. MailboxId is computed on demand since
/// ActorMailboxId is also a lightweight struct (no heap allocation).</remarks>
/// <param name="ActorType"></param>
/// <param name="Name"></param>
/// <param name="EntityId"></param>
public readonly record struct ActorThreadId(
    ActorType ActorType,
    string Name,
    string EntityId)
{
    public override string ToString()
        => $"{ActorType}.{Name}.{EntityId}";

    public ActorMailboxId MailboxId
        => new(ActorType, Name);
}


