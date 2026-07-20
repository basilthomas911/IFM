namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Represents a unique identifier for an actor's mailbox, consisting of the actor's type and name.
/// </summary>
/// <remarks>This is a zero-allocation value type used as dictionary keys throughout the actor system.
/// Value equality is provided by the record struct.</remarks>
/// <param name="ActorType"></param>
/// <param name="Name"></param>
public readonly record struct ActorMailboxId(
    ActorType ActorType,
    string Name)
{
    public override string ToString()
        => $"{ActorType}.{Name}";
}
