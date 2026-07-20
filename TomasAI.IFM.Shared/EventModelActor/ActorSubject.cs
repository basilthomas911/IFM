using MessagePack;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Represents an actor subject, which encapsulates the type, name, verb, and entity ID of an actor.
/// </summary>
/// <remarks>This is a zero-allocation value type. Identity properties (ActorId, ThreadId) are computed
/// on demand since the underlying types are also lightweight structs.</remarks>
[MessagePackObject]
public readonly record struct ActorSubject
{
    [Key(0)]
    public ActorType ActorType { get; init; }
    [Key(1)]
    public string Name { get; init; } 
    [Key(2)]
    public string Verb { get; init; } 
    [Key(3)]
    public string EntityId { get; init; }

    [IgnoreMember]
    public ActorMailboxId ActorId
        => new(ActorType, Name);

    [IgnoreMember]
    public ActorTypeId ActorTypeId
        => new(ActorType, Name, Verb);

    [IgnoreMember]
    public ActorThreadId ThreadId
        => new(ActorType, Name, EntityId);

    [IgnoreMember]
    public string StreamId
        => $"{ActorType}.{Name}.{EntityId}";

    [IgnoreMember]
    public static ActorSubject Default
        => new(ActorType.Default, "none", "none", "none");

    public override string ToString()
        => $"{ActorType}.{Name}.{Verb}.{EntityId}";

    public bool Is(ActorType actorType, string name, string verb)
        => ActorType == actorType && Name == name && Verb == verb;

    public ActorSubject SetActorType(ActorType actorType)
        => this with { ActorType = actorType };

    public ActorSubject SetVerb(string verb)
        => this with { Verb = verb };

    [SerializationConstructor]
    public ActorSubject(ActorType actorType, string name, string verb, string entityId)
    {
        ActorType = actorType;
        Name = name;
        Verb = verb;
        EntityId = entityId;
    }

}
